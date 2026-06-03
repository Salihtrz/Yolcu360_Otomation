namespace Yolcu360.DtoLayer.SearchDto
{
    /// <summary>
    /// Kullanıcının MainForm arama panelinde girdiği ham arama bilgileri.
    /// Tarih ve saat ayrı tutulur; otomasyon bunları siteye uygun formata çevirir.
    /// </summary>
    public class SearchRequestDto
    {
        public string PickupLocation { get; set; }
        public DateTime PickupDate { get; set; }
        public TimeSpan PickupTime { get; set; }
        public DateTime ReturnDate { get; set; }
        public TimeSpan ReturnTime { get; set; }

        /// <summary>Tarih + saatin birleşik hali (alış).</summary>
        public DateTime PickupDateTime => PickupDate.Date + PickupTime;

        /// <summary>Tarih + saatin birleşik hali (dönüş).</summary>
        public DateTime ReturnDateTime => ReturnDate.Date + ReturnTime;
    }
}
