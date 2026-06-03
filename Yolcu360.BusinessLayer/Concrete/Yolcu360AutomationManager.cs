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

            // HER ARAMA TEMİZ FORMDAN BAŞLAR. Önceki aramadan sonra tarayıcı ya sonuç sayfasında
            // (/arac-kiralama/search) ya da eski lokasyon/saat değerleri girili ana sayfada kalmış
            // olabilir. Bu "bayat bağlam" yüzünden ikinci aramada site, yeni lokasyon/saat yerine
            // önceki aramayı (ör. Yenibosna 10:00) koruyabiliyordu. Bunu kökten önlemek için ana
            // sayfa HER SEFERINDE yeniden yüklenir; böylece form boş ve tutarlı olur.
            Report("Site hazır olması bekleniyor...");
            await _browser.LoadUrlAsync(Yolcu360Constants.HomeUrl, ct);
            await _browser.WaitForPageLoadAsync(ct);

            var hasLocation = await _browser.WaitForElementAsync(
                Yolcu360Selectors.PickupLocationSelectors, Yolcu360Constants.DefaultWaitTimeoutSeconds, ct);
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

            // 2) Autocomplete önerisini seç. ÖNEMLİ: ilk öneriyi değil, yazılan metne EN İYİ eşleşeni
            //    tıkla (ör. "İstanbul - Yenibosna" yazınca ilk öneri "İstanbul Havalimanı" olabiliyordu).
            if (suggestionsOpened)
            {
                Report("Lokasyon önerisi seçiliyor...");
                await _browser.EvaluateBoolAsync(
                    JsHelper.BuildClickBestLocationSuggestionScript(
                        Yolcu360Selectors.LocationSuggestionSelectors, request.PickupLocation), ct);

                // DOĞRULAMA: seçim sonrası input'a yerleşen değeri oku ve logla. Beklenen ile
                // sitedeki gerçek lokasyon eşleşmiyorsa burada görünür (kör kalmayalım).
                await Task.Delay(700, ct);
                var committedLoc = await _browser.EvaluateStringAsync(
                    JsHelper.BuildGetInputValueScript(Yolcu360Selectors.PickupLocationSelectors), ct);
                LogHelper.Info($"Lokasyon seçildi → istenen: '{request.PickupLocation}' | sitedeki input: '{committedLoc}'");
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
            await SetSiteTimeAsync("Alış Saati", 0, request.PickupTime, ct);
            await SetSiteTimeAsync("Dönüş Saati", 1, request.ReturnTime, ct);

            // 5) Aramayı tetikle
            Report("Arama yapılıyor...");
            var clicked = await _browser.ClickElementAsync(Yolcu360Selectors.SearchButtonSelectors, ct);
            if (!clicked)
                throw new AutomationException("Arama butonu bulunamadı. Site yapısı değişmiş olabilir.");

            // Arama yeni sayfaya/AJAX'a yol açar.
            await _browser.WaitForPageLoadAsync(ct);

            // Site bazen bir bilgi/uyarı modalı (ör. saat dilimi uyarısı, çerez bildirimi) gösterir
            // ve bu, sonuçların yüklenmesini/görünmesini engelleyebilir. Best-effort kapatılır.
            await Task.Delay(800, ct);
            await _browser.EvaluateBoolAsync(JsHelper.BuildDismissModalScript(), ct);

            Report("Sonuçlar bekleniyor...");
            var hasResults = await _browser.WaitForResultsAsync(
                Yolcu360Selectors.ResultCardSelectors, Yolcu360Constants.ResultsWaitTimeoutSeconds, ct);
            if (!hasResults)
            {
                // Modal yeniden denenir (geç açılmış olabilir), sonra kısa bir kez daha beklenir.
                await _browser.EvaluateBoolAsync(JsHelper.BuildDismissModalScript(), ct);
                hasResults = await _browser.WaitForResultsAsync(
                    Yolcu360Selectors.ResultCardSelectors, 8, ct);
            }
            if (!hasResults)
                throw new AutomationException(
                    "Sonuç bulunamadı. Seçtiğiniz tarih/saat geçmiş olabilir veya o lokasyon-zaman için araç yok. " +
                    "Lütfen ileri bir tarih/saat seçip tekrar deneyin.");

            Report("Araç bilgileri çekiliyor...");
            // Not: ScrapeCurrentResultsAsync, kazımadan önce tüm sonuçları (lazy-load) yükler.
            var cars = await ScrapeCurrentResultsAsync(request, ct);

            Report($"İşlem tamamlandı. {cars.Count} araç bulundu.");
            return cars;
        }

        /// <summary>
        /// Site saat seçicisinden hedef saati seçer (yarım saate yuvarlanır). GERÇEK DOM (2026):
        /// sayfada 2 tetikleyici DIV (index 0 = Alış, 1 = Dönüş, metni "HH:MM", cursor:pointer);
        /// tıklayınca açılan menü body'ye taşınır ve seçenekler &lt;li&gt;HH:MM&lt;/li&gt; (48 adet).
        ///
        /// ÖNEMLİ: Menü YALNIZCA gerçek (CDP/trusted) tıklama ile açılır; JS .click() açmaz. Bu yüzden
        /// tetikleyici index ile bulunup CDP ile tıklanır, açılan menüde hedef li görünür alana
        /// kaydırılıp yine CDP ile tıklanır; her adımda GÖSTERİLEN değer okunup hedefle doğrulanır.
        /// </summary>
        private async Task SetSiteTimeAsync(string label, int index, TimeSpan time, CancellationToken ct)
        {
            var target = RoundToHalfHour(time);

            // Zaten doğru değer gösteriliyorsa dokunma.
            if (await GetDisplayedTimeAsync(index, ct) == target)
            {
                LogHelper.Info($"Saat zaten doğru ('{label}' = {target}).");
                return;
            }

            // SABIRLI EŞLEŞTİRME: birkaç tur. Her turda menüyü (CDP) aç; hedef li'yi görünür yapıp
            // önce JS .click() sonra CDP gerçek tıklama ile seç; her denemede gösterilen değeri doğrula.
            var ok = false;
            for (int outer = 1; outer <= 4 && !ok; outer++)
            {
                if (!await OpenHourListAsync(index, ct))
                {
                    LogHelper.Warning($"Saat menüsü açılamadı ('{label}'), tur {outer}.");
                    continue;
                }

                for (int step = 1; step <= 6 && !ok; step++)
                {
                    // (a) Menü açıkken li.click() (untrusted) çoğu zaman yeterli.
                    await _browser.EvaluateBoolAsync(JsHelper.BuildClickHourOptionJsScript(target), ct);
                    await Task.Delay(300, ct);
                    if (await GetDisplayedTimeAsync(index, ct) == target) { ok = true; break; }

                    // (b) CDP gerçek tıklama (li görünür alana kaydırılıp koordinatından).
                    if (!await IsHourListOpenAsync(ct))
                        if (!await OpenHourListAsync(index, ct)) break;
                    var rect = ParsePoint(await _browser.EvaluateStringAsync(JsHelper.BuildGetHourOptionRectScript(target), ct));
                    if (rect != null)
                    {
                        await _browser.RealClickAtAsync(rect.X, rect.Y, ct);
                        await Task.Delay(350, ct);
                        if (await GetDisplayedTimeAsync(index, ct) == target) { ok = true; break; }
                    }
                    else
                    {
                        // Hedef henüz render edilmemişse menüyü aşağı kaydır.
                        await _browser.EvaluateBoolAsync(JsHelper.BuildScrollHourListScript(), ct);
                        await Task.Delay(200, ct);
                    }
                }
            }

            if (ok)
                LogHelper.Info($"Saat eşleştirildi ('{label}' = {target}).");
            else
                LogHelper.Warning($"Saat EŞLEŞTİRİLEMEDİ ('{label}' = {target}). Varsayılan saat kalmış olabilir.");

            // Açık kalan menüyü kapat (Escape benzeri: nötr bir noktaya gerçek tıklama).
            if (await IsHourListOpenAsync(ct))
            {
                await _browser.RealClickAtAsync(5, 5, ct);
                await Task.Delay(250, ct);
            }
        }

        /// <summary>index. tetikleyicinin o an gösterdiği değeri (HH:MM) okur; doğrulama için.</summary>
        private async Task<string> GetDisplayedTimeAsync(int index, CancellationToken ct)
            => (await _browser.EvaluateStringAsync(JsHelper.BuildGetDisplayedTimeByIndexScript(index), ct))?.Trim() ?? "";

        /// <summary>Saat menüsü (metni HH:MM olan ≥10 li) açık mı?</summary>
        private async Task<bool> IsHourListOpenAsync(CancellationToken ct)
            => await _browser.EvaluateBoolAsync(JsHelper.BuildIsTimeMenuOpenScript(), ct);

        /// <summary>index. saat tetikleyicisine GERÇEK (CDP) tıklayarak menüyü açar; birkaç kez dener.</summary>
        private async Task<bool> OpenHourListAsync(int index, CancellationToken ct)
        {
            var pt = ParsePoint(await _browser.EvaluateStringAsync(JsHelper.BuildGetTimeTriggerRectByIndexScript(index), ct));
            if (pt == null) return false;
            for (int i = 1; i <= 3; i++)
            {
                await _browser.RealClickAtAsync(pt.X, pt.Y, ct);
                await Task.Delay(600, ct);
                if (await IsHourListOpenAsync(ct)) return true;
                pt = ParsePoint(await _browser.EvaluateStringAsync(JsHelper.BuildGetTimeTriggerRectByIndexScript(index), ct)) ?? pt;
            }
            return false;
        }

        /// <summary>"{x,y}" JSON'unu (BuildGet*RectScript çıktısı) noktaya çevirir; boş/geçersizse null.</summary>
        private static RectPoint ParsePoint(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonConvert.DeserializeObject<RectPoint>(json); }
            catch { return null; }
        }

        private sealed class RectPoint
        {
            public double X { get; set; }
            public double Y { get; set; }
        }

        /// <summary>Saati en yakın yarım saate yuvarlayıp "HH:mm" döndürür (site listesi 30 dk aralıklı).</summary>
        private static string RoundToHalfHour(TimeSpan t)
        {
            var totalMinutes = (int)Math.Round(t.TotalMinutes / 30.0) * 30;
            if (totalMinutes >= 24 * 60) totalMinutes = 23 * 60 + 30; // gün taşmasını engelle
            var rounded = TimeSpan.FromMinutes(totalMinutes);
            return $"{rounded.Hours:D2}:{rounded.Minutes:D2}";
        }

        /// <summary>
        /// Lazy-load ile TÜM sonuçları yükler: kart sayısı artık ARTMAYANA kadar (üst üste 3 kez aynı)
        /// sayfayı aşağı kaydırır. Böylece site 56 araç gösterirken uygulamada 20 kalması önlenir.
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

        public async Task<List<ResultCarDto>> ScrapeCurrentResultsAsync(SearchRequestDto request, CancellationToken ct = default)
        {
            // Kazımadan ÖNCE tüm sonuçları yükle (lazy-load): hem ilk aramada hem filtre
            // uygulandıktan sonra liste tamamlanmadan kazınmasın (site 56 → uygulama 20 sorunu).
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

            // Vites (bilinen tüm id'ler için hedef durumu uygula → seçilmeyenler de kapanır).
            // Id'ler UI'dan/siteden gelir; uygulamada ad→id sabit eşlemesi YOK.
            var wantTrans = (filter.TransmissionIds ?? new()).ToHashSet();
            foreach (var id in new[] { "1", "2" })
                await _browser.EvaluateBoolAsync(JsHelper.BuildSetCheckboxScript($"filter-transmission.{id}", wantTrans.Contains(id)), ct);

            // Yakıt
            var wantFuel = (filter.FuelIds ?? new()).ToHashSet();
            foreach (var id in new[] { "1", "2", "5", "7", "8", "11" })
                await _browser.EvaluateBoolAsync(JsHelper.BuildSetCheckboxScript($"filter-fuel.{id}", wantFuel.Contains(id)), ct);

            // Firma/Marka/Model (tek seçim, siteden dinamik gelen id son ekiyle): seçileni bırak,
            // grubun diğer tüm kutularını kapat.
            await _browser.EvaluateBoolAsync(JsHelper.BuildResetCheckboxGroupScript("filter-vendor.",
                string.IsNullOrWhiteSpace(filter.VendorId) ? Array.Empty<string>() : new[] { $"filter-vendor.{filter.VendorId.Trim()}" }), ct);
            await _browser.EvaluateBoolAsync(JsHelper.BuildResetCheckboxGroupScript("filter-brand.",
                string.IsNullOrWhiteSpace(filter.BrandId) ? Array.Empty<string>() : new[] { $"filter-brand.{filter.BrandId.Trim()}" }), ct);
            await _browser.EvaluateBoolAsync(JsHelper.BuildResetCheckboxGroupScript("filter-model.",
                string.IsNullOrWhiteSpace(filter.ModelId) ? Array.Empty<string>() : new[] { $"filter-model.{filter.ModelId.Trim()}" }), ct);

            // Koltuk Sayısı (checkbox grubu 'filter-seat.N'): tek seçim → grubu sıfırla, seçileni bırak.
            var seatKeep = string.IsNullOrWhiteSpace(filter.SeatCount)
                ? Array.Empty<string>() : new[] { $"filter-seat.{filter.SeatCount.Trim()}" };
            await _browser.EvaluateBoolAsync(JsHelper.BuildResetCheckboxGroupScript("filter-seat.", seatKeep), ct);

            // Araç Teslim Şekli (checkbox grubu 'filter-delivery_type.N').
            var deliveryKeep = string.IsNullOrWhiteSpace(filter.DeliveryType)
                ? Array.Empty<string>() : new[] { $"filter-delivery_type.{filter.DeliveryType.Trim()}" };
            await _browser.EvaluateBoolAsync(JsHelper.BuildResetCheckboxGroupScript("filter-delivery_type.", deliveryKeep), ct);

            // KM Sınırı ve Depozito: RADIO grupları (checkbox değil) → BuildSetRadioScript.
            await _browser.EvaluateBoolAsync(JsHelper.BuildSetRadioScript("filter-distance_limit.",
                string.IsNullOrWhiteSpace(filter.KmLimit) ? null : $"filter-distance_limit.{filter.KmLimit.Trim()}"), ct);
            await _browser.EvaluateBoolAsync(JsHelper.BuildSetRadioScript("filter-provision.",
                string.IsNullOrWhiteSpace(filter.Deposit) ? null : $"filter-provision.{filter.Deposit.Trim()}"), ct);

            // Site XHR ile listeyi güncellesin
            await Task.Delay(1500, ct);
            await _browser.WaitForResultsAsync(
                Yolcu360Selectors.ResultCardSelectors, Yolcu360Constants.ResultsWaitTimeoutSeconds, ct);

            var applied = filter.HasSiteFilter;
            LogHelper.Info(applied ? "Site üzerinde filtreler ayarlandı." : "Site filtreleri temizlendi.");
            return applied;
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
