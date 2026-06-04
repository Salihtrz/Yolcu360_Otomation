using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.BusinessLayer.Helpers;
using Yolcu360.Common.Constants;
using Yolcu360.Common.Helpers;
using Yolcu360.Common.Logging;

namespace Yolcu360.BusinessLayer.Concrete
{
    public class LoginAutomationManager : ILoginAutomationService
    {
        private readonly ICefSharpBrowserService _browser;

        public LoginAutomationManager(ICefSharpBrowserService browser)
        {
            _browser = browser;
        }

        public async Task StartPhoneLoginAsync(string phoneNumber, IProgress<string> progress, CancellationToken ct = default)
        {
            void Report(string s) { progress?.Report(s); LogHelper.Info("Login: " + s); }

            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new AutomationException("Telefon numarasi bos. Lutfen kayitli bir telefon numarasi girin.");

            Report("Yolcu360 giris sayfasi aciliyor...");
            await _browser.LoadUrlAsync(Yolcu360Constants.LoginUrl, ct);
            await _browser.WaitForPageLoadAsync(ct);
            await Task.Delay(2500, ct);

            Report("Telefon alani bekleniyor...");
            var hasPhone = await _browser.WaitForElementAsync(Yolcu360LoginSelectors.PhoneInputSelectors, 8, ct);
            if (!hasPhone)
            {
                Report("Giris ekrani aciliyor...");
                var opened = await _browser.ClickElementAsync(Yolcu360LoginSelectors.LoginButtonSelectors, ct);
                if (!opened)
                    await _browser.EvaluateBoolAsync(JsHelper.BuildClickByTextScript(Yolcu360LoginSelectors.LoginButtonTexts), ct);
                await Task.Delay(1200, ct);
                hasPhone = await _browser.WaitForElementAsync(Yolcu360LoginSelectors.PhoneInputSelectors, 8, ct);
            }

            if (!hasPhone)
            {
                _browser.SetBrowserVisible(true);
                LogHelper.Warning("Telefon input'u bulunamadi. Denenen selector'lar: " + string.Join(" | ", Yolcu360LoginSelectors.PhoneInputSelectors));
                throw new AutomationException("Giris icin telefon alani bulunamadi. Tarayici gorunur yapildi; girisi manuel tamamlayabilirsiniz.");
            }

            Report("Telefon numarasi giriliyor...");
            var normalized = NormalizePhone(phoneNumber);
            await _browser.EvaluateBoolAsync(JsHelper.BuildTypeRealScript(Yolcu360LoginSelectors.PhoneInputSelectors, normalized), ct);
            LogHelper.Info($"Telefon numarasi siteye yazildi: {MaskPhone(normalized)}");

            // "Devam Et" OTOMATIK tiklanir (kullanici tercihi: tarayici gizli + tam otomatik).
            // NOT: Yolcu360 reCAPTCHA otomatik tiklamayi bot sanip engelleyebilir; bu durumda asagidaki
            // OTP-ekrani bekleme adimi basarisiz olur ve tarayici GORUNUR yapilip kullaniciya elle
            // tamamlamasi bildirilir (bypass YOK, guvenli fallback).
            await Task.Delay(400, ct); // numara input/change/blur event'leri otursun
            Report("Devam Et'e tiklaniyor...");
            var clicked = await _browser.ClickElementAsync(Yolcu360LoginSelectors.PhoneContinueButtonSelectors, ct);
            if (!clicked)
                clicked = await _browser.EvaluateBoolAsync(JsHelper.BuildClickByTextScript(Yolcu360LoginSelectors.PhoneContinueButtonTexts), ct);
            LogHelper.Info(clicked ? "Devam Et otomatik tiklandi." : "Devam Et butonu bulunamadi/tiklanamadi.");

            // SMS kod ekraninin gelmesini bekle (~25 sn). reCAPTCHA engellerse veya buton tiklanamadiysa
            // gelmez → tarayiciyi gorunur yapip kullaniciya elle devam ettir.
            var otpScreen = await _browser.WaitForElementAsync(Yolcu360LoginSelectors.OtpScreenIndicatorSelectors, 25, ct);
            if (!otpScreen)
            {
                _browser.SetBrowserVisible(true);
                LogHelper.Warning("SMS kod ekrani algilanamadi (reCAPTCHA/otomatik tiklama engellenmis olabilir); tarayici gorunur yapildi.");
                throw new AutomationException(
                    "Yolcu360 otomatik devam adimini engelledi olabilir. Tarayici goruntulendi; " +
                    "lutfen 'Devam Et'e elle basin. SMS kodu yine de otomatik girilecek.");
            }

            Report("SMS kodu bekleniyor...");
        }

