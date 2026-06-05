using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.DataAccessLayer.Abstract
{
    public interface ICarResultRepository : IGenericRepository<CarResult>
    {
        /// <summary>Belirli bir rapora ait tüm araçları getirir.</summary>
        Task<List<CarResult>> GetNameByReportIdAsync(int reportId, CancellationToken ct = default);

        /// <summary>ReportId -> araç sayısı eşlemesi (geçmiş raporlar listesi için).</summary>
        Task<Dictionary<int, int>> GetCarCountsByReportAsync(CancellationToken ct = default);
    }
}
