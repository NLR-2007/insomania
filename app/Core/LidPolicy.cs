using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Insominia
{
    /// <summary>What Windows should do when the laptop lid is closed.</summary>
    internal enum LidAction
    {
        DoNothing = 0,
        Sleep     = 1,
        Hibernate = 2,
        ShutDown  = 3,
    }

    /// <summary>
    /// Reads and writes the "lid close action" power setting.
    /// Reading works as a normal user. WRITING NEEDS ADMIN, so writes are
    /// routed through ElevatedTask.
    /// </summary>
    internal static class LidPolicy
    {
        public const string SubButtons = "4f971e89-eebd-4455-a8de-9e59040e7347";
        public const string SettingLid = "5ca83367-6e45-459f-a27b-476b1d01c936";

        private const string SchemesRoot =
            @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";
        private const string SettingsRoot =
            @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings";

        // Same reasoning as the task cache: the UI asks for the lid state on
        // every refresh, and each miss would launch powercfg.exe. People change
        // power plans rarely, so a short cache costs nothing and saves a process
        // spawn every second.
        private static string _schemeCached;
        private static DateTime _schemeCheckedAt = DateTime.MinValue;
        private static readonly TimeSpan SchemeTtl = TimeSpan.FromSeconds(30);

        public static void InvalidateSchemeCache()
        {
            _schemeCheckedAt = DateTime.MinValue;
        }

        /// <summary>GUID of the power plan currently in use.</summary>
        public static string GetActiveSchemeGuid()
        {
            if (_schemeCached != null && DateTime.UtcNow - _schemeCheckedAt < SchemeTtl)
                return _schemeCached;

            _schemeCheckedAt = DateTime.UtcNow;
            try
            {
                string outp = RunCapture("powercfg", "/getactivescheme");
                Match m = Regex.Match(outp, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
                if (m.Success)
                {
                    _schemeCached = m.Groups[1].Value.ToLowerInvariant();
                    return _schemeCached;
                }
            }
            catch (Exception ex) { Log.Write("getactivescheme failed: " + ex.Message); }
            return _schemeCached;
        }

        /// <summary>Current lid action, read straight from the registry (works unelevated).</summary>
        public static bool TryRead(out LidAction onCharger, out LidAction onBattery)
        {
            onCharger = LidAction.Sleep;
            onBattery = LidAction.Sleep;

            string scheme = GetActiveSchemeGuid();
            if (scheme == null) return false;

            try
            {
                string path = SchemesRoot + "\\" + scheme + "\\" + SubButtons + "\\" + SettingLid;
                using (RegistryKey k = RegistryKey
                           .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                           .OpenSubKey(path, false))
                {
                    if (k == null) return false; // never changed from default
                    object ac = k.GetValue("ACSettingIndex");
                    object dc = k.GetValue("DCSettingIndex");
                    if (ac != null) onCharger = (LidAction)Convert.ToInt32(ac, CultureInfo.InvariantCulture);
                    if (dc != null) onBattery = (LidAction)Convert.ToInt32(dc, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Write("lid read failed: " + ex.Message);
                return false;
            }
        }

        public static bool IsSurvivingLidClose()
        {
            LidAction ac, dc;
            if (!TryRead(out ac, out dc)) return false;
            return ac == LidAction.DoNothing && dc == LidAction.DoNothing;
        }

        /// <summary>
        /// ELEVATED ONLY. Writes the lid action for both charger and battery.
        /// Also unhides the setting so the user can see it in Power Options.
        /// </summary>
        public static bool ApplyElevated(LidAction onCharger, LidAction onBattery)
        {
            string scheme = GetActiveSchemeGuid();
            if (scheme == null) { Log.Write("apply: no active scheme"); return false; }

            Unhide();

            bool ok = true;
            ok &= Run("powercfg", string.Format(CultureInfo.InvariantCulture,
                "/setacvalueindex {0} {1} {2} {3}", scheme, SubButtons, SettingLid, (int)onCharger));
            ok &= Run("powercfg", string.Format(CultureInfo.InvariantCulture,
                "/setdcvalueindex {0} {1} {2} {3}", scheme, SubButtons, SettingLid, (int)onBattery));
            // Re-activating the scheme is what makes the change take effect.
            ok &= Run("powercfg", "/setactive " + scheme);

            Log.Write(string.Format("apply lid ac={0} dc={1} ok={2}", onCharger, onBattery, ok));
            return ok;
        }

        /// <summary>Makes "Lid close action" visible in the Power Options UI (attribute 2 = show).</summary>
        private static void Unhide()
        {
            try
            {
                string path = SettingsRoot + "\\" + SubButtons + "\\" + SettingLid;
                using (RegistryKey k = RegistryKey
                           .OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                           .OpenSubKey(path, true))
                {
                    if (k != null) k.SetValue("Attributes", 2, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex) { Log.Write("unhide failed (not fatal): " + ex.Message); }
        }

        private static bool Run(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (Process p = Process.Start(psi))
                {
                    string err = p.StandardError.ReadToEnd();
                    p.StandardOutput.ReadToEnd();
                    p.WaitForExit(15000);
                    if (p.ExitCode != 0) Log.Write(exe + " " + args + " -> exit " + p.ExitCode + " " + err.Trim());
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Log.Write("run failed: " + exe + " " + args + " : " + ex.Message);
                return false;
            }
        }

        private static string RunCapture(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (Process p = Process.Start(psi))
            {
                string outp = p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                p.WaitForExit(15000);
                return outp;
            }
        }
    }
}
