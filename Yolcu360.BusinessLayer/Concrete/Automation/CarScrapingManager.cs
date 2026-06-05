using Yolcu360.BusinessLayer.Abstract.Automation;
using Yolcu360.BusinessLayer.Abstract.Browser;
using Yolcu360.Common.Automation;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.Common.Constants;
using Yolcu360.Common.Helpers;
using Yolcu360.Common.Logging;
using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.DtoLayer.SearchDto;

namespace Yolcu360.BusinessLayer.Concrete.Automation
{
    /// <summary>
    /// Yolcu360 sonuç sayfasının DOM'unu OKUR ve araç kartlarını <see cref="ResultCarDto"/>'ya çevirir.
    /// Lazy-load ile tüm kartları yükler, JS ile kazır, fiyat/firma alanlarını ayrıştırır ve mükerrerleri
    /// eler. Form doldurma/arama tetikleme burada DEĞİL (bkz. <see cref="CarSearchManager"/>).
    /// </summary>
    public class CarScrapingManager : ICarScrapingService
    {
        private readonly ICefSharpBrowserService _browser;

        public CarScrapingManager(ICefSharpBrowserService browser)
        {
            _browser = browser;
        }

        /// <summary>
        /// Mevcut sonuç sayfasındaki tüm araç kartlarını kazır. Kazımadan ÖNCE lazy-load ile tüm
        /// sonuçları yükler (site 56 gösterirken uygulamada 20 kalması sorununu önler).
        /// </summary>
        public async Task<List<ResultCarDto>> ScrapeCarsAsync(SearchRequestDto request, CancellationToken ct = default)
        {
            await ScrollToLoadMoreAsync(ct);

            var script = JsHelper.BuildScrapeResultsScript(
                Yolcu360Selectors.ResultCardSelectors,
                Yolcu360Selectors.CarModelSelectors,
                Yolcu360Selectors.RentalCompanySelectors,
                Yolcu360Selectors.TransmissionSelectors,
                Yolcu360Selectors.FuelSelectors,
                Yolcu360Selectors.SegmentSelectors,
                Yolcu360Selectors.PriceSelectors,
                Yolcu360Selectors.CarImageSelectors);

            var json = await _browser.EvaluateStringAsync(script, ct);
            var raw = ParseRaw(json);

            var sourceUrl = _browser.CurrentUrl;
            var now = DateTime.Now;

            var cars = raw.Select(r =>
            {
                var (price, currency) = ParsePrice(r.priceText);
                return new ResultCarDto
                {
                    CarModel = Clean(r.carModel),
                    // Kartta firma metni yok; logo UUID'sinden gerçek firma adına çevrilir.
                    RentalCompany = Yolcu360Suppliers.ResolveName(r.rentalCompany),
                    // Ham logo URL'si firma değerlendirme modalını açarken kart eşleştirmede kullanılır.
                    SupplierLogoUrl = r.rentalCompany,
                    TransmissionType = Clean(r.transmission),
                    FuelType = Clean(r.fuel),
                    Segment = Clean(r.segment),
                    Price = price,
                    Currency = currency,
                    PickupLocation = request.PickupLocation,
                    PickupDateTime = request.PickupDateTime,
                    ReturnDateTime = request.ReturnDateTime,
                    SourceUrl = sourceUrl,
                    ImageUrl = r.imageUrl,
                    ScrapedAt = now
                };
            }).ToList();

            // Lazy-load sırasında bazı kartlar tekrar render edilebildiğinden olası mükerrer kayıtlar
            // ele alınır (model + firma + fiyat + vites + yakıt anahtarıyla).
            cars = cars
                .GroupBy(c => $"{c.CarModel}|{c.RentalCompany}|{c.Price:F0}|{c.TransmissionType}|{c.FuelType}")
                .Select(g => g.First())
                .ToList();

            LogHelper.Info($"{cars.Count} araç kazındı.");
            return cars;
        }