        public async Task<bool> OpenLoginPageAsync(IProgress<string> progress, CancellationToken ct = default)
        {
            void Report(string s) { progress?.Report(s); LogHelper.Info("Login: " + s); }

            Report("Giris sayfasi aciliyor (telefonu kendiniz gireceksiniz)...");
            await _browser.LoadUrlAsync(Yolcu360Constants.LoginUrl, ct);
            await _browser.WaitForPageLoadAsync(ct);
            await Task.Delay(2000, ct);

            var hasPhone = await _browser.WaitForElementAsync(Yolcu360LoginSelectors.PhoneInputSelectors, 8, ct);
            if (!hasPhone)
            {
                Report("Giris formu aciliyor...");
                await _browser.ClickElementAsync(Yolcu360LoginSelectors.LoginButtonSelectors, ct);
                await _browser.EvaluateBoolAsync(JsHelper.BuildClickByTextScript(Yolcu360LoginSelectors.LoginButtonTexts), ct);
                await Task.Delay(1500, ct);
                hasPhone = await _browser.WaitForElementAsync(Yolcu360LoginSelectors.PhoneInputSelectors, 8, ct);
            }

            Report(hasPhone
                ? "Tarayicida telefonu yazip Devam Et'e basin; SMS kodu otomatik girilecek."
                : "Telefon formu acilamadi (zaten giris yapilmis olabilir).");

            return hasPhone;
        }

        public async Task<bool> SubmitOtpAsync(string otpCode, IProgress<string> progress, CancellationToken ct = default)
        {
            void Report(string s) { progress?.Report(s); LogHelper.Info("Login: " + s); }

            if (string.IsNullOrWhiteSpace(otpCode))
                return false;

            Report("Dogrulama kodu yaziliyor...");

            var single = await _browser.WaitForElementAsync(Yolcu360LoginSelectors.OtpSingleInputSelectors, 3, ct);
            var written = single
                ? await _browser.EvaluateBoolAsync(JsHelper.BuildTypeRealScript(Yolcu360LoginSelectors.OtpSingleInputSelectors, otpCode), ct)
                : await _browser.EvaluateBoolAsync(JsHelper.BuildFillOtpDigitsScript(Yolcu360LoginSelectors.OtpDigitInputSelectors, otpCode), ct);

            if (!written)
                written = await _browser.EvaluateBoolAsync(JsHelper.BuildFillOtpDigitsScript(Yolcu360LoginSelectors.OtpDigitInputSelectors, otpCode), ct);
            if (!written)
                written = await _browser.EvaluateBoolAsync(JsHelper.BuildTypeRealScript(Yolcu360LoginSelectors.OtpSingleInputSelectors, otpCode), ct);

            if (!written)
            {
                _browser.SetBrowserVisible(true);
                LogHelper.Warning("OTP giris alani bulunamadi.");
                throw new AutomationException("Dogrulama kodu alani bulunamadi. Tarayici gorunur yapildi; kodu elle girebilirsiniz.");
            }

            LogHelper.Info("OTP siteye yazildi (kod log'a yazilmaz).");
            Report("Giris dogrulaniyor...");

            var submit = await _browser.ClickElementAsync(Yolcu360LoginSelectors.OtpSubmitButtonSelectors, ct);
            if (!submit)
                submit = await _browser.EvaluateBoolAsync(JsHelper.BuildClickByTextScript(Yolcu360LoginSelectors.OtpSubmitButtonTexts), ct);

            await _browser.WaitForPageLoadAsync(ct);

            var ok = await WaitForLoginSuccessAsync(TimeSpan.FromSeconds(15), ct);
            if (!ok)
                ok = await LooksLikeOtpAcceptedAsync(ct);

            LogHelper.Info("OTP sonrasi login durumu: " + (ok ? "basarili" : "dogrulanamadi"));
            return ok;
        }

        public Task<bool> IsLoggedInAsync(CancellationToken ct = default)
            => _browser.IsLoggedInAsync(Yolcu360LoginSelectors.LoggedInIndicatorSelectors, ct);

        public Task<bool> WaitForLoginSuccessAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            var seconds = Math.Max(1, (int)timeout.TotalSeconds);
            return BrowserWaitHelper.PollUntilAsync(() => IsLoggedInAsync(ct), seconds, 500, ct);
        }

        private async Task<bool> LooksLikeOtpAcceptedAsync(CancellationToken ct)
        {
            try
            {
                var otpStillVisible = await _browser.EvaluateBoolAsync(JsHelper.BuildElementExistsScript(Yolcu360LoginSelectors.OtpScreenIndicatorSelectors), ct);
                var hasLoginError = await _browser.EvaluateBoolAsync(JsHelper.BuildElementExistsScript(Yolcu360LoginSelectors.LoginErrorSelectors), ct);
                var url = _browser.CurrentUrl ?? string.Empty;

                return !otpStillVisible
                    && !hasLoginError
                    && !url.Contains("/login", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                LogHelper.Warning("OTP kabul yedek kontrolu yapilamadi: " + ex.Message);
                return false;
            }
        }

        private static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("90")) digits = digits[2..];
            if (digits.Length == 11 && digits.StartsWith("0")) digits = digits[1..];
            return digits;
        }

        private static string MaskPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "(bos)";
            if (phone.Length <= 7) return new string('*', phone.Length);
            return phone[..3] + new string('*', phone.Length - 7) + phone[^4..];
        }
    }
}