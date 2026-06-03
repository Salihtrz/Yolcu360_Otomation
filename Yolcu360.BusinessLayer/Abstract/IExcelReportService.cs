using Yolcu360.DtoLayer.CarResultDto;

namespace Yolcu360.BusinessLayer.Abstract
{
    /// <summary>
    /// Araç sonuçlarını biçimlendirilmiş bir Excel (.xlsx) dosyasına aktarır.
    /// </summary>
    public interface IExcelReportService
    {
        Task ExportAsync(string filePath, string reportName, List<ResultCarDto> cars, CancellationToken ct = default);
    }
}
