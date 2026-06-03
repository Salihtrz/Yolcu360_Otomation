using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.Common.Constants;
using Yolcu360.Common.Helpers;
using Yolcu360.Common.Logging;
using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.DtoLayer.SearchDto;

namespace Yolcu360.BusinessLayer.Concrete
{
    /// <summary>
    /// Yolcu360 üzerinde arama formunu doldurur, aramayı başlatır, sonuçları bekler
    /// ve araç kartlarını kazır. Tüm beklemeler async/await ile yapılır (Thread.Sleep yok).
    ///
    /// ETİK/GÜVENLİK: Bu servis yalnızca normal kullanıcı etkileşimlerini taklit eder.
    /// Login otomatik yapılmaz, SMS/OTP/captcha ile uğraşmaz, yoğun istek atmaz.
    /// </summary>
    public class Yolcu360AutomationManager : IYolcu360AutomationService
    {
        private readonly ICefSharpBrowserService _browser;

        public Yolcu360AutomationManager(ICefSharpBrowserService browser)
        {
            _browser = browser;
        }

        public async Task<List<ResultCarDto>> SearchAsync(
            SearchRequestDto request, IProgress<string> progress, CancellationToken ct = default)
        {
            void Report(string s) { progress?.Report(s); LogHelper.Info("Arama: " + s); }

            Report("Site hazır olması bekleniyor...");
            await _browser.WaitForPageLoadAsync(ct);

            // Tekrar arama senaryosu: önceki aramadan sonra tarayıcı sonuç sayfasında
            // (/arac-kiralama/search) kalmış olabilir; orada arama formu yoktur. Form bulunamazsa
            // araç kiralama (ana) sayfasına dönülür ki yeni bir arama yapılabilsin.
            var hasLocation = await _browser.WaitForElementAsync(Yolcu360Selectors.PickupLocationSelectors, 4, ct);
            if (!hasLocation)
            {
                Report("Araç kiralama sayfasına dönülüyor...");
                await _browser.LoadUrlAsync(Yolcu360Constants.HomeUrl, ct);
                await _browser.WaitForPageLoadAsync(ct);
                hasLocation = await _browser.WaitForElementAsync(
                    Yolcu360Selectors.PickupLocationSelectors, Yolcu360Constants.DefaultWaitTimeoutSeconds, ct);
            }
            if (!hasLocation)
                throw new AutomationException("Arama alanı bulunamadı. Site yapısı değişmiş olabilir veya sayfa açılmamış olabilir.");

            // Sayfa DOM'da olsa da SPA (React/Vue) henüz input'a olay dinleyicilerini bağlamamış
            // olabilir. Yazmadan önce kısa bir "settle" süresi tanınır (Thread.Sleep değil, async).
            await Task.Delay(2500, ct);

            // 1) Alış yeri: gerçek klavye yazımı (autocomplete tetiklensin). Öneri açılmazsa
            //    bir kez daha denenir (ilk denemede hydration tamamlanmamış olabilir).
            Report("Alış yeri giriliyor...");
            bool suggestionsOpened = false;
            for (int attempt = 1; attempt <= 2 && !suggestionsOpened; attempt++)
            {
                await _browser.EvaluateBoolAsync(
                    JsHelper.BuildTypeRealScript(Yolcu360Selectors.PickupLocationSelectors, request.PickupLocation), ct);
                suggestionsOpened = await _browser.WaitForElementAsync(
                    Yolcu360Selectors.LocationSuggestionSelectors, 6, ct);
                if (!suggestionsOpened)
                    LogHelper.Warning($"Lokasyon önerisi {attempt}. denemede açılmadı, tekrar denenecek.");
            }

            // 2) Autocomplete önerisini seç (Yolcu360'da arama için zorunlu — koordinat/place id buradan gelir)
            if (suggestionsOpened)
            {
                Report("Lokasyon önerisi seçiliyor...");
                await _browser.ClickElementAsync(Yolcu360Selectors.LocationSuggestionSelectors, ct);
            }
            else
            {
                throw new AutomationException(
                    "Lokasyon önerisi açılmadı. Lütfen alış yerini daha açık yazın (örn. 'İstanbul Havalimanı').");
            }

            // 3) Takvimi aç
            Report("Tarihler seçiliyor...");
            await _browser.ClickElementAsync(Yolcu360Selectors.PickupDateTriggerSelectors, ct);

            if (!await _browser.WaitForElementAsync(new[] { ".month-header" }, 8, ct))
                throw new AutomationException("Tarih takvimi açılamadı. Site yapısı değişmiş olabilir.");

            // 4) Alış ve dönüş günlerini takvimden tıkla (aralık seçici: ilk tık başlangıç, ikinci tık bitiş)
            var pickupOk = await _browser.EvaluateBoolAsync(
                JsHelper.BuildClickCalendarDayScript(TurkishMonthHeader(request.PickupDate), request.PickupDate.Day), ct);
            var returnOk = await _browser.EvaluateBoolAsync(
                JsHelper.BuildClickCalendarDayScript(TurkishMonthHeader(request.ReturnDate), request.ReturnDate.Day), ct);

            if (!pickupOk || !returnOk)
                LogHelper.Warning($"Takvimde gün seçimi tam yapılamadı (pickup={pickupOk}, return={returnOk}). " +
                                  "Seçilen tarih takvimde görünür aralıkta olmayabilir.");

            // 4b) Saat seçimi: site saat seçicisi yarım saat aralıklı bir liste (li.hour-li).
            //     Tetikleyiciye tıklanır, ardından hedef saate (yarım saate yuvarlanmış) tıklanır.
            //     Best-effort: başarısız olursa site varsayılanı (10:00) ile devam edilir.
            await Task.Delay(1000, ct); // takvim panelinin oturması için
            await SetSiteTimeAsync("Alış Saati", request.PickupTime, ct);
            await SetSiteTimeAsync("Dönüş Saati", request.ReturnTime, ct);

            // 5) Aramayı tetikle
            Report("Arama yapılıyor...");
            var clicked = await _browser.ClickElementAsync(Yolcu360Selectors.SearchButtonSelectors, ct);
            if (!clicked)
                throw new AutomationException("Arama butonu bulunamadı. Site yapısı değişmiş olabilir.");

            // Arama yeni sayfaya/AJAX'a yol açar.
            await _browser.WaitForPageLoadAsync(ct);

            Report("Sonuçlar bekleniyor...");
            var hasResults = await _browser.WaitForResultsAsync(
                Yolcu360Selectors.ResultCardSelectors, Yolcu360Constants.ResultsWaitTimeoutSeconds, ct);
            if (!hasResults)
                throw new AutomationException("Sonuç bulunamadı veya site yapısı değişmiş olabilir.");

            // Liste lazy-load ile yüklenir (ilk ~20 kart). Daha fazlasını getirmek için
            // sayfa birkaç kez aşağı kaydırılır (Thread.Sleep değil, async bekleme).
            await ScrollToLoadMoreAsync(ct);

            Report("Araç bilgileri çekiliyor...");
            var cars = await ScrapeCurrentResultsAsync(request, ct);

            Report($"İşlem tamamlandı. {cars.Count} araç bulundu.");
            return cars;
        }

