namespace Yolcu360.PresentationLayer.Helpers
{
    /// <summary>
    /// Küçük tek satırlık metin giriş diyaloğu (WinForms'ta hazır InputBox olmadığı için).
    /// </summary>
    public static class PromptDialog
    {
        /// <summary>Kullanıcıdan metin ister. İptal edilirse null döner.</summary>
        public static string Show(string title, string message, string defaultValue = "")
        {
            using var form = new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(420, 140),
                ShowInTaskbar = false
            };

            var lbl = new Label { Left = 15, Top = 15, Width = 390, Text = message, AutoSize = false, Height = 30 };
            var txt = new TextBox { Left = 15, Top = 50, Width = 390, Text = defaultValue };
            var ok = new Button { Text = "Tamam", DialogResult = DialogResult.OK, Left = 245, Width = 75, Top = 90 };
            var cancel = new Button { Text = "İptal", DialogResult = DialogResult.Cancel, Left = 330, Width = 75, Top = 90 };

            form.Controls.Add(lbl);
            form.Controls.Add(txt);
            form.Controls.Add(ok);
            form.Controls.Add(cancel);
            form.AcceptButton = ok;
            form.CancelButton = cancel;

            return form.ShowDialog() == DialogResult.OK ? txt.Text?.Trim() : null;
        }
    }
}
