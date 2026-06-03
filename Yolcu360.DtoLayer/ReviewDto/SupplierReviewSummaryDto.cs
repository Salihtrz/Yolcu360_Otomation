using System.Collections.Generic;

namespace Yolcu360.DtoLayer.ReviewDto
{
    /// <summary>
    /// Seçili aracın kiralama firmasına ait, Yolcu360 üzerinde ZATEN GÖRÜNEN değerlendirme özeti.
    /// Genel puan, alt kırılım puanları (temizlik/teslimat/personel) ve misafir yorumlarını taşır.
    /// Salt-okunur analiz amaçlıdır; siteye hiçbir şey gönderilmez.
    /// </summary>
    public class SupplierReviewSummaryDto
    {
        public string SupplierName { get; set; } = "";

        /// <summary>Firma teslim noktası/şube adı (modalda firma adının altında görünür). İsteğe bağlı.</summary>
        public string LocationName { get; set; } = "";

        /// <summary>Genel ortalama yıldız puanı (0-5).</summary>
        public double OverallRating { get; set; }

        /// <summary>Toplam yorum sayısı (biliniyorsa).</summary>
        public int ReviewCount { get; set; }

        public double CleanlinessRating { get; set; }
        public double DeliverySpeedRating { get; set; }
        public double StaffRating { get; set; }

        /// <summary>Okunan misafir yorumları (carousel'de tek tek gösterilir).</summary>
        public List<SupplierReviewDto> Reviews { get; set; } = new();

        /// <summary>Bu özetin gerçek site verisinden mi yoksa örnek/dummy veriden mi geldiği (UI rozeti için).</summary>
        public bool IsSampleData { get; set; }

        /// <summary>Runtime cache anahtarı olarak kullanılabilecek firma logo URL'si (varsa). Loglanmaz.</summary>
        public string SupplierLogoUrl { get; set; } = "";
    }
}
