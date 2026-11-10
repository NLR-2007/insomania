using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;

namespace Insominia
{
    /// <summary>
    /// Changing the lid setting needs administrator rights. Asking the user for
    /// UAC every single time they flip the switch would be unusable, so instead
    /// we register ONE Windows Scheduled Task that runs Insominia elevated.
    /// After that one-time setup the tray app can trigger it silently.
    /// </summary>
    internal static class ElevatedTask
    {
        public const string TaskName = "Insominia_LidPolicy";

        public static bool IsCurrentProcessElevated()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        public static string ExePath
        {
            get { return Process.GetCurrentProcess().MainModule.FileName; }
        }

        public static bool IsRegistered()
        {
            int exit;
            RunCapture("schtasks", "/Query /TN \"" + TaskName + "\"", out exit);
            return exit == 0;
        }

        /// <summary>
        /// One-time setup. Shows a UAC prompt. Creates the elevated helper task.
        /// Returns false if the user cancelled UAC.
        /// </summary>
        public static bool Register()
        {
            string xmlPath = Path.Combine(Path.GetTempPath(), "insominia_task.xml");
            try
            {
                File.WriteAllText(xmlPath, BuildTaskXml(), new UnicodeEncoding(false, true));

                // schtasks must run elevated to create a HighestAvailable task.
                var psi = new ProcessStartInfo("schtasks",
                    "/Create /TN \"" + TaskName + "\" /XML \"" + xmlPath + "\" /F")
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(60000);
                    bool ok = p.ExitCode == 0;
                    Log.Write("task register exit=" + p.ExitCode);
                    return ok;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Log.Write("task register: UAC cancelled by user");
                return false; // user clicked No on the UAC dialog
            }
            catch (Exception ex)
            {
                Log.Write("task register failed: " + ex.Message);
                return false;
            }
            finally
            {
                try { if (File.Exists(xmlPath)) File.Delete(xmlPath); } catch { }
            }
        }

        public static bool Unregister()
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks", "/Delete /TN \"" + TaskName + "\" /F")
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                using (Process p = Process.Start(psi)) { p.WaitForExit(60000); return p.ExitCode == 0; }
            }
            catch { return false; }
        }

        /// <summary>
        /// Asks the elevated helper to apply a lid action. Silent - no UAC prompt.
        /// The value is passed through the settings file because scheduled task
        /// arguments cannot be changed without admin rights.
        /// </summary>
        public static bool RequestLid(LidAction onCharger, LidAction onBattery)
        {
            AppSettings.Set("PendingLidAc", (int)onCharger);
            AppSettings.Set("PendingLidDc", (int)onBattery);
            AppSettings.Set("PendingLidResult", "");
            AppSettings.Save();

            int exit;
            RunCapture("schtasks", "/Run /TN \"" + TaskName + "\"", out exit);
            if (exit != 0) { Log.Write("schtasks /Run failed exit=" + exit); return false; }

            // The helper runs in its own process; wait briefly for it to finish.
            for (int i = 0; i < 60; i++)
            {
                System.Threading.Thread.Sleep(250);
                AppSettings.Load();
                string result = AppSettings.GetString("PendingLidResult", "");
                if (result == "ok") return true;
                if (result == "fail") return false;
            }
            Log.Write("elevated helper timed out");
            return false;
        }

        /// <summary>Runs inside the ELEVATED helper process (--apply-lid-request).</summary>
        public static int HandleLidRequest()
        {
            AppSettings.Load();
            LidAction ac = (LidAction)AppSettings.GetInt("PendingLidAc", (int)LidAction.Sleep);
            LidAction dc = (LidAction)AppSettings.GetInt("PendingLidDc", (int)LidAction.Sleep);

            bool ok = LidPolicy.ApplyElevated(ac, dc);

            AppSettings.Load();                       // re-read in case the tray app wrote meanwhile
            AppSettings.Set("PendingLidResult", ok ? "ok" : "fail");
            AppSettings.Save();
            return ok ? 0 : 1;
        }

        private static string BuildTaskXml()
        {
            string user = WindowsIdentity.GetCurrent().Name;
            string exe = ExePath;
            StringBuilder sb = new StringBuilder();

            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n");
            sb.Append("<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n");
            sb.Append("  <RegistrationInfo>\r\n");
            sb.Append("    <Description>Insominia: applies the lid-close power setting on demand. Created by the Insominia app.</Description>\r\n");
            sb.Append("    <URI>\\").Append(TaskName).Append("</URI>\r\n");
            sb.Append("  </RegistrationInfo>\r\n");
            sb.Append("  <Triggers />\r\n");
            sb.Append("  <Principals>\r\n");
            sb.Append("    <Principal id=\"Author\">\r\n");
            sb.Append("      <UserId>").Append(Esc(user)).Append("</UserId>\r\n");
            sb.Append("      <LogonType>InteractiveToken</LogonType>\r\n");
            sb.Append("      <RunLevel>HighestAvailable</RunLevel>\r\n");
            sb.Append("    </Principal>\r\n");
            sb.Append("  </Principals>\r\n");
            sb.Append("  <Settings>\r\n");
            sb.Append("    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\r\n");
            sb.Append("    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n");
            sb.Append("    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n");
            sb.Append("    <AllowHardTerminate>true</AllowHardTerminate>\r\n");
            sb.Append("    <StartWhenAvailable>false</StartWhenAvailable>\r\n");
            sb.Append("    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>\r\n");
            sb.Append("    <AllowStartOnDemand>true</AllowStartOnDemand>\r\n");
            sb.Append("    <Enabled>true</Enabled>\r\n");
            sb.Append("    <Hidden>false</Hidden>\r\n");
            sb.Append("    <RunOnlyIfIdle>false</RunOnlyIfIdle>\r\n");
            sb.Append("    <WakeToRun>false</WakeToRun>\r\n");
            sb.Append("    <ExecutionTimeLimit>PT2M</ExecutionTimeLimit>\r\n");
            sb.Append("    <Priority>5</Priority>\r\n");
            sb.Append("  </Settings>\r\n");
            sb.Append("  <Actions Context=\"Author\">\r\n");
            sb.Append("    <Exec>\r\n");
            sb.Append("      <Command>").Append(Esc(exe)).Append("</Command>\r\n");
            sb.Append("      <Arguments>--apply-lid-request</Arguments>\r\n");
            sb.Append("    </Exec>\r\n");
            sb.Append("  </Actions>\r\n");
            sb.Append("</Task>\r\n");
            return sb.ToString();
        }

        private static string Esc(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static string RunCapture(string exe, string args, out int exitCode)
        {
            exitCode = -1;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (Process p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit(20000);
                    exitCode = p.ExitCode;
                    return o;
                }
            }
            catch (Exception ex) { Log.Write("runcapture failed: " + ex.Message); return ""; }
        }
    }
}
