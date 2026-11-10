using System;
using System.Diagnostics;
using System.IO;

// A stand-in for a working AI agent: does real CPU work and reports how much
// it got done each second. If EcoQoS throttling works, the rate must drop.
internal static class Burner
{
    private static int Main(string[] args)
    {
        string log = args.Length > 0 ? args[0] : "burner.log";
        File.WriteAllText(log, "pid=" + Process.GetCurrentProcess().Id + Environment.NewLine);

        Stopwatch sw = Stopwatch.StartNew();
        long iterations = 0;
        int second = 0;
        double acc = 0.0;

        while (second < 20)
        {
            // real arithmetic work, not a sleep
            acc += Math.Sqrt(iterations % 10007) * Math.Sin(iterations % 360);
            iterations++;

            if (sw.ElapsedMilliseconds >= 1000)
            {
                second++;
                File.AppendAllText(log,
                    second.ToString("00") + "  " + (iterations / 1000) + "k iterations/sec" + Environment.NewLine);
                iterations = 0;
                sw.Restart();
            }
        }

        File.AppendAllText(log, "done (checksum " + acc.ToString("0") + ")" + Environment.NewLine);
        return 0;
    }
}
