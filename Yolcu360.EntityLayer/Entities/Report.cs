namespace Yolcu360.EntityLayer.Entities
{
    /// <summary>
    /// Kaydedilmiş bir arama raporu. Bir rapor birden çok <see cref="CarResult"/> içerir (1 - N).
    /// </summary>
    public class Report
    {
        public int Id { get; set; }
        public string ReportName { get; set; }
        public string PickupLocation { get; set; }
        public DateTime PickupDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Bu rapora bağlı araç sonuçları (navigasyon amaçlı, DB'de ayrı tablo).</summary>
        public List<CarResult> CarResults { get; set; } = new();
    }
}