        public async Task<List<SiteFilterSectionDto>> ScrapeSiteFiltersAsync(CancellationToken ct = default)
        {
            var json = await _browser.EvaluateStringAsync(JsHelper.BuildScrapeFiltersScript(), ct);
            if (string.IsNullOrWhiteSpace(json))
                return new List<SiteFilterSectionDto>();
            try
            {
                return JsonConvert.DeserializeObject<List<SiteFilterSectionDto>>(json) ?? new List<SiteFilterSectionDto>();
            }
            catch (Exception ex)
            {
                LogHelper.Error("Filtre paneli okunamadı (JSON).", ex);
                return new List<SiteFilterSectionDto>();
            }
        }

        /// <summary>
        /// Lazy-load ile TÜM sonuçları yükler: kart sayısı artık ARTMAYANA kadar (üst üste 3 kez aynı)
        /// sayfayı aşağı kaydırır.
        /// </summary>
        private async Task ScrollToLoadMoreAsync(CancellationToken ct)
        {
            int last = -1, stable = 0;
            for (int i = 0; i < 40 && stable < 3; i++)
            {
                await _browser.EvaluateBoolAsync(
                    "(function(){ window.scrollTo(0, document.body.scrollHeight); return true; })();", ct);
                await Task.Delay(900, ct);
                var count = await GetResultCountAsync(ct);
                if (count == last) stable++; else { stable = 0; last = count; }
            }
            await _browser.EvaluateBoolAsync("(function(){ window.scrollTo(0,0); return true; })();", ct);
            LogHelper.Info($"Lazy-load tamamlandı: {last} kart yüklendi.");
        }

        /// <summary>Sayfadaki mevcut sonuç kartı sayısını döndürür.</summary>
        private async Task<int> GetResultCountAsync(CancellationToken ct)
        {
            var s = await _browser.EvaluateStringAsync(
                JsHelper.BuildCountResultsScript(Yolcu360Selectors.ResultCardSelectors), ct);
            return int.TryParse(s?.Trim(), out var n) ? n : 0;
        }

        private static string Clean(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();

        private static List<RawCar> ParseRaw(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<RawCar>();
            try
            {
                return JsonConvert.DeserializeObject<List<RawCar>>(json) ?? new List<RawCar>();
            }
            catch (Exception ex)
            {
                LogHelper.Error("Sonuç JSON'u ayrıştırılamadı.", ex);
                return new List<RawCar>();
            }
        }

        /// <summary>
        /// "1.234,56 TL" / "₺1.234" / "EUR 120" gibi metinlerden sayısal fiyat ve para birimini
        /// ayıklar. Türkçe binlik/ondalık biçimini de destekler.
        /// </summary>
        private static (decimal price, string currency) ParsePrice(string priceText)
        {
            if (string.IsNullOrWhiteSpace(priceText))
                return (0m, "TL");

            var currency = "TL";
            if (priceText.Contains("€") || priceText.Contains("EUR", StringComparison.OrdinalIgnoreCase)) currency = "EUR";
            else if (priceText.Contains("$") || priceText.Contains("USD", StringComparison.OrdinalIgnoreCase)) currency = "USD";
            else if (priceText.Contains("₺") || priceText.Contains("TL", StringComparison.OrdinalIgnoreCase)) currency = "TL";

            var numeric = Regex.Replace(priceText, @"[^\d.,]", "");
            if (string.IsNullOrEmpty(numeric))
                return (0m, currency);

            // Türkçe biçim: nokta binlik, virgül ondalık. Noktaları sil, virgülü noktaya çevir.
            numeric = numeric.Replace(".", "").Replace(",", ".");
            if (decimal.TryParse(numeric, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                return (price, currency);

            return (0m, currency);
        }

        /// <summary>JS scrape çıktısının ham karşılığı.</summary>
        private class RawCar
        {
            public string carModel { get; set; }
            public string rentalCompany { get; set; }
            public string transmission { get; set; }
            public string fuel { get; set; }
            public string segment { get; set; }
            public string priceText { get; set; }
            public string imageUrl { get; set; }
        }
    }
}
