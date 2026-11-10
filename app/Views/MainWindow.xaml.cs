using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Insominia.Controls;
using Insominia.Services;

namespace Insominia.Views
{
    public partial class MainWindow : Window
    {
        private readonly Controller _c = Controller.Instance;
        private List<AppEntry> _apps = new List<AppEntry>();
        private readonly HashSet<string> _picked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _suppress;

        private static readonly Color Live = Palette.Live;
        private static readonly Color Caution = Palette.Warn;
        private static readonly Color Idle = Palette.Idle;

        public MainWindow()
        {
            InitializeComponent();

            foreach (string n in _c.KeepApps) _picked.Add(n);

            SwMaster.OnColor = Live;
            SwSaver.OnColor = Palette.Accent;
            SwStartup.OnColor = Palette.Accent;

            TitleBar.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };

            BtnClose.Click += delegate { HideSmooth(); };
            BtnMinimise.Click += delegate { WindowState = WindowState.Minimized; };

            SwMaster.Toggled += delegate
            {
                if (_suppress) return;
                if (SwMaster.IsOn) { if (!_c.Activate()) SwMaster.IsOn = false; }
                else _c.Deactivate("switched off");
            };

            // Checked fires only when the selection CHANGES, so clicking the
            // item you are already on would do nothing. Handling Click as well
            // makes every click re-show its page, which is what people expect.
            NavApps.Checked += delegate { ShowPage(PageApps); };
            NavApps.Click += delegate { ShowPage(PageApps); };

            NavSettings.Checked += delegate { ShowPage(PageSettings); };
            NavSettings.Click += delegate { ShowPage(PageSettings); };

            NavAbout.Checked += delegate { ShowPage(PageAbout); RefreshDiagnostics(); };
            NavAbout.Click += delegate { ShowPage(PageAbout); RefreshDiagnostics(); };

            Search.TextChanged += delegate
            {
                SearchPlaceholder.Visibility = Search.Text.Length == 0
                    ? Visibility.Visible : Visibility.Collapsed;
                RenderApps();
            };

            BtnRefresh.Click += delegate { LoadApps(); };
            BtnClearAll.Click += delegate { _picked.Clear(); Commit(); RenderApps(); };

            SwStartup.Toggled += delegate { if (!_suppress) _c.SetStartWithWindows(SwStartup.IsOn); };
            SwSaver.Toggled += delegate { if (!_suppress) _c.SetBatterySaver(SwSaver.IsOn); };

            foreach (RadioButton chip in new[] { ChipNever, Chip30, Chip60, Chip120, Chip240 })
            {
                chip.Checked += delegate (object s, RoutedEventArgs e)
                {
                    if (_suppress) return;
                    _c.SetAutoStop(int.Parse((string)((RadioButton)s).Tag));
                };
            }

            BtnDoSetup.Click += delegate { DoSetup(); };
            BtnSideSetup.Click += delegate { DoSetup(); };

            BtnUndoSetup.Click += delegate
            {
                if (MessageBox.Show(this,
                        "Remove the helper? Insominia will no longer be able to keep working " +
                        "when the lid closes.",
                        "Remove setup", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _c.RemoveSetup();
                    Sync();
                }
            };

            BtnOpenLogs.Click += delegate
            {
                try
                {
                    System.IO.Directory.CreateDirectory(AppSettings.Dir);
                    Process.Start(new ProcessStartInfo("explorer.exe", "\"" + AppSettings.Dir + "\"")
                    { UseShellExecute = true });
                }
                catch (Exception ex) { Log.Write("open logs failed: " + ex.Message); }
            };
            BtnRefreshDiag.Click += delegate { RefreshDiagnostics(); };

            _c.StateChanged += delegate { Dispatcher.BeginInvoke((Action)Sync); };

            Loaded += delegate { LoadApps(); Sync(); };
        }

        private void DoSetup()
        {
            MessageBox.Show(this,
                "Windows only lets an administrator change what closing the lid does.\n\n" +
                "Insominia will ask once and register a small helper. After that the switch " +
                "works silently and you will never see this prompt again.",
                "One-time setup", MessageBoxButton.OK, MessageBoxImage.Information);

            _c.RunSetup();
            Sync();
        }

