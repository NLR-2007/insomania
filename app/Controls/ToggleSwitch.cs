using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Insominia.Services;

namespace Insominia.Controls
{
    /// <summary>
    /// The switch everything else hangs off, so it has to feel right.
    ///
    /// The knob travels with a slight overshoot and the track cross-fades at the
    /// same time, which is what reads as "physical" rather than "a checkbox".
    /// Everything is animated on the composition thread, so it stays smooth even
    /// while we are enumerating processes on the UI thread.
    /// </summary>
    public sealed class ToggleSwitch : Control
    {
        private Border _track;
        private Border _knob;
        private TranslateTransform _knobShift;
        private Border _glow;

        private const double TrackW = 48;
        private const double TrackH = 28;
        private const double KnobSize = 22;
        private const double Travel = TrackW - KnobSize - 6;

        public static readonly DependencyProperty IsOnProperty =
            DependencyProperty.Register(nameof(IsOn), typeof(bool), typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(false,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOnChanged));

        public static readonly DependencyProperty OnColorProperty =
            DependencyProperty.Register(nameof(OnColor), typeof(Color), typeof(ToggleSwitch),
                new PropertyMetadata(Palette.Live));

        /// <summary>Raised only for user-driven changes, never for programmatic ones.</summary>
        public event EventHandler Toggled;

        public bool IsOn
        {
            get { return (bool)GetValue(IsOnProperty); }
            set { SetValue(IsOnProperty, value); }
        }

        public Color OnColor
        {
            get { return (Color)GetValue(OnColorProperty); }
            set { SetValue(OnColorProperty, value); }
        }

        public ToggleSwitch()
        {
            Width = TrackW;
            Height = TrackH;
            Cursor = Cursors.Hand;
            Focusable = true;
            SnapsToDevicePixels = true;

            Build();

            MouseLeftButtonDown += (s, e) => { e.Handled = true; UserToggle(); };
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Space || e.Key == Key.Enter) { e.Handled = true; UserToggle(); }
            };

            MouseEnter += (s, e) => AnimateGlow(0.35);
            MouseLeave += (s, e) => AnimateGlow(0.0);

            Loaded += (s, e) => Render(IsOn, false);
        }

        private void Build()
        {
            _glow = new Border
            {
                Width = TrackW,
                Height = TrackH,
                CornerRadius = new CornerRadius(TrackH / 2),
                Background = Brushes.White,
                Opacity = 0,
            };

            _track = new Border
            {
                Width = TrackW,
                Height = TrackH,
                CornerRadius = new CornerRadius(TrackH / 2),
                Background = new SolidColorBrush(Palette.TrackOff),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Palette.TrackBorder),
            };

            _knobShift = new TranslateTransform(0, 0);

            _knob = new Border
            {
                Width = KnobSize,
                Height = KnobSize,
                CornerRadius = new CornerRadius(KnobSize / 2),
                Background = new SolidColorBrush(Palette.KnobOff),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0),
                RenderTransform = _knobShift,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 6,
                    ShadowDepth = 1,
                    Direction = 270,
                    Opacity = 0.45,
                    Color = Colors.Black,
                },
            };

            Grid root = new Grid();
            root.Children.Add(_glow);
            root.Children.Add(_track);
            root.Children.Add(_knob);

            AddVisualChild(root);
            _root = root;
        }

        // ---- minimal hosting so we can build the tree in code ----------------

        private Grid _root;
        protected override int VisualChildrenCount { get { return _root == null ? 0 : 1; } }
        protected override Visual GetVisualChild(int index) { return _root; }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_root != null) _root.Measure(availableSize);
            return new Size(TrackW, TrackH);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_root != null) _root.Arrange(new Rect(finalSize));
            return finalSize;
        }

        // ---- behaviour -------------------------------------------------------

        private void UserToggle()
        {
            Focus();
            IsOn = !IsOn;
            EventHandler h = Toggled;
            if (h != null) h(this, EventArgs.Empty);
        }

        private static void OnIsOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ToggleSwitch sw = (ToggleSwitch)d;
            sw.Render((bool)e.NewValue, true);
        }

        /// <summary>Moves the switch to a state, animated or instantly.</summary>
        private void Render(bool on, bool animate)
        {
            if (_track == null || _knob == null) return;

            Color trackTo = on ? OnColor : Palette.TrackOff;
            Color knobTo = on ? Palette.ContrastOn(OnColor) : Palette.KnobOff;
            double shiftTo = on ? Travel : 0;

            if (!animate)
            {
                _track.Background = new SolidColorBrush(trackTo);
                _knob.Background = new SolidColorBrush(knobTo);
                _knobShift.X = shiftTo;
                return;
            }

            // Brushes must be unfrozen to animate.
            SolidColorBrush trackBrush = _track.Background as SolidColorBrush;
            if (trackBrush == null || trackBrush.IsFrozen)
            {
                trackBrush = new SolidColorBrush(trackBrush == null ? trackTo : trackBrush.Color);
                _track.Background = trackBrush;
            }

            SolidColorBrush knobBrush = _knob.Background as SolidColorBrush;
            if (knobBrush == null || knobBrush.IsFrozen)
            {
                knobBrush = new SolidColorBrush(knobBrush == null ? knobTo : knobBrush.Color);
                _knob.Background = knobBrush;
            }

            Duration d = new Duration(TimeSpan.FromMilliseconds(190));

            trackBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(trackTo, d) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

            knobBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(knobTo, d) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

            // A touch of overshoot is what sells the movement as physical.
            _knobShift.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(shiftTo, d)
                {
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 },
                });
        }

        private void AnimateGlow(double to)
        {
            if (_glow == null) return;
            _glow.BeginAnimation(OpacityProperty,
                new DoubleAnimation(to * 0.12, new Duration(TimeSpan.FromMilliseconds(140)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                });
        }
    }
}
