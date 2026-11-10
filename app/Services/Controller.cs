using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Threading;

namespace Insominia.Services
{
    internal sealed class NoticeEventArgs : EventArgs
    {
        public string Title;
        public string Message;
        public bool IsWarning;
    }

    /// <summary>
    /// All of Insominia's behaviour lives here, with no reference to any window.
    /// The panel and the main window are both just views onto this.
    /// </summary>
    internal sealed class Controller
    {
        public static readonly Controller Instance = new Controller();

        private readonly SafetyMonitor _safety = new SafetyMonitor();
        private readonly DispatcherTimer _watchdog;
        private readonly DispatcherTimer _ticker;

        private bool _cleaned;

        public event EventHandler StateChanged;
        public event EventHandler<NoticeEventArgs> Notice;

        // ---- state -----------------------------------------------------------

        public bool IsActive { get; private set; }
        public DateTime ActivatedAt { get; private set; }
        public SafetyReading Health { get; private set; }
        public ThrottleResult LastThrottle { get; private set; }

        public bool BatterySaver { get; private set; }
        public int AutoStopMinutes { get; private set; }
        public HashSet<string> KeepApps { get; private set; }

        public bool SetupDone { get { return ElevatedTask.IsRegistered(); } }
        public bool LidSurvives { get { return LidPolicy.IsSurvivingLidClose(); } }
        public bool StartsWithWindows { get { return StartupManager.IsEnabled(); } }
        public int ThrottledCount { get { return ProcessThrottler.ThrottledCount; } }

        public TimeSpan Elapsed
        {
            get { return IsActive ? DateTime.Now - ActivatedAt : TimeSpan.Zero; }
        }

        public TimeSpan? Remaining
        {
            get
            {
                if (!IsActive || AutoStopMinutes <= 0) return null;
                TimeSpan left = TimeSpan.FromMinutes(AutoStopMinutes) - Elapsed;
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }

        /// <summary>Short sentence describing exactly what is true right now.</summary>
        public string StatusHeadline
        {
            get
            {
                if (!IsActive) return "Ready";
                return LidSurvives ? "Working" : "Partly on";
            }
        }

        public string StatusDetail
        {
            get
            {
                if (!IsActive)
                    return SetupDone
                        ? "Turn on before you walk away"
                        : "Finish setup to survive the lid closing";

                if (!LidSurvives)
                    return "Idle sleep blocked, but closing the lid will still sleep";

                string s = "You can close the lid";
                if (BatterySaver && LastThrottle != null)
                    s += "  ·  " + LastThrottle.Throttled + " apps slowed";
                return s;
            }
        }

        // ---- life ------------------------------------------------------------

        private Controller()
        {
            AppSettings.Load();
            _safety.LoadThresholds();

            AutoStopMinutes = AppSettings.GetInt("AutoStopMinutes", 0);
            BatterySaver = AppSettings.GetBool("BatterySaver", false);
            KeepApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in AppSettings.GetString("KeepApps", "").Split(','))
                if (part.Trim().Length > 0) KeepApps.Add(part.Trim());

            Health = _safety.Read();

            _watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _watchdog.Tick += (s, e) => Watch();

            // Separate, faster timer purely so the elapsed/remaining readout
            // counts smoothly instead of jumping every 15 seconds.
            _ticker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _ticker.Tick += (s, e) => Raise();

            SelfHeal();
            Log.Write("Insominia v2 started. exe=" + ElevatedTask.ExePath);
        }

        private void SelfHeal()
        {
            try
            {
                ProcessThrottler.RestoreOrphansFromLastRun();

                if (!AppSettings.Has("OriginalLidAc")) return;
                if (!LidPolicy.IsSurvivingLidClose()) return;
                if (!ElevatedTask.IsRegistered()) return;

                LidAction original = RememberedLid();
                Log.Write("self-heal: lid was left overridden, restoring " + original);
                ElevatedTask.RequestLid(original, original);
            }
            catch (Exception ex) { Log.Write("self-heal failed: " + ex.Message); }
        }

        // ---- commands --------------------------------------------------------

        public void Toggle()
        {
            if (IsActive) Deactivate("switched off");
            else Activate();
        }

        public bool Activate()
        {
            if (IsActive) return true;

            Health = _safety.Read();
            if (Health.Verdict == SafetyVerdict.Stop)
            {
                RaiseNotice("Cannot start", Health.Reason, true);
                return false;
            }

            if (!KeepAwake.Engage())
            {
                RaiseNotice("Windows refused", KeepAwake.LastError ?? "Could not block sleep.", true);
                return false;
            }

            bool lidOk = ApplyLid(LidAction.DoNothing);
            if (BatterySaver) LastThrottle = ProcessThrottler.ThrottleAllExcept(KeepApps);

            IsActive = true;
            ActivatedAt = DateTime.Now;
            AppSettings.Set("WasActive", true);
            AppSettings.Save();

            _watchdog.Start();
            _ticker.Start();
            Log.Write("ACTIVATED (lid override " + (lidOk ? "applied" : "NOT applied") + ")");
            Raise();
            return true;
        }

