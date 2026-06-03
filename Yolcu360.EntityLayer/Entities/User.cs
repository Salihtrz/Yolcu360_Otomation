namespace Yolcu360.EntityLayer.Entities
{
    /// <summary>
    /// Uygulama kullanıcısı. Kullanıcının Yolcu360 hesabıyla karıştırılmamalıdır;
    /// bu kayıt sadece yerel uygulama içindir.
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }

        /// <summary>
        /// Kullanıcının kendi Yolcu360 hesabına ait telefon numarası (SMS/OTP girişinde
        /// siteye yazılır). Yalnızca yerel olarak saklanır; bypass amacı taşımaz.
        /// </summary>
        public string PhoneNumber { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
