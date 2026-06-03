namespace Yolcu360.DtoLayer.SimulationDto
{
    /// <summary>
    /// Bir simülasyon kaydı oluşturmak için gereken girdiler. Gün sayısı, ek hizmet toplamı,
    /// genel toplam ve simülasyon kodu servis (SimulatedRentalManager) tarafından hesaplanır.
    /// </summary>
    public class CreateSimulatedRentalDto
    {
        public int? CarResultId { get; set; }

        // Araç bilgileri (seçili araçtan)
        public string CarModel { get; set; }
        public string RentalCompany { get; set; }
        public string TransmissionType { get; set; }
        public string FuelType { get; set; }
        public string Segment { get; set; }
        public string PickupLocation { get; set; }
        public DateTime PickupDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }
        public string ImageUrl { get; set; }

        /// <summary>Araç temel fiyatı (grid'den gelen fiyat ya da kullanıcı manuel girdiyse o).</summary>
        public decimal BasePrice { get; set; }

        public string PaymentMethod { get; set; }
        public DriverInfoDto Driver { get; set; } = new();
        public List<SimulatedRentalExtraDto> Extras { get; set; } = new();
    }
}
