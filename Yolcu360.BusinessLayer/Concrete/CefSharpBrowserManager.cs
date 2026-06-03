using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using Newtonsoft.Json;
using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.BusinessLayer.Helpers;
using Yolcu360.Common.Helpers;
using Yolcu360.Common.Logging;
using Yolcu360.DtoLayer.BrowserDto;

namespace Yolcu360.BusinessLayer.Concrete
{
    /// <summary>
    /// CefSharp Chromium tarayıcısının somut yöneticisi. Tüm beklemeler async/await,
    /// olaylar (LoadingStateChanged) ve TaskCompletionSource ile yapılır.
    /// </summary>
    public class CefSharpBrowserManager : ICefSharpBrowserService
    {
        private ChromiumWebBrowser _browser;
        private bool _visible = true;

        public bool IsInitialized { get; private set; }
        public bool IsBrowserVisible => _visible;
        public string CurrentUrl => _browser?.Address;

        public Control GetBrowserControl() => _browser;

        public Task InitializeAsync(string url, CancellationToken ct = default)
        {
            if (IsInitialized)
                return Task.CompletedTask;

            // CefSharp tek sefer başlatılır (UI thread'de, ilk tarayıcı oluşturulmadan önce).
            if (Cef.IsInitialized != true)
            {
                // SADELİK İLKESİ: Tarayıcıya hiçbir "anti-bot" numarası/bayrağı EKLENMEZ.
                // Düz/varsayılan CefSharp, kendi içinde tutarlı normal bir Chromium'dur ve
                // reCAPTCHA'yı en doğal şekilde böyle geçer. (Eklenen sahte UA / komut satırı
                // bayrakları tutarsızlık yaratıp puanı DÜŞÜRÜYORDU; hepsi kaldırıldı.)
                // CachePath + PersistSessionCookies yalnızca oturumun kalıcı olması içindir
                // (manuel login sonrası aynı session); bunlar standart kalıcılık ayarlarıdır.
                // Cache %AppData%\Yolcu360_Otomation\CefCache altında tutulur (cookie/session/localStorage kalıcı).
                var settings = new CefSettings
                {
                    CachePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Yolcu360_Otomation", "CefCache"),
                    PersistSessionCookies = true
                };

                Cef.Initialize(settings);
                LogHelper.Info("CefSharp başlatıldı (düz/varsayılan profil).");
            }

            _browser = new ChromiumWebBrowser(url)
            {
                Dock = DockStyle.Fill,
                Visible = _visible
            };
            // VIEWPORT KİLİDİ + sayfa yüklendi logu. Yolcu360 responsive — pencere büyütülünce site
            // masaüstü layout'una geçip selector'ları kırıyordu (takvim/saat/filtre). CDP (DevTools)
            // Emulation.setDeviceMetricsOverride ile sabit CSS viewport (1000 = tablet) zorlanır; her
            // TAM yüklemede (idempotent) uygulanır. DevTools çağrısı CEF UI thread'ine marshal edilir
            // (erken/yanlış thread çağrısı ExecutionEngineException ile çökertiyordu).
            _browser.LoadingStateChanged += async (s, e) =>
            {
                if (!e.IsLoading)
                {
                    LogHelper.Info($"Sayfa yüklendi: {_browser.Address}");
                    await ApplyViewportLockAsync();
                }
            };

            IsInitialized = true;
            LogHelper.Info($"Yolcu360 tarayıcı kontrolü oluşturuldu ({url}).");
            return Task.CompletedTask;
        }

        /// <summary>
        /// CDP (DevTools) Emulation.setDeviceMetricsOverride ile sabit bir CSS viewport zorlar
        /// (genişlik 1000 = tablet aralığı: site, selector'larımızın doğrulandığı layout'u render eder).
        /// Pencere büyütülse/küçültülse bile site DOM'u değişmez. Best-effort: hata olursa yalnızca loglanır.
        /// </summary>
        private async Task ApplyViewportLockAsync()
        {
            try
            {
                await DevToolsAsync("Emulation.setDeviceMetricsOverride",
                    new Dictionary<string, object>
                    {
                        ["width"] = 1000,
                        ["height"] = 980,
                        ["deviceScaleFactor"] = 1,
                        ["mobile"] = false
                    });
            }
            catch (Exception ex)
            {
                LogHelper.Warning("Viewport kilidi uygulanamadı (CEF): " + ex.Message);
            }
        }

