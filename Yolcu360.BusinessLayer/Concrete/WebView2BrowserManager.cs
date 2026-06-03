using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.BusinessLayer.Helpers;
using Yolcu360.Common.Helpers;
using Yolcu360.Common.Logging;

namespace Yolcu360.BusinessLayer.Concrete
{
    /// <summary>
    /// WebView2 tabanli tarayici yoneticisi. CefSharp ile ayni servis arayuzunu uygular;
    /// bu sayede Yolcu360 otomasyon kodu ve OTP akisi degismeden Edge runtime uzerinde calisir.
    /// </summary>
    public class WebView2BrowserManager : ICefSharpBrowserService
    {
        private WebView2 _browser;
        private bool _visible = true;
        private volatile bool _isNavigating;

        public bool IsInitialized { get; private set; }
        public bool IsBrowserVisible => _visible;
        public string CurrentUrl => _browser?.Source?.ToString();

        public Control GetBrowserControl() => _browser;

        public async Task InitializeAsync(string url, CancellationToken ct = default)
        {
            if (IsInitialized)
                return;

            _browser = new WebView2
            {
                Dock = DockStyle.Fill,
                Visible = _visible
            };

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Yolcu360_Otomation",
                "WebView2Profile");

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _browser.EnsureCoreWebView2Async(env);

            _browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _browser.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _browser.CoreWebView2.Settings.IsScriptEnabled = true;
            // Navigasyon durumunu izle: WaitForPageLoadAsync, aktif yukleme yoksa hemen donebilsin
            // (aksi halde zaten yuklu sayfada bir sonraki NavigationCompleted'i beklerken sonsuza takilir).
            _browser.CoreWebView2.NavigationStarting += (s, e) => _isNavigating = true;
            _browser.CoreWebView2.NavigationCompleted += (s, e) =>
            {
                _isNavigating = false;
                LogHelper.Info($"Sayfa yuklendi (WebView2): {_browser.Source}");
            };

            // VIEWPORT KİLİDİ: Yolcu360 responsive — DOM (ve selector'larımız) pencere genişliğiyle
            // değişiyor. Pencere büyütülünce site masaüstü layout'una geçip selector'ları kırıyordu
            // (ör. tarih takvimi). CDP ile sabit bir CSS viewport genişliği zorlanır; böylece pencere
            // boyutu ne olursa olsun site HEP aynı (selector'larımızın çalıştığı) layout'u render eder.
            await ApplyViewportLockAsync();

            IsInitialized = true;
            LogHelper.Info($"WebView2 tarayici kontrolu olusturuldu ({url}).");
            await LoadUrlAsync(url, ct);
        }

        /// <summary>
        /// CDP Emulation.setDeviceMetricsOverride ile sabit bir CSS viewport zorlar (genişlik 820 =
        /// tablet aralığı: site, selector'larımızın doğrulandığı layout'u render eder). Pencere
        /// büyütülse/küçültülse bile site DOM'u değişmez. Best-effort: hata olursa yalnızca loglanır.
        /// </summary>
        private async Task ApplyViewportLockAsync()
        {
            try
            {
                var p = JsonConvert.SerializeObject(new { width = 1000, height = 980, deviceScaleFactor = 1, mobile = false });
                await _browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", p);
                LogHelper.Info("WebView2 viewport sabitlendi (1000x980, tablet layout).");
            }
            catch (Exception ex)
            {
                LogHelper.Warning("Viewport kilidi uygulanamadı: " + ex.Message);
            }
        }

        public Task LoadUrlAsync(string url, CancellationToken ct = default)
        {
            EnsureInitialized();
            // Bayragi hemen set et: Source setter'i NavigationStarting'i asenkron tetikledigi icin
            // hemen ardindan cagrilan WaitForPageLoadAsync yuklemeyi kacirmasin.
            _isNavigating = true;
            _browser.Source = new Uri(url);
            return Task.CompletedTask;
        }

