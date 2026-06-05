namespace Yolcu360.Common.Automation
{
    /// <summary>
    /// Yolcu360 telefon + SMS/OTP giriş akışı için DOM selector'ları.
    ///
    /// İLK SIRADAKİLER GERÇEK ve DOĞRULANMIŞTIR (2026, /login sayfası canlı DOM'undan).
    /// Altındaki alternatifler site değişirse diye sezgisel yedeklerdir; otomasyon sırayla dener.
    /// Hiçbiri bulunamazsa kod KIRILMAZ; tarayıcı görünür yapılıp kullanıcıya manuel devam önerilir.
    ///
    /// Akış: <c>/login</c> sayfası açılır → telefon (#phn-input) yazılır → "Devam Et"
    /// (data-cms-key='text_continue') → SMS modalı açılır → kod (#sms_input) yazılır →
    /// "Doğrula" (data-cms-key='button_apply').
    /// </summary>
    public static class Yolcu360LoginSelectors
    {
        // Giriş penceresini/sayfasını açan buton/bağlantı.
        // NOT: /login sayfasına doğrudan gidildiği için genelde GEREKMEZ (yedek).
        public static readonly string[] LoginButtonSelectors =
        {
            "a[href*='login']",
            "a[href*='giris']",
            "button[class*='login']",
            "[data-testid*='login']",
            ".login-button"
        };

        public static readonly string[] LoginButtonTexts =
        {
            "Giriş Yap", "Üye Ol / Giriş Yap", "Giriş", "Üye Girişi"
        };

        // Telefon numarası input'u — GERÇEK: <input id="phn-input" name="tel" type="tel"
        // autocomplete="tel-national" data-maska="Z(###) ### ## ##"> (ülke kodu +90 ayrı dropdown).
        public static readonly string[] PhoneInputSelectors =
        {
            "#phn-input",
            "input[name='tel']",
            "input[autocomplete='tel-national']",
            "input[type='tel']",
            "input[inputmode='tel']"
        };

        // "Devam Et" — GERÇEK: <button data-cms-key="text_continue">Devam Et</button>
        public static readonly string[] PhoneContinueButtonSelectors =
        {
            "[data-cms-key='text_continue']",
            "button[data-cms-key='text_continue']",
            "button[type='submit']",
            "button[class*='continue']"
        };

        public static readonly string[] PhoneContinueButtonTexts =
        {
            "Devam Et", "Devam", "Kod Gönder", "Gönder"
        };

        // OTP TEK kutu — GERÇEK: <input id="sms_input" inputmode="numeric"
        // autocomplete="one-time-code" data-maska="######">
        public static readonly string[] OtpSingleInputSelectors =
        {
            "#sms_input",
            "input[autocomplete='one-time-code']",
            "input[inputmode='numeric'][data-maska='######']",
            "input[data-maska='######']",
            "input[name*='otp']",
            "input[name*='code']"
        };

        // OTP her hane ayrı kutu (Yolcu360'da KULLANILMIYOR; başka düzen için yedek).
        public static readonly string[] OtpDigitInputSelectors =
        {
            ".otp-input input",
            "[class*='otp'] input",
            "[class*='pincode'] input",
            "input[maxlength='1']"
        };

        // "Doğrula" — GERÇEK: <button data-cms-key="button_apply">Doğrula</button>
        public static readonly string[] OtpSubmitButtonSelectors =
        {
            "[data-cms-key='button_apply']",
            "button[data-cms-key='button_apply']",
            "button[type='submit']",
            "button[class*='verify']"
        };

        public static readonly string[] OtpSubmitButtonTexts =
        {
            "Doğrula", "Onayla", "Giriş Yap", "Tamam"
        };

        // SMS doğrulama modalının açıldığını sezmek için (best-effort).
        public static readonly string[] OtpScreenIndicatorSelectors =
        {
            "#sms_input",
            ".modal-overlay #sms_input",
            "input[autocomplete='one-time-code']"
        };

        // Giriş başarılı göstergeleri (oturum açıldı mı?). Başarılı login sonrası
        // /arac-kiralama veya hesap sayfasına yönlenir (best-effort).
        public static readonly string[] LoggedInIndicatorSelectors =
        {
            "a[href*='logout']",
            "a[href*='cikis']",
            "a[href*='hesabim']",
            "a[href*='profil']",
            ".user-menu",
            "[class*='account-menu']",
            "[class*='profile']"
        };

        // Hata mesajı göstergeleri (yanlış kod vb.).
        public static readonly string[] LoginErrorSelectors =
        {
            ".error",
            ".alert-danger",
            "[class*='error-message']",
            "[class*='invalid']"
        };
    }
}
