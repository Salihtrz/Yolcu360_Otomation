using Guna.UI2.WinForms;
using Yolcu360.BusinessLayer;
using Yolcu360.BusinessLayer.Concrete;
using Yolcu360.Common.Constants;
using Yolcu360.Common.Helpers;
using Yolcu360.Common.Logging;
using Yolcu360.PresentationLayer.Helpers;
using Yolcu360.PresentationLayer.UI;

namespace Yolcu360.PresentationLayer
{
    /// <summary>
    /// SADE giriş ekranı — giriş GERÇEK Edge (WebView2) tarayıcısında yapılır (CefSharp gömülü
    /// olduğu için reCAPTCHA düşük puan verip engelliyordu; gerçek Edge geçer). Akış: dropdown'dan
    /// numara seç → "Onayla" → gömülü WebView2'de numara yazılır, "Devam Et"e kullanıcı basar →
    /// SMS MacroDroid ile gelir, kod otomatik girilir → giriş başarılı olunca oturum çerezleri
    /// CefSharp otomasyon tarayıcısına KÖPRÜLENİR (otomasyon da girişli olur). Bypass YOK.
    /// </summary>
    public class OtpLoginForm : Form
    {
        private const string SiteCookieUrl = "https://www.yolcu360.com";

        private readonly AppServices _services;
        private readonly Action<bool> _setBrowserVisible;
        private readonly Action<bool> _onLoginSuccess;
        private readonly IProgress<string> _progress;

        private CancellationTokenSource _cts;
        private Action<string> _otpHandler;
        private bool _otpSubmitStarted;
        private bool _loginDetected;
        private System.Windows.Forms.Timer _loginPollTimer;

        private ComboBox _cmbPhone;
        private Guna2Button _btnConfirm;
        private Label _lblStatus;
        private Panel _loginHost;
        private Control _browserCtrl;   // WebView2 login kontrolü (kapanışta ayrılır, dispose edilmez)

        public OtpLoginForm(AppServices services, Action<bool> setBrowserVisible, Action<bool> onLoginSuccess)
        {
            _services = services;
            _setBrowserVisible = setBrowserVisible;
            _onLoginSuccess = onLoginSuccess;
            _progress = new Progress<string>(SetStatus);

            BuildUi();
            Load += OnLoad;
            FormClosing += OnClosing;
        }

        private void BuildUi()
        {
            Text = "Giriş Yap (SMS)";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1040, 820);
            MinimumSize = new Size(900, 680);
            BackColor = ThemeColors.Background;
            Font = new Font("Segoe UI", 9.5f);
            MaximizeBox = true;

            var top = new Panel { Dock = DockStyle.Top, Height = 118, BackColor = ThemeColors.Surface, Padding = new Padding(20, 12, 20, 10) };
            top.Controls.Add(new Label
            {
                Text = "Giriş Yap (SMS)",
                AutoSize = true,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = ThemeColors.TextDark,
                Location = new Point(20, 12)
            });

            top.Controls.Add(new Label
            {
                Text = "Numara",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = ThemeColors.TextDark,
                Location = new Point(20, 52)
            });
            _cmbPhone = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                Location = new Point(90, 49),
                Width = 240,
                Font = new Font("Segoe UI", 10.5f)
            };
            top.Controls.Add(_cmbPhone);

            _btnConfirm = UiStyleHelper.Button("Onayla", ButtonVariant.Primary, 130, 30);
            _btnConfirm.Location = new Point(344, 48);
            _btnConfirm.Click += OnConfirmClick;
            top.Controls.Add(_btnConfirm);

            top.Controls.Add(new Label
            {
                Text = "Giriş aşağıdaki gerçek tarayıcıda yapılır. 'Devam Et'e siz basın; SMS kodu otomatik girilecek. Başarılı olunca oturum uygulamaya aktarılır.",
                AutoSize = false,
                Size = new Size(990, 20),
                ForeColor = ThemeColors.TextMuted,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(20, 88)
            });

            _lblStatus = new Label
            {
                Text = "Numara seçip Onayla'ya basın.",
                AutoSize = false,
                Width = 500,
                Height = 24,
                Location = new Point(490, 50),
                ForeColor = ThemeColors.Primary,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            top.Controls.Add(_lblStatus);

            _loginHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            Controls.Add(_loginHost);
            Controls.Add(top);
        }

