using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.DtoLayer.ReviewDto;

namespace Yolcu360.BusinessLayer.Abstract
{
    /// <summary>
    /// Seçili aracın kiralama firmasına ait, Yolcu360 üzerinde ZATEN GÖRÜNEN değerlendirme
    /// bilgilerini (genel/alt puanlar + misafir yorumları) okur. SALT OKUNUR: siteye yorum
    /// gönderilmez, yeni değerlendirme oluşturulmaz. Otomasyon tarayıcısının (CefSharp) o an
    /// gösterdiği sonuç sayfası üzerinden çalışır; ana arama akışını bozmaz (modalı açıp kapatır).
    /// </summary>
    public interface ISupplierReviewService
    {
        /// <summary>
        /// Verilen aracın firmasının değerlendirme özetini döner. Bulunamazsa <c>null</c>.
        /// </summary>
        Task<SupplierReviewSummaryDto> GetSupplierReviewsAsync(ResultCarDto car, int maxReviews = 30, CancellationToken ct = default);
    }
}
