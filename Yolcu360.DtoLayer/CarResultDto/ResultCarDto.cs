namespace Yolcu360.DtoLayer.CarResultDto
{
    /// <summary>
    /// DataGridView'de gösterilen ve siteden kazınan araç sonucu.
    /// UI ile servisler arasında taşınır.
    /// </summary>
    public class ResultCarDto
    {
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
        /// <summary>
        /// Firma logosunun tam URL'si (/supplier/&lt;uuid&gt;.png). Firma değerlendirme modalını
        /// açarken doğru araç kartını DOM'da kesin eşleştirmek için kullanılır. DB'ye yazılmaz.
        /// </summary>
        public string SupplierLogoUrl { get; set; }
        public DateTime ScrapedAt { get; set; }
    }
}