        public Task WaitForPageLoadAsync(CancellationToken ct = default)
        {
            EnsureInitialized();

            // Aktif bir yukleme yoksa hemen don (CefSharp ile ayni davranis). Aksi halde
            // zaten yuklenmis sayfada gelmeyecek bir NavigationCompleted beklenir ve takilir.
            if (!_isNavigating)
                return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = null;
            handler = (s, e) =>
            {
                _browser.CoreWebView2.NavigationCompleted -= handler;
                tcs.TrySetResult(true);
            };
            _browser.CoreWebView2.NavigationCompleted += handler;

            ct.Register(() =>
            {
                _browser.CoreWebView2.NavigationCompleted -= handler;
                tcs.TrySetCanceled();
            });

            return tcs.Task;
        }

        public async Task<bool> EvaluateBoolAsync(string script, CancellationToken ct = default)
        {
            var json = await SafeExecuteScriptAsync(script, ct);
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try { return JsonConvert.DeserializeObject<bool>(json); }
            catch { return false; }
        }

        public async Task<string> EvaluateStringAsync(string script, CancellationToken ct = default)
        {
            var json = await SafeExecuteScriptAsync(script, ct);
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return null;
            try { return JsonConvert.DeserializeObject<string>(json); }
            catch { return json.Trim('"'); }
        }

        public async Task<bool> WaitForElementAsync(string[] selectors, int timeoutSeconds, CancellationToken ct = default)
        {
            var script = JsHelper.BuildElementExistsScript(selectors);
            var found = await BrowserWaitHelper.PollUntilAsync(
                () => EvaluateBoolAsync(script, ct), timeoutSeconds, 300, ct);

            if (!found)
                LogHelper.Warning($"Element bulunamadi (zaman asimi). Denenen selector'lar: {string.Join(" | ", selectors)}");
            return found;
        }

        public async Task<bool> ClickElementAsync(string[] selectors, CancellationToken ct = default)
        {
            var ok = await EvaluateBoolAsync(JsHelper.BuildClickScript(selectors), ct);
            LogHelper.Info(ok
                ? $"Tiklandi: {selectors[0]} (ve alternatifleri)"
                : $"Tiklanacak element bulunamadi: {string.Join(" | ", selectors)}");
            return ok;
        }

