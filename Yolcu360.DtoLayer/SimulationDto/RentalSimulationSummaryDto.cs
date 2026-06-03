namespace Yolcu360.DtoLayer.SimulationDto
{
    /// <summary>
    /// Simülasyon özet ekranı ve PNG çıktısı için görünüm modeli. Servis, kaydı oluşturduktan
    /// sonra (kod + hesaplanmış toplamlarla) bunu döndürür.
    /// </summary>
    public class RentalSimulationSummaryDto
    {
        public string SimulationCode { get; set; }

        // Araç
        public string CarModel { get; set; }
        public string RentalCompany { get; set; }
        public string TransmissionType { get; set; }
        public string FuelType { get; set; }
        public string Segment { get; set; }
        public string ImageUrl { get; set; }

        // Alış / dönüş
        public string PickupLocation { get; set; }
        public DateTime PickupDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }
        public int RentalDays { get; set; }

        // Sürücü
        public DriverInfoDto Driver { get; set; } = new();

        // Ek hizmetler + fiyat
        public List<SimulatedRentalExtraDto> Extras { get; set; } = new();
        public decimal BasePrice { get; set; }
        public decimal ExtraServicesTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public string PaymentMethod { get; set; }

        public DateTime SimulationDate { get; set; }
    }
}
