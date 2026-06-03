using System.Drawing;

namespace Yolcu360.PresentationLayer.UI
{
    /// <summary>
    /// Uygulama genelinde kullanılan modern dashboard renk paleti (yalnızca UI).
    /// </summary>
    public static class ThemeColors
    {
        public static readonly Color Background = Color.FromArgb(0xF3, 0xF4, 0xF6);
        public static readonly Color Surface = Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static readonly Color SidebarDark = Color.FromArgb(0x11, 0x18, 0x27);
        public static readonly Color SidebarHover = Color.FromArgb(0x1F, 0x29, 0x37);
        public static readonly Color Primary = Color.FromArgb(0x25, 0x63, 0xEB);
        public static readonly Color PrimaryDark = Color.FromArgb(0x1E, 0x40, 0xAF);
        public static readonly Color Accent = Color.FromArgb(0x06, 0xB6, 0xD4);
        public static readonly Color Success = Color.FromArgb(0x10, 0xB9, 0x81);
        public static readonly Color Danger = Color.FromArgb(0xEF, 0x44, 0x44);
        public static readonly Color Warning = Color.FromArgb(0xF5, 0x9E, 0x0B);
        public static readonly Color TextDark = Color.FromArgb(0x11, 0x18, 0x27);
        public static readonly Color TextMuted = Color.FromArgb(0x6B, 0x72, 0x80);
        public static readonly Color Border = Color.FromArgb(0xE5, 0xE7, 0xEB);

        // Sidebar metin renkleri
        public static readonly Color SidebarText = Color.FromArgb(0xCB, 0xD5, 0xE1);
        public static readonly Color SidebarTextActive = Color.White;

        // Grid renkleri
        public static readonly Color GridHeader = Color.FromArgb(0x1E, 0x29, 0x3B);
        public static readonly Color GridAltRow = Color.FromArgb(0xF8, 0xFA, 0xFC);
        public static readonly Color RowHighlight = Color.FromArgb(0xD1, 0xFA, 0xE5); // en ucuz N (açık yeşil)

        /// <summary>Bir rengi belirtilen oranda koyulaştırır (hover/pressed efektleri için).</summary>
        public static Color Darken(Color c, float factor = 0.12f)
        {
            int R = (int)(c.R * (1 - factor));
            int G = (int)(c.G * (1 - factor));
            int B = (int)(c.B * (1 - factor));
            return Color.FromArgb(c.A, Clamp(R), Clamp(G), Clamp(B));
        }

        private static int Clamp(int v) => v < 0 ? 0 : (v > 255 ? 255 : v);
    }
}
