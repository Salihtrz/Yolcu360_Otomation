using Guna.UI2.WinForms;
using Yolcu360.BusinessLayer;
using Yolcu360.BusinessLayer.Concrete;
using Yolcu360.Common.Logging;
using Yolcu360.DataAccessLayer.Config;
using Yolcu360.PresentationLayer.Helpers;
using Yolcu360.PresentationLayer.UI;

namespace Yolcu360.PresentationLayer
{
    public class OtpLoginForm : Form
    {
        private readonly AppServices _services;
        private readonly Action<bool> _setBrowserVisible;
        private readonly Action<bool> _onLoginSuccess;
        private readonly IProgress<string> _progress;

        private CancellationTokenSource _cts;
        private Action<string> _otpHandler;
        private bool _otpSubmitStarted;
        private bool _loginDetected;
        private System.Windows.Forms.Timer _loginPollTimer;

        private Guna2TextBox _txtPhone;
        private Guna2TextBox _txtManualOtp;
        private Guna2Button _btnSavePhone;
        private Guna2Button _btnAutoLogin;
        private Guna2Button _btnManualPhone;
        private Guna2Button _btnSubmitManualOtp;
        private Guna2Button _btnStartListener;
        private Guna2Button _btnStopListener;
        private Guna2Button _btnClearSession;
        private Label _lblLoginStatus;
        private Label _lblOtpStatus;
        private Label _lblFlowStatus;
        private Label _lblListenerStatus;
        private TextBox _txtMacroInfo;

        public OtpLoginForm(AppServices services, Action<bool> setBrowserVisible, Action<bool> onLoginSuccess)
        {
            _services = services;
            _setBrowserVisible = setBrowserVisible;
            _onLoginSuccess = onLoginSuccess;
            _progress = new Progress<string>(SetFlowStatus);

            BuildUi();
            Load += OtpLoginForm_Load;
            FormClosing += OtpLoginForm_Closing;
        }

