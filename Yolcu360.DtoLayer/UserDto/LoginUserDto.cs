namespace Yolcu360.DtoLayer.UserDto
{
    /// <summary>
    /// Yerel uygulama girişi için kullanılır. Yolcu360 sitesine giriş için kullanılmaz;
    /// site girişi her zaman kullanıcı tarafından manuel yapılır (SMS/OTP dahil).
    /// </summary>
    public class LoginUserDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
