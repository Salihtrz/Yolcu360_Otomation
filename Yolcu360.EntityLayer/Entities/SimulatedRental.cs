namespace Yolcu360.EntityLayer.Entities
{
    /// <summary>
    /// Uygulama içinde yapılan bir araç kiralama SİMÜLASYONU kaydı (gerçek rezervasyon/ödeme DEĞİL).
    /// Bir simülasyon birden çok ek hizmet (<see cref="SimulatedRentalExtra"/>) içerebilir (1 - N).
    /// Kişisel sürücü bilgileri yalnızca simülasyon amaçlı tutulur, hiçbir gerçek servise gönderilmez.
    /// </summary>
    public class SimulatedRental
    {
        public int Id { get; set; }

        /// <summary>Kaynak araç sonucunun Id'si (varsa). Grid'den gelen araç DB'de değilse null kalır.</summary>
        public int? CarResultId { get; set; }

        // ---- Araç bilgileri (seçili araçtan kopyalanır) ----
        public string CarModel { get; set; }
        public string RentalCompany { get; set; }
        public string TransmissionType { get; set; }
        public string FuelType { get; set; }
        public string Segment { get; set; }
        public string PickupLocation { get; set; }
        public DateTime PickupDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }
        public int RentalDays { get; set; }

        // ---- Fiyatlar ----
        public decimal BasePrice { get; set; }
        public decimal ExtraServicesTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public string PaymentMethod { get; set; }

        // ---- Sürücü bilgileri (yalnızca simülasyon) ----
        public string DriverFirstName { get; set; }
        public string DriverLastName { get; set; }
        public string DriverPhone { get; set; }
        public string DriverEmail { get; set; }
        public string DriverIdentityNo { get; set; }
        public DateTime DriverBirthDate { get; set; }
        public string DriverLicenseNo { get; set; }
        public DateTime DriverLicenseDate { get; set; }

        // ---- Simülasyon meta ----
        public string SimulationStatus { get; set; }
        public string SimulationCode { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Bu simülasyona bağlı ek hizmetler (navigasyon; DB'de ayrı tablo).</summary>
        public List<SimulatedRentalExtra> Extras { get; set; } = new();
    }
}
