using Dapper;
using Yolcu360.DataAccessLayer.Abstract;
using Yolcu360.DataAccessLayer.Concrete;
using Yolcu360.DataAccessLayer.Context;
using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.DataAccessLayer.Repositories
{
    public class ReportRepository : GenericRepository<Report>, IReportRepository
    {
        public ReportRepository(MySqlConnectionFactory factory)
            : base(factory, "Reports") { }

        public async Task<int> CreateReportWithCarsAsync(
            Report report, IEnumerable<CarResult> cars, CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);
            try
            {
                // Rapor başlığı eklenir, oluşan Id alınır.
                var reportId = await InsertWithConnectionAsync(conn, tx, report, ct);

                var carList = cars?.ToList() ?? new List<CarResult>();
                foreach (var car in carList)
                    car.ReportId = reportId;

                if (carList.Count > 0)
                {
                    const string carSql = @"
INSERT INTO `CarResults`
(`ReportId`,`CarModel`,`RentalCompany`,`TransmissionType`,`FuelType`,`Segment`,
 `Price`,`Currency`,`PickupLocation`,`PickupDateTime`,`ReturnDateTime`,
 `SourceUrl`,`ImageUrl`,`ScrapedAt`)
VALUES
(@ReportId,@CarModel,@RentalCompany,@TransmissionType,@FuelType,@Segment,
 @Price,@Currency,@PickupLocation,@PickupDateTime,@ReturnDateTime,
 @SourceUrl,@ImageUrl,@ScrapedAt);";

                    await conn.ExecuteAsync(new CommandDefinition(carSql, carList, tx, cancellationToken: ct));
                }

                await tx.CommitAsync(ct);
                return reportId;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<Report> GetReportByNameAsync(string reportName, CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            return await conn.QueryFirstOrDefaultAsync<Report>(
                new CommandDefinition(
                    "SELECT * FROM `Reports` WHERE `ReportName` = @reportName LIMIT 1;",
                    new { reportName }, cancellationToken: ct));
        }
    }
}