        private void BuildUi()
        {
            Text = "Telefon + SMS ile Giris";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(680, 700);
            BackColor = ThemeColors.Background;
            Font = new Font("Segoe UI", 9.5f);
            AutoScroll = true;

            const int x = 20;

            Controls.Add(new Label
            {
                Text = "Telefon + SMS Dogrulama ile Giris",
                AutoSize = true,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = ThemeColors.TextDark,
                Location = new Point(x, 16),
                BackColor = Color.Transparent
            });

            Controls.Add(new Label
            {
                Text = "Yalnizca kendi hesabi ve kendi numaraniz icin. SMS/captcha bypass yapilmaz.",
                AutoSize = false,
                Size = new Size(640, 34),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ThemeColors.TextMuted,
                Location = new Point(x, 46),
                BackColor = Color.Transparent
            });

            Controls.Add(Caption("Telefon Numarasi (kayitli)", x, 88));
            _txtPhone = UiStyleHelper.TextBox(260);
            _txtPhone.Location = new Point(x, 110);
            _txtPhone.PlaceholderText = "+905xxxxxxxxx";
            Controls.Add(_txtPhone);

            _btnSavePhone = UiStyleHelper.Button("Numarayi Kaydet", ButtonVariant.Secondary, 160, 34);
            _btnSavePhone.Location = new Point(x + 272, 110);
            _btnSavePhone.Click += OnSavePhoneClick;
            Controls.Add(_btnSavePhone);

            _btnAutoLogin = UiStyleHelper.Button("Telefonu Otomatik Yaz", ButtonVariant.Secondary, 200, 42);
            _btnAutoLogin.Location = new Point(x, 156);
            _btnAutoLogin.Click += OnAutoLoginClick;
            Controls.Add(_btnAutoLogin);

            _btnManualPhone = UiStyleHelper.Button("Telefonu Kendim Gir + SMS Bekle", ButtonVariant.Accent, 300, 42);
            _btnManualPhone.Location = new Point(x + 212, 156);
            _btnManualPhone.Click += OnManualPhoneClick;
            Controls.Add(_btnManualPhone);

            _lblLoginStatus = StatusLabel("Giris: bekleniyor", ThemeColors.TextMuted, x, 206);
            _lblOtpStatus = StatusLabel("SMS kodu: -", ThemeColors.TextMuted, x, 232);
            _lblFlowStatus = StatusLabel("Hazir.", ThemeColors.TextMuted, x, 258);
            _lblFlowStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            Controls.Add(_lblLoginStatus);
            Controls.Add(_lblOtpStatus);
            Controls.Add(_lblFlowStatus);

            Controls.Add(Divider(x, 292));
            Controls.Add(Caption("Kodu Manuel Gir (MacroDroid calismazsa)", x, 304));
            _txtManualOtp = UiStyleHelper.TextBox(130);
            _txtManualOtp.Location = new Point(x, 328);
            _txtManualOtp.MaxLength = 6;
            _txtManualOtp.PlaceholderText = "6 haneli kod";
            Controls.Add(_txtManualOtp);

            _btnSubmitManualOtp = UiStyleHelper.Button("Kodu Gonder", ButtonVariant.Primary, 160, 34);
            _btnSubmitManualOtp.Location = new Point(x + 142, 328);
            _btnSubmitManualOtp.Click += OnSubmitManualOtpClick;
            Controls.Add(_btnSubmitManualOtp);

            Controls.Add(Divider(x, 372));
            _btnStartListener = UiStyleHelper.Button("Dinleyici Baslat", ButtonVariant.Success, 170, 34);
            _btnStartListener.Location = new Point(x, 386);
            _btnStartListener.Click += OnStartListenerClick;
            Controls.Add(_btnStartListener);

            _btnStopListener = UiStyleHelper.Button("Dinleyiciyi Durdur", ButtonVariant.Danger, 170, 34);
            _btnStopListener.Location = new Point(x + 182, 386);
            _btnStopListener.Click += OnStopListenerClick;
            Controls.Add(_btnStopListener);

            _btnClearSession = UiStyleHelper.Button("Oturumu Temizle", ButtonVariant.Secondary, 170, 34);
            _btnClearSession.Location = new Point(x + 364, 386);
            _btnClearSession.Click += OnClearSessionClick;
            Controls.Add(_btnClearSession);

            _lblListenerStatus = StatusLabel("Dinleyici: kapali", ThemeColors.TextMuted, x, 428);
            Controls.Add(_lblListenerStatus);

            Controls.Add(Caption("MacroDroid Ayarlari (telefon -> bilgisayar)", x, 458));
            _txtMacroInfo = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(x, 482),
                Size = new Size(640, 196),
                Font = new Font("Consolas", 9f),
                BackColor = Color.White,
                ForeColor = ThemeColors.TextDark,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_txtMacroInfo);
        }

