using Dapper;
using Yolcu360.DataAccessLayer.Abstract;
using Yolcu360.DataAccessLayer.Concrete;
using Yolcu360.DataAccessLayer.Context;
using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.DataAccessLayer.Repositories
{
    public class CarResultRepository : GenericRepository<CarResult>, ICarResultRepository
    {
        public CarResultRepository(MySqlConnectionFactory factory)
            : base(factory, "CarResults") { }

        public async Task<List<CarResult>> GetNameByReportIdAsync(int reportId, CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            var rows = await conn.QueryAsync<CarResult>(
                new CommandDefinition(
                    "SELECT * FROM `CarResults` WHERE `ReportId` = @reportId ORDER BY `Price` ASC;",
                    new { reportId }, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<Dictionary<int, int>> GetCarCountsByReportAsync(CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            var rows = await conn.QueryAsync(
                new CommandDefinition(
                    "SELECT `ReportId` AS ReportId, COUNT(*) AS Cnt FROM `CarResults` GROUP BY `ReportId`;",
                    cancellationToken: ct));

            var result = new Dictionary<int, int>();
            foreach (var row in rows)
                result[(int)row.ReportId] = Convert.ToInt32(row.Cnt);
            return result;
        }
    }
}
