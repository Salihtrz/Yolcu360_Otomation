namespace Yolcu360.DtoLayer.SimulationDto
{
    /// <summary>
    /// Kaydedilmiş bir simülasyonun okunması (geçmiş listesi + detay). Ek hizmetler dahildir.
    /// </summary>
    public class ResultSimulatedRentalDto
    {
        public int Id { get; set; }
        public int? CarResultId { get; set; }

        public string CarModel { get; set; }
        public string RentalCompany { get; set; }
        public string TransmissionType { get; set; }
        public string FuelType { get; set; }
        public string Segment { get; set; }
        public string PickupLocation { get; set; }
        public DateTime PickupDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }
        public int RentalDays { get; set; }

        public decimal BasePrice { get; set; }
        public decimal ExtraServicesTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public string PaymentMethod { get; set; }

        public string DriverFirstName { get; set; }
        public string DriverLastName { get; set; }
        public string DriverPhone { get; set; }
        public string DriverEmail { get; set; }
        public string DriverIdentityNo { get; set; }
        public DateTime DriverBirthDate { get; set; }
        public string DriverLicenseNo { get; set; }
        public DateTime DriverLicenseDate { get; set; }

        public string SimulationStatus { get; set; }
        public string SimulationCode { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<SimulatedRentalExtraDto> Extras { get; set; } = new();

        // ---- Grid/görünüm yardımcıları ----
        public string DriverFullName => $"{DriverFirstName} {DriverLastName}".Trim();
    }
}
