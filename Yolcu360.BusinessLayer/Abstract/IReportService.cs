using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.DtoLayer.ReportDto;

namespace Yolcu360.BusinessLayer.Abstract
{
    public interface IReportService
    {
        /// <summary>Rapor + araçları kaydeder, yeni rapor Id'sini döndürür.</summary>
        Task<int> SaveReportAsync(CreateReportDto dto, CancellationToken ct = default);

        /// <summary>Var olan bir raporun üzerine yazar (önce siler, sonra yeniden ekler).</summary>
        Task<int> OverwriteReportAsync(string reportName, CreateReportDto dto, CancellationToken ct = default);

        /// <summary>Aynı isimde rapor var mı?</summary>
        Task<bool> ReportNameExistsAsync(string reportName, CancellationToken ct = default);

        /// <summary>Geçmiş raporların özet listesi (araç sayısı dahil).</summary>
        Task<List<ResultReportDto>> GetAllReportsAsync(CancellationToken ct = default);

        /// <summary>Bir rapora ait araçları DataGridView'e uygun DTO olarak getirir.</summary>
        Task<List<ResultCarDto>> GetReportCarsAsync(int reportId, CancellationToken ct = default);

        /// <summary>Raporu (ve FK cascade ile araçlarını) kalıcı olarak siler.</summary>
        Task DeleteReportAsync(int reportId, CancellationToken ct = default);
    }
}
