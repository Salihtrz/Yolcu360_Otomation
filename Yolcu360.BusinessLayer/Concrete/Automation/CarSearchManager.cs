using Yolcu360.BusinessLayer.Abstract.Automation;
using Yolcu360.BusinessLayer.Abstract.Browser;
using Yolcu360.Common.Automation;
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
    /// Yolcu360 arama formunu doldurur, aramayı tetikler ve sonuçları kazıtır. Kazıma işini
    /// <see cref="ICarScrapingService"/>'e devreder. Site üzerinde filtre uygulamayı da yönetir.
    ///
    /// Akış (bkz. <see cref="SearchCarsAsync"/>): sayfayı aç → lokasyon → tarih → saat → aramayı
    /// gönder ve sonuçları bekle → kazı. Her adım ayrı bir private metottur (sunumda okunabilir olsun).
    ///
    /// ETİK/GÜVENLİK: Yalnızca normal kullanıcı etkileşimini taklit eder; login/SMS/captcha ile
    /// uğraşmaz, yoğun istek atmaz.
    /// </summary>
    public class CarSearchManager : ICarSearchService
    {
        private readonly ICefSharpBrowserService _browser;
        private readonly ICarScrapingService _scraping;

        public CarSearchManager(ICefSharpBrowserService browser, ICarScrapingService scraping)
        {
            _browser = browser;
            _scraping = scraping;
        }

        /// <summary>
        /// ARAMA GİRİŞ NOKTASI. Formu doldurur, aramayı tetikler, sonuçları bekler ve kazınmış
        /// araç listesini döner. Adımlar yüksek seviyede okunur:
        /// </summary>
        public async Task<List<ResultCarDto>> SearchCarsAsync(
            SearchRequestDto request, IProgress<string> progress, CancellationToken ct = default)
        {
            await OpenSearchPageAsync(progress, ct);
            await FillPickupLocationAsync(request, progress, ct);
            await SelectDatesAsync(request, progress, ct);
            await SelectTimesAsync(request, ct);
            await SubmitSearchAndWaitResultsAsync(progress, ct);

            Report(progress, "Araç bilgileri çekiliyor...");
            var cars = await _scraping.ScrapeCarsAsync(request, ct);
            Report(progress, $"İşlem tamamlandı. {cars.Count} araç bulundu.");
            return cars;
        }

        // ---- Arama adımları (SearchCarsAsync sırasıyla çağırır) ----

        /// <summary>
        /// Ana sayfayı HER aramada yeniden yükler (bayat bağlamı önler: önceki lokasyon/saat kalmasın)
        /// ve arama formunun (lokasyon input'u) hazır olmasını bekler.
        /// </summary>
        private async Task OpenSearchPageAsync(IProgress<string> progress, CancellationToken ct)
        {
            Report(progress, "Site hazır olması bekleniyor...");
            await _browser.LoadUrlAsync(Yolcu360Constants.HomeUrl, ct);
            await _browser.WaitForPageLoadAsync(ct);

            var hasLocation = await _browser.WaitForElementAsync(
                Yolcu360Selectors.PickupLocationSelectors, Yolcu360Constants.DefaultWaitTimeoutSeconds, ct);
            if (!hasLocation)
                throw new AutomationException("Arama alanı bulunamadı. Site yapısı değişmiş olabilir veya sayfa açılmamış olabilir.");

            // SPA (React/Vue) input'a olay dinleyicilerini bağlasın diye kısa "settle" süresi.
            await Task.Delay(2500, ct);
        }

        /// <summary>Alış yerini gerçek klavye yazımıyla girer ve metne en iyi eşleşen autocomplete önerisini seçer.</summary>
        private async Task FillPickupLocationAsync(SearchRequestDto request, IProgress<string> progress, CancellationToken ct)
        {
            Report(progress, "Alış yeri giriliyor...");
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

            if (!suggestionsOpened)
                throw new AutomationException(
                    "Lokasyon önerisi açılmadı. Lütfen alış yerini daha açık yazın (örn. 'İstanbul Havalimanı').");

            // İlk öneriyi değil, yazılan metne EN İYİ eşleşeni tıkla.
            Report(progress, "Lokasyon önerisi seçiliyor...");
            await _browser.EvaluateBoolAsync(
                JsHelper.BuildClickBestLocationSuggestionScript(
                    Yolcu360Selectors.LocationSuggestionSelectors, request.PickupLocation), ct);

            // DOĞRULAMA: seçim sonrası input'a yerleşen değeri logla (kör kalmayalım).
            await Task.Delay(700, ct);
            var committedLoc = await _browser.EvaluateStringAsync(
                JsHelper.BuildGetInputValueScript(Yolcu360Selectors.PickupLocationSelectors), ct);
            LogHelper.Info($"Lokasyon seçildi → istenen: '{request.PickupLocation}' | sitedeki input: '{committedLoc}'");
        }

        /// <summary>Takvimi açar ve alış/dönüş günlerini tıklar (aralık seçici).</summary>
        private async Task SelectDatesAsync(SearchRequestDto request, IProgress<string> progress, CancellationToken ct)
        {
            Report(progress, "Tarihler seçiliyor...");
            await _browser.ClickElementAsync(Yolcu360Selectors.PickupDateTriggerSelectors, ct);

            if (!await _browser.WaitForElementAsync(new[] { ".month-header" }, 8, ct))
                throw new AutomationException("Tarih takvimi açılamadı. Site yapısı değişmiş olabilir.");

            var pickupOk = await _browser.EvaluateBoolAsync(
                JsHelper.BuildClickCalendarDayScript(TurkishMonthHeader(request.PickupDate), request.PickupDate.Day), ct);
            var returnOk = await _browser.EvaluateBoolAsync(
                JsHelper.BuildClickCalendarDayScript(TurkishMonthHeader(request.ReturnDate), request.ReturnDate.Day), ct);

            if (!pickupOk || !returnOk)
                LogHelper.Warning($"Takvimde gün seçimi tam yapılamadı (pickup={pickupOk}, return={returnOk}). " +
                                  "Seçilen tarih takvimde görünür aralıkta olmayabilir.");
        }

        /// <summary>Alış ve dönüş saatlerini (yarım saate yuvarlanmış) site saat seçicisinden seçer.</summary>
        private async Task SelectTimesAsync(SearchRequestDto request, CancellationToken ct)
        {
            await Task.Delay(1000, ct); // takvim panelinin oturması için
            await SetSiteTimeAsync("Alış Saati", 0, request.PickupTime, ct);
            await SetSiteTimeAsync("Dönüş Saati", 1, request.ReturnTime, ct);
        }

        /// <summary>Aramayı tetikler, sonuç sayfasını bekler, araya giren modalları kapatır ve sonuçları bekler.</summary>
        private async Task SubmitSearchAndWaitResultsAsync(IProgress<string> progress, CancellationToken ct)
        {
            Report(progress, "Arama yapılıyor...");
            var clicked = await _browser.ClickElementAsync(Yolcu360Selectors.SearchButtonSelectors, ct);
            if (!clicked)
                throw new AutomationException("Arama butonu bulunamadı. Site yapısı değişmiş olabilir.");

            await _browser.WaitForPageLoadAsync(ct);

            // Site bazen bilgi/uyarı modalı gösterir ve sonuçların görünmesini engeller. Best-effort kapat.
            await Task.Delay(800, ct);
            await _browser.EvaluateBoolAsync(JsHelper.BuildDismissModalScript(), ct);

            Report(progress, "Sonuçlar bekleniyor...");
            var hasResults = await _browser.WaitForResultsAsync(
                Yolcu360Selectors.ResultCardSelectors, Yolcu360Constants.ResultsWaitTimeoutSeconds, ct);
            if (!hasResults)
            {
                await _browser.EvaluateBoolAsync(JsHelper.BuildDismissModalScript(), ct);
                hasResults = await _browser.WaitForResultsAsync(Yolcu360Selectors.ResultCardSelectors, 8, ct);
            }
            if (!hasResults)
                throw new AutomationException(
                    "Sonuç bulunamadı. Seçtiğiniz tarih/saat geçmiş olabilir veya o lokasyon-zaman için araç yok. " +
                    "Lütfen ileri bir tarih/saat seçip tekrar deneyin.");
        }

        // ---- Filtre uygulama ----

        public async Task<bool> ApplyFiltersOnWebsiteAsync(FilterRequestDto filter, CancellationToken ct = default)
        {
            // Yolcu360 sonuç sayfasındaki gerçek filtre kutuları (2026):
            //   input id="filter-transmission.1" (Manuel) / ".2" (Otomatik)
            //   input id="filter-fuel.1" (Benzin) / ".2" (Dizel) / ".5" (LPG) / ".7" (Hybrid) / ".8" (Benzin/Dizel) / ".11" (Elektrik)
            //   input id="filter-vendor.<slug>" / "filter-brand.<id>" / "filter-model.<id>"
            // Kutular durum-bazlı (idempotent) ayarlanır — yalnızca mevcut durum hedeften farklıysa
            // değişir; böylece ikinci "Uygula"da zaten seçili filtreler kapanmaz.
            if (filter == null) return false;

            // Vites (bilinen tüm id'ler için hedef durumu uygula → seçilmeyenler de kapanır).
            var wantTrans = (filter.TransmissionIds ?? new()).ToHashSet();
            foreach (var id in new[] { "1", "2" })
                await _browser.EvaluateBoolAsync(JsHelper.BuildSetCheckboxScript($"filter-transmission.{id}", wantTrans.Contains(id)), ct);

            // Yakıt
            var wantFuel = (filter.FuelIds ?? new()).ToHashSet();
            foreach (var id in new[] { "1", "2", "5", "7", "8", "11" })
                await _browser.EvaluateBoolAsync(JsHelper.BuildSetCheckboxScript($"filter-fuel.{id}", wantFuel.Contains(id)), ct);

            // Firma/Marka/Model (tek seçim): seçileni bırak, grubun diğer tüm kutularını kapat.
            await _browser.EvaluateBoolAsync(JsHelper.BuildResetCheckboxGroupScript("filter-vendor.",
                string.IsNullOrWhiteSpace(filter.VendorId) ? Array.Empty<string>() : new[] { $"filter-vendor.{filter.VendorId.Trim()}" }), ct);
            await _browser.EvaluateBoolAsync(JsHelper.BuildResetCheckboxGroupScript("filter-brand.",
                string.IsNullOrWhiteSpace(filter.BrandId) ? Array.Empty<string>() : new[] { $"filter-brand.{filter.BrandId.Trim()}" }), ct);
            await _browser.EvaluateBoolAsync(JsHelper.BuildResetCheckboxGroupScript("filter-model.",
                string.IsNullOrWhiteSpace(filter.ModelId) ? Array.Empty<string>() : new[] { $"filter-model.{filter.ModelId.Trim()}" }), ct);

            // Koltuk Sayısı (checkbox grubu): tek seçim → grubu sıfırla, seçileni bırak.
            var seatKeep = string.IsNullOrWhiteSpace(filter.SeatCount)
                ? Array.Empty<string>() : new[] { $"filter-seat.{filter.SeatCount.Trim()}" };
            await _browser.EvaluateBoolAsync(JsHelper.BuildResetCheckboxGroupScript("filter-seat.", seatKeep), ct);

            // Araç Teslim Şekli (checkbox grubu).
            var deliveryKeep = string.IsNullOrWhiteSpace(filter.DeliveryType)
                ? Array.Empty<string>() : new[] { $"filter-delivery_type.{filter.DeliveryType.Trim()}" };
            await _browser.EvaluateBoolAsync(JsHelper.BuildResetCheckboxGroupScript("filter-delivery_type.", deliveryKeep), ct);

            // KM Sınırı ve Depozito: RADIO grupları (checkbox değil).
            await _browser.EvaluateBoolAsync(JsHelper.BuildSetRadioScript("filter-distance_limit.",
                string.IsNullOrWhiteSpace(filter.KmLimit) ? null : $"filter-distance_limit.{filter.KmLimit.Trim()}"), ct);
            await _browser.EvaluateBoolAsync(JsHelper.BuildSetRadioScript("filter-provision.",
                string.IsNullOrWhiteSpace(filter.Deposit) ? null : $"filter-provision.{filter.Deposit.Trim()}"), ct);

            // Site XHR ile listeyi güncellesin.
            await Task.Delay(1500, ct);
            await _browser.WaitForResultsAsync(
                Yolcu360Selectors.ResultCardSelectors, Yolcu360Constants.ResultsWaitTimeoutSeconds, ct);

            var applied = filter.HasSiteFilter;
            LogHelper.Info(applied ? "Site üzerinde filtreler ayarlandı." : "Site filtreleri temizlendi.");
            return applied;
        }

        // ---- Saat seçimi yardımcıları ----

        /// <summary>
        /// Site saat seçicisinden hedef saati seçer (yarım saate yuvarlanır). Menü YALNIZCA gerçek
        /// (CDP/trusted) tıklama ile açılır; tetikleyici index ile bulunup tıklanır, hedef li görünür
        /// alana kaydırılıp seçilir; her adımda gösterilen değer doğrulanır.
        /// </summary>
        private async Task SetSiteTimeAsync(string label, int index, TimeSpan time, CancellationToken ct)
        {
            var target = RoundToHalfHour(time);

            if (await GetDisplayedTimeAsync(index, ct) == target)
            {
                LogHelper.Info($"Saat zaten doğru ('{label}' = {target}).");
                return;
            }

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
                        await _browser.EvaluateBoolAsync(JsHelper.BuildScrollHourListScript(), ct);
                        await Task.Delay(200, ct);
                    }
                }
            }

            if (ok)
                LogHelper.Info($"Saat eşleştirildi ('{label}' = {target}).");
            else
                LogHelper.Warning($"Saat EŞLEŞTİRİLEMEDİ ('{label}' = {target}). Varsayılan saat kalmış olabilir.");

            // Açık kalan menüyü kapat (nötr bir noktaya gerçek tıklama).
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

        /// <summary>"{x,y}" JSON'unu noktaya çevirir; boş/geçersizse null.</summary>
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

        // Yolcu360 takvimindeki ay başlığı: "Haziran 2026" (Türkçe ay adı + yıl).
        private static readonly string[] TurkishMonths =
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        private static string TurkishMonthHeader(DateTime d) => $"{TurkishMonths[d.Month - 1]} {d.Year}";

        private static void Report(IProgress<string> progress, string message)
        {
            progress?.Report(message);
            LogHelper.Info("Arama: " + message);
        }
    }
}
