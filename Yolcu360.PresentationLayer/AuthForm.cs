using Yolcu360.BusinessLayer.Concrete.Automation;
using Yolcu360.Common.Automation;
using Guna.UI2.WinForms;
using Yolcu360.BusinessLayer;
using Yolcu360.BusinessLayer.Concrete;
using Yolcu360.Common.Constants;
using Yolcu360.Common.Helpers;
using Yolcu360.Common.Logging;
using Yolcu360.DtoLayer.BrowserDto;
using Yolcu360.DtoLayer.UserDto;
using Yolcu360.PresentationLayer.Helpers;
using Yolcu360.PresentationLayer.UI;

namespace Yolcu360.PresentationLayer
{
    /// <summary>
    /// Uygulamanın ZORUNLU giriş kapısı. İki mod içerir: KAYIT (e-posta/şifre/telefon) ve GİRİŞ
    /// (e-posta/şifre). Giriş başarılı olunca hesaba bağlı telefon numarası DB'den çekilir ve
    /// Yolcu360 SMS/OTP giriş akışı ARKA PLANDA (gizli WebView2) bu numarayla başlatılır:
    ///   numara yazılır → "Devam Et" otomatik tıklanır → SMS kodu MacroDroid ile gelir → kod
    ///   otomatik girilir → oturum CefSharp otomasyon tarayıcısına köprülenir.
    /// Tüm SMS/kod işlemleri sırasında ekranı bir YÜKLENİYOR paneli kaplar. WebView2 hiçbir zaman
    /// kullanıcıya pencere olarak gösterilmez (opak panellerin arkasında render edilir); yalnızca
    /// otomatik adım engellenirse (reCAPTCHA vb.) elle tamamlamak için öne çıkarılır. Bypass YOK.
    /// </summary>
    public class AuthForm : Form
    {
        // Yolcu360 hem apex hem www origin'inde sunuluyor; giriş sonrası apex'e (www'siz) düşüyor.
        // Oturum (çerez + localStorage) HER İKİ origin'e de köprülenir; localStorage origin'e özeldir.
        private static readonly string[] SiteOrigins = { "https://yolcu360.com", "https://www.yolcu360.com" };

        private readonly AppServices _services;
        private readonly IProgress<string> _progress;

        private CancellationTokenSource _cts;
        private Action<string> _otpHandler;
        private bool _otpSubmitStarted;
        private bool _loginDetected;
        private System.Windows.Forms.Timer _loginPollTimer;

        // WebView2 (login) bu panelin arkasında render edilir; kullanıcı görmez.
        private Panel _browserLayer;
        private Control _browserCtrl;

        // Üstte: e-posta/şifre giriş + kayıt kartı (WebView2'yi tamamen kaplar).
        private Panel _authPanel;
        private Guna2Panel _card;
        private Panel _loginPanel;
        private Panel _registerPanel;

        private Guna2TextBox _txtEmail;
        private Guna2TextBox _txtPassword;
        private Guna2Button _btnLogin;

        private Guna2TextBox _txtRegEmail;
        private Guna2TextBox _txtRegPassword;
        private Guna2TextBox _txtRegPhone;
        private Guna2Button _btnRegister;

        private Label _lblAuthStatus;

        // Yükleniyor (SMS/kod) overlay'i.
        private Panel _loadingPanel;
        private Label _lblLoadingStatus;

        // Otomatik adım engellenirse elle devam için üst bilgi şeridi.
        private Panel _manualBar;
        private Label _lblManual;

        /// <summary>Tüm akış (uygulama girişi + Yolcu360 oturumu) başarıyla tamamlandıysa true.</summary>
        public bool AuthSucceeded { get; private set; }

        public AuthForm(AppServices services)
        {
            _services = services;
            _progress = new Progress<string>(SetLoadingStatus);

            BuildUi();
            Load += OnLoad;
            FormClosing += OnClosing;
        }

        // ----------------------------------------------------------------- UI

