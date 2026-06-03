namespace Yolcu360.EntityLayer.Entities
{
    /// <summary>
    /// Kullanıcının kaydettiği sık kullanılan arama profili (lokasyon + tarih/saat).
    /// "Tek tıkla tekrar ara" için kullanılır.
    /// </summary>
    public class SearchProfile
    {
        public int Id { get; set; }
        public string ProfileName { get; set; }
        public string PickupLocation { get; set; }
        public DateTime PickupDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
