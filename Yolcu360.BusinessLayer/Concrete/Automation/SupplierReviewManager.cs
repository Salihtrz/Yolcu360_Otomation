using Yolcu360.BusinessLayer.Abstract.Automation;
using Yolcu360.BusinessLayer.Abstract.Browser;
using Yolcu360.Common.Automation;
using Newtonsoft.Json.Linq;
using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.Common.Constants;
using Yolcu360.Common.Helpers;
using Yolcu360.Common.Logging;
using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.DtoLayer.ReviewDto;

namespace Yolcu360.BusinessLayer.Concrete.Automation
{
    /// <summary>
    /// CefSharp üzerinden firma değerlendirme modalını açıp okuyan servis. Akış:
    /// 1) Seçili aracın firma logosuna (UUID) göre DOM'da kartı bul, "Yorum"u görünür alana kaydır.
    /// 2) Trusted (CDP) tıklama ile modalı aç (gerekirse JS click yedeği).
    /// 3) Modal açılınca puanları ve yorumları oku.
    /// 4) Modalı kapat (ana arama akışı bozulmaz).
    /// Hiçbir adımda siteye veri GÖNDERİLMEZ. Yorum metni / yazar adı LOGLANMAZ.
    /// </summary>
    public class SupplierReviewManager : ISupplierReviewService
    {
        private readonly ICefSharpBrowserService _browser;

        public SupplierReviewManager(ICefSharpBrowserService browser) => _browser = browser;

        public async Task<SupplierReviewSummaryDto> GetSupplierReviewsAsync(ResultCarDto car, int maxReviews = 30, CancellationToken ct = default)
        {
            if (car == null) return null;
            var supplier = string.IsNullOrWhiteSpace(car.RentalCompany) ? "?" : car.RentalCompany.Trim();
            var uuid = ExtractUuid(car.SupplierLogoUrl);

            if (string.IsNullOrWhiteSpace(uuid))
            {
                LogHelper.Info($"Degerlendirme: '{supplier}' icin firma logosu/UUID yok, kart eslestirilemiyor.");
                return null;
            }

            LogHelper.Info($"Degerlendirme cekme basladi: firma='{supplier}'.");
            try
            {
                // 1) Kartı bul + "Yorum" tetikleyicisini görünür alana kaydır, merkez koordinatı al.
                var findJson = await _browser.EvaluateStringAsync(
                    JsHelper.BuildFindReviewTriggerScript(
                        uuid,
                        Yolcu360ReviewSelectors.CarCardSelectors,
                        Yolcu360ReviewSelectors.CardSupplierLogoSelectors,
                        Yolcu360ReviewSelectors.ReviewTriggerSelectors), ct);
                ct.ThrowIfCancellationRequested();

                var find = TryParse(findJson);
                if (find == null || find.Value<bool>("found") != true)
                {
                    LogHelper.Info($"Degerlendirme: '{supplier}' icin arac karti / Yorum dugmesi bulunamadi (selector denendi: car-card + [data-cms-key=comment]).");
                    return null;
                }
                LogHelper.Info($"Degerlendirme: '{supplier}' Yorum dugmesi bulundu, modal aciliyor.");

                // 2) Trusted (CDP) tıklama — saat seçicide kanıtlanan yöntem.
                var x = find.Value<double>("x");
                var y = find.Value<double>("y");
                await _browser.RealClickAtAsync(x, y, ct);

                var opened = await WaitModalAsync(true, 6, ct);
                if (!opened)
                {
                    // JS click yedeği (bazı durumlarda trusted tıklama widget'a ulaşmazsa).
                    LogHelper.Info($"Degerlendirme: '{supplier}' modal CDP ile acilmadi, JS click deneniyor.");
                    await _browser.EvaluateBoolAsync(
                        JsHelper.BuildClickReviewTriggerScript(
                            uuid,
                            Yolcu360ReviewSelectors.CarCardSelectors,
                            Yolcu360ReviewSelectors.CardSupplierLogoSelectors,
                            Yolcu360ReviewSelectors.ReviewTriggerSelectors), ct);
                    opened = await WaitModalAsync(true, 5, ct);
                }
                if (!opened)
                {
                    LogHelper.Info($"Degerlendirme: '{supplier}' modal acilamadi.");
                    return null;
                }

                // 3) Modaldan oku. İçerik render'ı için kısa bir bekleme + tek deneme yeniden.
                SupplierReviewSummaryDto summary = null;
                for (var attempt = 0; attempt < 3 && !ct.IsCancellationRequested; attempt++)
                {
                    await Task.Delay(250, ct);
                    var json = await _browser.EvaluateStringAsync(
                        JsHelper.BuildScrapeReviewModalScript(Yolcu360ReviewSelectors.ReviewModalSelectors, maxReviews), ct);
                    summary = MapSummary(json, supplier, car);
                    if (summary != null && (summary.Reviews.Count > 0 || summary.OverallRating > 0)) break;
                }

                if (summary != null)
                    LogHelper.Info($"Degerlendirme okundu: firma='{supplier}', genel={summary.OverallRating:0.0}, yorum_sayisi={summary.ReviewCount}, okunan_yorum={summary.Reviews.Count}.");
                else
                    LogHelper.Info($"Degerlendirme: '{supplier}' modal acildi ama veri okunamadi.");

                return summary;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogHelper.Error($"Degerlendirme cekme hatasi (firma='{supplier}').", ex);
                return null;
            }
            finally
            {
                // 4) Modalı her durumda kapat (ana arama akışı bozulmasın). İptal olsa bile dener.
                try
                {
                    await _browser.EvaluateBoolAsync(
                        JsHelper.BuildCloseReviewModalScript(Yolcu360ReviewSelectors.ReviewCloseButtonSelectors), CancellationToken.None);
                    await WaitModalAsync(false, 3, CancellationToken.None);
                }
                catch { /* kapatma yedeği başarısızsa yut: sonraki açılış zaten yeni modal kullanır */ }
            }
        }

