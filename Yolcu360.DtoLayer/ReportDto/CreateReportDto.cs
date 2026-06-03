using Yolcu360.DtoLayer.CarResultDto;

namespace Yolcu360.DtoLayer.ReportDto
{
    /// <summary>
    /// Yeni bir rapor kaydetmek için kullanılan DTO. Rapor başlığı bilgileri ile
    /// birlikte DataGridView'deki araç listesini taşır.
    /// </summary>
    public class CreateReportDto
    {
        public string ReportName { get; set; }
        public string PickupLocation { get; set; }
        public DateTime PickupDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }
        public List<ResultCarDto> Cars { get; set; } = new();
    }
}
