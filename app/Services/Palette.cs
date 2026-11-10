using System.Windows.Media;

namespace Insominia.Services
{
    /// <summary>
    /// The handful of colours that code needs as well as XAML.
    ///
    /// These were previously written as raw hex in five different files, which
    /// is how a palette drifts. Anything a control sets in code comes from here,
    /// and the values match Themes/Theme.xaml exactly.
    ///
    /// The scheme is monochrome on purpose: greys carry the interface and the
    /// accent below is the logo cyan; amber and red are strictly alerts.
    /// </summary>
    internal static class Palette
    {
        /// <summary>Switches and primary buttons carry the logo cyan.</summary>
        public static readonly Color SwitchOn = Color.FromRgb(0x20, 0xE8, 0xD2);

        /// <summary>The logo colour, sampled from Assets/logo.png.</summary>
        public static readonly Color Accent = Color.FromRgb(0x20, 0xE8, 0xD2);

        /// <summary>Working. The brand colour: on means lit.</summary>
        public static readonly Color Live = Color.FromRgb(0x20, 0xE8, 0xD2);

        /// <summary>Needs attention, but not an emergency.</summary>
        public static readonly Color Warn = Color.FromRgb(0xFF, 0xB2, 0x24);

        /// <summary>Stop.</summary>
        public static readonly Color Danger = Color.FromRgb(0xFF, 0x45, 0x3A);

        /// <summary>Off / inactive.</summary>
        public static readonly Color Idle = Color.FromRgb(0x5B, 0x5B, 0x61);

        // Switch parts
        public static readonly Color TrackOff = Color.FromRgb(0x2A, 0x2A, 0x2E);
        public static readonly Color TrackBorder = Color.FromRgb(0x33, 0x33, 0x38);
        public static readonly Color KnobOff = Color.FromRgb(0x6E, 0x6E, 0x75);

        /// <summary>Tile behind an app icon.</summary>
        public static readonly Color IconTile = Color.FromRgb(0x20, 0x20, 0x24);

        /// <summary>Same colour at a given alpha, for soft borders and tints.</summary>
        public static Color Alpha(Color c, byte a)
        {
            return Color.FromArgb(a, c.R, c.G, c.B);
        }

        /// <summary>
        /// Picks black or white for whatever sits ON a colour. With a white
        /// "on" state the switch knob has to flip dark, so this is computed
        /// rather than hard-coded - swap the palette and it still reads.
        /// </summary>
        public static Color ContrastOn(Color background)
        {
            double luma = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255.0;
            return luma > 0.6 ? Color.FromRgb(0x0A, 0x0A, 0x0B) : Colors.White;
        }
    }
}
