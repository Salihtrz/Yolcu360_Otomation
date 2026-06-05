using CefSharp;
using Yolcu360.Common.Logging;

namespace Yolcu360.PresentationLayer
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                LogHelper.Error("Uygulama beklenmeyen şekilde sonlandı.", ex);
                MessageBox.Show("Uygulama beklenmeyen bir hatayla karşılaştı. Detaylar log dosyasına yazıldı.\n\n" + ex.Message,
                    "Kritik Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (Cef.IsInitialized == true)
                    Cef.Shutdown();
            }
        }
    }
}
