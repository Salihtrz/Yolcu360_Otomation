using System.Windows.Forms;
using Yolcu360.DtoLayer.BrowserDto;

namespace Yolcu360.BusinessLayer.Abstract.Browser
{
    /// <summary>
    /// CefSharp Chromium tarayýcýsýný yöneten servis. Düþük seviye JS çalýþtýrma ve
    /// element düzeyi yardýmcýlar burada toplanýr. Thread.Sleep KULLANILMAZ; tüm
    /// beklemeler async/await + olay/TaskCompletionSource ile yapýlýr.
    /// </summary>
    public interface ICefSharpBrowserService
    {
        /// <summary>WinForms panelde barýndýrýlmak üzere tarayýcý kontrolünü döndürür.</summary>
        Control GetBrowserControl();

        /// <summary>CefSharp'ý baþlatýr ve verilen adresi yükler.</summary>
        Task InitializeAsync(string url, CancellationToken ct = default);

        Task LoadUrlAsync(string url, CancellationToken ct = default);

        /// <summary>Aktif bir sayfa yüklemesi varsa tamamlanmasýný bekler.</summary>
        Task WaitForPageLoadAsync(CancellationToken ct = default);

        // ---- Düþük seviye JS ----
        Task<bool> EvaluateBoolAsync(string script, CancellationToken ct = default);
        Task<string> EvaluateStringAsync(string script, CancellationToken ct = default);

        // ---- Element düzeyi yardýmcýlar (selector listesi alternatifli denenir) ----
        Task<bool> WaitForElementAsync(string[] selectors, int timeoutSeconds, CancellationToken ct = default);
        Task<bool> ClickElementAsync(string[] selectors, CancellationToken ct = default);

        /// <summary>
        /// Viewport koordinatýna (CSS px) GERÇEK (trusted) fare týklamasý gönderir. JS ile dispatch
        /// edilen olaylar isTrusted=false olduðu için bazý SPA widget'larý (ör. Yolcu360 saat seçici)
        /// tepki vermez; bu metot gerçek giriþle (CefSharp: CDP Input.dispatchMouseEvent) çözer.
        /// </summary>
        Task<bool> RealClickAtAsync(double x, double y, CancellationToken ct = default);
        Task<bool> SetInputValueAsync(string[] selectors, string value, CancellationToken ct = default);
        Task<string> GetTextBySelectorsAsync(string[] selectors, CancellationToken ct = default);
        Task<List<string>> GetElementsTextAsync(string[] selectors, CancellationToken ct = default);

        /// <summary>En az bir sonuç kartý görünene kadar (veya zaman aþýmýna kadar) bekler.</summary>
        Task<bool> WaitForResultsAsync(string[] cardSelectors, int timeoutSeconds, CancellationToken ct = default);

        // ---- Görünürlük ve login ----
        void SetBrowserVisible(bool visible);
        bool IsBrowserVisible { get; }
        Task<bool> IsLoggedInAsync(string[] loggedInSelectors, CancellationToken ct = default);

        string CurrentUrl { get; }
        bool IsInitialized { get; }

        /// <summary>
        /// Kullanýcýnýn GERÇEK tarayýcýsýndan kopyaladýðý oturum çerezlerini ("ad=deðer; ad2=deðer2"
        /// biçimi) CEF'e aktarýr. reCAPTCHA'yý zaten geçmiþ olan kendi oturumunu yeniden kullanmak
        /// içindir; CEF üzerinden YENÝ bir giriþ/captcha YAPILMAZ. Çerez deðerleri log'lanmaz.
        /// </summary>
        /// <returns>Baþarýyla yazýlan çerez sayýsý.</returns>
        Task ClearSessionAsync(CancellationToken ct = default);

        /// <summary>
        /// YUMUÞAK ÇIKIÞ (logout): yalnýzca Yolcu360 alan adýnýn oturum çerezlerini ve
        /// localStorage/sessionStorage'ýný siler; Google/reCAPTCHA güven çerezlerini (ör. _GRECAPTCHA)
        /// KORUR. Böylece çýkýþtan hemen sonra tekrar giriþte reCAPTCHA "þüpheli yeni tarayýcý"
        /// muamelesi yapýp düþük puan vermez (recaptcha_score_too_low azalýr).
        /// </summary>
        Task ClearSiteSessionAsync(CancellationToken ct = default);

        /// <summary>Verilen URL için geçerli oturum çerezlerini dýþa aktarýr (motorlar arasý köprü için).</summary>
        Task<List<BrowserCookieDto>> ExportCookiesAsync(string url, CancellationToken ct = default);

        /// <summary>Verilen çerezleri bu tarayýcý motoruna yükler (URL alan adýna yazar).</summary>
        Task ImportCookiesAsync(string url, List<BrowserCookieDto> cookies, CancellationToken ct = default);
    }
}