        // ---- pages -------------------------------------------------------------

        private void ShowPage(UIElement page)
        {
            foreach (UIElement p in new UIElement[] { PageApps, PageSettings, PageAbout })
                p.Visibility = p == page ? Visibility.Visible : Visibility.Collapsed;

            page.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(170)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                });
        }

        // ---- app list ----------------------------------------------------------

        private void LoadApps()
        {
            _apps = ProcessThrottler.ListVisibleApps();
            RenderApps();
        }

        /// <summary>
        /// Split into "kept" and "will be slowed" rather than one flat list.
        /// Both halves are useful information, and it stops the page looking
        /// half empty when only a few apps are running.
        /// </summary>
        private void RenderApps()
        {
            string q = Search.Text.Trim();

            ListKept.Items.Clear();
            ListSlowed.Items.Clear();

            int kept = 0, slowed = 0, hidden = 0;

            foreach (AppEntry app in _apps)
            {
                bool match = q.Length == 0 ||
                             app.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             app.ProcessName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!match) { hidden++; continue; }

                bool isKept = _picked.Contains(app.ProcessName);
                Border row = BuildRow(app, isKept);

                if (isKept) { ListKept.Items.Add(row); kept++; }
                else { ListSlowed.Items.Add(row); slowed++; }
            }

            LabelKept.Text = "KEPT AT FULL SPEED" + (kept > 0 ? "   " + kept : "");
            LabelKept.Visibility = kept > 0 ? Visibility.Visible : Visibility.Collapsed;
            ListKept.Visibility = kept > 0 ? Visibility.Visible : Visibility.Collapsed;

            LabelSlowed.Text = "WILL BE SLOWED DOWN" + (slowed > 0 ? "   " + slowed : "");
            LabelSlowed.Visibility = slowed > 0 ? Visibility.Visible : Visibility.Collapsed;
            ListSlowed.Visibility = slowed > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (kept == 0 && slowed == 0)
            {
                EmptyNote.Visibility = Visibility.Visible;
                EmptyNote.Text = q.Length > 0
                    ? "No app matches \"" + q + "\"."
                    : "No apps with a window are running right now.";
            }
            else EmptyNote.Visibility = Visibility.Collapsed;

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int keptCount = 0;
            long keptBytes = 0, slowedBytes = 0;

            foreach (AppEntry a in _apps)
            {
                if (_picked.Contains(a.ProcessName)) { keptCount++; keptBytes += a.MemoryBytes; }
                else slowedBytes += a.MemoryBytes;
            }

            AppsSummary.Text = keptCount + " kept at full speed  ·  " +
                               Math.Max(0, _apps.Count - keptCount) + " slowed  ·  " +
                               Mb(slowedBytes) + " held back";

            NavAppsBadge.Text = keptCount > 0 ? keptCount.ToString() : "";
            SideFooter.Text = keptCount + " app" + (keptCount == 1 ? "" : "s") + " protected";
        }

        private static string Mb(long bytes)
        {
            double mb = bytes / 1024.0 / 1024.0;
            return mb >= 1024 ? (mb / 1024.0).ToString("0.0") + " GB" : mb.ToString("0") + " MB";
        }

        private Border BuildRow(AppEntry app, bool isKept)
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // --- icon ---
            Border iconHost = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
            };

            ImageSource icon = IconCache.For(app.ExePath);
            if (icon != null)
            {
                iconHost.Background = new SolidColorBrush(Palette.IconTile);
                iconHost.Child = new Image
                {
                    Source = icon,
                    Width = 22,
                    Height = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            }
            else
            {
                // No readable icon: a coloured initial still makes the row scannable.
                iconHost.Background = IconCache.FallbackBrush(app.DisplayName);
                iconHost.Child = new TextBlock
                {
                    Text = (app.DisplayName ?? "?").Substring(0, 1).ToUpperInvariant(),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 15,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            }
            Grid.SetColumn(iconHost, 0);

            // --- text ---
            StackPanel text = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            };
            text.Children.Add(new TextBlock
            {
                Text = app.DisplayName,
                Style = (Style)FindResource("Body"),
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            text.Children.Add(new TextBlock
            {
                Text = app.MemoryText + "  ·  " + app.ProcessText,
                Style = (Style)FindResource("Caption"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
            });
            Grid.SetColumn(text, 1);

            // --- switch ---
            ToggleSwitch sw = new ToggleSwitch
            {
                VerticalAlignment = VerticalAlignment.Center,
                IsOn = isKept,
                OnColor = Live,
            };
            Grid.SetColumn(sw, 2);

            string name = app.ProcessName;
            sw.Toggled += delegate
            {
                if (sw.IsOn) _picked.Add(name); else _picked.Remove(name);
                Commit();
                // Re-render so the app moves between the two sections.
                RenderApps();
            };

            grid.Children.Add(iconHost);
            grid.Children.Add(text);
            grid.Children.Add(sw);

            Border row = new Border { Style = (Style)FindResource("AppRow"), Child = grid };
            if (isKept) row.BorderBrush = new SolidColorBrush(Palette.Alpha(Palette.Live, 0x55));
            return row;
        }

        private void Commit()
        {
            _c.SetKeepApps(_picked);
        }

        // ---- sync --------------------------------------------------------------

        private void Sync()
        {
            _suppress = true;
            try
            {
                bool on = _c.IsActive;

                SwMaster.IsOn = on;
                SwStartup.IsOn = _c.StartsWithWindows;
                SwSaver.IsOn = _c.BatterySaver;

                SideState.Text = _c.StatusHeadline;
                SideSub.Text = on
                    ? (_c.LidSurvives ? "lid close is safe" : "idle sleep only")
                    : "lid close sleeps";

                Color accent = !on ? Idle : (_c.LidSurvives ? Live : Caution);
                SideSub.Foreground = new SolidColorBrush(accent);
                StatusCard.Background = on
                    ? (Brush)FindResource("CardSurfaceLive")
                    : (Brush)FindResource("CardSurface");
                StatusCard.BorderBrush = on
                    ? new SolidColorBrush(Color.FromArgb(0x55, accent.R, accent.G, accent.B))
                    : (Brush)FindResource("Border");

                SafetyReading h = _c.Health;
                SideBatteryValue.Text = h.BatteryPercent >= 0
                    ? h.BatteryPercent + "%" + (h.OnCharger ? " charging" : "")
                    : "--";

                double frac = h.BatteryPercent >= 0 ? h.BatteryPercent / 100.0 : 0;
                AnimateWidth(SideBatteryFill, 176 * frac);
                SideBatteryFill.Background = h.BatteryPercent <= 15
                    ? (Brush)FindResource("Danger")
                    : (h.BatteryPercent <= 25 ? (Brush)FindResource("Warn") : (Brush)FindResource("LiveGradient"));

                if (on)
                {
                    TimerRow.Visibility = Visibility.Visible;
                    TimeSpan? left = _c.Remaining;
                    if (left.HasValue)
                    {
                        TimerLabel.Text = "STOPS IN";
                        TimerValue.Text = Fmt(left.Value);
                    }
                    else
                    {
                        TimerLabel.Text = "RUNNING";
                        TimerValue.Text = Fmt(_c.Elapsed);
                    }
                }
                else TimerRow.Visibility = Visibility.Collapsed;

                int m = _c.AutoStopMinutes;
                ChipNever.IsChecked = m == 0;
                Chip30.IsChecked = m == 30;
                Chip60.IsChecked = m == 60;
                Chip120.IsChecked = m == 120;
                Chip240.IsChecked = m == 240;

                bool setupDone = _c.SetupDone;
                SideSetup.Visibility = setupDone ? Visibility.Collapsed : Visibility.Visible;
                SetupState.Text = setupDone
                    ? "Done. The switch works silently - no more admin prompts."
                    : "Not done yet. Without it, closing the lid will still sleep the laptop.";
                BtnDoSetup.IsEnabled = !setupDone;
                BtnUndoSetup.IsEnabled = setupDone;

                SafetyText.Text =
                    "Insominia stops by itself below 15% battery when unplugged, or above 90 °C.\n\n" +
                    "Battery now:  " + h.BatteryText + "\n" +
                    "Temperature:  " + h.TemperatureText;
            }
            finally { _suppress = false; }
        }

        private static string Fmt(TimeSpan t)
        {
            if (t.TotalHours >= 1) return (int)t.TotalHours + "h " + t.Minutes + "m";
            if (t.TotalMinutes >= 1) return t.Minutes + "m " + t.Seconds + "s";
            return t.Seconds + "s";
        }

        private static void AnimateWidth(FrameworkElement el, double to)
        {
            el.BeginAnimation(WidthProperty,
                new DoubleAnimation(Math.Max(0, to), new Duration(TimeSpan.FromMilliseconds(320)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                });
        }

        private void RefreshDiagnostics()
        {
            _c.RefreshHealth();
            SafetyReading h = _c.Health;
            LidAction ac, dc;
            bool readOk = LidPolicy.TryRead(out ac, out dc);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("STATE");
            sb.AppendLine("  Insominia          " + (_c.IsActive ? "ON since " + _c.ActivatedAt.ToString("HH:mm:ss") : "OFF"));
            sb.AppendLine("  Sleep block        " + (KeepAwake.IsEngaged ? "engaged" : "not engaged"));
            sb.AppendLine("  Auto-stop          " + _c.AutoStopText());
            sb.AppendLine();
            sb.AppendLine("LID");
            sb.AppendLine("  On charger         " + (readOk ? ac.ToString() : "system default (Sleep)"));
            sb.AppendLine("  On battery         " + (readOk ? dc.ToString() : "system default (Sleep)"));
            sb.AppendLine("  Survives lid close " + (_c.LidSurvives ? "YES" : "no"));
            sb.AppendLine("  Saved original     " + (LidAction)AppSettings.GetInt("OriginalLidAc", 1));
            sb.AppendLine();
            sb.AppendLine("HEALTH");
            sb.AppendLine("  Battery            " + h.BatteryText);
            sb.AppendLine("  Temperature        " + h.TemperatureText);
            sb.AppendLine("  Verdict            " + h.Verdict + (h.Reason.Length > 0 ? " - " + h.Reason : ""));
            sb.AppendLine();
            sb.AppendLine("BATTERY SAVER");
            sb.AppendLine("  Mode               " + (_c.BatterySaver ? "on" : "off"));
            sb.AppendLine("  Apps kept fast     " + (_picked.Count == 0 ? "(none)" : string.Join(", ", Names())));
            sb.AppendLine("  Slowed right now   " + _c.ThrottledCount + " processes");
            sb.AppendLine();
            sb.AppendLine("SETUP");
            sb.AppendLine("  Helper task        " + (_c.SetupDone ? "registered" : "NOT registered"));
            sb.AppendLine("  Start with Windows " + (_c.StartsWithWindows ? "yes" : "no"));
            sb.AppendLine("  Executable         " + ElevatedTask.ExePath);
            sb.AppendLine("  Settings           " + AppSettings.FilePath);

            DiagText.Text = sb.ToString();
        }

        private string[] Names()
        {
            string[] a = new string[_picked.Count];
            _picked.CopyTo(a);
            Array.Sort(a, StringComparer.OrdinalIgnoreCase);
            return a;
        }

        // ---- show / hide --------------------------------------------------------

        /// <summary>Opens the window. By default it lands on Apps, because that
        /// is what the tray item promises.</summary>
        public void ShowSmooth()
        {
            ShowSmooth(true);
        }

        public void ShowSmooth(bool goToApps)
        {
            LoadApps();
            Sync();

            if (goToApps)
            {
                NavApps.IsChecked = true;
                ShowPage(PageApps);
            }

            Show();
            WindowState = WindowState.Normal;
            Activate();

            Opacity = 0;
            BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, new Duration(TimeSpan.FromMilliseconds(190)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                });
        }

        public void HideSmooth()
        {
            DoubleAnimation fade = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(130)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            };
            fade.Completed += delegate { Hide(); };
            BeginAnimation(OpacityProperty, fade);
        }
    }
}
