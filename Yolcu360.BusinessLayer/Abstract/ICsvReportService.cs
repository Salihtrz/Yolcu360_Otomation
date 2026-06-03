using Yolcu360.DtoLayer.CarResultDto;

namespace Yolcu360.BusinessLayer.Abstract
{
    /// <summary>
    /// Araç sonuçlarını CSV (Excel ile uyumlu) dosyasına aktarır.
    /// </summary>
    public interface ICsvReportService
    {
        Task ExportAsync(string filePath, List<ResultCarDto> cars, CancellationToken ct = default);
    }
}
