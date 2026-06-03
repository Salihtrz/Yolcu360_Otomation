namespace Yolcu360.BusinessLayer.Abstract
{
    /// <summary>
    /// Yolcu360 telefon + SMS/OTP giriş akışını CefSharp üzerinde yürütür.
    ///
    /// ETİK/GÜVENLİK: Yalnızca kullanıcının KENDİ numarası ve KENDİ OTP kodu için normal
    /// kullanıcı etkileşimini otomatikleştirir. Captcha/SMS bypass YOK, yoğun istek YOK.
    /// Selector'lar bulunamazsa kod kırılmaz; çağıran tarayıcıyı görünür yapıp kullanıcıya
    /// manuel devam önerir.
    /// </summary>
    public interface ILoginAutomationService
    {
        /// <summary>
        /// Login ekranını açar, telefon numarasını yazar ve "Devam/Kod Gönder" butonunu tetikler.
        /// Ardından SMS kod ekranının gelmesini bekler.
        /// </summary>
        Task StartPhoneLoginAsync(string phoneNumber, IProgress<string> progress, CancellationToken ct = default);

        /// <summary>
        /// Giriş sayfasını/formunu açar (telefon yazmaz, "Devam Et"e basmaz). Telefon + "Devam Et"
        /// adımını KULLANICI elle yapar — bu, sitenin reCAPTCHA (bot) puanını yüksek tutar.
        /// Form gelmezse sitenin "Giriş Yap" düğmesini açmayı dener.
        /// </summary>
        /// <returns>Telefon formu kullanıcı girişine hazırsa true; gelmezse (ör. zaten giriş
        /// yapılmışsa) false.</returns>
        Task<bool> OpenLoginPageAsync(IProgress<string> progress, CancellationToken ct = default);

        /// <summary>
        /// 6 haneli OTP kodunu doğrulama alan(lar)ına yazar ve "Doğrula/Giriş" butonunu tetikler.
        /// </summary>
        Task<bool> SubmitOtpAsync(string otpCode, IProgress<string> progress, CancellationToken ct = default);

        /// <summary>Şu anda oturum açık görünüyor mu?</summary>
        Task<bool> IsLoggedInAsync(CancellationToken ct = default);

        /// <summary>Oturum açılana kadar (veya zaman aşımına kadar) bekler.</summary>
        Task<bool> WaitForLoginSuccessAsync(TimeSpan timeout, CancellationToken ct = default);
    }
}
