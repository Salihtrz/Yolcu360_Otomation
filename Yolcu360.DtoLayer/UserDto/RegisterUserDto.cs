namespace Yolcu360.DtoLayer.UserDto
{
    /// <summary>
    /// Yeni yerel uygulama kullanıcısı kaydı için kullanılır. Telefon numarası, daha sonra
    /// Yolcu360 SMS/OTP giriş akışında bu hesaba bağlı olarak otomatik kullanılır (kullanıcı
    /// girişte numarayı tekrar girmez). Şifre DB'ye hash'lenerek saklanır.
    /// </summary>
    public class RegisterUserDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string PhoneNumber { get; set; }
    }
}
