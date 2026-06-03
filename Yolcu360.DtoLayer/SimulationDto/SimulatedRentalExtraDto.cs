namespace Yolcu360.DtoLayer.SimulationDto
{
    /// <summary>Simülasyona eklenen tek bir ek hizmet satırı (ad + simüle fiyat).</summary>
    public class SimulatedRentalExtraDto
    {
        public string ExtraName { get; set; }
        public decimal ExtraPrice { get; set; }
    }
}
