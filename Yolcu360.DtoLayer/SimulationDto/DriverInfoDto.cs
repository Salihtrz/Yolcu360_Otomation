namespace Yolcu360.DtoLayer.SimulationDto
{
    /// <summary>
    /// Simülasyon için alınan sürücü bilgileri. SADECE simülasyon amaçlıdır; gerçek bir
    /// rezervasyon/ödeme servisine veya Yolcu360 sitesine GÖNDERİLMEZ.
    /// </summary>
    public class DriverInfoDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string IdentityNo { get; set; }
        public DateTime BirthDate { get; set; }
        public string LicenseNo { get; set; }
        public DateTime LicenseDate { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}
