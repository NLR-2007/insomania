using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Insominia.Services;

namespace Insominia.Views
{
    public partial class PanelWindow : Window
    {
        private readonly Controller _c = Controller.Instance;
        private bool _suppress;      // stops UI updates from firing user handlers
        private bool _closingForReal;
        private DateTime _ignoreDeactivateUntil = DateTime.MinValue;

        public PanelWindow()
        {
            InitializeComponent();

            MainSwitch.OnColor = Palette.Live;    // lamp amber = working
            SaverSwitch.OnColor = Palette.Accent;  // clay = feature on

            MainSwitch.Toggled += delegate
            {
                if (_suppress) return;
                if (MainSwitch.IsOn)
                {
                    if (!_c.Activate()) MainSwitch.IsOn = false;
                }
                else _c.Deactivate("switched off");
            };

            SaverSwitch.Toggled += delegate
            {
                if (_suppress) return;
                _c.SetBatterySaver(SaverSwitch.IsOn);
            };

            foreach (RadioButton chip in new[] { ChipNever, Chip30, Chip60, Chip120, Chip240 })
            {
                chip.Checked += delegate (object s, RoutedEventArgs e)
                {
                    if (_suppress) return;
                    _c.SetAutoStop(int.Parse((string)((RadioButton)s).Tag));
                };
            }

            BtnOpenMain.Click += delegate { App.ShowMainWindow(); Hide(); };
            BtnSetup.Click += delegate { DoSetup(); };
            BtnQuit.Click += delegate { App.QuitApp(); };

            _c.StateChanged += delegate { Dispatcher.BeginInvoke((Action)Sync); };

            Deactivated += delegate
            {
                if (_closingForReal) return;
                if (DateTime.UtcNow < _ignoreDeactivateUntil) return;
                HideSmooth();
            };

            Sync();
        }