        public void Deactivate(string why)
        {
            if (!IsActive) return;

            ProcessThrottler.RestoreAll();
            LastThrottle = null;

            ApplyLid(RememberedLid());
            KeepAwake.Release();

            IsActive = false;
            _watchdog.Stop();
            _ticker.Stop();

            AppSettings.Set("WasActive", false);
            AppSettings.Save();

            Log.Write("DEACTIVATED (" + why + ")");
            Raise();
        }

        public void SetBatterySaver(bool on)
        {
            BatterySaver = on;
            AppSettings.Set("BatterySaver", on);
            AppSettings.Save();

            if (IsActive)
            {
                if (on) LastThrottle = ProcessThrottler.ThrottleAllExcept(KeepApps);
                else { ProcessThrottler.RestoreAll(); LastThrottle = null; }
            }
            Raise();
        }

        public void SetKeepApps(IEnumerable<string> names)
        {
            KeepApps = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

            string[] arr = new string[KeepApps.Count];
            KeepApps.CopyTo(arr);
            AppSettings.Set("KeepApps", string.Join(",", arr));
            AppSettings.Save();

            if (IsActive && BatterySaver)
            {
                ProcessThrottler.RestoreAll();
                LastThrottle = ProcessThrottler.ThrottleAllExcept(KeepApps);
            }
            Raise();
        }

        public void SetAutoStop(int minutes)
        {
            AutoStopMinutes = minutes;
            AppSettings.Set("AutoStopMinutes", minutes);
            AppSettings.Save();
            Raise();
        }

        public void SetStartWithWindows(bool on)
        {
            StartupManager.SetEnabled(on);
            Raise();
        }

        /// <summary>One-time UAC. Returns false if the user declined.</summary>
        public bool RunSetup()
        {
            bool ok = ElevatedTask.Register();

            // If we were already on, apply the override now rather than making
            // the user switch off and on again.
            if (ok && IsActive && !LidPolicy.IsSurvivingLidClose())
                ApplyLid(LidAction.DoNothing);

            Raise();
            return ok;
        }

        public bool RemoveSetup()
        {
            if (IsActive) Deactivate("setup removed");
            bool ok = ElevatedTask.Unregister();
            Raise();
            return ok;
        }

        public void RefreshHealth()
        {
            Health = _safety.Read();
            Raise();
        }

        // ---- internals -------------------------------------------------------

        private void Watch()
        {
            Health = _safety.Read();

            if (Health.Verdict == SafetyVerdict.Stop)
            {
                string reason = Health.Reason;
                Deactivate("safety: " + reason);
                RaiseNotice("Insominia stopped", reason + " Check your task.", true);
                return;
            }

            if (AutoStopMinutes > 0 && Elapsed.TotalMinutes >= AutoStopMinutes)
            {
                Deactivate("auto-stop");
                RaiseNotice("Insominia stopped", "The " + AutoStopText() + " limit was reached.", false);
                return;
            }

            Raise();
        }

        public string AutoStopText()
        {
            if (AutoStopMinutes <= 0) return "no limit";
            if (AutoStopMinutes < 60) return AutoStopMinutes + " min";
            return (AutoStopMinutes / 60.0).ToString("0.#", CultureInfo.InvariantCulture) + " h";
        }

        private LidAction RememberedLid()
        {
            return (LidAction)AppSettings.GetInt("OriginalLidAc", (int)LidAction.Sleep);
        }

        private bool ApplyLid(LidAction target)
        {
            LidAction ac, dc;
            if (LidPolicy.TryRead(out ac, out dc))
            {
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

        private void Raise()
        {
            EventHandler h = StateChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void RaiseNotice(string title, string message, bool warning)
        {
            EventHandler<NoticeEventArgs> h = Notice;
            if (h != null)
                h(this, new NoticeEventArgs { Title = title, Message = message, IsWarning = warning });
        }

        /// <summary>Never leave the machine unable to sleep, or apps stuck slow.</summary>
        public void Shutdown()
        {
            if (_cleaned) return;
            _cleaned = true;
            try
            {
                if (IsActive) Deactivate("app closing");
                else { ProcessThrottler.RestoreAll(); KeepAwake.Release(); }
                Log.Write("Insominia exited cleanly.");
            }
            catch (Exception ex) { Log.Write("shutdown error: " + ex.Message); }
        }
    }
}
