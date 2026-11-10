using System;
using Microsoft.Win32;

namespace Insominia
{
    /// <summary>Optional "start with Windows" entry. Per-user, so no admin needed.</summary>
    internal static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Insominia";

        /// <summary>
        /// Passed only by the Windows startup entry. It is how the app tells
        /// "the user just double-clicked me" apart from "Windows started me at
        /// login" — the first should open the panel, the second must not throw
        /// a window in your face every time you sign in.
        /// </summary>
        public const string AutoStartFlag = "--autostart";

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
                    if (enabled)
                        k.SetValue(ValueName, "\"" + ElevatedTask.ExePath + "\" " + AutoStartFlag);
                    else if (k.GetValue(ValueName) != null) k.DeleteValue(ValueName);
                }
            }
            catch (Exception ex) { Log.Write("startup toggle failed: " + ex.Message); }
        }
    }
}