        // CDP/DevTools çağrıları IBrowserHost.ExecuteDevToolsMethod ile yapılır. ÖNEMLİ: bu çağrı
        // CEF UI THREAD'inde olmalı; aksi halde ExecutionEngineException (ölümcül native crash) olur.
        // Bu yüzden gerekirse Cef.UIThreadTaskFactory ile marshal edilir.
        private int _devToolsMsgId;

        private async Task DevToolsAsync(string method, IDictionary<string, object> parameters)
        {
            var host = _browser?.GetBrowser()?.GetHost();
            if (host == null) return;

            void Invoke() => host.ExecuteDevToolsMethod(
                System.Threading.Interlocked.Increment(ref _devToolsMsgId), method, parameters);

            if (Cef.CurrentlyOnThread(CefThreadIds.TID_UI))
                Invoke();
            else
                await Cef.UIThreadTaskFactory.StartNew(Invoke);
        }

        public Task LoadUrlAsync(string url, CancellationToken ct = default)
        {
            EnsureInitialized();
            _browser.LoadUrl(url);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Aktif yükleme tamamlanana kadar bekler. Hiç yükleme yoksa hemen döner.
        /// LoadingStateChanged olayı + TaskCompletionSource kullanır.
        /// </summary>
        public Task WaitForPageLoadAsync(CancellationToken ct = default)
        {
            EnsureInitialized();

            if (_browser.IsBrowserInitialized && !_browser.IsLoading)
                return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<LoadingStateChangedEventArgs> handler = null;
            handler = (s, e) =>
            {
                if (!e.IsLoading)
                {
                    _browser.LoadingStateChanged -= handler;
                    tcs.TrySetResult(true);
                }
            };
            _browser.LoadingStateChanged += handler;

            ct.Register(() =>
            {
                _browser.LoadingStateChanged -= handler;
                tcs.TrySetCanceled();
            });

            return tcs.Task;
        }

        public async Task<bool> EvaluateBoolAsync(string script, CancellationToken ct = default)
        {
            var resp = await SafeEvaluateAsync(script, ct);
            return resp != null && resp.Success && resp.Result is bool b && b;
        }

        public async Task<string> EvaluateStringAsync(string script, CancellationToken ct = default)
        {
            var resp = await SafeEvaluateAsync(script, ct);
            if (resp != null && resp.Success && resp.Result != null)
                return resp.Result.ToString();
            return null;
        }

        public async Task<bool> WaitForElementAsync(string[] selectors, int timeoutSeconds, CancellationToken ct = default)
        {
            var script = JsHelper.BuildElementExistsScript(selectors);
            var found = await BrowserWaitHelper.PollUntilAsync(
                () => EvaluateBoolAsync(script, ct), timeoutSeconds, 300, ct);

            if (!found)
                LogHelper.Warning($"Element bulunamadı (zaman aşımı). Denenen selector'lar: {string.Join(" | ", selectors)}");
            return found;
        }

        public async Task<bool> ClickElementAsync(string[] selectors, CancellationToken ct = default)
        {
            var script = JsHelper.BuildClickScript(selectors);
            var ok = await EvaluateBoolAsync(script, ct);
            LogHelper.Info(ok
                ? $"Tıklandı: {selectors[0]} (ve alternatifleri)"
                : $"Tıklanacak element bulunamadı: {string.Join(" | ", selectors)}");
            return ok;
        }

        public async Task<bool> RealClickAtAsync(double x, double y, CancellationToken ct = default)
        {
            EnsureInitialized();
            try
            {
                ct.ThrowIfCancellationRequested();
                // Gerçek (trusted) fare olayları: CDP (DevTools) Input.dispatchMouseEvent. Koordinatlar
                // CSS px (getBoundingClientRect ile aynı, viewport override düzleminde). Bazı Vue
                // widget'ları (ör. saat menüsü) JS ile dispatch edilen (isTrusted=false) olaylara
                // tepki vermez; bu yöntem gerçek girişle çözer. move → press → release.
                await DispatchMouseAsync("mouseMoved", x, y, withButton: false);
                await DispatchMouseAsync("mousePressed", x, y, withButton: true);
                await DispatchMouseAsync("mouseReleased", x, y, withButton: true);
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogHelper.Warning("CEF gerçek tıklama (CDP) hatası: " + ex.Message);
                return false;
            }
        }

        private async Task DispatchMouseAsync(string type, double x, double y, bool withButton)
        {
            var p = new Dictionary<string, object> { ["type"] = type, ["x"] = x, ["y"] = y };
            if (withButton)
            {
                p["button"] = "left";
                p["buttons"] = 1;
                p["clickCount"] = 1;
            }
            await DevToolsAsync("Input.dispatchMouseEvent", p);
        }

        public async Task<bool> SetInputValueAsync(string[] selectors, string value, CancellationToken ct = default)
        {
            var script = JsHelper.BuildSetInputValueScript(selectors, value);
            var ok = await EvaluateBoolAsync(script, ct);
            LogHelper.Info(ok
                ? $"Değer yazıldı ('{value}') -> {selectors[0]}"
                : $"Input bulunamadı: {string.Join(" | ", selectors)}");
            return ok;
        }

        public async Task<string> GetTextBySelectorsAsync(string[] selectors, CancellationToken ct = default)
        {
            var script = JsHelper.BuildGetTextScript(selectors);
            return await EvaluateStringAsync(script, ct);
        }

        public async Task<List<string>> GetElementsTextAsync(string[] selectors, CancellationToken ct = default)
        {
            var script = JsHelper.BuildGetElementsTextScript(selectors);
            var json = await EvaluateStringAsync(script, ct);
            if (string.IsNullOrWhiteSpace(json))
                return new List<string>();
            try
            {
                return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public async Task<bool> WaitForResultsAsync(string[] cardSelectors, int timeoutSeconds, CancellationToken ct = default)
        {
            var script = JsHelper.BuildHasResultsScript(cardSelectors);
            var found = await BrowserWaitHelper.PollUntilAsync(
                () => EvaluateBoolAsync(script, ct), timeoutSeconds, 400, ct);

            LogHelper.Info(found ? "Sonuç kartları yüklendi." : "Sonuç kartları bulunamadı (zaman aşımı).");
            return found;
        }

        public async Task<bool> IsLoggedInAsync(string[] loggedInSelectors, CancellationToken ct = default)
        {
            var script = JsHelper.BuildElementExistsScript(loggedInSelectors);
            return await EvaluateBoolAsync(script, ct);
        }

        public async Task ClearSessionAsync(CancellationToken ct = default)
        {
            EnsureInitialized();

            try
            {
                await SafeEvaluateAsync(@"(function(){
                    try { localStorage.clear(); } catch(e) {}
                    try { sessionStorage.clear(); } catch(e) {}
                    return true;
                })();", ct);
                LogHelper.Info("CEF local/session storage temizlendi.");
            }
            catch (Exception ex)
            {
                LogHelper.Warning("CEF storage temizlenemedi: " + ex.Message);
            }

            try
            {
                var manager = Cef.GetGlobalCookieManager();
                await manager.DeleteCookiesAsync("https://www.yolcu360.com", null);
                await manager.DeleteCookiesAsync("https://yolcu360.com", null);
                await manager.DeleteCookiesAsync(null, null);
                await manager.FlushStoreAsync();
                LogHelper.Info("CEF oturum cerezleri temizlendi.");
            }
            catch (Exception ex)
            {
                LogHelper.Warning("CEF oturum cerezleri temizlenemedi: " + ex.Message);
            }

            try
            {
                // GetBrowserHost()/RequestContext tarayıcı tam başlatılmamışsa null olabilir → null-güvenli.
                var ctx = _browser?.GetBrowser()?.GetHost()?.RequestContext;
                if (ctx != null)
                    await ctx.ClearHttpAuthCredentialsAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Warning("CEF auth context temizligi tamamlanamadi: " + ex.Message);
            }
        }

        public async Task ClearSiteSessionAsync(CancellationToken ct = default)
        {
            // YUMUŞAK ÇIKIŞ: yalnızca Yolcu360 alan adının çerezleri + storage temizlenir.
            // Google/reCAPTCHA güven çerezlerine (farklı alan adı) DOKUNULMAZ → çıkış sonrası tekrar
            // girişte reCAPTCHA seni "şüpheli yeni tarayıcı" sayıp engellemez.
            EnsureInitialized();

            try
            {
                await SafeEvaluateAsync(@"(function(){
                    try { localStorage.clear(); } catch(e) {}
                    try { sessionStorage.clear(); } catch(e) {}
                    return true;
                })();", ct);
            }
            catch (Exception ex)
            {
                LogHelper.Warning("CEF storage temizlenemedi (yumuşak çıkış): " + ex.Message);
            }

            try
            {
                var manager = Cef.GetGlobalCookieManager();
                // SADECE Yolcu360 alan adı çerezleri (null,null = TÜM çerezler KULLANILMAZ).
                await manager.DeleteCookiesAsync("https://www.yolcu360.com", null);
                await manager.DeleteCookiesAsync("https://yolcu360.com", null);
                await manager.FlushStoreAsync();
                LogHelper.Info("Yolcu360 oturumu temizlendi (yumuşak çıkış, reCAPTCHA güveni korundu).");
            }
            catch (Exception ex)
            {
                LogHelper.Warning("CEF yumuşak çıkış tamamlanamadı: " + ex.Message);
            }
        }

        public async Task<List<BrowserCookieDto>> ExportCookiesAsync(string url, CancellationToken ct = default)
        {
            var result = new List<BrowserCookieDto>();
            try
            {
                var manager = Cef.GetGlobalCookieManager();
                var cookies = await manager.VisitUrlCookiesAsync(url, true);
                if (cookies != null)
                    foreach (var c in cookies)
                        result.Add(new BrowserCookieDto
                        {
                            Name = c.Name, Value = c.Value, Domain = c.Domain, Path = c.Path,
                            Secure = c.Secure, HttpOnly = c.HttpOnly, Expires = c.Expires
                        });
            }
            catch (Exception ex) { LogHelper.Warning("CEF çerez export hatası: " + ex.Message); }
            return result;
        }

        public async Task ImportCookiesAsync(string url, List<BrowserCookieDto> cookies, CancellationToken ct = default)
        {
            if (cookies == null || cookies.Count == 0) return;
            try
            {
                var manager = Cef.GetGlobalCookieManager();
                int ok = 0;
                foreach (var c in cookies)
                {
                    var cef = new Cookie
                    {
                        Name = c.Name, Value = c.Value, Domain = c.Domain, Path = c.Path,
                        Secure = c.Secure, HttpOnly = c.HttpOnly, Expires = c.Expires
                    };
                    if (await manager.SetCookieAsync(url, cef)) ok++;
                }
                await manager.FlushStoreAsync();
                LogHelper.Info($"CEF'e {ok}/{cookies.Count} oturum çerezi köprülendi.");
            }
            catch (Exception ex) { LogHelper.Warning("CEF çerez import hatası: " + ex.Message); }
        }

        public void SetBrowserVisible(bool visible)
        {
            _visible = visible;
            if (_browser != null)
                _browser.Visible = visible;
            LogHelper.Info($"Tarayıcı görünürlüğü: {(visible ? "Görünür" : "Gizli")}");
        }

        // ---- Yardımcılar ----

        private async Task<JavascriptResponse> SafeEvaluateAsync(string script, CancellationToken ct)
        {
            EnsureInitialized();
            try
            {
                if (!_browser.CanExecuteJavascriptInMainFrame)
                {
                    // Sayfa JS çalıştırmaya hazır değilse kısaca bekle.
                    var ready = await BrowserWaitHelper.PollUntilAsync(
                        () => Task.FromResult(_browser.CanExecuteJavascriptInMainFrame),
                        5, 200, ct);
                    if (!ready)
                        return null;
                }
                return await _browser.EvaluateScriptAsync(script);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.Error("JavaScript çalıştırma hatası.", ex);
                return null;
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized || _browser == null)
                throw new InvalidOperationException("Tarayıcı henüz başlatılmadı. Önce InitializeAsync çağrılmalı.");
        }
    }
}
