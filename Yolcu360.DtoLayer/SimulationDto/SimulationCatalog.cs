namespace Yolcu360.DtoLayer.SimulationDto
{
    /// <summary>
    /// Simülasyon ek hizmet kataloğu ve ödeme yöntemleri (Config). Hem UI (canlı fiyat gösterimi)
    /// hem de servis (kayıt sırasında yeniden hesaplama) aynı kaynağı kullanır.
    /// </summary>
    public static class SimulationCatalog
    {
        /// <summary>Ek hizmet adı → simüle fiyat (TL).</summary>
        public static IReadOnlyList<SimulatedRentalExtraDto> Extras { get; } = new List<SimulatedRentalExtraDto>
        {
            new() { ExtraName = "Ek sürücü",          ExtraPrice = 500m },
            new() { ExtraName = "Bebek koltuğu",      ExtraPrice = 300m },
            new() { ExtraName = "Navigasyon",         ExtraPrice = 250m },
            new() { ExtraName = "Kış lastiği",        ExtraPrice = 400m },
            new() { ExtraName = "Tam sigorta paketi", ExtraPrice = 1000m },
            new() { ExtraName = "Yol yardım paketi",  ExtraPrice = 350m },
            new() { ExtraName = "HGS / OGS paketi",   ExtraPrice = 200m },
        };

        public static IReadOnlyList<string> PaymentMethods { get; } = new[]
        {
            "Ofiste ödeme",
            "Kredi kartı ile ödeme (simülasyon)",
            "Banka kartı ile ödeme (simülasyon)"
        };

        /// <summary>Verilen ek hizmet adının katalog fiyatını döndürür; bulunamazsa 0.</summary>
        public static decimal PriceOf(string extraName)
            => Extras.FirstOrDefault(e => string.Equals(e.ExtraName, extraName, StringComparison.OrdinalIgnoreCase))?.ExtraPrice ?? 0m;
    }
}
