namespace Yolcu360.PresentationLayer.Helpers
{
    /// <summary>
    /// WinForms arayüzü için küçük yardımcılar. Uzun işlemlerde butonları kilitlemek,
    /// durum/ilerleme göstermek ve kullanıcıya mesaj vermek için kullanılır.
    /// </summary>
    public static class UiHelper
    {
        public static void RunOnUi(Control owner, Action action)
        {
            if (owner.InvokeRequired)
                owner.BeginInvoke(action);
            else
                action();
        }

        public static void SetControlsEnabled(bool enabled, params Control[] controls)
        {
            foreach (var c in controls)
                if (c != null) c.Enabled = enabled;
        }

        public static void Info(string message, string title = "Bilgi")
            => MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

        public static void Warn(string message, string title = "Uyarı")
            => MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);

        public static void Error(string message, string title = "Hata")
            => MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

        public static bool Confirm(string message, string title = "Onay")
            => MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }
}