        private static Label Caption(string text, int x, int y) => new()
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = ThemeColors.TextDark,
            Location = new Point(x, y),
            BackColor = Color.Transparent
        };

        private static Label StatusLabel(string text, Color color, int x, int y) => new()
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = color,
            Location = new Point(x, y),
            BackColor = Color.Transparent
        };

        private static Control Divider(int x, int y) => new Panel
        {
            Location = new Point(x, y),
            Size = new Size(640, 1),
            BackColor = ThemeColors.Border
        };

        private async void OtpLoginForm_Load(object sender, EventArgs e)
        {
            try
            {
                var phone = await _services.UserService.GetSavedPhoneNumberAsync();
                if (!string.IsNullOrWhiteSpace(phone))
                    _txtPhone.Text = phone;
            }
            catch (Exception ex)
            {
                LogHelper.Warning("Kayitli telefon okunamadi: " + ex.Message);
            }

            _otpHandler = code => UiHelper.RunOnUi(this, () =>
            {
                _txtManualOtp.Text = code;
                SetOtpStatus("SMS kodu alindi.", ThemeColors.Success);
                _ = SubmitReceivedOtpAsync(code);
            });
            _services.OtpReceiverService.OtpReceived += _otpHandler;

            RefreshMacroInfo();
            UpdateListenerStatus();
            StartLoginPoller();

            if (OtpReceiverOptions.IsDefaultToken)
                SetFlowStatus("Uyari: OTP token varsayilan durumda. appsettings.json icinden degistirin.");
        }

        private void OtpLoginForm_Closing(object sender, FormClosingEventArgs e)
        {
            try { _loginPollTimer?.Stop(); _loginPollTimer?.Dispose(); } catch { }
            try { if (_otpHandler != null) _services.OtpReceiverService.OtpReceived -= _otpHandler; } catch { }
            try { _cts?.Cancel(); } catch { }
            try { _services.OtpReceiverService.Stop(); } catch { }
        }

        private async void OnSavePhoneClick(object sender, EventArgs e)
        {
            var phone = (_txtPhone.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(phone))
            {
                UiHelper.Warn("Once telefon numarasini girin.");
                return;
            }

            try
            {
                await _services.UserService.SavePhoneNumberAsync(phone);
                SetFlowStatus("Telefon numarasi kaydedildi.");
            }
            catch (Exception ex)
            {
                LogHelper.Error("Telefon kaydedilemedi.", ex);
                UiHelper.Warn("Telefon kaydedilemedi. Veritabani baglantisini kontrol edin.\n\n" + ex.Message);
            }
        }

        private async void OnAutoLoginClick(object sender, EventArgs e)
        {
            var phone = (_txtPhone.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(phone))
            {
                UiHelper.Warn("Lutfen telefon numarasini girin.");
                return;
            }

            _cts = new CancellationTokenSource();
            SetBusy(true);
            try
            {
                try { await _services.UserService.SavePhoneNumberAsync(phone, _cts.Token); } catch { }
                _setBrowserVisible?.Invoke(true);
                await EnsureListenerStartedAsync();
                UpdateListenerStatus();

                SetLoginStatus("Giris: telefon yaziliyor...", ThemeColors.Warning);
                await _services.LoginAutomationService.StartPhoneLoginAsync(phone, _progress, _cts.Token);
                SetOtpStatus("SMS kodu bekleniyor.", ThemeColors.Warning);
            }
            catch (AutomationException ax)
            {
                SetLoginStatus("Giris tamamlanamadi.", ThemeColors.Danger);
                UiHelper.Warn(ax.Message);
            }
            catch (OperationCanceledException)
            {
                SetLoginStatus("Giris iptal edildi.", ThemeColors.TextMuted);
            }
            catch (Exception ex)
            {
                LogHelper.Error("Otomatik giris hatasi.", ex);
                UiHelper.Error("Giris sirasinda hata olustu.\n\n" + ex.Message);
            }
            finally
            {
                UpdateListenerStatus();
                SetBusy(false);
            }
        }

        private async void OnManualPhoneClick(object sender, EventArgs e)
        {
            _cts = new CancellationTokenSource();
            SetBusy(true);
            try
            {
                _setBrowserVisible?.Invoke(true);
                await EnsureListenerStartedAsync();
                UpdateListenerStatus();

                SetLoginStatus("Giris: telefonu tarayicida siz girin.", ThemeColors.Warning);
                var ready = await _services.LoginAutomationService.OpenLoginPageAsync(_progress, _cts.Token);
                if (!ready)
                {
                    var already = await _services.LoginAutomationService.IsLoggedInAsync(_cts.Token);
                    if (already)
                    {
                        FinishLogin(true);
                        return;
                    }

                    SetLoginStatus("Giris formu acilamadi.", ThemeColors.Danger);
                    UiHelper.Warn("Telefon formu acilamadi. Tarayicida manuel devam edebilirsiniz.");
                    return;
                }

                SetOtpStatus("SMS kodu bekleniyor.", ThemeColors.Warning);
            }
            catch (AutomationException ax)
            {
                SetLoginStatus("Giris tamamlanamadi.", ThemeColors.Danger);
                UiHelper.Warn(ax.Message);
            }
            catch (OperationCanceledException)
            {
                SetLoginStatus("Giris iptal edildi.", ThemeColors.TextMuted);
            }
            catch (Exception ex)
            {
                LogHelper.Error("Manuel telefon + OTP hatasi.", ex);
                UiHelper.Error("Giris sirasinda hata olustu.\n\n" + ex.Message);
            }
            finally
            {
                UpdateListenerStatus();
                SetBusy(false);
            }
        }

        private async void OnSubmitManualOtpClick(object sender, EventArgs e)
        {
            var code = (_txtManualOtp.Text ?? string.Empty).Trim();
            if (!_services.OtpReceiverService.ValidateOtpCode(code))
            {
                UiHelper.Warn("Lutfen 6 haneli sayisal kodu girin.");
                return;
            }

            _cts ??= new CancellationTokenSource();
            await SubmitReceivedOtpAsync(code);
        }

        private async void OnStartListenerClick(object sender, EventArgs e)
        {
            try
            {
                await EnsureListenerStartedAsync();
                UpdateListenerStatus();
                SetFlowStatus("OTP dinleyici baslatildi.");
            }
            catch (Exception ex)
            {
                LogHelper.Error("OTP dinleyici baslatilamadi.", ex);
                UiHelper.Error("OTP dinleyici baslatilamadi.\n\n" + ex.Message);
            }
        }

        private void OnStopListenerClick(object sender, EventArgs e)
        {
            _services.OtpReceiverService.Stop();
            UpdateListenerStatus();
            SetFlowStatus("OTP dinleyici durduruldu.");
        }

        private async void OnClearSessionClick(object sender, EventArgs e)
        {
            SetBusy(true);
            try
            {
                try { _cts?.Cancel(); } catch { }
                _cts = new CancellationTokenSource();
                _otpSubmitStarted = false;

                _services.OtpReceiverService.Stop();
                UpdateListenerStatus();

                _setBrowserVisible?.Invoke(true);
                await _services.BrowserService.ClearSessionAsync(_cts.Token);
                await _services.BrowserService.LoadUrlAsync(Yolcu360.Common.Constants.Yolcu360Constants.LoginUrl, _cts.Token);
                await _services.BrowserService.WaitForPageLoadAsync(_cts.Token);

                SetLoginStatus("Oturum temizlendi.", ThemeColors.TextMuted);
                SetOtpStatus("SMS kodu: -", ThemeColors.TextMuted);
                SetFlowStatus("Oturum temizlendi. Login ekrani yeniden acildi.");
            }
            catch (Exception ex)
            {
                LogHelper.Error("Oturum temizleme hatasi.", ex);
                UiHelper.Error("Oturum temizlenirken hata olustu.\n\n" + ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task EnsureListenerStartedAsync()
        {
            if (_services.OtpReceiverService.IsListening)
                return;

            _otpSubmitStarted = false;
            await _services.OtpReceiverService.StartAsync();
        }

        private async Task SubmitReceivedOtpAsync(string code)
        {
            if (!_services.OtpReceiverService.ValidateOtpCode(code))
                return;
            if (_otpSubmitStarted)
                return;

            _otpSubmitStarted = true;
            SetBusy(true);
            try
            {
                _setBrowserVisible?.Invoke(true);
                SetOtpStatus("SMS kodu otomatik yaziliyor...", ThemeColors.Warning);
                var token = _cts?.Token ?? CancellationToken.None;
                var ok = await _services.LoginAutomationService.SubmitOtpAsync(code, _progress, token);
                FinishLogin(ok);
            }
            catch (AutomationException ax)
            {
                _otpSubmitStarted = false;
                SetLoginStatus("Giris tamamlanamadi.", ThemeColors.Danger);
                UiHelper.Warn(ax.Message);
            }
            catch (OperationCanceledException)
            {
                _otpSubmitStarted = false;
                SetLoginStatus("Giris iptal edildi.", ThemeColors.TextMuted);
            }
            catch (Exception ex)
            {
                _otpSubmitStarted = false;
                LogHelper.Error("OTP otomatik gonderme hatasi.", ex);
                UiHelper.Error("Kod otomatik gonderilirken hata olustu.\n\n" + ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void FinishLogin(bool ok)
        {
            if (ok)
            {
                _loginDetected = true;
                try { _loginPollTimer?.Stop(); } catch { }
                try { _services.OtpReceiverService.Stop(); } catch { }
                SetLoginStatus("Giris yapildi.", ThemeColors.Success);
                SetOtpStatus("SMS kodu dogrulandi.", ThemeColors.Success);
                _onLoginSuccess?.Invoke(true);
                _setBrowserVisible?.Invoke(false);
                BeginInvoke(new Action(Close));
            }
            else
            {
                SetLoginStatus("Giris dogrulanamadi.", ThemeColors.Danger);
                _setBrowserVisible?.Invoke(true);
            }
        }

        private void RefreshMacroInfo()
        {
            var ips = _services.OtpReceiverService.GetLocalIPv4Addresses();
            var ip = ips.Count > 0 ? ips[0] : "127.0.0.1";
            var port = OtpReceiverOptions.Port;
            var token = OtpReceiverOptions.Token;

            _txtMacroInfo.Text =
                "MacroDroid kurulumu (telefon):\r\n" +
                "  1) Trigger : SMS Received\r\n" +
                "  2) SMS icindeki 6 haneli kodu regex ile al:  \\b\\d{6}\\b\r\n" +
                "  3) Action  : HTTP Request (POST)\r\n" +
                $"     URL    : http://{ip}:{port}/otp\r\n" +
                "     Header : Content-Type: application/json\r\n" +
                "     Body   :\r\n" +
                "       {\r\n" +
                "         \"smsMetni\": \"{sms_message}\",\r\n" +
                "         \"source\": \"MacroDroid\",\r\n" +
                $"         \"token\": \"{token}\"\r\n" +
                "       }\r\n\r\n" +
                $"  GET test : http://{ip}:{port}/otp?code=123456&token={token}\r\n\r\n" +
                $"  Bilgisayar IP adresleri: {string.Join(", ", ips)}\r\n" +
                $"  Port: {port}\r\n" +
                "  Telefon ve bilgisayar ayni Wi-Fi aginda olmali.";
        }

        private void UpdateListenerStatus()
        {
            var svc = _services.OtpReceiverService;
            if (svc.IsListening)
            {
                var mode = svc.UsingTcpFallback ? "TCP" : "HTTP";
                SetListenerStatus($"Dinleyici: acik (port {svc.Port}, {mode})", ThemeColors.Success);
            }
            else
            {
                SetListenerStatus("Dinleyici: kapali", ThemeColors.TextMuted);
            }
        }

        private void SetLoginStatus(string text, Color color) =>
            UiHelper.RunOnUi(this, () => { _lblLoginStatus.Text = text; _lblLoginStatus.ForeColor = color; });

        private void SetOtpStatus(string text, Color color) =>
            UiHelper.RunOnUi(this, () => { _lblOtpStatus.Text = text; _lblOtpStatus.ForeColor = color; });

        private void SetListenerStatus(string text, Color color) =>
            UiHelper.RunOnUi(this, () => { _lblListenerStatus.Text = text; _lblListenerStatus.ForeColor = color; });

        private void SetFlowStatus(string text) =>
            UiHelper.RunOnUi(this, () => _lblFlowStatus.Text = text);

        private void SetBusy(bool busy)
        {
            UiHelper.SetControlsEnabled(!busy,
                _btnAutoLogin, _btnManualPhone, _btnSubmitManualOtp, _btnSavePhone, _btnStartListener, _btnStopListener, _btnClearSession);
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void StartLoginPoller()
        {
            _loginPollTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _loginPollTimer.Tick += async (s, e) => await DetectCompletedLoginAsync();
            _loginPollTimer.Start();
        }

        private async Task DetectCompletedLoginAsync()
        {
            if (_loginDetected || IsDisposed)
                return;

            try
            {
                var loggedIn = await _services.LoginAutomationService.IsLoggedInAsync();
                if (!loggedIn)
                    return;

                _loginDetected = true;
                try { _loginPollTimer?.Stop(); } catch { }
                try { _services.OtpReceiverService.Stop(); } catch { }
                SetLoginStatus("Giris yapildi.", ThemeColors.Success);
                SetOtpStatus("SMS kodu dogrulandi.", ThemeColors.Success);
                SetFlowStatus("Giris tarayicida tamamlandi.");
                _onLoginSuccess?.Invoke(true);
                _setBrowserVisible?.Invoke(false);
                Close();
            }
            catch (Exception ex)
            {
                LogHelper.Warning("Login durum kontrolu yapilamadi: " + ex.Message);
            }
        }
    }
}