        /// <summary>
        /// Site saat seçicisinden (li.hour-li) hedef saati seçer. Saat yarım saate yuvarlanır.
        /// Best-effort: tetikleyici/seçenek bulunamazsa sessizce geçilir (varsayılan saat kalır).
        /// </summary>
        private async Task SetSiteTimeAsync(string label, TimeSpan time, CancellationToken ct)
        {
            var target = RoundToHalfHour(time);
            var listOpenScript = "(function(){ return document.querySelectorAll('li.hour-li').length > 0; })();";

            // Tetikleyiciye tıkla ve saat listesinin gerçekten açıldığını doğrula; açılmazsa
            // (Vue widget'ı bazen ilk tıkta tepki vermiyor) birkaç kez yeniden dene.
            var listOpened = false;
            for (int attempt = 1; attempt <= 2 && !listOpened; attempt++)
            {
                var triggerOk = await _browser.EvaluateBoolAsync(JsHelper.BuildClickTimeTriggerScript(label), ct);
                if (!triggerOk)
                {
                    LogHelper.Warning($"Saat tetikleyicisi bulunamadı ('{label}'), varsayılan saat kullanılacak.");
                    return;
                }
                await Task.Delay(1000, ct);
                listOpened = await _browser.EvaluateBoolAsync(listOpenScript, ct);
            }

            if (!listOpened)
            {
                LogHelper.Warning($"Saat listesi açılmadı ('{label}'), varsayılan saat kalacak.");
                return;
            }

            var optionOk = await _browser.EvaluateBoolAsync(JsHelper.BuildClickTimeOptionScript(target), ct);
            LogHelper.Info(optionOk
                ? $"Saat seçildi ('{label}' = {target})."
                : $"Saat seçeneği bulunamadı ('{label}' = {target}), varsayılan saat kalacak.");

            // Açık kalan liste sonraki adımı engellemesin diye nötr bir tıklama ile kapat.
            if (!optionOk)
                await _browser.EvaluateBoolAsync("(function(){ document.body.click(); return true; })();", ct);

            await Task.Delay(400, ct);
        }

