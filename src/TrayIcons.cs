using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Insominia
{
    internal enum TrayState
    {
        Idle,
        Active,
        Warning,
    }

    /// <summary>
    /// Draws the tray icons at runtime so the app stays a single .exe with no
    /// embedded binary assets.
    /// </summary>
    internal static class TrayIcons
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);

        private static readonly Dictionary<TrayState, Icon> Cache = new Dictionary<TrayState, Icon>();

        public static Icon Get(TrayState state)
        {
            Icon cached;
            if (Cache.TryGetValue(state, out cached)) return cached;

            Icon icon = Build(state);
            Cache[state] = icon;
            return icon;
        }

        private static Icon Build(TrayState state)
        {
            Color fill;
            switch (state)
            {
                case TrayState.Active:  fill = Color.FromArgb(46, 204, 113); break; // green
                case TrayState.Warning: fill = Color.FromArgb(230, 126, 34); break; // orange
                default:                fill = Color.FromArgb(130, 136, 145); break; // grey
            }

            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    // Outer ring
                    using (Pen ring = new Pen(Color.FromArgb(220, fill), 3f))
                        g.DrawEllipse(ring, 3, 3, 25, 25);

                    // Inner dot - solid when active, hollow when idle
                    if (state == TrayState.Idle)
                    {
                        using (Pen p = new Pen(Color.FromArgb(160, fill), 2f))
                            g.DrawEllipse(p, 12, 12, 7, 7);
                    }
                    else
                    {
                        using (Brush b = new SolidBrush(fill))
                            g.FillEllipse(b, 10, 10, 11, 11);
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    // Clone so the icon survives after we free the GDI handle.
                    using (Icon temp = Icon.FromHandle(hIcon))
                        return (Icon)temp.Clone();
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }
    }
}
