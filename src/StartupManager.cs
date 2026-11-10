using System;
using Microsoft.Win32;

namespace Insominia
{
    /// <summary>Optional "start with Windows" entry. Per-user, so no admin needed.</summary>
    internal static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Insominia";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                    return k != null && k.GetValue(ValueName) != null;
            }
            catch { return false; }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (enabled) k.SetValue(ValueName, "\"" + ElevatedTask.ExePath + "\"");
                    else if (k.GetValue(ValueName) != null) k.DeleteValue(ValueName);
                }
            }
            catch (Exception ex) { Log.Write("startup toggle failed: " + ex.Message); }
        }
    }
}