        /// <summary>Saati en yakın yarım saate yuvarlayıp "HH:mm" döndürür (site listesi 30 dk aralıklı).</summary>
        private static string RoundToHalfHour(TimeSpan t)
        {
            var totalMinutes = (int)Math.Round(t.TotalMinutes / 30.0) * 30;
            if (totalMinutes >= 24 * 60) totalMinutes = 23 * 60 + 30; // gün taşmasını engelle
            var rounded = TimeSpan.FromMinutes(totalMinutes);
            return $"{rounded.Hours:D2}:{rounded.Minutes:D2}";
        }

        /// <summary>Lazy-load ile daha fazla sonuç yüklemek için sayfayı birkaç kez aşağı kaydırır.</summary>
        private async Task ScrollToLoadMoreAsync(CancellationToken ct, int times = 4)
        {
            for (int i = 0; i < times; i++)
            {
                await _browser.EvaluateBoolAsync(
                    "(function(){ window.scrollTo(0, document.body.scrollHeight); return true; })();", ct);
                await Task.Delay(1200, ct);
            }
            // Başa dön (scrape için fark etmez, sadece düzen).
            await _browser.EvaluateBoolAsync("(function(){ window.scrollTo(0,0); return true; })();", ct);
        }

        public async Task<List<ResultCarDto>> ScrapeCurrentResultsAsync(SearchRequestDto request, CancellationToken ct = default)
        {
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

            // Lazy-load sırasında bazı kartlar tekrar render edilebildiğinden olası
            // mükerrer kayıtlar ele alınır (model + firma + fiyat + vites + yakıt anahtarıyla).
            cars = cars
                .GroupBy(c => $"{c.CarModel}|{c.RentalCompany}|{c.Price:F0}|{c.TransmissionType}|{c.FuelType}")
                .Select(g => g.First())
                .ToList();

            LogHelper.Info($"{cars.Count} araç kazındı.");
            return cars;
        }

