using System;
using System.Collections.Generic;
using System.Diagnostics;
using Insominia;

internal static class Diag
{
    private static int Main(string[] args)
    {
        AppSettings.Load();

        // --throttle <pid> / --restore <pid> : used to prove EcoQoS really works
        if (args.Length == 2 && (args[0] == "--throttle" || args[0] == "--restore"))
        {
            int target;
            if (!int.TryParse(args[1], out target)) { Console.WriteLine("bad pid"); return 2; }
            bool eco = args[0] == "--throttle";
            bool ok = ProcessThrottler.SelfTest(target, eco);
            Console.WriteLine((eco ? "throttle " : "restore ") + target + " -> " + (ok ? "OK" : "FAILED"));
            return ok ? 0 : 1;
        }

        bool appsOnly = args.Length > 0 && args[0] == "--apps";
        if (!appsOnly) PrintCore();
        PrintApps();
        return 0;
    }

    private static void PrintCore()
    {
        Console.WriteLine("=== Insominia diagnostics ===");
        Console.WriteLine();
        Console.WriteLine("-- environment --");
        Console.WriteLine("  elevated now      : " + ElevatedTask.IsCurrentProcessElevated());
        Console.WriteLine("  helper registered : " + ElevatedTask.IsRegistered());
        Console.WriteLine("  start with windows: " + StartupManager.IsEnabled());
        Console.WriteLine();

        Console.WriteLine("-- lid behaviour --");
        LidAction ac, dc;
        bool ok = LidPolicy.TryRead(out ac, out dc);
        Console.WriteLine("  on charger        : " + (ok ? ac.ToString() : "system default (Sleep)"));
        Console.WriteLine("  on battery        : " + (ok ? dc.ToString() : "system default (Sleep)"));
        Console.WriteLine("  survives lid close: " + LidPolicy.IsSurvivingLidClose());
        Console.WriteLine();

        Console.WriteLine("-- health --");
        SafetyMonitor mon = new SafetyMonitor();
        mon.LoadThresholds();
        SafetyReading r = mon.Read();
        Console.WriteLine("  battery           : " + r.BatteryText);
        Console.WriteLine("  temperature       : " + r.TemperatureText);
        Console.WriteLine("  verdict           : " + r.Verdict + (r.Reason.Length > 0 ? " - " + r.Reason : ""));
        Console.WriteLine();

        Console.WriteLine("-- keep awake --");
        bool engaged = KeepAwake.Engage();
        Console.WriteLine("  engage/release    : " + (engaged ? "OK" : "FAILED - " + KeepAwake.LastError));
        KeepAwake.Release();
        Console.WriteLine();
    }

    private static void PrintApps()
    {
        Console.WriteLine("-- battery saver --");
        Console.WriteLine("  mode              : " + (AppSettings.GetBool("BatterySaver", false) ? "on" : "off"));
        Console.WriteLine("  apps kept fast    : " + AppSettings.GetString("KeepApps", "(none chosen)"));
        Console.WriteLine("  currently slowed  : " + AppSettings.GetString("ThrottledPids", "(none)"));
        Console.WriteLine();

        Console.WriteLine("  EcoQoS self-test on this very process:");
        int me = Process.GetCurrentProcess().Id;
        Console.WriteLine("    throttle        : " + (ProcessThrottler.SelfTest(me, true) ? "OK" : "FAILED"));
        Console.WriteLine("    restore         : " + (ProcessThrottler.SelfTest(me, false) ? "OK" : "FAILED"));
        Console.WriteLine();

        List<AppEntry> apps = ProcessThrottler.ListVisibleApps();
        Console.WriteLine("  visible apps the picker would offer (" + apps.Count + "):");
        foreach (AppEntry a in apps)
            Console.WriteLine("    " + a.DisplayName.PadRight(34) + a.MemoryText.PadLeft(8) +
                              "   " + a.Pids.Count + " proc   [" + a.ProcessName + "]");
    }
}
