namespace Yolcu360.DtoLayer.SearchDto
{
    /// <summary>Site filtre panelinden dinamik okunan tek bir seçenek (ör. "Garenta", id "garenta").</summary>
    public class SiteFilterOptionDto
    {
        /// <summary>Tam input id'si, ör. "filter-brand.63" / "filter-vendor.garenta".</summary>
        public string Id { get; set; }
        /// <summary>Kullanıcıya gösterilen etiket (adet "(8)" ayıklanmış), ör. "Audi".</summary>
        public string Label { get; set; }
    }

    /// <summary>Site filtre panelindeki bir bölüm (ör. Araç Markası) ve seçenekleri.</summary>
    public class SiteFilterSectionDto
    {
        /// <summary>data-cms-key, ör. "filter_brand", "filter_vendor", "filter_model".</summary>
        public string Key { get; set; }
        /// <summary>Bölüm başlığı, ör. "Araç Markası".</summary>
        public string Title { get; set; }
        /// <summary>"checkbox" veya "radio".</summary>
        public string Type { get; set; }
        public List<SiteFilterOptionDto> Options { get; set; } = new();
    }
}
