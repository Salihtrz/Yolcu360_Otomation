namespace Yolcu360.Common.Constants
{
    /// <summary>
    /// Yolcu360 ile ilgili sabit değerler. Site adresleri tek yerde tutulur.
    /// </summary>
    public static class Yolcu360Constants
    {
        public const string BaseUrl = "https://www.yolcu360.com/";
        public const string HomeUrl = "https://www.yolcu360.com/";

        /// <summary>
        /// Giriş sayfası. Telefon alanı (#phn-input) doğrudan bu sayfada gelir; ayrı bir
        /// "Giriş Yap" butonuna tıklamaya gerek yoktur (doğrulandı, 2026).
        /// </summary>
        public const string LoginUrl = "https://www.yolcu360.com/login";

        /// <summary>Sayfa/element bekleme işlemleri için varsayılan zaman aşımı (saniye).</summary>
        public const int DefaultWaitTimeoutSeconds = 25;

        /// <summary>Sonuçların yüklenmesi için zaman aşımı (saniye).</summary>
        public const int ResultsWaitTimeoutSeconds = 40;
    }
}
