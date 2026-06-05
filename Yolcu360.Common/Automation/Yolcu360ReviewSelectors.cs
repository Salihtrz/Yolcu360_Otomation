namespace Yolcu360.Common.Automation
{
    /// <summary>
    /// Firma değerlendirme / misafir yorumu okuma için DOM selector'ları.
    /// Mevcut <see cref="Yolcu360Selectors"/> mantığı bozulmadan, yorum özelliği için
    /// gereken selector'lar burada toplanır (alternatif listeler halinde).
    ///
    /// KAYNAK: 2026 gerçek Yolcu360 arama sonuç sayfası + "Değerlendirmeler" modalı DOM'u
    /// incelenerek çıkarılmıştır. Renk tabanlı Tailwind sınıfları (ör. bg-[#0068F4]) CSS
    /// selector'da kaçış gerektirip kırılgan olduğundan; mümkün olan her yerde KARARLI
    /// data-cms-key öznitelikleri ve yapısal sınıflar (.car-card, .modal-overlay) tercih edildi.
    ///
    /// ÖNEMLİ: Bu özellik SALT OKUNUR'dur. Hiçbir selector ile siteye yorum gönderilmez.
    /// </summary>
    public static class Yolcu360ReviewSelectors
    {
        // ---- Sonuç listesindeki araç kartı ve içindeki "Yorum" tetikleyicisi ----

        /// <summary>Tek bir araç kartı.</summary>
        public static readonly string[] CarCardSelectors =
        {
            ".car-card",
            "#car_card_list > div",
        };

        /// <summary>Kart içindeki firma logosu (src'de /supplier/&lt;uuid&gt;.png bulunur).</summary>
        public static readonly string[] CardSupplierLogoSelectors =
        {
            "img[src*='/supplier/']",
            "figure img[id='Car Rental']",
        };

        /// <summary>Kart içindeki araç model adı (ör. "Fiat Doblo").</summary>
        public static readonly string[] CardModelSelectors =
        {
            ".text-dark-gray.text-lg.font-bold",
            ".text-lg.font-bold",
        };

        /// <summary>Kart içindeki firma genel puan rozeti (ör. "4.5"). NOT: id kartlar arası tekrar eder.</summary>
        public static readonly string[] CardRatingValueSelectors =
        {
            "#card-car-rating-value",
            "[id='card-car-rating-value']",
        };

        /// <summary>
        /// "Yorum" tetikleyicisi — tıklanınca değerlendirme modalı açılır.
        /// data-cms-key='comment' olan kapsayıcı; içinde "400" + "Yorum" altı çizili span'ler.
        /// </summary>
        public static readonly string[] ReviewTriggerSelectors =
        {
            "[data-cms-key='comment']",
            ".car-card [data-cms-key='comment']",
        };

        // ---- Açılan değerlendirme modalı ----

        public static readonly string[] ReviewModalSelectors =
        {
            ".modal-overlay",
            ".modal",
        };

        public static readonly string[] ReviewModalTitleSelectors =
        {
            "[data-cms-key='comment_modal_title']",
        };

        public static readonly string[] ReviewCloseButtonSelectors =
        {
            ".modal-overlay .icon-close",
            ".modal .icon-close",
        };

        /// <summary>Modal başlığındaki firma adı (ör. "Garenta").</summary>
        public static readonly string[] SupplierNameSelectors =
        {
            ".modal-overlay .text-steel.font-semibold",
        };

        /// <summary>Modal başlığındaki teslim noktası / şube adı (ör. "Sabiha Gökçen Havalimanı").</summary>
        public static readonly string[] SupplierLocationSelectors =
        {
            ".modal-overlay .font-bold.text-dark-gray",
        };

        /// <summary>Toplam yorum sayısı metni (ör. "400 Yorum").</summary>
        public static readonly string[] ReviewCountSelectors =
        {
            ".modal-overlay .text-steel.font-semibold.text-sm",
        };

        // Alt kırılım puanları kararlı data-cms-key ile işaretli; sayısal değer aynı kutunun
        // ikinci satırındaki metnin sonundadır (JsHelper bunu ayrıştırır).
        public static readonly string[] CleanlinessRatingSelectors =
        {
            "[data-cms-key='comment_rating_text1']",
        };

        public static readonly string[] DeliverySpeedRatingSelectors =
        {
            "[data-cms-key='comment_rating_text2']",
        };

        public static readonly string[] StaffRatingSelectors =
        {
            "[data-cms-key='comment_rating_text3']",
        };

        // ---- Modal içindeki tekil yorum kartları ----

        /// <summary>
        /// Misafir değerlendirme başlığı (yorumların başladığı yer). Yorum kartları bunun
        /// sonrasındaki .bg-white.rounded-[10px] bloklarıdır.
        /// </summary>
        public static readonly string[] ReviewListTitleSelectors =
        {
            "[data-cms-key='comment_title']",
        };

        /// <summary>Tek yorum kartı (yorum metni içeren .bg-white blok).</summary>
        public static readonly string[] ReviewCardSelectors =
        {
            ".modal-overlay .bg-white.rounded-\\[10px\\]",
        };

        /// <summary>Yorum metni (&lt;p&gt;).</summary>
        public static readonly string[] ReviewTextSelectors =
        {
            ".border-t p.text-sm.font-semibold.text-black",
            "p.text-sm.font-semibold.text-black",
        };

        /// <summary>Yorum sahibinin baş harfleri (gri yuvarlak rozet).</summary>
        public static readonly string[] ReviewAuthorSelectors =
        {
            ".w-12.h-12.rounded-full",
        };

        /// <summary>Yorum tarihi (ör. "31 Mayıs 2026").</summary>
        public static readonly string[] ReviewDateSelectors =
        {
            ".text-sm.text-steel",
        };
    }
}
