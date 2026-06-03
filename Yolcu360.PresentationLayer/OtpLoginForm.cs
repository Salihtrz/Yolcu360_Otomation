using Guna.UI2.WinForms;
using Yolcu360.BusinessLayer;
using Yolcu360.BusinessLayer.Concrete;
using Yolcu360.Common.Logging;
using Yolcu360.PresentationLayer.Helpers;
using Yolcu360.PresentationLayer.UI;

namespace Yolcu360.PresentationLayer
{
    /// <summary>
    /// SADE giriş ekranı. Dropdown'dan kayıtlı bir numara seçilir (veya yeni numara yazılır),
    /// "Onayla" denince tarayıcıda telefon alanına numara YAZILIR — "Devam Et"e kullanıcı kendisi
    /// basar. SMS gelince MacroDroid kodu otomatik gönderir, kod siteye otomatik girilir ve giriş
    /// tamamlanır. Arka plandaki OTP dinleyici/otomatik kod akışı korunur; yalnızca UI sadeleştirildi.
    /// </summary>
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

        private ComboBox _cmbPhone;
        private Guna2Button _btnConfirm;
        private Label _lblStatus;

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
            Text = "Giriş Yap";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(440, 230);
            BackColor = ThemeColors.Background;
            Font = new Font("Segoe UI", 9.5f);

            const int x = 24;

            Controls.Add(new Label
            {
                Text = "Giriş Yap",
                AutoSize = true,
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = ThemeColors.TextDark,
                Location = new Point(x, 20)
            });

            Controls.Add(new Label
            {
                Text = "Numara",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = ThemeColors.TextDark,
                Location = new Point(x, 66)
            });

            _cmbPhone = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown, // seçilebilir + yeni numara yazılabilir
                Location = new Point(x, 90),
                Width = 392,
                Font = new Font("Segoe UI", 10.5f)
            };
            Controls.Add(_cmbPhone);

            _btnConfirm = UiStyleHelper.Button("Onayla", ButtonVariant.Primary, 392, 42);
            _btnConfirm.Location = new Point(x, 132);
            _btnConfirm.Click += OnConfirmClick;
            Controls.Add(_btnConfirm);

            _lblStatus = new Label
            {
                Text = "Numara seçip Onayla'ya basın.",
                AutoSize = false,
                Size = new Size(392, 40),
                Location = new Point(x, 184),
                ForeColor = ThemeColors.TextMuted,
                Font = new Font("Segoe UI", 8.75f)
            };
            Controls.Add(_lblStatus);
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
            catch (Exception ex)
            {
                LogHelper.Warning("Kayıtlı numaralar okunamadı: " + ex.Message);
            }

            // SMS kodu geldiğinde otomatik gönder (MacroDroid → dinleyici → bu olay).
            _otpHandler = code => UiHelper.RunOnUi(this, () => _ = SubmitReceivedOtpAsync(code));
            _services.OtpReceiverService.OtpReceived += _otpHandler;

            StartLoginPoller();
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
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
                // Yeni numarayı kaydet (dropdown'da kalıcı olsun).
                try { await _services.UserService.AddPhoneNumberAsync(phone, _cts.Token); } catch { }

                _setBrowserVisible?.Invoke(true);
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
            catch (OperationCanceledException)
            {
                SetStatus("Giriş iptal edildi.");
            }
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
            if (_otpSubmitStarted) return;

            _otpSubmitStarted = true;
            SetBusy(true);
            try
            {
                _setBrowserVisible?.Invoke(true);
                SetStatus("SMS kodu otomatik giriliyor...");
                var token = _cts?.Token ?? CancellationToken.None;
                var ok = await _services.LoginAutomationService.SubmitOtpAsync(code, _progress, token);
                FinishLogin(ok);
            }
            catch (Exception ex)
            {
                _otpSubmitStarted = false;
                LogHelper.Error("OTP otomatik gönderme hatası.", ex);
                UiHelper.Warn("Kod otomatik gönderilirken bir sorun oluştu. Tarayıcıdan elle deneyebilirsiniz.");
            }
            finally { SetBusy(false); }
        }

        private void FinishLogin(bool ok)
        {
            if (ok)
            {
                _loginDetected = true;
                try { _loginPollTimer?.Stop(); } catch { }
                try { _services.OtpReceiverService.Stop(); } catch { }
                SetStatus("Giriş yapıldı.");
                _onLoginSuccess?.Invoke(true);
                _setBrowserVisible?.Invoke(false);
                BeginInvoke(new Action(Close));
            }
            else
            {
                SetStatus("Giriş doğrulanamadı. Tarayıcıdan kontrol edin.");
                _setBrowserVisible?.Invoke(true);
            }
        }

        private void StartLoginPoller()
        {
            _loginPollTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _loginPollTimer.Tick += async (s, e) => await DetectCompletedLoginAsync();
            _loginPollTimer.Start();
        }

        private async Task DetectCompletedLoginAsync()
        {
            if (_loginDetected || IsDisposed) return;
            try
            {
                if (!await _services.LoginAutomationService.IsLoggedInAsync()) return;

                _loginDetected = true;
                try { _loginPollTimer?.Stop(); } catch { }
                try { _services.OtpReceiverService.Stop(); } catch { }
                SetStatus("Giriş yapıldı.");
                _onLoginSuccess?.Invoke(true);
                _setBrowserVisible?.Invoke(false);
                Close();
            }
            catch (Exception ex)
            {
                LogHelper.Warning("Giriş durum kontrolü yapılamadı: " + ex.Message);
            }
        }

        private void SetBusy(bool busy)
        {
            UiHelper.SetControlsEnabled(!busy, _btnConfirm, _cmbPhone);
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void SetStatus(string text) => UiHelper.RunOnUi(this, () => _lblStatus.Text = text);
    }
}
