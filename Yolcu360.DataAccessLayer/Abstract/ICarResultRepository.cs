using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.DataAccessLayer.Abstract
{
    public interface ICarResultRepository : IGenericRepository<CarResult>
    {
        /// <summary>Belirli bir rapora ait tüm araçları getirir.</summary>
        Task<List<CarResult>> GetByReportIdAsync(int reportId, CancellationToken ct = default);

        /// <summary>ReportId -> araç sayısı eşlemesi (geçmiş raporlar listesi için).</summary>
        Task<Dictionary<int, int>> GetCountsByReportAsync(CancellationToken ct = default);
    }
}
