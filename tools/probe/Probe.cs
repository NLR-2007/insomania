using System;
using System.Runtime.InteropServices;

// STEP 1 PROBE: can we stop Windows from sleeping, and can we read lid policy?
// Run it, then check with:  powercfg /requests
class Probe
{
    [Flags]
    enum ExecState : uint
    {
        SystemRequired  = 0x00000001,
        DisplayRequired = 0x00000002,
        AwayModeRequired= 0x00000040,
        Continuous      = 0x80000000,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint SetThreadExecutionState(ExecState esFlags);

    static void Main()
    {
        Console.WriteLine("=== Insominia Step 1 probe ===");

        // 1. Ask Windows to keep the SYSTEM awake (screen may still turn off - that is what we want)
        uint prev = SetThreadExecutionState(ExecState.Continuous | ExecState.SystemRequired | ExecState.AwayModeRequired);
        if (prev == 0)
            Console.WriteLine("[FAIL] SetThreadExecutionState returned 0. err=" + Marshal.GetLastWin32Error());
        else
            Console.WriteLine("[ OK ] Sleep block ENGAGED (previous state flags = 0x" + prev.ToString("X") + ")");

        Console.WriteLine();
        Console.WriteLine("Holding the block for 25 seconds...");
        Console.WriteLine("While this runs, open another terminal and run:  powercfg /requests");
        Console.WriteLine("You should see Insominia.Probe.exe listed under SYSTEM or AWAYMODE.");

        for (int i = 25; i > 0; i--)
        {
            Console.Write("\r  " + i + "s remaining   ");
            System.Threading.Thread.Sleep(1000);
        }

        // 2. Release it - very important, otherwise the laptop never sleeps again
        SetThreadExecutionState(ExecState.Continuous);
        Console.WriteLine();
        Console.WriteLine("[ OK ] Sleep block RELEASED. Laptop is back to normal.");
    }
}
