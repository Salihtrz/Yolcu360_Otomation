namespace Yolcu360.DtoLayer.LoginDto
{
    /// <summary>
    /// Telefon + SMS/OTP ile Yolcu360 girişini başlatmak için gerekli bilgiler.
    /// Yalnızca kullanıcının kendi numarası ve kendi OTP kodu için kullanılır.
    /// </summary>
    public class LoginRequestDto
    {
        public string PhoneNumber { get; set; }

        /// <summary>OTP kodunun beklenme süresi (saniye). Varsayılan 120 sn.</summary>
        public int TimeoutSeconds { get; set; } = 120;
    }
}
