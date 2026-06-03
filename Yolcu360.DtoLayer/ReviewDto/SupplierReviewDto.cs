namespace Yolcu360.DtoLayer.ReviewDto
{
    /// <summary>
    /// Yolcu360 firma değerlendirme modalında görünen TEK bir misafir yorumu.
    /// Bu veri yalnızca OKUNUR; siteye yorum gönderilmez, yeni yorum oluşturulmaz.
    /// Kişisel veri (ad-soyad) loglanmaz; AuthorName yalnızca UI'da gösterilir.
    /// </summary>
    public class SupplierReviewDto
    {
        /// <summary>Yorum sahibinin baş harfleri / adı (sitede çoğu kez sadece baş harfler verilir).</summary>
        public string AuthorName { get; set; } = "";

        /// <summary>Yorum tarihi metni (ör. "31 Mayıs 2026"). Site verdiyse gösterilir.</summary>
        public string ReviewDate { get; set; } = "";

        /// <summary>Yorumun genel puanı (0-5). Bulunamazsa 0.</summary>
        public double Rating { get; set; }

        /// <summary>Yorum metni.</summary>
        public string Comment { get; set; } = "";

        /// <summary>Kaynak (ör. "Yolcu360"). Yalnızca bilgi amaçlı.</summary>
        public string Source { get; set; } = "Yolcu360";
    }
}
