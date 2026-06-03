using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.DataAccessLayer.Abstract
{
    public interface IReportRepository : IGenericRepository<Report>
    {
        /// <summary>
        /// Raporu ve ona bağlı araçları tek bir transaction içinde kaydeder.
        /// Yeni rapor Id'sini döndürür.
        /// </summary>
        Task<int> CreateReportWithCarsAsync(Report report, IEnumerable<CarResult> cars, CancellationToken ct = default);

        /// <summary>Aynı isimde rapor var mı kontrolü için; yoksa null döner.</summary>
        Task<Report> GetByNameAsync(string reportName, CancellationToken ct = default);
    }
}