        private void BuildUi()
        {
            Text = "Yolcu360 - Giriş";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(960, 700);
            MinimumSize = new Size(760, 600);
            BackColor = ThemeColors.Background;
            Font = UiStyleHelper.BaseFont;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedSingle;

            // 1) En arkada: WebView2'nin yaşayacağı katman.
            _browserLayer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Controls.Add(_browserLayer);

            // 2) Otomatik adım engellenirse görünür olacak elle-devam şeridi (normalde gizli).
            _manualBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = ThemeColors.Warning, Visible = false };
            _lblManual = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(12, 0, 12, 0),
                Text = ""
            };
            _manualBar.Controls.Add(_lblManual);
            Controls.Add(_manualBar);

            // 3) Üstte: opak giriş/kayıt katmanı (WebView2'yi tamamen kapatır).
            _authPanel = new Panel { Dock = DockStyle.Fill, BackColor = ThemeColors.Background };
            BuildAuthCard();
            Controls.Add(_authPanel);
            _authPanel.BringToFront();

            // 4) En üstte (gerektiğinde): yükleniyor overlay'i.
            BuildLoadingPanel();
            Controls.Add(_loadingPanel);
            _loadingPanel.Visible = false;

            _authPanel.Resize += (s, e) => CenterCard();
        }

        private void BuildAuthCard()
        {
            _card = UiStyleHelper.NewCard();
            _card.Size = new Size(420, 540);

            var title = new Label
            {
                Text = "Yolcu360 Otomasyon",
                AutoSize = true,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = ThemeColors.TextDark,
                Location = new Point(28, 26),
                BackColor = Color.Transparent
            };
            var subtitle = new Label
            {
                Text = "Devam etmek için giriş yapın",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = ThemeColors.TextMuted,
                Location = new Point(28, 60),
                BackColor = Color.Transparent
            };
            _card.Controls.Add(title);
            _card.Controls.Add(subtitle);

            BuildLoginPanel();
            BuildRegisterPanel();
            _card.Controls.Add(_loginPanel);
            _card.Controls.Add(_registerPanel);

            _lblAuthStatus = new Label
            {
                AutoSize = false,
                Size = new Size(364, 48),
                Location = new Point(28, 480),
                ForeColor = ThemeColors.Primary,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.Transparent,
                Text = ""
            };
            _card.Controls.Add(_lblAuthStatus);

            _authPanel.Controls.Add(_card);
            ShowMode(login: true);
        }

        private void BuildLoginPanel()
        {
            _loginPanel = new Panel { Location = new Point(0, 96), Size = new Size(420, 380), BackColor = Color.Transparent };

            _loginPanel.Controls.Add(MakeFieldLabel("E-posta", 28, 8));
            _txtEmail = MakeInput(28, 32);
            _loginPanel.Controls.Add(_txtEmail);

            _loginPanel.Controls.Add(MakeFieldLabel("Şifre", 28, 80));
            _txtPassword = MakeInput(28, 104);
            _txtPassword.UseSystemPasswordChar = true;
            _txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; btnLogin_Click(s, e); } };
            _loginPanel.Controls.Add(_txtPassword);

            _btnLogin = UiStyleHelper.Button("Giriş Yap", ButtonVariant.Primary, 364, 42);
            _btnLogin.Location = new Point(28, 164);
            _btnLogin.Click += btnLogin_Click;
            _loginPanel.Controls.Add(_btnLogin);

            var lnk = MakeLink("Hesabın yok mu? Kayıt Ol", 28, 220);
            lnk.LinkClicked += (s, e) => { ClearStatus(); ShowMode(login: false); };
            _loginPanel.Controls.Add(lnk);
        }

        private void BuildRegisterPanel()
        {
            _registerPanel = new Panel { Location = new Point(0, 96), Size = new Size(420, 380), BackColor = Color.Transparent };

            _registerPanel.Controls.Add(MakeFieldLabel("E-posta", 28, 8));
            _txtRegEmail = MakeInput(28, 32);
            _registerPanel.Controls.Add(_txtRegEmail);

            _registerPanel.Controls.Add(MakeFieldLabel("Şifre", 28, 76));
            _txtRegPassword = MakeInput(28, 100);
            _txtRegPassword.UseSystemPasswordChar = true;
            _registerPanel.Controls.Add(_txtRegPassword);

            _registerPanel.Controls.Add(MakeFieldLabel("Telefon (Yolcu360 hesabınız)", 28, 144));
            _txtRegPhone = MakeInput(28, 168);
            _txtRegPhone.PlaceholderText = "+90 5xx xxx xx xx";
            _registerPanel.Controls.Add(_txtRegPhone);

            _btnRegister = UiStyleHelper.Button("Kayıt Ol", ButtonVariant.Success, 364, 42);
            _btnRegister.Location = new Point(28, 224);
            _btnRegister.Click += btnRegister_Click;
            _registerPanel.Controls.Add(_btnRegister);

            var lnk = MakeLink("Zaten hesabın var mı? Giriş Yap", 28, 280);
            lnk.LinkClicked += (s, e) => { ClearStatus(); ShowMode(login: true); };
            _registerPanel.Controls.Add(lnk);
        }

        private void BuildLoadingPanel()
        {
            _loadingPanel = new Panel { Dock = DockStyle.Fill, BackColor = ThemeColors.Background };

            var box = new Panel { Size = new Size(420, 160), BackColor = Color.Transparent };

            var bar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Location = new Point(20, 30),
                Size = new Size(380, 14)
            };
            box.Controls.Add(bar);

            _lblLoadingStatus = new Label
            {
                Text = "Lütfen bekleyin...",
                AutoSize = false,
                Size = new Size(380, 70),
                Location = new Point(20, 60),
                TextAlign = ContentAlignment.TopCenter,
                ForeColor = ThemeColors.TextDark,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            box.Controls.Add(_lblLoadingStatus);

            _loadingPanel.Controls.Add(box);
            _loadingPanel.Resize += (s, e) =>
            {
                box.Left = Math.Max(0, (_loadingPanel.Width - box.Width) / 2);
                box.Top = Math.Max(0, (_loadingPanel.Height - box.Height) / 2);
            };
        }

        private static Label MakeFieldLabel(string text, int x, int y) => new()
        {
            Text = text,
            AutoSize = true,
            ForeColor = ThemeColors.TextMuted,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(x, y),
            BackColor = Color.Transparent
        };

        private static Guna2TextBox MakeInput(int x, int y)
        {
            var t = UiStyleHelper.TextBox(364);
            t.Location = new Point(x, y);
            t.Height = 38;
            return t;
        }

        private static LinkLabel MakeLink(string text, int x, int y) => new()
        {
            Text = text,
            AutoSize = true,
            Location = new Point(x, y),
            Font = new Font("Segoe UI", 9f),
            LinkColor = ThemeColors.Primary,
            ActiveLinkColor = ThemeColors.PrimaryDark,
            BackColor = Color.Transparent
        };

        private void ShowMode(bool login)
        {
            _loginPanel.Visible = login;
            _registerPanel.Visible = !login;
            if (login) _txtEmail?.Focus(); else _txtRegEmail?.Focus();
        }

        private void CenterCard()
        {
            if (_card == null) return;
            _card.Left = Math.Max(0, (_authPanel.Width - _card.Width) / 2);
            _card.Top = Math.Max(0, (_authPanel.Height - _card.Height) / 2);
        }

        // ----------------------------------------------------------------- Load

        private async void OnLoad(object sender, EventArgs e)
        {
            CenterCard();

            // GİRİŞ tarayıcısını (WebView2) önceden, ARKA PLANDA başlat ve forma (görünmez katmana) göm.
            // Yolcu360 otomasyonu yalnızca uygulama girişi başarılı olunca tetiklenir.
            try
            {
                SetLoadingStatus("Hazırlanıyor...");
                await _services.LoginBrowserService.InitializeAsync(Yolcu360Constants.LoginUrl);
                _browserCtrl = _services.LoginBrowserService.GetBrowserControl();
                if (_browserCtrl != null)
                {
                    _browserCtrl.Dock = DockStyle.Fill;
                    _browserLayer.Controls.Clear();
                    _browserLayer.Controls.Add(_browserCtrl);
                    // Render edilsin (reCAPTCHA gerçek tarayıcı bekler) ama opak panel arkasında kalsın.
                    _services.LoginBrowserService.SetBrowserVisible(true);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("Giriş tarayıcısı başlatılamadı.", ex);
            }
        }

        // ----------------------------------------------------------------- Register

        private async void btnRegister_Click(object sender, EventArgs e)
        {
            var dto = new RegisterUserDto
            {
                Email = (_txtRegEmail.Text ?? string.Empty).Trim(),
                Password = _txtRegPassword.Text ?? string.Empty,
                PhoneNumber = (_txtRegPhone.Text ?? string.Empty).Trim()
            };

            SetAuthBusy(true);
            try
            {
                await _services.UserService.RegisterAsync(dto);
                SetAuthStatus("Kayıt başarılı. Şimdi e-posta ve şifrenizle giriş yapın.", ThemeColors.Success);
                // Girişi kolaylaştır: e-postayı taşı, giriş moduna geç.
                _txtEmail.Text = dto.Email;
                _txtRegEmail.Clear(); _txtRegPassword.Clear(); _txtRegPhone.Clear();
                ShowMode(login: true);
                _txtPassword.Focus();
            }
            catch (AuthenticationException aex)
            {
                SetAuthStatus(aex.Message, ThemeColors.Warning);
            }
            catch (Exception ex)
            {
                LogHelper.Error("Kayıt hatası.", ex);
                SetAuthStatus("Kayıt sırasında bir hata oluştu.", ThemeColors.Danger);
            }
            finally { SetAuthBusy(false); }
        }

        // ----------------------------------------------------------------- Login

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            var dto = new LoginUserDto
            {
                Email = (_txtEmail.Text ?? string.Empty).Trim(),
                Password = _txtPassword.Text ?? string.Empty
            };

            SetAuthBusy(true);
            try
            {
                // 1) UYGULAMA girişi (DB, hash doğrulaması).
                AuthenticatedUserDto authUser;
                try
                {
                    authUser = await _services.UserService.LoginAsync(dto);
                }
                catch (AuthenticationException aex)
                {
                    SetAuthStatus(aex.Message, ThemeColors.Warning);
                    return;
                }

                _services.CurrentUser = authUser;

                // 2) Yolcu360 SMS akışı başlasın. Tarayıcı GÖRÜNÜR yapılır ki kullanıcı numaranın
                //    otomatik yazıldığını, "Devam Et"in tıklandığını ve SMS kodunun girildiğini
                //    CANLI görebilsin. Üstte ne yapıldığını anlatan bir durum şeridi gösterilir.
                ShowBrowserStage("Uygulama girişi başarılı. Yolcu360 açılıyor, bilgiler otomatik dolduruluyor...");
                await StartSmsLoginAsync(authUser.PhoneNumber);
            }
            catch (OperationCanceledException) { ShowAuthCard(); SetAuthStatus("İşlem iptal edildi.", ThemeColors.Warning); }
            catch (Exception ex)
            {
                ShowAuthCard();
                LogHelper.Error("Giriş hatası.", ex);
                SetAuthStatus("Giriş sırasında bir hata oluştu.", ThemeColors.Danger);
            }
            finally { SetAuthBusy(false); }
        }

        /// <summary>
        /// Yolcu360 telefon + SMS/OTP akışını DB'den gelen numarayla başlatır. OTP dinleyici (MacroDroid)
        /// ve giriş poller'ı korunur. Otomatik "Devam Et" adımı engellenirse (AutomationException),
        /// WebView2 öne çıkarılıp kullanıcıya elle tamamlatılır; OTP yine otomatik girilir.
        /// </summary>
        private async Task StartSmsLoginAsync(string phoneNumber)
        {
            _cts = new CancellationTokenSource();
            _otpSubmitStarted = false;
            _loginDetected = false;

            await EnsureListenerStartedAsync();
            if (_otpHandler == null)
            {
                _otpHandler = code => UiHelper.RunOnUi(this, () => _ = SubmitReceivedOtpAsync(code));
                _services.OtpReceiverService.OtpReceived += _otpHandler;
            }
            StartLoginPoller();

            // StartPhoneLoginAsync /login'e TAZE gider (kendi içinde), telefon alanını bulur, numarayı
            // yazar ve "Devam Et"e tıklar — tarayıcı görünür olduğu için kullanıcı izler. Kullanıcı
            // Yolcu360'a ZATEN girişliyse /login ana sayfaya yönlenir; bunu giriş poller'ı algılayıp
            // doğrudan köprüler. NOT: Burada AYRI bir ön-navigasyon YAPMIYORUZ — aynı /login'e art arda
            // iki gidiş WebView2'de NavigationCompleted'ı tetiklemeyip beklemeyi kilitliyordu.
            try
            {
                await _services.LoginAutomationService.StartPhoneLoginAsync(phoneNumber, _progress, _cts.Token);
                if (!_loginDetected)
                    SetLoadingStatus("SMS kodu bekleniyor... Kod gelince otomatik girilecek.\n" + BuildListeningHint());
            }
            catch (OperationCanceledException) { /* form kapandı/iptal (poller köprülemiş olabilir) */ }
            catch (AutomationException ax)
            {
                // Telefon alanı bulunamadı VEYA otomatik "Devam Et" engellendi. Poller bu sırada
                // köprülediyse (zaten girişli) sorun yok; değilse kullanıcı elle devam etsin.
                if (!_loginDetected) EnterManualMode(ax.Message);
            }
        }

        private async Task SubmitReceivedOtpAsync(string code)
        {
            if (!_services.OtpReceiverService.ValidateOtpCode(code)) return;
            if (_otpSubmitStarted || _loginDetected) return;

            _otpSubmitStarted = true;
            ShowBrowserStage("SMS kodu otomatik giriliyor..."); // tarayıcı görünür kalsın, kodun yazıldığını gör
            try
            {
                var token = _cts?.Token ?? CancellationToken.None;
                var ok = await _services.LoginAutomationService.SubmitOtpAsync(code, _progress, token);
                if (ok) await BridgeAndFinishAsync();
                else if (!_loginDetected) SetLoadingStatus("Giriş doğrulanamadı. Tarayıcıdan kontrol edebilirsiniz.");
            }
            catch (Exception ex)
            {
                _otpSubmitStarted = false;
                if (_loginDetected)
                    LogHelper.Info("OTP gönderimi sonrası geç istisna (giriş zaten tamamlandı): " + ex.Message);
                else
                {
                    LogHelper.Error("OTP otomatik gönderme hatası.", ex);
                    SetLoadingStatus("Kod otomatik girilemedi. Tarayıcıdan elle deneyebilirsiniz.");
                }
            }
        }

        // ----------------------------------------------------------------- Bridge / verify (WebView2 → CefSharp oturum köprüleme)

        private async Task WaitForLoginBrowserSessionAsync()
        {
            int lastLen = -1, stable = 0;
            for (int i = 0; i < 12 && stable < 2; i++)
            {
                await Task.Delay(500);
                var ls = await _services.LoginBrowserService.EvaluateStringAsync(JsHelper.BuildGetLocalStorageScript());
                var len = (ls ?? string.Empty).Length;
                if (len == lastLen && len > 2) stable++;
                else { stable = 0; lastLen = len; }
            }
        }

        private bool IsOnLoginPage()
        {
            var url = _services.LoginBrowserService.CurrentUrl ?? string.Empty;
            return url.Contains("/login", StringComparison.OrdinalIgnoreCase);
        }

        private async Task BridgeAndFinishAsync()
        {
            if (_loginDetected) return;
            _loginDetected = true;
            try { _loginPollTimer?.Stop(); } catch { }
            try { _services.OtpReceiverService.Stop(); } catch { }

            // Köprüleme (~10-15 sn) ARKA PLANDA yapılır: WebView2 ana sayfasını gizle, ekranı kaplayan
            // "Oturum aktarılıyor..." + progress bar panelini göster. Böylece ana sayfa boş yere açık kalmaz.
            ShowLoadingOverlay("Oturum aktarılıyor...");
            try
            {
                await WaitForLoginBrowserSessionAsync();

                // Yolcu360 oturum token'ı genelde localStorage'da ve ORIGIN'e özeldir. WebView2 giriş
                // sonrası apex (https://yolcu360.com) origin'inde olur; localStorage'ı oradan oku.
                var lsJson = await _services.LoginBrowserService.EvaluateStringAsync(JsHelper.BuildGetLocalStorageScript());
                bool hasLs = !string.IsNullOrWhiteSpace(lsJson) && lsJson != "{}";
                LogHelper.Info($"Köprü: localStorage boyutu {(lsJson ?? "").Length} karakter.");

                // ÇEREZLER: hem apex hem www için WebView2'den topla (tekilleştir) ve CefSharp'a aktar.
                // Çerezler kendi Domain'iyle aktarıldığı için her iki URL'e de set etmek host-only/apex
                // çerezlerinin reddedilmesini önler.
                var cookies = new List<BrowserCookieDto>();
                foreach (var u in SiteOrigins)
                    cookies.AddRange(await _services.LoginBrowserService.ExportCookiesAsync(u + "/"));
                cookies = cookies
                    .GroupBy(c => $"{c.Name}|{c.Domain}|{c.Path}", StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
                foreach (var u in SiteOrigins)
                    await _services.BrowserService.ImportCookiesAsync(u + "/", cookies);

                // localStorage'ı CefSharp'ta HEM apex HEM www origin'ine yaz. Böylece SPA hangi origin'i
                // kullanırsa kullansın token'ı bulur (www↔apex localStorage'ları ayrıdır).
                foreach (var origin in SiteOrigins)
                {
                    await _services.BrowserService.LoadUrlAsync(origin + "/");
                    await Task.Delay(2500); // origin yüklensin (localStorage o origin'e yazılacak)
                    if (hasLs)
                        await _services.BrowserService.EvaluateBoolAsync(JsHelper.BuildSetLocalStorageScript(lsJson));
                }
                await _services.BrowserService.LoadUrlAsync(Yolcu360Constants.HomeUrl);
                await Task.Delay(2500); // SPA token'ı okuyup girişli render etsin
                LogHelper.Info($"{cookies.Count} çerez + localStorage (apex+www) CefSharp'a köprülendi.");
                // Otomasyon tarayıcısı ana sayfada (girişli) kalır; arama buradan devam eder. Ayrı bir
                // /login DOĞRULAMA adımı YAPILMIYOR — yalnızca bilgi amaçlıydı ve giriş penceresini ~8 sn
                // fazladan açık tutuyordu. Çerez + localStorage köprülendi; bu yeterli.
            }
            catch (Exception ex)
            {
                LogHelper.Warning("Oturum köprüleme tamamlanamadı: " + ex.Message);
            }

            // WebView2'de Yolcu360 girişi başarılı oldu ve oturum CefSharp'a köprülendi. Kullanıcı GİRİŞ
            // YAPMIŞ sayılır; giriş (WebView2) penceresi HEMEN kapatılır ve ana uygulamaya geçilir.
            LogHelper.Info("Giriş tamamlandı; oturum köprülendi, giriş penceresi kapatılıyor.");
            SetLoadingStatus("Giriş başarılı. Uygulama açılıyor...");
            AuthSucceeded = true;
            if (!IsDisposed) BeginInvoke(new Action(() => { DialogResult = DialogResult.OK; Close(); }));
        }

        private void StartLoginPoller()
        {
            if (_loginPollTimer != null) return;
            _loginPollTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _loginPollTimer.Tick += async (s, e) => await DetectCompletedLoginAsync();
            _loginPollTimer.Start();
        }

        private async Task DetectCompletedLoginAsync()
        {
            if (_loginDetected || _otpSubmitStarted || IsDisposed) return;
            try
            {
                if (!IsOnLoginPage() || await _services.LoginAutomationService.IsLoggedInAsync())
                    await BridgeAndFinishAsync();
            }
            catch (Exception ex) { LogHelper.Warning("Giriş durum kontrolü yapılamadı: " + ex.Message); }
        }

        private async Task EnsureListenerStartedAsync()
        {
            if (_services.OtpReceiverService.IsListening) return;
            _otpSubmitStarted = false;
            await _services.OtpReceiverService.StartAsync();
        }

        /// <summary>
        /// SMS beklerken gösterilir: uygulamanın hangi IP:port'ta dinlediğini söyler. Kullanıcı,
        /// telefondaki MacroDroid'in bu adrese (ve aynı Wi-Fi'a) gönderdiğini doğrulayabilir.
        /// </summary>
        private string BuildListeningHint()
        {
            try
            {
                var otp = _services.OtpReceiverService;
                if (!otp.IsListening) return string.Empty;
                var ip = otp.GetLocalIPv4Addresses().FirstOrDefault() ?? "127.0.0.1";
                return $"Kod dinleniyor: {ip}:{otp.Port}  (telefon aynı Wi-Fi'da olmalı)";
            }
            catch { return string.Empty; }
        }

        // ----------------------------------------------------------------- Manual fallback

        /// <summary>
        /// Otomatik "Devam Et" engellendiğinde: yükleniyor + giriş panellerini gizler, gizli WebView2'yi
        /// öne çıkarır ve kullanıcıdan elle "Devam Et"e basmasını ister. OTP dinleyici/poller çalışmaya
        /// devam ettiği için kod geldiğinde yine otomatik girilir ve giriş tamamlanır.
        /// </summary>
        private void EnterManualMode(string message)
            => ShowBrowserStage(message, warn: true);

        // ----------------------------------------------------------------- Status / busy helpers

        /// <summary>
        /// WebView2'yi ÖNE çıkarır (görünür yapar) ve üstte bir durum şeridi gösterir. Yolcu360 SMS
        /// akışı boyunca kullanılır: kullanıcı numaranın yazıldığını, "Devam Et"in tıklandığını ve
        /// kodun girildiğini canlı görür. <paramref name="warn"/>=true ise şerit uyarı rengindedir
        /// (otomatik adım engellendi, elle devam et).
        /// </summary>
        private void ShowBrowserStage(string status, bool warn = false) => UiHelper.RunOnUi(this, () =>
        {
            _loadingPanel.Visible = false;
            _authPanel.Visible = false;
            _manualBar.BackColor = warn ? ThemeColors.Warning : ThemeColors.Primary;
            _lblManual.Text = status;
            _manualBar.Visible = true;
            _browserLayer.BringToFront();
            _manualBar.BringToFront();
            try { _services.LoginBrowserService.SetBrowserVisible(true); } catch { }
        });

        /// <summary>Giriş/kayıt kartını geri gösterir (tarayıcı sahnesini ve yükleniyor panelini gizler).</summary>
        private void ShowAuthCard() => UiHelper.RunOnUi(this, () =>
        {
            _loadingPanel.Visible = false;
            _manualBar.Visible = false;
            _authPanel.Visible = true;
            _authPanel.BringToFront();
            CenterCard();
        });

        /// <summary>
        /// Tarayıcıyı GİZLEYİP (ana sayfa görünmesin) ekranı kaplayan yükleniyor panelini gösterir:
        /// "Oturum aktarılıyor..." metni + marquee progress bar. Çerez/localStorage köprülemesi bu
        /// panel altında (arka planda) yapılır.
        /// </summary>
        private void ShowLoadingOverlay(string text) => UiHelper.RunOnUi(this, () =>
        {
            _manualBar.Visible = false;
            _lblLoadingStatus.Text = text;
            _loadingPanel.Visible = true;
            _loadingPanel.BringToFront();
        });

        // Durum metni: hem yükleniyor panelini hem tarayıcı şeridini günceller (hangisi görünürse).
        private void SetLoadingStatus(string text) => UiHelper.RunOnUi(this, () =>
        {
            _lblLoadingStatus.Text = text;
            _lblManual.Text = text;
        });

        private void SetAuthStatus(string text, Color color) => UiHelper.RunOnUi(this, () =>
        {
            _lblAuthStatus.ForeColor = color;
            _lblAuthStatus.Text = text;
        });

        private void ClearStatus() => SetAuthStatus(string.Empty, ThemeColors.Primary);

        private void SetAuthBusy(bool busy)
        {
            UiHelper.SetControlsEnabled(!busy, _txtEmail, _txtPassword, _btnLogin,
                _txtRegEmail, _txtRegPassword, _txtRegPhone, _btnRegister);
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        // ----------------------------------------------------------------- Closing

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            // WebView2 kontrolünü formdan AYIR (form dispose ederken yok etmesin; tekrar girişte
            // aynı kontrol yeniden kullanılır).
            try { if (_browserCtrl != null) _browserLayer.Controls.Remove(_browserCtrl); } catch { }
            try { _loginPollTimer?.Stop(); _loginPollTimer?.Dispose(); } catch { }
            try { if (_otpHandler != null) _services.OtpReceiverService.OtpReceived -= _otpHandler; } catch { }
            try { _cts?.Cancel(); } catch { }
            try { _services.OtpReceiverService.Stop(); } catch { }
        }
    }
}
