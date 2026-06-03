namespace Yolcu360.EntityLayer.Entities
{
    /// <summary>
    /// Yolcu360 sonuç sayfasından kazınan tek bir araç kiralama seçeneği.
    /// </summary>
    public class CarResult
    {
        public int Id { get; set; }

        /// <summary>Bağlı olduğu <see cref="Report"/> kaydının Id'si (FK).</summary>
        public int ReportId { get; set; }

        public string CarModel { get; set; }
        public string RentalCompany { get; set; }
        public string TransmissionType { get; set; }
        public string FuelType { get; set; }
        public string Segment { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; }
        public string PickupLocation { get; set; }
        public DateTime PickupDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }
        public string SourceUrl { get; set; }
        public string ImageUrl { get; set; }
        public DateTime ScrapedAt { get; set; }
    }
}
