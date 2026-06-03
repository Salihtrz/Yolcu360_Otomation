namespace Yolcu360.DtoLayer.SimulationDto
{
    /// <summary>
    /// Simülasyon hesaplama yardımcıları. UI (canlı önizleme) ve servis (otoriter kayıt) aynı
    /// mantığı kullansın diye tek yerde toplanmıştır.
    /// </summary>
    public static class SimulationCalculator
    {
        /// <summary>
        /// Kiralama gün sayısı = (dönüş - alış), yukarı yuvarlanır. 0 veya negatifse minimum 1 gün.
        /// </summary>
        public static int RentalDays(DateTime pickup, DateTime ret)
        {
            var days = (int)Math.Ceiling((ret - pickup).TotalDays);
            return days < 1 ? 1 : days;
        }

        /// <summary>Seçili ek hizmetlerin toplam fiyatı.</summary>
        public static decimal ExtrasTotal(IEnumerable<SimulatedRentalExtraDto> extras)
            => extras?.Sum(e => e.ExtraPrice) ?? 0m;

        /// <summary>Genel toplam = araç temel fiyatı + ek hizmet toplamı.</summary>
        public static decimal GrandTotal(decimal basePrice, decimal extrasTotal)
            => basePrice + extrasTotal;
    }
}
