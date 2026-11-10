using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;

namespace Insominia
{
    internal enum TrayState
    {
        Idle,
        Active,
        Warning,
    }

    /// <summary>
    /// Tray icons built from the real logo, so the tray, the taskbar and the
    /// window all show the same mark.
    ///
    /// State is shown by treatment rather than by a different shape: idle is
    /// desaturated and dimmed, working is full brand colour, and a problem
    /// tints the mark amber.
    /// </summary>
    internal static class TrayIcons
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);

        private static readonly Dictionary<TrayState, Icon> Cache = new Dictionary<TrayState, Icon>();
        private static Bitmap _logo;
        private static bool _logoTried;

        public static Icon Get(TrayState state)
        {
            Icon cached;
            if (Cache.TryGetValue(state, out cached)) return cached;

            Icon icon = Build(state);
            Cache[state] = icon;
            return icon;
        }

        /// <summary>Loads the logo once out of the packed WPF resources.</summary>
        private static Bitmap Logo()
        {
            if (_logoTried) return _logo;
            _logoTried = true;

            try
            {
                Uri uri = new Uri("pack://application:,,,/Assets/logo.png", UriKind.Absolute);
                System.Windows.Resources.StreamResourceInfo info = Application.GetResourceStream(uri);
                if (info != null && info.Stream != null)
                    using (info.Stream)
                        _logo = new Bitmap(info.Stream);
            }
            catch (Exception ex)
            {
                Log.Write("tray logo unavailable, falling back to a drawn mark: " + ex.Message);
            }
            return _logo;
        }

        private static Icon Build(TrayState state)
        {
            Bitmap logo = Logo();

            using (Bitmap bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);

                    if (logo != null) DrawLogo(g, logo, state);
                    else DrawFallback(g, state);
                }

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (Icon temp = Icon.FromHandle(hIcon))
                        return (Icon)temp.Clone();
                }
                finally { DestroyIcon(hIcon); }
            }
        }

        private static void DrawLogo(Graphics g, Bitmap logo, TrayState state)
        {
            using (ImageAttributes attrs = new ImageAttributes())
            {
                attrs.SetColorMatrix(MatrixFor(state));
                g.DrawImage(logo,
                    new Rectangle(1, 1, 30, 30),
                    0, 0, logo.Width, logo.Height,
                    GraphicsUnit.Pixel, attrs);
            }
        }

        private static ColorMatrix MatrixFor(TrayState state)
        {
            switch (state)
            {
                case TrayState.Active:
                    return new ColorMatrix();   // untouched: full brand cyan

                case TrayState.Warning:
                    // Push towards amber so a problem is obvious at 16 px.
                    return new ColorMatrix(new float[][]
                    {
                        new float[] {1.70f, 0.00f, 0.00f, 0, 0},
                        new float[] {0.45f, 0.72f, 0.00f, 0, 0},
                        new float[] {0.00f, 0.00f, 0.18f, 0, 0},
                        new float[] {0,     0,     0,     1, 0},
                        new float[] {0,     0,     0,     0, 1},
                    });

                default:
                    // Idle: greyscale, then dimmed - present but clearly off.
                    return new ColorMatrix(new float[][]
                    {
                        new float[] {0.299f, 0.299f, 0.299f, 0, 0},
                        new float[] {0.587f, 0.587f, 0.587f, 0, 0},
                        new float[] {0.114f, 0.114f, 0.114f, 0, 0},
                        new float[] {0,      0,      0,   0.62f, 0},
                        new float[] {0.12f,  0.12f,  0.12f,  0, 1},
                    });
            }
        }

        /// <summary>Used only if the logo resource cannot be loaded.</summary>
        private static void DrawFallback(Graphics g, TrayState state)
        {
            Color fill;
            switch (state)
            {
                case TrayState.Active:  fill = Color.FromArgb(0x20, 0xE8, 0xD2); break;
                case TrayState.Warning: fill = Color.FromArgb(0xFF, 0xB2, 0x24); break;
                default:                fill = Color.FromArgb(0x6E, 0x6E, 0x75); break;
            }

            using (Pen ring = new Pen(Color.FromArgb(230, fill), 3f))
                g.DrawEllipse(ring, 4, 4, 24, 24);

            if (state != TrayState.Idle)
                using (Brush b = new SolidBrush(fill))
                    g.FillEllipse(b, 11, 11, 10, 10);
        }
    }
}
