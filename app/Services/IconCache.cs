using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
// System.Drawing and System.Windows.Media both define Brush, Color and Image.
// Aliasing keeps WPF's versions unqualified, which is what the rest of the
// file wants, and names the one GDI type we actually need.
using Drawing = System.Drawing;

namespace Insominia.Services
{
    /// <summary>
    /// Pulls the real icon out of an app's executable, so the list reads as
    /// "Chrome, VS Code, Copilot" at a glance instead of three identical rows.
    ///
    /// Extraction is not free, so results are cached by path, including the
    /// failures - retrying a file we cannot read just costs time every refresh.
    /// </summary>
    internal static class IconCache
    {
        private static readonly Dictionary<string, ImageSource> Cache =
            new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static ImageSource For(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return null;

            ImageSource cached;
            if (Cache.TryGetValue(exePath, out cached)) return cached;

            ImageSource img = Extract(exePath);
            Cache[exePath] = img;      // null is cached too, deliberately
            return img;
        }

        private static ImageSource Extract(string exePath)
        {
            IntPtr hIcon = IntPtr.Zero;
            try
            {
                using (Drawing.Icon icon = Drawing.Icon.ExtractAssociatedIcon(exePath))
                {
                    if (icon == null) return null;

                    hIcon = icon.Handle;
                    ImageSource src = Imaging.CreateBitmapSourceFromHIcon(
                        hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                    src.Freeze();      // frozen so it can cross threads and render cheaply
                    return src;
                }
            }
            catch (Exception ex)
            {
                Log.Write("icon extract failed for " + exePath + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>A coloured initial, used when an app has no readable icon.</summary>
        public static Brush FallbackBrush(string name)
        {
            // Deterministic hue from the name, so the same app always looks the same.
            int hash = 0;
            foreach (char ch in name ?? "?") hash = (hash * 31 + char.ToLowerInvariant(ch)) & 0x7FFFFFFF;

            double hue = hash % 360;
            Color c = FromHsl(hue, 0.42, 0.52);
            Color c2 = FromHsl((hue + 24) % 360, 0.45, 0.42);

            LinearGradientBrush b = new LinearGradientBrush(c, c2, 45);
            b.Freeze();
            return b;
        }

        private static Color FromHsl(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = l - c / 2;

            double r, g, bl;
            if (h < 60) { r = c; g = x; bl = 0; }
            else if (h < 120) { r = x; g = c; bl = 0; }
            else if (h < 180) { r = 0; g = c; bl = x; }
            else if (h < 240) { r = 0; g = x; bl = c; }
            else if (h < 300) { r = x; g = 0; bl = c; }
            else { r = c; g = 0; bl = x; }

            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((bl + m) * 255));
        }
    }
}
