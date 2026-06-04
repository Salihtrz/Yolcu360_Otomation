namespace Yolcu360.DtoLayer.UserDto
{
    /// <summary>
    /// Başarılı uygulama girişinden sonra bellekte tutulan aktif kullanıcıyı temsil eder.
    /// Şifre BİLİNÇLİ olarak burada taşınmaz; yalnızca giriş sonrası gerekli olan kimlik
    /// ve Yolcu360 telefon numarası bulunur. PhoneNumber, Yolcu360 SMS/OTP giriş akışında
    /// siteye yazılacak numaradır (kullanıcı UI'dan girmez/seçmez).
    /// </summary>
    public class AuthenticatedUserDto
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
    }
}
