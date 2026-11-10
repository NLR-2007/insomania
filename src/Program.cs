using System;
using System.Threading;
using System.Windows.Forms;

namespace Insominia
{
    internal static class Program
    {
        private const string MutexName = "Insominia_SingleInstance_8f2a1c";

        [STAThread]
        private static int Main(string[] args)
        {
            // Elevated helper mode: applies the lid setting, then exits.
            // Launched by the scheduled task, never by the user directly.
            foreach (string a in args)
            {
                if (string.Equals(a, "--apply-lid-request", StringComparison.OrdinalIgnoreCase))
                    return ElevatedTask.HandleLidRequest();
            }

            bool isNew;
            using (Mutex single = new Mutex(true, MutexName, out isNew))
            {
                if (!isNew)
                {
                    MessageBox.Show("Insominia is already running. Look for its icon near the clock.",
                        "Insominia", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.ThreadException += (s, e) =>
                {
                    Log.Write("UNHANDLED (ui): " + e.Exception);
                    KeepAwake.Release();   // fail safe: do not leave the laptop awake forever
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    Log.Write("UNHANDLED: " + e.ExceptionObject);
                    KeepAwake.Release();
                };

                try
                {
                    Application.Run(new TrayApp());
                }
                finally
                {
                    KeepAwake.Release();
                }
                return 0;
            }
        }
    }
}
