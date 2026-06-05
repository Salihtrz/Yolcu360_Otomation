namespace Yolcu360.Common.Automation
{
    /// <summary>
    /// Yolcu360 sitesindeki DOM selector'larının TEK merkezi yeri.
    ///
    /// Önemli: Her alan için tek bir selector değil, alternatif selector LİSTESİ
    /// tutulur. Site yapısı değişirse otomasyon sırayla dener; hiçbiri çalışmazsa
    /// koda değil, sadece bu dosyaya dokunmak yeterli olur.
    ///
    /// Not: Aşağıdaki selector'lar genel/sezgisel tahminlerdir. Site güncellendiğinde
    /// gerçek DOM'a göre buradaki diziler güncellenmelidir.
    /// </summary>
    public static class Yolcu360Selectors
    {
        // ---- Alış lokasyonu ----
        // Gerçek site (2026): input id="inputPickUpLocation" data-cms-key="placeholder_pickup_location"
        public static readonly string[] PickupLocationSelectors =
        {
            "#inputPickUpLocation",
            "input[data-cms-key='placeholder_pickup_location']",
            "input[id*='PickUpLocation']",
            "input[placeholder*='Alış']",
            "input[placeholder*='Nereden']"
        };

        // Lokasyon yazıldıktan sonra açılan otomatik tamamlama (autocomplete) seçenekleri.
        public static readonly string[] LocationSuggestionSelectors =
        {
            ".search-autocomplete .location-item",
            ".location-item",
            ".autocomplete-item",
            ".suggestion-item",
            "li[role='option']"
        };

        // ---- Alış tarihi ----
        public static readonly string[] PickupDateSelectors =
        {
            "input[name='pickupDate']",
            "input[placeholder*='Alış Tarihi']",
            "input[id*='pickupDate']",
            ".pickup-date input",
            "input[type='date']"
        };

        // ---- Alış saati ----
        public static readonly string[] PickupTimeSelectors =
        {
            "select[name='pickupTime']",
            "input[name='pickupTime']",
            "input[placeholder*='Alış Saati']",
            ".pickup-time select",
            ".pickup-time input"
        };

        // ---- Dönüş tarihi ----
        public static readonly string[] ReturnDateSelectors =
        {
            "input[name='dropoffDate']",
            "input[name='returnDate']",
            "input[placeholder*='Dönüş Tarihi']",
            "input[id*='returnDate']",
            "input[id*='dropoff']",
            ".return-date input"
        };

        // ---- Dönüş saati ----
        public static readonly string[] ReturnTimeSelectors =
        {
            "select[name='dropoffTime']",
            "select[name='returnTime']",
            "input[name='returnTime']",
            "input[placeholder*='Dönüş Saati']",
            ".return-time select",
            ".return-time input"
        };

        // ---- Arama (Ara) butonu ----
        // Gerçek site (2026): id="search" data-cms-key="search" (metin: "ARAÇ ARA")
        public static readonly string[] SearchButtonSelectors =
        {
            "#search",
            "[data-cms-key='search']",
            "button[type='submit']",
            "button[class*='search']",
            ".search-btn"
        };

        // Alış tarihi widget'ını (takvimi) açan tetikleyici eleman.
        public static readonly string[] PickupDateTriggerSelectors =
        {
            "[data-cms-key='pickup_date']",
            "[data-cms-key='pickup_date_time']"
        };

        // ---- Sonuç kartları (her araç bir kart) ----
        // Gerçek site (2026): <div class="py-2 car-card">
        public static readonly string[] ResultCardSelectors =
        {
            ".car-card",
            "div[class*='car-card']",
            ".vehicle-card",
            ".search-result-item"
        };

        // ---- Kart içi alanlar (kart elemanına göre relative aranır) ----
        // Model: <div class="text-dark-gray text-lg font-bold">Opel Corsa</div>
        public static readonly string[] CarModelSelectors =
        {
            ".text-dark-gray.text-lg.font-bold",
            ".text-lg.font-bold",
            ".car-name",
            ".car-model",
            "h3"
        };

        // Firma site'de logo görseli olarak gösterilir (metin ad yok). Logo URL'i alınır.
        public static readonly string[] RentalCompanySelectors =
        {
            "figure img[src*='supplier']",
            "img[src*='supplier']",
            "img[src*='vendor']"
        };

        // Vites: <span data-cms-key="filter_transmission_1"><i class="icon-gear-type"></i>Manuel</span>
        public static readonly string[] TransmissionSelectors =
        {
            "[data-cms-key^='filter_transmission']",
            ".icon-gear-type",
            "[class*='transmission']"
        };

        // Yakıt: <span data-cms-key="filter_fuel_1"><i class="icon-gas-type"></i>Benzin</span>
        public static readonly string[] FuelSelectors =
        {
            "[data-cms-key^='filter_fuel']",
            ".icon-gas-type",
            "[class*='fuel']"
        };

        // Segment/sınıf: <span data-cms-key="filter_class_type_1"><i class="icon-confort"></i> Ekonomi</span>
        public static readonly string[] SegmentSelectors =
        {
            "[data-cms-key^='filter_class_type']",
            "[class*='segment']",
            "[class*='category']"
        };

        // Fiyat (toplam): <div id="car_total_price" ...>6.273 TL</div>
        public static readonly string[] PriceSelectors =
        {
            "#car_total_price",
            "[id='car_total_price']",
            "[data-cms-key='text_daily_price2']",
            "[class*='price']"
        };

        // Araç görseli: <img src="https://integration-static.yolcu360.com/vehicle/...png">
        public static readonly string[] CarImageSelectors =
        {
            "img[src*='/vehicle/']",
            "img[src*='vehicle']",
            "img[class*='car']",
            "img"
        };

        // ---- Sonuç sayfası filtre elemanları (site üzerinde filtreleme için) ----
        public static readonly string[] FilterTransmissionSelectors =
        {
            "input[name*='transmission']",
            "label[class*='transmission'] input",
            "[data-filter='transmission'] input"
        };

        public static readonly string[] FilterFuelSelectors =
        {
            "input[name*='fuel']",
            "label[class*='fuel'] input",
            "[data-filter='fuel'] input"
        };

        public static readonly string[] FilterCompanySelectors =
        {
            "input[name*='supplier']",
            "input[name*='company']",
            "[data-filter='supplier'] input"
        };

        // ---- Login ile ilgili elemanlar (sadece tespit için; otomasyon login yapmaz) ----
        public static readonly string[] LoginButtonSelectors =
        {
            "a[href*='login']",
            "button[class*='login']",
            "a[class*='login']",
            "[data-action='login']"
        };

        // Kullanıcının giriş yapıp yapmadığını sezmek için (manuel login tespiti).
        public static readonly string[] LoggedInIndicatorSelectors =
        {
            "a[href*='logout']",
            "a[href*='hesabim']",
            ".user-menu",
            "[class*='account-menu']",
            "[class*='profile']"
        };
    }
}
