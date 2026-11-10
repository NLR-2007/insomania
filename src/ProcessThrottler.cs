using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace Insominia
{
    /// <summary>One app the user can choose to keep running at full speed.</summary>
    internal sealed class AppEntry
    {
        public string ProcessName;       // e.g. "chrome" - stable across restarts, so we save this
        public string DisplayName;       // e.g. "Google Chrome"
        public List<int> Pids = new List<int>();
        public long MemoryBytes;

        public string MemoryText
        {
            get { return (MemoryBytes / 1024.0 / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " MB"; }
        }

        public override string ToString()
        {
            string label = DisplayName + "   (" + MemoryText;
            if (Pids.Count > 1) label += ", " + Pids.Count + " processes";
            return label + ")";
        }
    }

    internal sealed class ThrottleResult
    {
        public int Throttled;
        public int Skipped;
        public int Failed;
    }

    /// <summary>
    /// Windows cannot sleep some apps and keep others awake - the CPU is either
    /// running or it is not. So instead of NOT waking the other apps, we keep
    /// the system awake and hold the unwanted apps back.
    ///
    /// We use EcoQoS (the same mechanism Windows uses for background browser
    /// tabs): the process is moved to efficiency cores and clocked down. It
    /// stays alive and connected - it is just slow and cheap. Fully reversible,
    /// and it cannot lose anybody's unsaved work.
    /// </summary>
    internal static class ProcessThrottler
    {
        // ---- Win32 ---------------------------------------------------------

        private const int ProcessPowerThrottling = 4;
        private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
        private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

        private const uint PROCESS_SET_INFORMATION = 0x0200;
        private const uint PROCESS_SET_LIMITED_INFORMATION = 0x2000;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessInformation(
            IntPtr hProcess, int informationClass,
            ref PROCESS_POWER_THROTTLING_STATE information, int informationSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        // ---- never touch these ---------------------------------------------

        /// <summary>
        /// Throttling any of these would make the machine feel broken, break
        /// security software, or kill audio. They are off limits, always.
        /// </summary>
        private static readonly HashSet<string> NeverTouch =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // us
                "insominia",
                // shell and core OS
                "explorer", "dwm", "csrss", "winlogon", "wininit", "smss", "services",
                "lsass", "svchost", "fontdrvhost", "sihost", "ctfmon", "taskhostw",
                "dllhost", "conhost", "runtimebroker", "applicationframehost",
                "shellexperiencehost", "startmenuexperiencehost", "searchhost",
                "textinputhost", "lockapp", "systemsettings", "useroobebroker",
                "wudfhost", "audiodg", "spoolsv", "registry", "memory compression",
                // security - throttling antivirus is a genuinely bad idea
                "msmpeng", "nissrv", "securityhealthservice", "securityhealthsystray",
                "mpdefendercoreservice",
            };

        private static readonly List<int> ThrottledPids = new List<int>();

        public static bool AnyThrottled { get { return ThrottledPids.Count > 0; } }
        public static int ThrottledCount { get { return ThrottledPids.Count; } }

        // ---- listing -------------------------------------------------------

        /// <summary>
        /// Apps the user would recognise: anything with a real window. Grouped by
        /// process name, because a browser is many processes but one "app".
        /// </summary>
        public static List<AppEntry> ListVisibleApps()
        {
            Dictionary<string, AppEntry> byName =
                new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (NeverTouch.Contains(p.ProcessName)) { p.Dispose(); continue; }
                    if (p.MainWindowHandle == IntPtr.Zero) { p.Dispose(); continue; }
                    if (string.IsNullOrEmpty(p.MainWindowTitle)) { p.Dispose(); continue; }

                    AppEntry entry;
                    if (!byName.TryGetValue(p.ProcessName, out entry))
                    {
                        entry = new AppEntry
                        {
                            ProcessName = p.ProcessName,
                            DisplayName = FriendlyName(p),
                        };
                        byName[p.ProcessName] = entry;
                    }

                    entry.Pids.Add(p.Id);
                    entry.MemoryBytes += p.WorkingSet64;
                }
                catch { /* process died or access denied - ignore it */ }
                finally { try { p.Dispose(); } catch { } }
            }

            // Fold in the sibling processes of each app (a browser's tab processes
            // have no window of their own but burn the most power).
            AddWindowlessSiblings(byName);

            List<AppEntry> list = new List<AppEntry>(byName.Values);
            list.Sort(delegate (AppEntry a, AppEntry b)
            {
                return b.MemoryBytes.CompareTo(a.MemoryBytes);   // heaviest first
            });
            return list;
        }

        private static void AddWindowlessSiblings(Dictionary<string, AppEntry> byName)
        {
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    AppEntry entry;
                    if (!byName.TryGetValue(p.ProcessName, out entry)) continue;
                    if (entry.Pids.Contains(p.Id)) continue;

                    entry.Pids.Add(p.Id);
                    entry.MemoryBytes += p.WorkingSet64;
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
        }

        private static string FriendlyName(Process p)
        {
            try
            {
                string desc = p.MainModule.FileVersionInfo.FileDescription;
                if (!string.IsNullOrEmpty(desc)) return desc;
            }
            catch { /* 32/64-bit or permission mismatch - fall through */ }

            string n = p.ProcessName;
            return n.Length > 0 ? char.ToUpper(n[0]) + n.Substring(1) : n;
        }

        // ---- apply / restore -----------------------------------------------

        /// <summary>
        /// Throttles every user app EXCEPT the ones named in keepRunning and
        /// everything underneath them. Returns what it did.
        /// </summary>
        public static ThrottleResult ThrottleAllExcept(IEnumerable<string> keepRunning)
        {
            ThrottleResult result = new ThrottleResult();

            HashSet<string> keepNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string n in keepRunning) keepNames.Add(n);

            // An agent task is never one process. If the user protects the
            // terminal, its node/python/git children must be protected too,
            // otherwise we throttle the very thing we were asked to keep alive.
            HashSet<int> protectedPids = ExpandToProcessTrees(keepNames);

            foreach (Process p in Process.GetProcesses())
            {
                int pid = p.Id;
                string name = p.ProcessName;
                try
                {
                    if (pid <= 4) { result.Skipped++; continue; }
                    if (NeverTouch.Contains(name)) { result.Skipped++; continue; }
                    if (keepNames.Contains(name)) { result.Skipped++; continue; }
                    if (protectedPids.Contains(pid)) { result.Skipped++; continue; }

                    if (SetEcoQoS(pid, true))
                    {
                        ThrottledPids.Add(pid);
                        result.Throttled++;
                    }
                    else result.Failed++;
                }
                catch { result.Failed++; }
                finally { try { p.Dispose(); } catch { } }
            }

            SaveThrottledPids();
            Log.Write(string.Format(CultureInfo.InvariantCulture,
                "throttle: {0} throttled, {1} left alone, {2} failed (kept: {3})",
                result.Throttled, result.Skipped, result.Failed,
                string.Join(", ", new List<string>(keepNames).ToArray())));
            return result;
        }

        /// <summary>Puts every throttled process back to normal. Safe to call twice.</summary>
        public static void RestoreAll()
        {
            int restored = 0;
            foreach (int pid in ThrottledPids)
                if (SetEcoQoS(pid, false)) restored++;

            if (ThrottledPids.Count > 0)
                Log.Write("throttle: restored " + restored + " of " + ThrottledPids.Count + " processes");

            ThrottledPids.Clear();
            SaveThrottledPids();
        }

        /// <summary>
        /// After a crash or force-kill, processes stay throttled forever. On the
        /// next start we clear anything we left behind.
        /// </summary>
        public static void RestoreOrphansFromLastRun()
        {
            string saved = AppSettings.GetString("ThrottledPids", "");
            if (saved.Length == 0) return;

            int restored = 0;
            foreach (string part in saved.Split(','))
            {
                int pid;
                if (!int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out pid))
                    continue;
                if (SetEcoQoS(pid, false)) restored++;
            }

            Log.Write("throttle self-heal: cleared " + restored + " leftover throttled processes");
            AppSettings.Set("ThrottledPids", "");
            AppSettings.Save();
        }

        private static void SaveThrottledPids()
        {
            StringBuilder sb = new StringBuilder();
            foreach (int pid in ThrottledPids)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(pid.ToString(CultureInfo.InvariantCulture));
            }
            AppSettings.Set("ThrottledPids", sb.ToString());
            AppSettings.Save();
        }

        /// <summary>
        /// Turns EcoQoS on or off for one process.
        ///
        /// Note on the "off" case: there are two ways to switch throttling off.
        /// ControlMask = 0 means "let Windows decide", and Windows will happily
        /// keep a background process throttled - measured here as an app that
        /// never regained speed after being restored. That is the worst bug this
        /// feature could have, so "off" means the explicit
        /// ControlMask = EXECUTION_SPEED, StateMask = 0, which is a hard
        /// "do not throttle this process".
        /// </summary>
        private static bool SetEcoQoS(int pid, bool eco)
        {
            IntPtr h = OpenProcess(
                PROCESS_SET_INFORMATION | PROCESS_SET_LIMITED_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION,
                false, pid);

            if (h == IntPtr.Zero)
            {
                h = OpenProcess(PROCESS_SET_INFORMATION, false, pid);
                if (h == IntPtr.Zero) return false;   // protected or already gone
            }

            try
            {
                PROCESS_POWER_THROTTLING_STATE state = new PROCESS_POWER_THROTTLING_STATE
                {
                    Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                    // Always control execution speed; the state decides which way.
                    ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                    StateMask = eco ? PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0,
                };

                return SetProcessInformation(h, ProcessPowerThrottling,
                    ref state, Marshal.SizeOf(typeof(PROCESS_POWER_THROTTLING_STATE)));
            }
            catch { return false; }
            finally { CloseHandle(h); }
        }

        /// <summary>Exposed for the diagnostics tool to verify EcoQoS works on this machine.</summary>
        public static bool SelfTest(int pid, bool eco) { return SetEcoQoS(pid, eco); }

        // ---- process trees --------------------------------------------------

        /// <summary>
        /// Every descendant of a kept app is kept too. This is what stops us
        /// throttling the node/python processes an AI agent actually runs in.
        /// </summary>
        private static HashSet<int> ExpandToProcessTrees(HashSet<string> keepNames)
        {
            HashSet<int> keep = new HashSet<int>();

            try
            {
                Dictionary<int, int> parentOf = new Dictionary<int, int>();
                Dictionary<int, string> nameOf = new Dictionary<int, string>();

                using (ManagementObjectSearcher s = new ManagementObjectSearcher(
                           "SELECT ProcessId, ParentProcessId, Name FROM Win32_Process"))
                {
                    foreach (ManagementObject mo in s.Get())
                    {
                        using (mo)
                        {
                            try
                            {
                                int pid = Convert.ToInt32(mo["ProcessId"], CultureInfo.InvariantCulture);
                                int ppid = Convert.ToInt32(mo["ParentProcessId"], CultureInfo.InvariantCulture);
                                string nm = Convert.ToString(mo["Name"]);
                                if (nm != null && nm.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                    nm = nm.Substring(0, nm.Length - 4);

                                parentOf[pid] = ppid;
                                nameOf[pid] = nm;
                            }
                            catch { }
                        }
                    }
                }

                // Seed: every process whose name the user chose to keep.
                List<int> seeds = new List<int>();
                foreach (KeyValuePair<int, string> kv in nameOf)
                    if (kv.Value != null && keepNames.Contains(kv.Value)) seeds.Add(kv.Key);

                foreach (int seed in seeds) keep.Add(seed);

                // Walk every process up to its root; if any ancestor is kept, keep it.
                foreach (int pid in parentOf.Keys)
                {
                    int cursor = pid;
                    int hops = 0;
                    while (hops++ < 32)
                    {
                        int parent;
                        if (!parentOf.TryGetValue(cursor, out parent) || parent == 0 || parent == cursor) break;
                        if (keep.Contains(parent)) { keep.Add(pid); break; }
                        cursor = parent;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("process tree scan failed, protecting by name only: " + ex.Message);
            }

            return keep;
        }
    }
}