        private async void OnLoad(object sender, EventArgs e)
        {
            // Kayıtlı numaraları doldur.
            try
            {
                var numbers = await _services.UserService.GetAllPhoneNumbersAsync();
                _cmbPhone.Items.Clear();
                foreach (var n in numbers) _cmbPhone.Items.Add(n);
                if (_cmbPhone.Items.Count > 0) _cmbPhone.SelectedIndex = 0;
            }
            catch (Exception ex) { LogHelper.Warning("Kayıtlı numaralar okunamadı: " + ex.Message); }

            // Login sırasında CefSharp otomasyon penceresini gizle (kafa karıştıran fazladan pencere olmasın).
            try { _setBrowserVisible?.Invoke(false); } catch { }

            // GİRİŞ tarayıcısını (WebView2) başlat ve forma göm.
            try
            {
                SetStatus("Giriş tarayıcısı hazırlanıyor...");
                await _services.LoginBrowserService.InitializeAsync(Yolcu360Constants.LoginUrl);
                _browserCtrl = _services.LoginBrowserService.GetBrowserControl();
                if (_browserCtrl != null)
                {
                    _browserCtrl.Dock = DockStyle.Fill;
                    _loginHost.Controls.Clear();
                    _loginHost.Controls.Add(_browserCtrl);
                    _services.LoginBrowserService.SetBrowserVisible(true);
                }
                SetStatus("Numara seçip Onayla'ya basın.");
            }
            catch (Exception ex)
            {
                LogHelper.Error("Giriş tarayıcısı başlatılamadı.", ex);
                SetStatus("Giriş tarayıcısı başlatılamadı.");
            }

            // SMS kodu geldiğinde otomatik gönder (MacroDroid → dinleyici → bu olay).
            _otpHandler = code => UiHelper.RunOnUi(this, () => _ = SubmitReceivedOtpAsync(code));
            _services.OtpReceiverService.OtpReceived += _otpHandler;

            StartLoginPoller();

            // ZATEN GİRİŞLİ Mİ? Girişli kullanıcıda /login ana sayfaya yönlendirilir; bu durumda
            // yeniden giriş GEREKMEZ — mevcut oturum çerezleri doğrudan CefSharp'a köprülenir.
            await Task.Delay(2500);
            if (!_loginDetected && !IsOnLoginPage())
            {
                SetStatus("Zaten girişlisiniz. Oturum uygulamaya aktarılıyor...");
                await BridgeAndFinishAsync();
            }
        }

        /// <summary>
        /// WebView2 (login) tarayıcısının oturum token'ını localStorage'a yazmasını bekler: localStorage
        /// içeriği DOLU ve STABİL olana kadar (üst üste 2 kez aynı boyut) yoklar; en fazla ~6 sn. Bu,
        /// "token henüz yazılmadan export ettik" yarışını (flaky giriş) önler.
        /// </summary>
        private async Task WaitForLoginBrowserSessionAsync()
        {
            int lastLen = -1, stable = 0;
            for (int i = 0; i < 12 && stable < 2; i++)
            {
                await Task.Delay(500);
                var ls = await _services.LoginBrowserService.EvaluateStringAsync(JsHelper.BuildGetLocalStorageScript());
                var len = (ls ?? string.Empty).Length;
                if (len == lastLen && len > 2) stable++;   // "{}" = 2; >2 → içerik var
                else { stable = 0; lastLen = len; }
            }
        }

        /// <summary>
        /// CefSharp girişli mi? /login'e gidip GİRİŞLİ kullanıcıda ana sayfaya yönlendirilmesini izler.
        /// CefSharp WaitForPageLoad yarışını önlemek için: LoadUrl sonrası önce yeterince bekle (LoadUrl
        /// işlensin), sonra birkaç saniye URL'i izle (yönlendi mi?). /login'de kalırsa girişsiz.
        /// </summary>
        private async Task<bool> VerifyCefLoggedInAsync()
        {
            try
            {
                await _services.BrowserService.LoadUrlAsync(Yolcu360Constants.LoginUrl);
                await Task.Delay(2500); // LoadUrl + /login yüklensin (+ girişliyse redirect başlasın)
                for (int i = 0; i < 12; i++) // 6 sn boyunca son durumu izle
                {
                    var url = (_services.BrowserService.CurrentUrl ?? string.Empty).ToLowerInvariant();
                    if (url.Contains("yolcu360.com") && !url.Contains("/login"))
                        return true; // /login'den ana sayfaya geçmiş → girişli
                    await Task.Delay(500);
                }
            }
            catch (Exception ex) { LogHelper.Warning("Giriş doğrulaması yapılamadı: " + ex.Message); }
            return false;
        }