        public async Task<bool> ApplyFiltersOnWebsiteAsync(FilterRequestDto filter, CancellationToken ct = default)
        {
            // Yolcu360 sonuç sayfasındaki gerçek filtre kutuları (2026):
            //   input id="filter-transmission.1" (Manuel) / ".2" (Otomatik)
            //   input id="filter-fuel.1" (Benzin) / ".2" (Dizel) / ".5" (LPG) / ".7" (Hybrid) / ".8" (Benzin/Dizel) / ".11" (Elektrik)
            //   input id="filter-vendor.<slug>"  (slug = firma adının küçük harfli hali, boşluklar korunur)
            //   input id="filter-brand.<id>"     (id = markanın sayısal kimliği)
            // ÖNEMLİ: Kutular durum-bazlı (idempotent) ayarlanır — yalnızca mevcut durum hedeften
            // farklıysa tıklanır. Böylece ikinci kez "Uygula" denince zaten seçili filtreler kapanmaz.
            if (filter == null) return false;

            // İstenen vites/yakıt id kümeleri
            var wantTrans = (filter.TransmissionTypes ?? new()).Select(MapTransmissionId).Where(x => x != null).ToHashSet();
            var wantFuel = (filter.FuelTypes ?? new()).Select(MapFuelId).Where(x => x != null).ToHashSet();

            // Vites (bilinen tüm id'ler için hedef durumu uygula → seçilmeyenler de kapanır)
            foreach (var id in new[] { "1", "2" })
                await _browser.EvaluateBoolAsync(JsHelper.BuildSetCheckboxScript($"filter-transmission.{id}", wantTrans.Contains(id)), ct);

            // Yakıt
            foreach (var id in new[] { "1", "2", "5", "7", "8", "11" })
                await _browser.EvaluateBoolAsync(JsHelper.BuildSetCheckboxScript($"filter-fuel.{id}", wantFuel.Contains(id)), ct);

            // Firma (tek seçim): seçili olanı bırak, diğer tüm vendor kutularını kapat
            var vendorKeep = new List<string>();
            if (!string.IsNullOrWhiteSpace(filter.RentalCompany))
                vendorKeep.Add($"filter-vendor.{filter.RentalCompany.Trim().ToLowerInvariant()}");
            await _browser.EvaluateBoolAsync(JsHelper.BuildResetCheckboxGroupScript("filter-vendor.", vendorKeep.ToArray()), ct);

            // Marka (tek seçim): seçili olanı bırak, diğer marka kutularını kapat
            var brandKeep = new List<string>();
            var brandId = MapBrandId(filter.Brand);
            if (brandId != null)
                brandKeep.Add($"filter-brand.{brandId}");
            await _browser.EvaluateBoolAsync(JsHelper.BuildResetCheckboxGroupScript("filter-brand.", brandKeep.ToArray()), ct);

            // Site XHR ile listeyi güncellesin
            await Task.Delay(1500, ct);
            await _browser.WaitForResultsAsync(
                Yolcu360Selectors.ResultCardSelectors, Yolcu360Constants.ResultsWaitTimeoutSeconds, ct);

            var applied = filter.HasAnyFilter;
            LogHelper.Info(applied ? "Site üzerinde filtreler ayarlandı." : "Site filtreleri temizlendi.");
            return applied;
        }

        /// <summary>Marka adını Yolcu360'ın sayısal marka id'sine çevirir; bilinmiyorsa null.</summary>
        private static string MapBrandId(string brand)
        {
            if (string.IsNullOrWhiteSpace(brand)) return null;
            return brand.Trim().ToLowerInvariant() switch
            {
                "alfa romeo" => "53",
                "audi" => "58",
                "bmw" => "59",
                "byd" => "124",
                "chery" => "108",
                "citroen" or "citroën" => "61",
                "cupra" => "94",
                "dacia" => "62",
                "ds" => "152",
                "fiat" => "63",
                "ford" => "64",
                "hyundai" => "66",
                "jeep" => "67",
                "kia" => "68",
                "mercedes" or "mercedes benz" or "mercedes-benz" => "54",
                "mini" => "73",
                "nissan" => "74",
                "opel" => "75",
                "peugeot" => "76",
                "renault" => "78",
                "seat" => "79",
                "skoda" or "škoda" => "80",
                "suzuki" => "82",
                _ => null
            };
        }

        private static string MapTransmissionId(string t)
        {
            t = t?.Trim().ToLowerInvariant();
            return t switch
            {
                "manuel" or "manual" => "1",
                "otomatik" or "automatic" or "auto" => "2",
                _ => null
            };
        }

