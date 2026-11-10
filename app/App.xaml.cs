using System;
using System.Threading;
using System.Windows;
using Insominia.Services;
using Insominia.Views;
using WinForms = System.Windows.Forms;

namespace Insominia
{
    public partial class App : Application
    {
        private const string MutexName = "Insominia_SingleInstance_8f2a1c";

        private static Mutex _single;
        private static PanelWindow _panel;
        private static MainWindow _main;
        private static WinForms.NotifyIcon _tray;
        private static bool _reportedCrash;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Elevated helper mode: applies the lid setting, then exits with a
            // status code. Launched only by the scheduled task, never by hand.
            foreach (string a in e.Args)
            {
                if (string.Equals(a, "--apply-lid-request", StringComparison.OrdinalIgnoreCase))
                {
                    int code = ElevatedTask.HandleLidRequest();
                    Shutdown(code);
                    return;
                }
            }

            bool isNew;
            _single = new Mutex(true, MutexName, out isNew);
            if (!isNew)
            {
                MessageBox.Show("Insominia is already running. Its icon is near the clock.",
                    "Insominia", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            }

            DispatcherUnhandledException += (s, ev) =>
            {
                Log.Write("UNHANDLED (ui): " + ev.Exception);
                KeepAwake.Release();      // fail safe - never strand the laptop awake

                // Swallowing silently once hid a window that had failed to
                // build at all: the app looked like it was running while half
                // of it did not exist. Say so, once, then carry on so the tray
                // icon stays available and the laptop is never left awake.
                if (!_reportedCrash)
                {
                    _reportedCrash = true;
                    MessageBox.Show(
                        "Something went wrong inside Insominia.\n\n" +
                        ev.Exception.GetType().Name + ": " + ev.Exception.Message +
                        "\n\nThe full details are in the log:\n" + AppSettings.LogPath,
                        "Insominia", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                ev.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                Log.Write("UNHANDLED: " + ev.ExceptionObject);
                KeepAwake.Release();
            };

            Controller c = Controller.Instance;

            _panel = new PanelWindow();
            _main = new MainWindow();

            BuildTray();

            c.StateChanged += delegate { UpdateTray(); };
            c.Notice += delegate (object s, NoticeEventArgs n)
            {
                try
                {
                    _tray.BalloonTipTitle = n.Title;
                    _tray.BalloonTipText = n.Message;
                    _tray.BalloonTipIcon = n.IsWarning ? WinForms.ToolTipIcon.Warning : WinForms.ToolTipIcon.Info;
                    _tray.ShowBalloonTip(6000);
                }
                catch { }
            };

            Microsoft.Win32.SystemEvents.SessionEnding += delegate { QuitApp(); };
            UpdateTray();

            // Double-clicking an app and getting nothing at all feels broken,
            // however correct "it lives in the tray" is. So a manual launch
            // always opens the panel; only the Windows startup entry, which
            // passes --autostart, starts quietly in the tray.
            bool autoStarted = false;
            foreach (string a in e.Args)
                if (string.Equals(a, StartupManager.AutoStartFlag, StringComparison.OrdinalIgnoreCase))
                    autoStarted = true;

            AppSettings.Set("HasLaunchedBefore", true);
            AppSettings.Save();

            Log.Write("startup: autoStarted=" + autoStarted + " args=[" + string.Join(" ", e.Args) + "]");

            if (!autoStarted)
            {
                _panel.ShowSmooth();
            }
        }

        private void BuildTray()
        {
            _tray = new WinForms.NotifyIcon
            {
                Icon = TrayIcons.Get(TrayState.Idle),
                Text = "Insominia",
                Visible = true,
            };

            _tray.MouseClick += delegate (object s, WinForms.MouseEventArgs me)
            {
                if (me.Button == WinForms.MouseButtons.Left)
                    Current.Dispatcher.BeginInvoke((Action)(() => _panel.ToggleVisibility()));
            };

            WinForms.ContextMenuStrip menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Open Insominia", null, delegate
            {
                Current.Dispatcher.BeginInvoke((Action)(() => _panel.ShowSmooth()));
            });
            // "&&" because WinForms reads a single & as a keyboard-shortcut
            // marker and swallows it, which rendered this as "Apps  settings".
            menu.Items.Add("Apps && settings", null, delegate
            {
                Current.Dispatcher.BeginInvoke((Action)ShowMainWindow);
            });
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("Quit", null, delegate
            {
                Current.Dispatcher.BeginInvoke((Action)QuitApp);
            });
            _tray.ContextMenuStrip = menu;
        }

        private static void UpdateTray()
        {
            if (_tray == null) return;
            Controller c = Controller.Instance;

            TrayState state = !c.IsActive
                ? TrayState.Idle
                : (c.Health != null && c.Health.Verdict == SafetyVerdict.Warn
                    ? TrayState.Warning
                    : TrayState.Active);

            _tray.Icon = TrayIcons.Get(state);

            string text = c.IsActive
                ? (c.LidSurvives ? "Insominia - working, lid close is safe" : "Insominia - idle sleep blocked only")
                : "Insominia - ready";
            _tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }

        public static void ShowMainWindow()
        {
            if (_main == null) return;
            _main.ShowSmooth();
        }

        public static void QuitApp()
        {
            try
            {
                Controller.Instance.Shutdown();
                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
                if (_panel != null) _panel.CloseForReal();
                if (_main != null) _main.Close();
            }
            catch (Exception ex) { Log.Write("quit error: " + ex.Message); }
            finally { Current.Shutdown(); }
        }
    }
}