        /// <summary>WebView2 şu an Yolcu360 giriş sayfasında mı? (ana sayfaya yönlendiyse girişli demektir.)</summary>
        private bool IsOnLoginPage()
        {
            var url = _services.LoginBrowserService.CurrentUrl ?? string.Empty;
            return url.Contains("/login", StringComparison.OrdinalIgnoreCase);
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            // WebView2 kontrolünü formdan AYIR (form dispose ederken onu yok etmesin; tekrar
            // giriş açılışında aynı kontrol yeniden kullanılır).
            try { if (_browserCtrl != null) _loginHost.Controls.Remove(_browserCtrl); } catch { }
            try { _loginPollTimer?.Stop(); _loginPollTimer?.Dispose(); } catch { }
            try { if (_otpHandler != null) _services.OtpReceiverService.OtpReceived -= _otpHandler; } catch { }
            try { _cts?.Cancel(); } catch { }
            try { _services.OtpReceiverService.Stop(); } catch { }
        }

        private async void OnConfirmClick(object sender, EventArgs e)
        {
            var phone = (_cmbPhone.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(phone))
            {
                UiHelper.Warn("Lütfen bir numara seçin veya girin.");
                return;
            }

            _cts = new CancellationTokenSource();
            _otpSubmitStarted = false;
            SetBusy(true);
            try
            {
                try { await _services.UserService.AddPhoneNumberAsync(phone, _cts.Token); } catch { }

                await EnsureListenerStartedAsync();

                SetStatus("Numara tarayıcıya yazılıyor...");
                await _services.LoginAutomationService.StartPhoneLoginAsync(phone, _progress, _cts.Token);
                SetStatus("Numara yazıldı. Tarayıcıda 'Devam Et'e siz basın; SMS kodu otomatik girilecek.");
            }
            catch (AutomationException ax)
            {
                SetStatus("Giriş tamamlanamadı.");
                UiHelper.Warn(ax.Message);
            }
            catch (OperationCanceledException) { SetStatus("Giriş iptal edildi."); }
            catch (Exception ex)
            {
                LogHelper.Error("Giriş hatası.", ex);
                UiHelper.Error("Giriş sırasında hata oluştu.\n\n" + ex.Message);
            }
            finally { SetBusy(false); }
        }

        private async Task EnsureListenerStartedAsync()
        {
            if (_services.OtpReceiverService.IsListening) return;
            _otpSubmitStarted = false;
            await _services.OtpReceiverService.StartAsync();
        }

        private async Task SubmitReceivedOtpAsync(string code)
        {
            if (!_services.OtpReceiverService.ValidateOtpCode(code)) return;
            // Zaten gönderiliyorsa ya da giriş poller tarafından TAMAMLANDIYSA tekrar deneme.
            if (_otpSubmitStarted || _loginDetected) return;

            _otpSubmitStarted = true;
            SetBusy(true);
            try
            {
                SetStatus("SMS kodu otomatik giriliyor...");
                var token = _cts?.Token ?? CancellationToken.None;
                var ok = await _services.LoginAutomationService.SubmitOtpAsync(code, _progress, token);
                if (ok) await BridgeAndFinishAsync();
                else if (!_loginDetected) SetStatus("Giriş doğrulanamadı. Tarayıcıdan kontrol edin.");
            }
            catch (Exception ex)
            {
                _otpSubmitStarted = false;
                // Giriş poller tarafından zaten tamamlandıysa (yarış) bu geç istisna ZARARSIZDIR → uyarma.
                if (_loginDetected)
                    LogHelper.Info("OTP gönderimi sonrası geç istisna (giriş zaten tamamlandı): " + ex.Message);
                else
                {
                    LogHelper.Error("OTP otomatik gönderme hatası.", ex);
                    UiHelper.Warn("Kod otomatik gönderilirken bir sorun oluştu. Tarayıcıdan elle deneyebilirsiniz.");
                }
            }
            finally { if (!IsDisposed) SetBusy(false); }
        }

