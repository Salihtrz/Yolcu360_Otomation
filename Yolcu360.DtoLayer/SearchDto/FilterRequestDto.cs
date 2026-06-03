namespace Yolcu360.DtoLayer.SearchDto
{
    /// <summary>
    /// Sonuç listesine uygulanacak filtre kriterleri.
    /// Vites/yakıt birden çok seçilebildiği için liste tutulur; boş liste = filtre yok.
    /// </summary>
    public class FilterRequestDto
    {
        public List<string> TransmissionTypes { get; set; } = new();
        public List<string> FuelTypes { get; set; } = new();
        public string Segment { get; set; }
        public string RentalCompany { get; set; }
        public string Brand { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        public bool HasAnyFilter =>
            (TransmissionTypes?.Count ?? 0) > 0 ||
            (FuelTypes?.Count ?? 0) > 0 ||
            !string.IsNullOrWhiteSpace(Segment) ||
            !string.IsNullOrWhiteSpace(RentalCompany) ||
            !string.IsNullOrWhiteSpace(Brand) ||
            MinPrice.HasValue ||
            MaxPrice.HasValue;
    }
}
