using System.Windows.Forms;

namespace Yolcu360.BusinessLayer.Abstract
{
    /// <summary>
    /// CefSharp Chromium tarayıcısını yöneten servis. Düşük seviye JS çalıştırma ve
    /// element düzeyi yardımcılar burada toplanır. Thread.Sleep KULLANILMAZ; tüm
    /// beklemeler async/await + olay/TaskCompletionSource ile yapılır.
    /// </summary>
    public interface ICefSharpBrowserService
    {
        /// <summary>WinForms panelde barındırılmak üzere tarayıcı kontrolünü döndürür.</summary>
        Control GetBrowserControl();

        /// <summary>CefSharp'ı başlatır ve verilen adresi yükler.</summary>
        Task InitializeAsync(string url, CancellationToken ct = default);

        Task LoadUrlAsync(string url, CancellationToken ct = default);

        /// <summary>Aktif bir sayfa yüklemesi varsa tamamlanmasını bekler.</summary>
        Task WaitForPageLoadAsync(CancellationToken ct = default);

        // ---- Düşük seviye JS ----
        Task<bool> EvaluateBoolAsync(string script, CancellationToken ct = default);
        Task<string> EvaluateStringAsync(string script, CancellationToken ct = default);

        // ---- Element düzeyi yardımcılar (selector listesi alternatifli denenir) ----
        Task<bool> WaitForElementAsync(string[] selectors, int timeoutSeconds, CancellationToken ct = default);
        Task<bool> ClickElementAsync(string[] selectors, CancellationToken ct = default);

        /// <summary>
        /// Viewport koordinatına (CSS px) GERÇEK (trusted) fare tıklaması gönderir. JS ile dispatch
        /// edilen olaylar isTrusted=false olduğu için bazı SPA widget'ları (ör. Yolcu360 saat seçici)
        /// tepki vermez; bu metot gerçek girişle (WebView2: CDP Input.dispatchMouseEvent) çözer.
        /// </summary>
        Task<bool> RealClickAtAsync(double x, double y, CancellationToken ct = default);
        Task<bool> SetInputValueAsync(string[] selectors, string value, CancellationToken ct = default);
        Task<string> GetTextBySelectorsAsync(string[] selectors, CancellationToken ct = default);
        Task<List<string>> GetElementsTextAsync(string[] selectors, CancellationToken ct = default);

        /// <summary>En az bir sonuç kartı görünene kadar (veya zaman aşımına kadar) bekler.</summary>
        Task<bool> WaitForResultsAsync(string[] cardSelectors, int timeoutSeconds, CancellationToken ct = default);

        // ---- Görünürlük ve login ----
        void SetBrowserVisible(bool visible);
        bool IsBrowserVisible { get; }
        Task<bool> IsLoggedInAsync(string[] loggedInSelectors, CancellationToken ct = default);

        string CurrentUrl { get; }
        bool IsInitialized { get; }

        /// <summary>
        /// Kullanıcının GERÇEK tarayıcısından kopyaladığı oturum çerezlerini ("ad=değer; ad2=değer2"
        /// biçimi) CEF'e aktarır. reCAPTCHA'yı zaten geçmiş olan kendi oturumunu yeniden kullanmak
        /// içindir; CEF üzerinden YENİ bir giriş/captcha YAPILMAZ. Çerez değerleri log'lanmaz.
        /// </summary>
        /// <returns>Başarıyla yazılan çerez sayısı.</returns>
        Task ClearSessionAsync(CancellationToken ct = default);
    }
}
