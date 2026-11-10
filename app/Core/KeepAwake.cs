using System;
using System.Runtime.InteropServices;

namespace Insominia
{
    [Flags]
    internal enum ExecState : uint
    {
        SystemRequired   = 0x00000001,
        DisplayRequired  = 0x00000002,
        UserPresent      = 0x00000004,
        AwayModeRequired = 0x00000040,
        Continuous       = 0x80000000,
    }

    /// <summary>
    /// Tells Windows "a task is running, do not go to sleep".
    /// Deliberately does NOT request DisplayRequired - we WANT the screen to
    /// switch off when the lid closes. Only the system must stay awake.
    /// Needs no administrator rights.
    /// </summary>
    internal static class KeepAwake
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetThreadExecutionState(ExecState esFlags);

        public static bool IsEngaged { get; private set; }
        public static string LastError { get; private set; }

        /// <summary>Must be called from the thread that stays alive (the UI thread).</summary>
        public static bool Engage()
        {
            uint prev = SetThreadExecutionState(
                ExecState.Continuous | ExecState.SystemRequired | ExecState.AwayModeRequired);

            if (prev == 0)
            {
                // Away mode is not supported everywhere - retry without it.
                prev = SetThreadExecutionState(ExecState.Continuous | ExecState.SystemRequired);
            }

            IsEngaged = prev != 0;
            LastError = IsEngaged ? null : "SetThreadExecutionState failed (win32=" + Marshal.GetLastWin32Error() + ")";
            return IsEngaged;
        }

        public static void Release()
        {
            SetThreadExecutionState(ExecState.Continuous);
            IsEngaged = false;
        }
    }
}
