namespace Yolcu360.EntityLayer.Entities
{
    /// <summary>
    /// Bir simülasyon kiralamasına eklenen tek bir ek hizmet (ör. "Bebek koltuğu" / 300 TL).
    /// <see cref="SimulatedRental"/> ile N - 1 ilişkilidir.
    /// </summary>
    public class SimulatedRentalExtra
    {
        public int Id { get; set; }
        public int SimulatedRentalId { get; set; }
        public string ExtraName { get; set; }
        public decimal ExtraPrice { get; set; }
    }
}