        /// <summary>Modalın açılmasını/kapanmasını bekler (polling; Thread.Sleep yok).</summary>
        private async Task<bool> WaitModalAsync(bool wantOpen, int timeoutSeconds, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                if (ct.IsCancellationRequested) return false;
                bool open;
                try { open = await _browser.EvaluateBoolAsync(JsHelper.BuildIsReviewModalOpenScript(Yolcu360ReviewSelectors.ReviewModalSelectors), ct); }
                catch { open = false; }
                if (open == wantOpen) return true;
                await Task.Delay(200, ct);
            }
            return false;
        }

        private static SupplierReviewSummaryDto MapSummary(string json, string supplierFallback, ResultCarDto car)
        {
            var o = TryParse(json);
            if (o == null || o.Value<bool>("found") != true) return null;

            var summary = new SupplierReviewSummaryDto
            {
                SupplierName = NonEmpty(o.Value<string>("supplierName"), supplierFallback),
                LocationName = o.Value<string>("location") ?? "",
                OverallRating = o.Value<double?>("overall") ?? 0,
                ReviewCount = o.Value<int?>("count") ?? 0,
                CleanlinessRating = o.Value<double?>("cleanliness") ?? 0,
                DeliverySpeedRating = o.Value<double?>("delivery") ?? 0,
                StaffRating = o.Value<double?>("staff") ?? 0,
                SupplierLogoUrl = car?.SupplierLogoUrl ?? "",
                IsSampleData = false
            };

            if (o["reviews"] is JArray arr)
            {
                foreach (var r in arr)
                {
                    var comment = (r.Value<string>("comment") ?? "").Trim();
                    if (string.IsNullOrEmpty(comment)) continue;
                    summary.Reviews.Add(new SupplierReviewDto
                    {
                        AuthorName = (r.Value<string>("author") ?? "").Trim(),
                        ReviewDate = (r.Value<string>("date") ?? "").Trim(),
                        Rating = r.Value<double?>("rating") ?? 0,
                        Comment = comment,
                        Source = "Yolcu360"
                    });
                }
            }

            if (summary.ReviewCount <= 0) summary.ReviewCount = summary.Reviews.Count;
            return summary;
        }

        private static JObject TryParse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JObject.Parse(json); }
            catch { return null; }
        }

        private static string NonEmpty(string value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        /// <summary>Logo URL'sinden dosya adını (UUID) çıkarır. Örn: .../supplier/&lt;uuid&gt;.png → uuid.</summary>
        private static string ExtractUuid(string logoUrl)
        {
            if (string.IsNullOrWhiteSpace(logoUrl)) return "";
            var noQuery = logoUrl.Split('?', '#')[0];
            var slash = noQuery.LastIndexOf('/');
            var file = slash >= 0 ? noQuery.Substring(slash + 1) : noQuery;
            var dot = file.LastIndexOf('.');
            return dot > 0 ? file.Substring(0, dot) : file;
        }
    }
}