        /// <summary>
        /// Giriş başarılı: WebView2'deki oturum çerezlerini CefSharp otomasyon tarayıcısına köprüler,
        /// ana tarayıcıyı yeniler (girişli) ve formu kapatır.
        /// </summary>
        private async Task BridgeAndFinishAsync()
        {
            if (_loginDetected) return;
            _loginDetected = true;
            try { _loginPollTimer?.Stop(); } catch { }
            try { _services.OtpReceiverService.Stop(); } catch { }

            bool loggedIn = false;
            try
            {
                // ÖNEMLİ: Giriş başarılı olunca SPA, oturum token'ını localStorage'a/çereze yazar ama
                // bu birkaç yüz ms sürebilir. Çok erken export edersek token'ı KAÇIRIRIZ (flaky giriş).
                // Token'ın yazılmasını bekle: localStorage doluncaya kadar (max ~6sn) yokla.
                SetStatus("Oturum hazırlanıyor...");
                await WaitForLoginBrowserSessionAsync();

                SetStatus("Oturum uygulamaya aktarılıyor...");

                // 1) ÇEREZLER: WebView2 → CefSharp.
                var cookies = await _services.LoginBrowserService.ExportCookiesAsync(SiteCookieUrl);
                await _services.BrowserService.ImportCookiesAsync(SiteCookieUrl, cookies);

                // 2) localStorage: Yolcu360 auth token'ı genelde çerezde değil localStorage'da → onu da
                //    köprüle. WebView2'den oku; CefSharp'ı yolcu360 origin'ine getirip yaz; sonra reload.
                var lsJson = await _services.LoginBrowserService.EvaluateStringAsync(JsHelper.BuildGetLocalStorageScript());
                LogHelper.Info($"Köprü: localStorage boyutu {(lsJson ?? "").Length} karakter.");
                await _services.BrowserService.LoadUrlAsync(Yolcu360Constants.HomeUrl);
                await Task.Delay(3000); // CefSharp yolcu360 origin'i yüklesin
                if (!string.IsNullOrWhiteSpace(lsJson) && lsJson != "{}")
                    await _services.BrowserService.EvaluateBoolAsync(JsHelper.BuildSetLocalStorageScript(lsJson));
                await _services.BrowserService.LoadUrlAsync(Yolcu360Constants.HomeUrl);
                await Task.Delay(2000); // SPA token'ı okuyup girişli render etsin
                LogHelper.Info($"{cookies.Count} çerez + localStorage CefSharp'a köprülendi.");

                // 3) DOĞRULAMA: CefSharp /login'e gidince ana sayfaya dönüyorsa GİRİŞLİ demektir.
                // Viewport tablet kilitli olduğu için kullanıcı göremiyor → programatik doğrula + sidebar.
                loggedIn = await VerifyCefLoggedInAsync();

                // Ana sayfaya dön (otomasyon buradan devam eder).
                await _services.BrowserService.LoadUrlAsync(Yolcu360Constants.HomeUrl);
            }
            catch (Exception ex)
            {
                LogHelper.Warning("Oturum köprüleme/doğrulama tamamlanamadı: " + ex.Message);
            }

            if (loggedIn)
            {
                SetStatus("Giriş yapıldı ve doğrulandı.");
                LogHelper.Info("CefSharp girişli doğrulandı (oturum köprülendi).");
                _onLoginSuccess?.Invoke(true);
            }
            else
            {
                SetStatus("Oturum aktarılamadı — girişli görünmüyor.");
                LogHelper.Warning("Köprü sonrası CefSharp girişli doğrulanamadı.");
                _onLoginSuccess?.Invoke(false);
            }
            BeginInvoke(new Action(Close));
        }

        private void StartLoginPoller()
        {
            _loginPollTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _loginPollTimer.Tick += async (s, e) => await DetectCompletedLoginAsync();
            _loginPollTimer.Start();
        }

        private async Task DetectCompletedLoginAsync()
        {
            // OTP gönderimi sürüyorsa poller geri çekilir — köprülemeyi OTP yolu tamamlasın (yarışı önler).
            if (_loginDetected || _otpSubmitStarted || IsDisposed) return;
            try
            {
                // Giriş tamam sayılır: WebView2 giriş sayfasından ana sayfaya geçtiyse (zaten girişli
                // ya da OTP başarılı) veya logged-in göstergesi bulunduysa.
                if (!IsOnLoginPage() || await _services.LoginAutomationService.IsLoggedInAsync())
                    await BridgeAndFinishAsync();
            }
            catch (Exception ex) { LogHelper.Warning("Giriş durum kontrolü yapılamadı: " + ex.Message); }
        }

        private void SetBusy(bool busy)
        {
            UiHelper.SetControlsEnabled(!busy, _btnConfirm, _cmbPhone);
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void SetStatus(string text) => UiHelper.RunOnUi(this, () => _lblStatus.Text = text);
    }
}
