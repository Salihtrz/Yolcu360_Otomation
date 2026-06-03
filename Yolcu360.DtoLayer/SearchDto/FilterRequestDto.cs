namespace Yolcu360.DtoLayer.SearchDto
{
    /// <summary>
    /// Sonuç listesine uygulanacak filtre kriterleri.
    ///
    /// TÜM filtreler YALNIZCA SİTE üzerinde uygulanır (uygulama içi/local filtreleme YOK):
    /// Yolcu360'ın kendi filtre kutuları/radio'ları ayarlanır ve sonuç yeniden kazınır
    /// (vites, yakıt, marka, şirket/vendor, model, koltuk, km, teslim, depozito). Değerler
    /// siteden DİNAMİK okunan id son ekleriyle (filter-*.<id>) taşınır; uygulamada sabit liste yoktur.
    /// </summary>
    public class FilterRequestDto
    {
        // ---- Site filtreleri (id son ekleriyle; boş = uygulanmaz) ----
        public List<string> TransmissionIds { get; set; } = new(); // ör. "1","2"
        public List<string> FuelIds { get; set; } = new();         // ör. "1","2","5","7","8","11"
        public string BrandId { get; set; }                        // filter-brand.<id>  ör. "63"
        public string VendorId { get; set; }                       // filter-vendor.<slug> ör. "garenta"
        public string ModelId { get; set; }                        // filter-model.<id>  ör. "374"
        public string SeatCount { get; set; }                      // filter-seat.<n>
        public string KmLimit { get; set; }                        // filter-distance_limit.<range>
        public string DeliveryType { get; set; }                   // filter-delivery_type.<id>
        public string Deposit { get; set; }                        // filter-provision.<range> (radio)

        /// <summary>Site üzerinde uygulanacak (re-scrape gerektiren) bir filtre var mı?</summary>
        public bool HasSiteFilter =>
            (TransmissionIds?.Count ?? 0) > 0 ||
            (FuelIds?.Count ?? 0) > 0 ||
            !string.IsNullOrWhiteSpace(BrandId) ||
            !string.IsNullOrWhiteSpace(VendorId) ||
            !string.IsNullOrWhiteSpace(ModelId) ||
            !string.IsNullOrWhiteSpace(SeatCount) ||
            !string.IsNullOrWhiteSpace(KmLimit) ||
            !string.IsNullOrWhiteSpace(DeliveryType) ||
            !string.IsNullOrWhiteSpace(Deposit);
    }
}
