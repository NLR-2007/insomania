using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Insominia
{
    /// <summary>
    /// The tray app. One switch: keep the machine working when the lid closes.
    /// </summary>
    internal sealed class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly SafetyMonitor _safety = new SafetyMonitor();
        private readonly Timer _watchdog;

        private ToolStripMenuItem _miStatus;
        private ToolStripMenuItem _miToggle;
        private ToolStripMenuItem _miAutoStop;
        private ToolStripMenuItem _miSetup;
        private ToolStripMenuItem _miStartup;
        private ToolStripMenuItem _miApps;
        private ToolStripMenuItem _miSaver;

        private bool _active;
        private DateTime _activatedAt;
        private int _autoStopMinutes;          // 0 = never
        private SafetyReading _lastReading = new SafetyReading();

        private HashSet<string> _keepApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _batterySaver;
        private ThrottleResult _lastThrottle;

        public TrayApp()
        {
            AppSettings.Load();
            _safety.LoadThresholds();
            _autoStopMinutes = AppSettings.GetInt("AutoStopMinutes", 0);
            _batterySaver = AppSettings.GetBool("BatterySaver", false);
            LoadKeepApps();

            _menu = new ContextMenuStrip();
            BuildMenu();

            _tray = new NotifyIcon
            {
                Icon = TrayIcons.Get(TrayState.Idle),
                Text = "Insominia - off",
                Visible = true,
                ContextMenuStrip = _menu,
            };
            _tray.DoubleClick += (s, e) => ToggleActive();

            _watchdog = new Timer { Interval = 15000 };   // every 15 seconds
            _watchdog.Tick += (s, e) => Tick();

            Application.ApplicationExit += (s, e) => Cleanup();
            Microsoft.Win32.SystemEvents.SessionEnding += (s, e) => Cleanup();

            RefreshUi();
            Log.Write("Insominia started. exe=" + ElevatedTask.ExePath);

            SelfHeal();

            if (AppSettings.GetBool("RestoreLastState", false) && AppSettings.GetBool("WasActive", false))
                Activate(silent: true);
        }

        /// <summary>
        /// If the app was force-killed or crashed while ON, the lid override is
        /// still in place and the user's laptop would never sleep. Put it back.
        /// </summary>
        private void SelfHeal()
        {
            try
            {
                // Apps left slowed down by a crash must be sped back up.
                ProcessThrottler.RestoreOrphansFromLastRun();

                if (_active) return;
                if (!AppSettings.Has("OriginalLidAc")) return;   // we never changed it
                if (!LidPolicy.IsSurvivingLidClose()) return;    // nothing to undo
                if (!ElevatedTask.IsRegistered()) return;        // cannot undo without the helper

                Log.Write("self-heal: lid override was left applied, restoring " + RememberedLidAction());
                bool ok = ElevatedTask.RequestLid(RememberedLidAction(), RememberedLidAction());
                Log.Write("self-heal: restore " + (ok ? "ok" : "FAILED"));
                RefreshUi();
            }
            catch (Exception ex) { Log.Write("self-heal error: " + ex.Message); }
        }

        // ---------------------------------------------------------------- menu

        private void BuildMenu()
        {
            ToolStripMenuItem title = new ToolStripMenuItem("Insominia") { Enabled = false };
            title.Font = new Font(title.Font, FontStyle.Bold);

            _miStatus = new ToolStripMenuItem("...") { Enabled = false };

            _miToggle = new ToolStripMenuItem("Keep working when lid closes", null, (s, e) => ToggleActive())
            {
                CheckOnClick = false,
            };

            _miAutoStop = new ToolStripMenuItem("Auto-stop after");
            AddAutoStopOption("Never", 0);
            AddAutoStopOption("30 minutes", 30);
            AddAutoStopOption("1 hour", 60);
            AddAutoStopOption("2 hours", 120);
            AddAutoStopOption("4 hours", 240);

            _miSaver = new ToolStripMenuItem("Battery saver: slow down other apps", null, (s, e) =>
            {
                _batterySaver = !_batterySaver;
                AppSettings.Set("BatterySaver", _batterySaver);
                AppSettings.Save();

                // Take effect straight away if we are already running.
                if (_active)
                {
                    if (_batterySaver) ApplyBatterySaver();
                    else ProcessThrottler.RestoreAll();
                }
                RefreshUi();
            });

            _miApps = new ToolStripMenuItem("Choose apps to keep running...", null, (s, e) => ChooseApps());

            _miSetup = new ToolStripMenuItem("One-time setup (needs admin once)", null, (s, e) => RunSetup());

            _miStartup = new ToolStripMenuItem("Start with Windows", null, (s, e) =>
            {
                bool now = !StartupManager.IsEnabled();
                StartupManager.SetEnabled(now);
                RefreshUi();
            });

            _menu.Items.Add(title);
            _menu.Items.Add(_miStatus);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_miToggle);
            _menu.Items.Add(_miAutoStop);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_miSaver);
            _menu.Items.Add(_miApps);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_miSetup);
            _menu.Items.Add(_miStartup);
            _menu.Items.Add(new ToolStripMenuItem("Details / diagnostics...", null, (s, e) => ShowDetails()));
            _menu.Items.Add(new ToolStripMenuItem("Open log folder", null, (s, e) => OpenLogFolder()));
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => ExitApp()));
        }

        private void AddAutoStopOption(string text, int minutes)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text, null, (s, e) =>
            {
                _autoStopMinutes = minutes;
                AppSettings.Set("AutoStopMinutes", minutes);
                AppSettings.Save();
                RefreshUi();
            });
            item.Tag = minutes;
            _miAutoStop.DropDownItems.Add(item);
        }

        // ------------------------------------------------------------- actions

        private void ToggleActive()
        {
            if (_active) Deactivate("switched off by you");
            else Activate(silent: false);
        }

        private void Activate(bool silent)
        {
            if (_active) return;

            // 1. Safety gate first - never start into a bad state.
            SafetyReading reading = _safety.Read();
            _lastReading = reading;
            if (reading.Verdict == SafetyVerdict.Stop)
            {
                if (!silent)
                    MessageBox.Show(
                        "Cannot start right now.\r\n\r\n" + reading.Reason +
                        "\r\n\r\nFix that first, then try again.",
                        "Insominia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 2. Stop Windows sleeping from inactivity. No admin needed.
            if (!KeepAwake.Engage())
            {
                MessageBox.Show("Windows refused the stay-awake request.\r\n\r\n" + KeepAwake.LastError,
                    "Insominia", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 3. Make the lid close do nothing. This part needs the elevated helper.
            bool lidOk = ApplyLid(LidAction.DoNothing);

            // 4. Slow down everything the user did not ask to keep running.
            if (_batterySaver) ApplyBatterySaver();

            _active = true;
            _activatedAt = DateTime.Now;
            AppSettings.Set("WasActive", true);
            AppSettings.Save();

            _watchdog.Start();
            RefreshUi();
            Log.Write("ACTIVATED (lid override " + (lidOk ? "applied" : "NOT applied") + ")");

            if (!silent)
            {
                if (lidOk)
                    Notify("Insominia is on",
                        "You can close the lid. Your task keeps running." +
                        (_autoStopMinutes > 0 ? " Auto-stops after " + AutoStopText() + "." : ""));
                else
                    Notify("Partly on",
                        "Idle sleep is blocked, but closing the lid will still sleep the laptop. " +
                        "Run the one-time setup to fix that.");
            }
        }

        private void Deactivate(string why)
        {
            if (!_active) return;

            // Speed the other apps back up BEFORE anything else, so the machine
            // feels normal the instant the user switches off.
            ProcessThrottler.RestoreAll();
            _lastThrottle = null;

            ApplyLid(RememberedLidAction());
            KeepAwake.Release();

            _active = false;
            _watchdog.Stop();
            AppSettings.Set("WasActive", false);
            AppSettings.Save();

            RefreshUi();
            Log.Write("DEACTIVATED (" + why + ")");
        }

        /// <summary>Applies a lid action through the elevated helper, remembering the original first.</summary>
        private bool ApplyLid(LidAction target)
        {
            LidAction ac, dc;
            if (LidPolicy.TryRead(out ac, out dc))
            {
                // Remember the user's own setting exactly once, so we can always put it back.
                if (!AppSettings.Has("OriginalLidAc") && ac != LidAction.DoNothing)
                {
                    AppSettings.Set("OriginalLidAc", (int)ac);
                    AppSettings.Set("OriginalLidDc", (int)dc);
                    AppSettings.Save();
                }
            }

            if (!ElevatedTask.IsRegistered()) return false;
            return ElevatedTask.RequestLid(target, target);
        }

        private LidAction RememberedLidAction()
        {
            return (LidAction)AppSettings.GetInt("OriginalLidAc", (int)LidAction.Sleep);
        }

        // ------------------------------------------------------ battery saver

        private void LoadKeepApps()
        {
            _keepApps.Clear();
            string raw = AppSettings.GetString("KeepApps", "");
            foreach (string part in raw.Split(','))
            {
                string name = part.Trim();
                if (name.Length > 0) _keepApps.Add(name);
            }
        }

        private void SaveKeepApps()
        {
            string[] names = new string[_keepApps.Count];
            _keepApps.CopyTo(names);
            AppSettings.Set("KeepApps", string.Join(",", names));
            AppSettings.Save();
        }

        private void ApplyBatterySaver()
        {
            _lastThrottle = ProcessThrottler.ThrottleAllExcept(_keepApps);
        }

        private void ChooseApps()
        {
            using (AppPickerForm picker = new AppPickerForm(_keepApps))
            {
                picker.TopMost = true;
                if (picker.ShowDialog() != DialogResult.OK) return;

                _keepApps = picker.Selected;
                SaveKeepApps();

                // Re-apply immediately so the new choice takes effect now.
                if (_active && _batterySaver)
                {
                    ProcessThrottler.RestoreAll();
                    ApplyBatterySaver();
                }

                if (!_batterySaver)
                {
                    DialogResult turnOn = MessageBox.Show(
                        "Saved.\r\n\r\nBattery saver is currently off, so nothing will be slowed down yet. " +
                        "Turn it on now?",
                        "Insominia", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (turnOn == DialogResult.Yes)
                    {
                        _batterySaver = true;
                        AppSettings.Set("BatterySaver", true);
                        AppSettings.Save();
                        if (_active) ApplyBatterySaver();
                    }
                }

                RefreshUi();
            }
        }

        private void RunSetup()
        {
            if (ElevatedTask.IsRegistered())
            {
                DialogResult answer = MessageBox.Show(
                    "Setup is already done.\r\n\r\nRemove it? Insominia will then no longer be able to " +
                    "keep working when the lid closes.",
                    "Insominia setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (answer == DialogResult.Yes)
                {
                    if (_active) Deactivate("setup removed");
                    ElevatedTask.Unregister();
                    RefreshUi();
                }
                return;
            }

            MessageBox.Show(
                "Windows only lets an administrator change what the lid-close button does.\r\n\r\n" +
                "Insominia will ask for permission ONCE and register a small helper task. " +
                "After that the switch works silently - you will never see this prompt again.\r\n\r\n" +
                "Click OK, then choose Yes on the Windows prompt.",
                "One-time setup", MessageBoxButtons.OK, MessageBoxIcon.Information);

            bool ok = ElevatedTask.Register();

            // If Insominia was already ON when setup finished, the lid override
            // was never applied. Apply it now instead of making the user
            // switch off and on again.
            if (ok && _active && !LidPolicy.IsSurvivingLidClose())
            {
                bool lidOk = ApplyLid(LidAction.DoNothing);
                Log.Write("setup: applied lid override to the running session (" + (lidOk ? "ok" : "failed") + ")");
            }

            RefreshUi();

            MessageBox.Show(
                ok ? "Setup complete. The switch will now work silently."
                   : "Setup was not completed. Without it, Insominia can still block idle sleep, " +
                     "but closing the lid will put the laptop to sleep.",
                "Insominia setup", MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        // -------------------------------------------------------------- ticker

        private void Tick()
        {
            SafetyReading reading = _safety.Read();
            _lastReading = reading;

            if (reading.Verdict == SafetyVerdict.Stop)
            {
                Deactivate("safety: " + reading.Reason);
                Notify("Insominia stopped", reading.Reason + " Your task may still be running - check it.");
                RefreshUi();
                return;
            }

            if (_autoStopMinutes > 0 &&
                (DateTime.Now - _activatedAt).TotalMinutes >= _autoStopMinutes)
            {
                Deactivate("auto-stop after " + _autoStopMinutes + " min");
                Notify("Insominia stopped", "The " + AutoStopText() + " limit was reached.");
                RefreshUi();
                return;
            }

            if (reading.Verdict == SafetyVerdict.Warn)
                Log.Write("warn: " + reading.Reason);

            RefreshUi();
        }

        // ------------------------------------------------------------------ ui

        private void RefreshUi()
        {
            bool setupDone = ElevatedTask.IsRegistered();
            bool lidSurvives = LidPolicy.IsSurvivingLidClose();

            TrayState state = !_active
                ? TrayState.Idle
                : (_lastReading.Verdict == SafetyVerdict.Warn ? TrayState.Warning : TrayState.Active);

            _tray.Icon = TrayIcons.Get(state);

            string head = _active
                ? (lidSurvives ? "ON - lid close is safe" : "ON - idle sleep blocked only")
                : "OFF";

            _tray.Text = Truncate("Insominia - " + head, 63);

            _miStatus.Text = head +
                "   |   battery " + _lastReading.BatteryText +
                "   |   temp " + _lastReading.TemperatureText;

            _miToggle.Text = _active
                ? "Turn OFF (back to normal)"
                : "Turn ON - keep working when lid closes";

            _miSetup.Text = setupDone
                ? "One-time setup: done (click to remove)"
                : "One-time setup (needs admin once)";
            _miSetup.Checked = setupDone;

            _miStartup.Checked = StartupManager.IsEnabled();

            _miSaver.Checked = _batterySaver;
            _miSaver.Text = "Battery saver: " + (_batterySaver ? "on" : "off") +
                (_batterySaver && _active && _lastThrottle != null
                    ? "  (" + _lastThrottle.Throttled + " apps slowed)"
                    : "");

            _miApps.Text = _keepApps.Count == 0
                ? "Choose apps to keep running... (none chosen yet)"
                : "Choose apps to keep running... (" + _keepApps.Count + " chosen)";

            _miAutoStop.Text = "Auto-stop after: " + AutoStopText();
            foreach (ToolStripItem item in _miAutoStop.DropDownItems)
            {
                ToolStripMenuItem mi = item as ToolStripMenuItem;
                if (mi != null) mi.Checked = (int)mi.Tag == _autoStopMinutes;
            }
        }

        private string AutoStopText()
        {
            if (_autoStopMinutes <= 0) return "never";
            if (_autoStopMinutes < 60) return _autoStopMinutes + " min";
            return (_autoStopMinutes / 60.0).ToString("0.#", CultureInfo.InvariantCulture) + " h";
        }

        private void ShowDetails()
        {
            LidAction ac, dc;
            bool readOk = LidPolicy.TryRead(out ac, out dc);
            SafetyReading r = _safety.Read();
            _lastReading = r;

            string text =
                "STATE\r\n" +
                "  Insominia:          " + (_active ? "ON since " + _activatedAt.ToString("HH:mm:ss") : "OFF") + "\r\n" +
                "  Sleep block:        " + (KeepAwake.IsEngaged ? "engaged" : "not engaged") + "\r\n" +
                "  Auto-stop:          " + AutoStopText() + "\r\n\r\n" +
                "LID BEHAVIOUR\r\n" +
                "  On charger:         " + (readOk ? ac.ToString() : "unknown (system default)") + "\r\n" +
                "  On battery:         " + (readOk ? dc.ToString() : "unknown (system default)") + "\r\n" +
                "  Survives lid close: " + (LidPolicy.IsSurvivingLidClose() ? "YES" : "no") + "\r\n" +
                "  Saved original:     " + RememberedLidAction() + "\r\n\r\n" +
                "HEALTH\r\n" +
                "  Battery:            " + r.BatteryText + "\r\n" +
                "  Temperature:        " + r.TemperatureText + "\r\n" +
                "  Verdict:            " + r.Verdict + (r.Reason.Length > 0 ? " - " + r.Reason : "") + "\r\n" +
                "  Stop limits:        below " + _safety.StopBatteryPercent + "% battery, above " +
                                                 _safety.StopTemperatureC.ToString("0") + " C\r\n\r\n" +
                "BATTERY SAVER\r\n" +
                "  Mode:               " + (_batterySaver ? "on - other apps slowed down" : "off - all apps full speed") + "\r\n" +
                "  Apps kept fast:     " + (_keepApps.Count == 0 ? "(none chosen)" : string.Join(", ", KeepAppsArray())) + "\r\n" +
                "  Currently slowed:   " + ProcessThrottler.ThrottledCount + " processes\r\n\r\n" +
                "SETUP\r\n" +
                "  Helper task:        " + (ElevatedTask.IsRegistered() ? "registered" : "NOT registered") + "\r\n" +
                "  Start with Windows: " + (StartupManager.IsEnabled() ? "yes" : "no") + "\r\n" +
                "  Settings file:      " + AppSettings.FilePath;

            MessageBox.Show(text, "Insominia - details", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenLogFolder()
        {
            try
            {
                System.IO.Directory.CreateDirectory(AppSettings.Dir);
                Process.Start("explorer.exe", "\"" + AppSettings.Dir + "\"");
            }
            catch (Exception ex) { Log.Write("open log folder failed: " + ex.Message); }
        }

        private void Notify(string title, string message)
        {
            try
            {
                _tray.BalloonTipTitle = title;
                _tray.BalloonTipText = message;
                _tray.BalloonTipIcon = ToolTipIcon.Info;
                _tray.ShowBalloonTip(6000);
            }
            catch { }
        }

        private string[] KeepAppsArray()
        {
            string[] names = new string[_keepApps.Count];
            _keepApps.CopyTo(names);
            Array.Sort(names, StringComparer.OrdinalIgnoreCase);
            return names;
        }

        private static string Truncate(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max);
        }

        // ----------------------------------------------------------- shutdown

        private void ExitApp()
        {
            if (_active)
            {
                DialogResult answer = MessageBox.Show(
                    "Insominia is ON. If you exit, closing the lid will put the laptop to sleep again " +
                    "and any running task will be paused.\r\n\r\nExit anyway?",
                    "Insominia", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }

            Cleanup();
            ExitThread();
        }

        private bool _cleaned;

        private void Cleanup()
        {
            if (_cleaned) return;
            _cleaned = true;

            try
            {
                // Never leave the user's laptop unable to sleep, and never leave
                // their apps stuck in slow mode.
                if (_active) Deactivate("app closing");
                else { ProcessThrottler.RestoreAll(); KeepAwake.Release(); }

                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
                Log.Write("Insominia exited cleanly.");
            }
            catch (Exception ex) { Log.Write("cleanup error: " + ex.Message); }
        }
    }
}