        private static string MapFuelId(string f)
        {
            f = f?.Trim().ToLowerInvariant();
            return f switch
            {
                "benzin" => "1",
                "dizel" or "diesel" => "2",
                "lpg" => "5",
                "hibrit" or "hybrid" => "7",
                "elektrik" or "electric" => "11",
                _ => null
            };
        }

        public List<ResultCarDto> ApplyFiltersLocally(List<ResultCarDto> cars, FilterRequestDto filter)
        {
            if (cars == null) return new List<ResultCarDto>();
            if (filter == null || !filter.HasAnyFilter) return cars;

            IEnumerable<ResultCarDto> q = cars;

            if (filter.TransmissionTypes?.Count > 0)
                q = q.Where(c => filter.TransmissionTypes.Any(t =>
                    !string.IsNullOrEmpty(c.TransmissionType) &&
                    c.TransmissionType.Contains(t, StringComparison.OrdinalIgnoreCase)));

            if (filter.FuelTypes?.Count > 0)
                q = q.Where(c => filter.FuelTypes.Any(f =>
                    !string.IsNullOrEmpty(c.FuelType) &&
                    c.FuelType.Contains(f, StringComparison.OrdinalIgnoreCase)));

            if (!string.IsNullOrWhiteSpace(filter.Segment))
                q = q.Where(c => !string.IsNullOrEmpty(c.Segment) &&
                    c.Segment.Contains(filter.Segment, StringComparison.OrdinalIgnoreCase));

            // Firma (RentalCompany) artık logo UUID'sinden gerçek ada çevrildiği için yerel
            // olarak da filtrelenebilir.
            if (!string.IsNullOrWhiteSpace(filter.RentalCompany))
                q = q.Where(c => !string.IsNullOrEmpty(c.RentalCompany) &&
                    c.RentalCompany.Contains(filter.RentalCompany, StringComparison.OrdinalIgnoreCase));

            // Marka: araç modeli marka adıyla başlar/içerir (ör. "Fiat Egea" -> Fiat).
            if (!string.IsNullOrWhiteSpace(filter.Brand))
                q = q.Where(c => !string.IsNullOrEmpty(c.CarModel) &&
                    c.CarModel.Contains(filter.Brand, StringComparison.OrdinalIgnoreCase));

            if (filter.MinPrice.HasValue)
                q = q.Where(c => c.Price >= filter.MinPrice.Value);

            if (filter.MaxPrice.HasValue)
                q = q.Where(c => c.Price <= filter.MaxPrice.Value);

            var result = q.ToList();
            LogHelper.Info($"Yerel filtre uygulandı: {cars.Count} -> {result.Count} araç.");
            return result;
        }

        // ---- Yardımcı metotlar ----

        // Yolcu360 takvimindeki ay başlığı formatı: "Haziran 2026" (Türkçe ay adı + yıl).
        private static readonly string[] TurkishMonths =
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        private static string TurkishMonthHeader(DateTime d) => $"{TurkishMonths[d.Month - 1]} {d.Year}";

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
        /// "1.234,56 TL" / "₺1.234" / "EUR 120" gibi metinlerden sayısal fiyat ve para
        /// birimini ayıklar. Türkçe binlik/ondalık biçimini de destekler.
        /// </summary>
        private static (decimal price, string currency) ParsePrice(string priceText)
        {
            if (string.IsNullOrWhiteSpace(priceText))
                return (0m, "TL");

            var currency = "TL";
            if (priceText.Contains("€") || priceText.Contains("EUR", StringComparison.OrdinalIgnoreCase)) currency = "EUR";
            else if (priceText.Contains("$") || priceText.Contains("USD", StringComparison.OrdinalIgnoreCase)) currency = "USD";
            else if (priceText.Contains("₺") || priceText.Contains("TL", StringComparison.OrdinalIgnoreCase)) currency = "TL";

            // Sadece rakam, nokta ve virgülü tut.
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

    /// <summary>Otomasyon sırasında kullanıcıya gösterilebilecek anlamlı hata.</summary>
    public class AutomationException : Exception
    {
        public AutomationException(string message) : base(message) { }
    }
}