        public async Task<bool> RealClickAtAsync(double x, double y, CancellationToken ct = default)
        {
            EnsureInitialized();
            try
            {
                ct.ThrowIfCancellationRequested();
                var cwv = _browser.CoreWebView2;
                // Gerçek (trusted) fare olayları: CDP Input.dispatchMouseEvent. Koordinatlar CSS px
                // (getBoundingClientRect ile aynı düzlem). move → press → release sırasıyla gönderilir.
                await cwv.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
                    JsonConvert.SerializeObject(new { type = "mouseMoved", x, y }));
                await cwv.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
                    JsonConvert.SerializeObject(new { type = "mousePressed", x, y, button = "left", buttons = 1, clickCount = 1 }));
                await cwv.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
                    JsonConvert.SerializeObject(new { type = "mouseReleased", x, y, button = "left", buttons = 1, clickCount = 1 }));
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogHelper.Warning("WebView2 gerçek tıklama (CDP) hatası: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> SetInputValueAsync(string[] selectors, string value, CancellationToken ct = default)
        {
            var ok = await EvaluateBoolAsync(JsHelper.BuildSetInputValueScript(selectors, value), ct);
            LogHelper.Info(ok
                ? $"Deger yazildi -> {selectors[0]}"
                : $"Input bulunamadi: {string.Join(" | ", selectors)}");
            return ok;
        }

        public Task<string> GetTextBySelectorsAsync(string[] selectors, CancellationToken ct = default)
            => EvaluateStringAsync(JsHelper.BuildGetTextScript(selectors), ct);

        public async Task<List<string>> GetElementsTextAsync(string[] selectors, CancellationToken ct = default)
        {
            var json = await EvaluateStringAsync(JsHelper.BuildGetElementsTextScript(selectors), ct);
            if (string.IsNullOrWhiteSpace(json))
                return new List<string>();
            try { return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>(); }
            catch { return new List<string>(); }
        }

        public async Task<bool> WaitForResultsAsync(string[] cardSelectors, int timeoutSeconds, CancellationToken ct = default)
        {
            var found = await BrowserWaitHelper.PollUntilAsync(
                () => EvaluateBoolAsync(JsHelper.BuildHasResultsScript(cardSelectors), ct),
                timeoutSeconds, 400, ct);
            LogHelper.Info(found ? "Sonuc kartlari yuklendi." : "Sonuc kartlari bulunamadi (zaman asimi).");
            return found;
        }

        public Task<bool> IsLoggedInAsync(string[] loggedInSelectors, CancellationToken ct = default)
            => EvaluateBoolAsync(JsHelper.BuildElementExistsScript(loggedInSelectors), ct);

        public async Task ClearSessionAsync(CancellationToken ct = default)
        {
            EnsureInitialized();
            try
            {
                _browser.CoreWebView2.CookieManager.DeleteAllCookies();
                await SafeExecuteScriptAsync(@"(function(){
                    try { localStorage.clear(); } catch(e) {}
                    try { sessionStorage.clear(); } catch(e) {}
                    return true;
                })();", ct);
                LogHelper.Info("WebView2 oturum verileri temizlendi.");
            }
            catch (Exception ex)
            {
                LogHelper.Warning("WebView2 oturum temizligi tamamlanamadi: " + ex.Message);
            }
        }

        public async Task ClearSiteSessionAsync(CancellationToken ct = default)
        {
            EnsureInitialized();
            try
            {
                // Yalnızca Yolcu360 alan adına ait çerezleri sil; reCAPTCHA/Google güven çerezleri
                // (farklı alan adında: google.com/gstatic.com) bu sorguya dahil OLMAZ → korunur.
                var cm = _browser.CoreWebView2.CookieManager;
                var cookies = await cm.GetCookiesAsync("https://www.yolcu360.com");
                int removed = 0;
                foreach (var c in cookies)
                {
                    var name = c.Name ?? string.Empty;
                    // Garanti: yolcu360 alanında bir reCAPTCHA çerezi varsa onu da koru.
                    if (name.StartsWith("_GRECAPTCHA", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("rc::", StringComparison.OrdinalIgnoreCase))
                        continue;
                    cm.DeleteCookie(c);
                    removed++;
                }

                // Yolcu360 sayfasının localStorage/sessionStorage'ı (oturum/token burada olabilir).
                // reCAPTCHA güveni çerezde tutulduğu için bu temizlik onu etkilemez.
                await SafeExecuteScriptAsync(@"(function(){
                    try { localStorage.clear(); } catch(e) {}
                    try { sessionStorage.clear(); } catch(e) {}
                    return true;
                })();", ct);

                LogHelper.Info($"Yolcu360 oturumu temizlendi (yumuşak çıkış): {removed} çerez silindi, reCAPTCHA güveni korundu.");
            }
            catch (Exception ex)
            {
                LogHelper.Warning("Yumuşak çıkış tamamlanamadı: " + ex.Message);
            }
        }

        public void SetBrowserVisible(bool visible)
        {
            _visible = visible;
            if (_browser != null)
                _browser.Visible = visible;
            LogHelper.Info($"Tarayici gorunurlugu: {(visible ? "Gorunur" : "Gizli")}");
        }

        private async Task<string> SafeExecuteScriptAsync(string script, CancellationToken ct)
        {
            EnsureInitialized();
            try
            {
                ct.ThrowIfCancellationRequested();
                return await _browser.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.Error("WebView2 JavaScript calistirma hatasi.", ex);
                return null;
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized || _browser?.CoreWebView2 == null)
                throw new InvalidOperationException("Tarayici henuz baslatilmadi. Once InitializeAsync cagrilmali.");
        }
    }
}