        private void DoSetup()
        {
            MessageBox.Show(this,
                "Windows only lets an administrator change what closing the lid does.\n\n" +
                "Insominia will ask once and register a small helper. After that the switch " +
                "works silently and you will never see this prompt again.",
                "One-time setup", MessageBoxButton.OK, MessageBoxImage.Information);

            bool ok = _c.RunSetup();
            Sync();

            if (!ok)
                MessageBox.Show(this,
                    "Setup was not completed. Insominia can still block idle sleep, but closing " +
                    "the lid will put the laptop to sleep.",
                    "Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // ---- showing and hiding ------------------------------------------------

        /// <summary>Slides up from the tray with a short fade. 200ms, eased out.</summary>
        public void ShowSmooth()
        {
            Sync();

            Opacity = 0;
            PanelShift.Y = 14;
            PanelScale.ScaleX = PanelScale.ScaleY = 0.98;

            Show();

            // Position AFTER Show: SizeToContent only resolves the real height
            // once the window exists, and before that ActualHeight is 0.
            PositionNearTray();

            // Opening from the tray menu makes that menu close, which fires
            // Deactivated on us straight away and would hide the panel the
            // instant it appeared. Ignore deactivation briefly while settling.
            _ignoreDeactivateUntil = DateTime.UtcNow.AddMilliseconds(450);

            Activate();
            Focus();

            Duration d = new Duration(TimeSpan.FromMilliseconds(200));
            CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            BeginAnimation(OpacityProperty, new DoubleAnimation(1, d) { EasingFunction = ease });
            PanelShift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, d) { EasingFunction = ease });
            PanelScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1, d) { EasingFunction = ease });
            PanelScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1, d) { EasingFunction = ease });
        }

        public void HideSmooth()
        {
            if (!IsVisible) return;

            Duration d = new Duration(TimeSpan.FromMilliseconds(130));
            DoubleAnimation fade = new DoubleAnimation(0, d)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            };
            fade.Completed += delegate { if (Opacity <= 0.02) Hide(); };

            BeginAnimation(OpacityProperty, fade);
            PanelShift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(10, d) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
        }

        public void ToggleVisibility()
        {
            if (IsVisible && Opacity > 0.5) HideSmooth();
            else ShowSmooth();
        }

        /// <summary>
        /// Anchors the panel just above the tray.
        ///
        /// This used to read the work area from WinForms, in DEVICE pixels, and
        /// convert using PresentationSource - which is null until the window has
        /// actually been shown, so the scale silently fell back to 1.0. On any
        /// scaled display that put the panel off the right-hand edge of the
        /// screen: it opened every time, just where nobody could see it.
        /// SystemParameters.WorkArea is already in WPF units, so there is no
        /// conversion to get wrong.
        /// </summary>
        private void PositionNearTray()
        {
            UpdateLayout();

            Rect wa = SystemParameters.WorkArea;
            double h = ActualHeight > 0 ? ActualHeight : 520;

            Left = Math.Max(wa.Left + 8, wa.Right - Width - 4);
            Top = Math.Max(wa.Top + 8, wa.Bottom - h - 4);
        }

        public void CloseForReal()
        {
            _closingForReal = true;
            Close();
        }

        // ---- view sync ---------------------------------------------------------

        private void Sync()
        {
            _suppress = true;
            try
            {
                bool on = _c.IsActive;

                MainSwitch.IsOn = on;
                SaverSwitch.IsOn = _c.BatterySaver;

                Headline.Text = _c.StatusHeadline;
                Detail.Text = _c.StatusDetail;

                Color dot = !on
                    ? Palette.Idle
                    : (_c.LidSurvives ? Palette.Live : Palette.Warn);
                StatusDot.Fill = new SolidColorBrush(dot);
                PulseDot(on);

                // battery
                SafetyReading h = _c.Health;
                BatteryValue.Text = h.BatteryPercent >= 0 ? h.BatteryPercent + "%" : "--";
                double frac = h.BatteryPercent >= 0 ? h.BatteryPercent / 100.0 : 0;
                AnimateBar(BatteryFill, Math.Max(0, 120 * frac));
                BatteryFill.Background = h.BatteryPercent <= 15
                    ? (Brush)FindResource("Danger")
                    : (h.BatteryPercent <= 25 ? (Brush)FindResource("Warn") : (Brush)FindResource("LiveGradient"));

                // right-hand readout changes meaning with state
                if (on)
                {
                    TimeSpan? left = _c.Remaining;
                    if (left.HasValue)
                    {
                        RightLabel.Text = "STOPS IN";
                        RightValue.Text = Format(left.Value);
                        RightHint.Text = "running " + Format(_c.Elapsed);
                    }
                    else
                    {
                        RightLabel.Text = "RUNNING FOR";
                        RightValue.Text = Format(_c.Elapsed);
                        RightHint.Text = h.OnCharger ? "on charger" : "on battery";
                    }
                }
                else
                {
                    RightLabel.Text = "POWER";
                    RightValue.Text = h.OnCharger ? "Charging" : "On battery";
                    RightHint.Text = _c.KeepApps.Count + " apps protected";
                }

                // chips
                int m = _c.AutoStopMinutes;
                ChipNever.IsChecked = m == 0;
                Chip30.IsChecked = m == 30;
                Chip60.IsChecked = m == 60;
                Chip120.IsChecked = m == 120;
                Chip240.IsChecked = m == 240;

                SaverDetail.Text = _c.KeepApps.Count == 0
                    ? "Pick the apps to keep fast first"
                    : _c.KeepApps.Count + " app(s) stay fast, the rest slow down";

                SetupBanner.Visibility = _c.SetupDone ? Visibility.Collapsed : Visibility.Visible;

                FooterHint.Text = h.Verdict == SafetyVerdict.Warn ? h.Reason : "";
                FooterHint.Foreground = (Brush)FindResource("Warn");
            }
            finally { _suppress = false; }
        }

        private static string Format(TimeSpan t)
        {
            if (t.TotalHours >= 1) return (int)t.TotalHours + "h " + t.Minutes + "m";
            if (t.TotalMinutes >= 1) return t.Minutes + "m " + t.Seconds + "s";
            return t.Seconds + "s";
        }

        private static void AnimateBar(FrameworkElement bar, double to)
        {
            bar.BeginAnimation(WidthProperty,
                new DoubleAnimation(to, new Duration(TimeSpan.FromMilliseconds(320)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                });
        }

        private bool _pulsing;

        /// <summary>A slow breathing dot, so an idle panel still reads as "alive".</summary>
        private void PulseDot(bool on)
        {
            if (on == _pulsing) return;
            _pulsing = on;

            if (!on)
            {
                StatusDot.BeginAnimation(OpacityProperty, null);
                StatusDot.Opacity = 1;
                return;
            }

            DoubleAnimation pulse = new DoubleAnimation(1, 0.35,
                new Duration(TimeSpan.FromMilliseconds(1100)))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            StatusDot.BeginAnimation(OpacityProperty, pulse);
        }
    }
}
