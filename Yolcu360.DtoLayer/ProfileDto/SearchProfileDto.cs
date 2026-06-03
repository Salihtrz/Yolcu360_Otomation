namespace Yolcu360.DtoLayer.ProfileDto
{
    /// <summary>Kaydedilmiş arama profilinin UI'da gösterilen hali.</summary>
    public class SearchProfileDto
    {
        public int Id { get; set; }
        public string ProfileName { get; set; }
        public string PickupLocation { get; set; }
        public DateTime PickupDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }
        public DateTime CreatedAt { get; set; }

        public override string ToString() => ProfileName;
    }
}
