using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace Yolcu360.PresentationLayer.UI
{
    public enum ButtonVariant { Primary, Secondary, Accent, Success, Danger, Warning, Sidebar, Ghost }

    /// <summary>
    /// Guna.UI2 tabanlı modern kontrol üreticileri ve stil yardımcıları (yalnızca görsel).
    /// </summary>
    public static class UiStyleHelper
    {
        public static readonly Font BaseFont = new("Segoe UI", 9.5f);
        public static readonly Font LabelFont = new("Segoe UI", 9.5f);
        public static readonly Font InputFont = new("Segoe UI", 9.75f);

        // ---- Buton ----
        public static Guna2Button Button(string text, ButtonVariant variant, int width = 0, int height = 36)
        {
            var b = new Guna2Button
            {
                Text = text,
                BorderRadius = 8,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                Height = height,
                AutoRoundedCorners = false,
                DisabledState = { FillColor = Color.FromArgb(0xE5, 0xE7, 0xEB), ForeColor = Color.FromArgb(0x9C, 0xA3, 0xAF) }
            };
            b.ShadowDecoration.Enabled = false;
            ApplyVariant(b, variant); // varyant fontu da değiştirebilir (sidebar), ölçümden önce uygula

            // Metni HER ZAMAN sığdır: ölçülen metin genişliği + iç boşluk. Verilen width yalnızca
            // ALT SINIR'dır; metin daha genişse buton büyür. Böylece DPI/font ne olursa olsun
            // buton yazıları kesilmez.
            var needed = TextRenderer.MeasureText(text, b.Font).Width + 34;
            b.Width = Math.Max(width, needed);
            return b;
        }

        public static void ApplyVariant(Guna2Button b, ButtonVariant v)
        {
            b.BorderThickness = 0;
            switch (v)
            {
                case ButtonVariant.Primary:
                    b.FillColor = ThemeColors.Primary; b.ForeColor = Color.White;
                    b.HoverState.FillColor = ThemeColors.PrimaryDark; break;
                case ButtonVariant.Accent:
                    b.FillColor = ThemeColors.Accent; b.ForeColor = Color.White;
                    b.HoverState.FillColor = ThemeColors.Darken(ThemeColors.Accent); break;
                case ButtonVariant.Success:
                    b.FillColor = ThemeColors.Success; b.ForeColor = Color.White;
                    b.HoverState.FillColor = ThemeColors.Darken(ThemeColors.Success); break;
                case ButtonVariant.Danger:
                    b.FillColor = ThemeColors.Danger; b.ForeColor = Color.White;
                    b.HoverState.FillColor = ThemeColors.Darken(ThemeColors.Danger); break;
                case ButtonVariant.Warning:
                    b.FillColor = ThemeColors.Warning; b.ForeColor = Color.White;
                    b.HoverState.FillColor = ThemeColors.Darken(ThemeColors.Warning); break;
                case ButtonVariant.Secondary:
                    b.FillColor = ThemeColors.Surface; b.ForeColor = ThemeColors.TextDark;
                    b.BorderThickness = 1; b.BorderColor = ThemeColors.Border;
                    b.HoverState.FillColor = Color.FromArgb(0xEE, 0xF2, 0xF7); b.HoverState.ForeColor = ThemeColors.TextDark; break;
                case ButtonVariant.Ghost:
                    b.FillColor = Color.Transparent; b.ForeColor = ThemeColors.Primary;
                    b.HoverState.FillColor = Color.FromArgb(0xEE, 0xF2, 0xF7); break;
                case ButtonVariant.Sidebar:
                    b.FillColor = ThemeColors.SidebarDark; b.ForeColor = ThemeColors.SidebarText;
                    b.HoverState.FillColor = ThemeColors.SidebarHover; b.HoverState.ForeColor = Color.White;
                    b.BorderRadius = 8; b.TextAlign = HorizontalAlignment.Left;
                    b.Font = new Font("Segoe UI", 10f, FontStyle.Regular); break;
            }
        }

        public static void SetSidebarActive(Guna2Button b, bool active)
        {
            if (active) { b.FillColor = ThemeColors.Primary; b.ForeColor = Color.White; b.HoverState.FillColor = ThemeColors.Primary; }
            else { b.FillColor = ThemeColors.SidebarDark; b.ForeColor = ThemeColors.SidebarText; b.HoverState.FillColor = ThemeColors.SidebarHover; }
        }

        // ---- Inputlar ----
        public static Guna2TextBox TextBox(int width = 0)
        {
            var t = new Guna2TextBox
            {
                BorderRadius = 7,
                BorderColor = ThemeColors.Border,
                FillColor = ThemeColors.Surface,
                Font = InputFont,
                ForeColor = ThemeColors.TextDark,
                Height = 34
            };
            t.FocusedState.BorderColor = ThemeColors.Primary;
            if (width > 0) t.Width = width;
            return t;
        }

        public static Guna2ComboBox Combo(int width = 0)
        {
            var c = new Guna2ComboBox
            {
                BorderRadius = 7,
                BorderColor = ThemeColors.Border,
                FillColor = ThemeColors.Surface,
                Font = InputFont,
                ForeColor = ThemeColors.TextDark,
                Height = 34
            };
            c.FocusedState.BorderColor = ThemeColors.Primary;
            if (width > 0) c.Width = width;
            return c;
        }

        public static Guna2CheckBox Check(string text)
        {
            var c = new Guna2CheckBox
            {
                Text = text,
                AutoSize = true,
                Font = BaseFont,
                ForeColor = ThemeColors.TextDark,
                Cursor = Cursors.Hand
            };
            c.CheckedState.FillColor = ThemeColors.Primary;
            c.CheckedState.BorderColor = ThemeColors.Primary;
            c.UncheckedState.BorderColor = ThemeColors.TextMuted;
            return c;
        }

        public static Guna2DateTimePicker DatePicker(int width = 0)
        {
            var d = new Guna2DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                BorderRadius = 7,
                BorderColor = ThemeColors.Border,
                FillColor = ThemeColors.Surface,
                Font = InputFont,
                ForeColor = ThemeColors.TextDark,
                Height = 34
            };
            if (width > 0) d.Width = width;
            return d;
        }

        public static Label FieldLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            ForeColor = ThemeColors.TextMuted,
            Font = LabelFont,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(2, 9, 6, 3),
            BackColor = Color.Transparent
        };

        // ---- Kart ----
        public static Guna2Panel NewCard()
        {
            var p = new Guna2Panel
            {
                FillColor = ThemeColors.Surface,
                BorderRadius = 14,
                BorderColor = ThemeColors.Border,
                BorderThickness = 1,
                Margin = new Padding(6)
            };
            p.ShadowDecoration.Enabled = true;
            p.ShadowDecoration.Depth = 5;
            p.ShadowDecoration.Shadow = new Padding(4);
            p.ShadowDecoration.Color = Color.FromArgb(70, 100, 116, 139);
            return p;
        }

        /// <summary>Başlıklı kart: içerik (Dock=Fill) ve isteğe bağlı sağ üst aksiyon kontrolü ile.</summary>
        public static Guna2Panel Card(string title, Control content, Control headerRight = null)
        {
            var card = NewCard();
            card.Dock = DockStyle.Fill;
            var inner = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(16, 10, 16, 12)
            };
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var titleBar = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            titleBar.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = ThemeColors.TextDark,
                Location = new Point(0, 3),
                BackColor = Color.Transparent
            });
            if (headerRight != null)
            {
                titleBar.Controls.Add(headerRight);
                void place() { headerRight.Left = Math.Max(0, titleBar.Width - headerRight.Width); headerRight.Top = 2; }
                titleBar.Resize += (s, e) => place();
                headerRight.Left = 400; headerRight.Top = 2;
            }

            content.Dock = DockStyle.Fill;
            inner.Controls.Add(titleBar, 0, 0);
            inner.Controls.Add(content, 0, 1);
            card.Controls.Add(inner);
            return card;
        }
    }
}
