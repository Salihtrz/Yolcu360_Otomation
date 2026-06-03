namespace Yolcu360.DtoLayer.ReportDto
{
    /// <summary>
    /// Geçmiş raporlar listesinde (ReportsForm) gösterilen özet rapor bilgisi.
    /// </summary>
    public class ResultReportDto
    {
        public int Id { get; set; }
        public string ReportName { get; set; }
        public string PickupLocation { get; set; }
        public DateTime PickupDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Bu rapora bağlı araç sayısı (CarResults tablosundan COUNT).</summary>
        public int CarCount { get; set; }
    }
}
