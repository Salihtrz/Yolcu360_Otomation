using Dapper;
using Yolcu360.DataAccessLayer.Abstract;
using Yolcu360.DataAccessLayer.Concrete;
using Yolcu360.DataAccessLayer.Context;
using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.DataAccessLayer.Repositories
{
    public class SimulatedRentalRepository : GenericRepository<SimulatedRental>, ISimulatedRentalRepository
    {
        public SimulatedRentalRepository(MySqlConnectionFactory factory)
            : base(factory, "SimulatedRentals") { }

        public async Task<int> CreateRentalWithExtrasAsync(
            SimulatedRental rental, IEnumerable<SimulatedRentalExtra> extras, CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);
            try
            {
                var rentalId = await InsertWithConnectionAsync(conn, tx, rental, ct);

                var extraList = extras?.ToList() ?? new List<SimulatedRentalExtra>();
                foreach (var ex in extraList)
                    ex.SimulatedRentalId = rentalId;

                if (extraList.Count > 0)
                {
                    const string extraSql = @"
INSERT INTO `SimulatedRentalExtras` (`SimulatedRentalId`,`ExtraName`,`ExtraPrice`)
VALUES (@SimulatedRentalId,@ExtraName,@ExtraPrice);";
                    await conn.ExecuteAsync(new CommandDefinition(extraSql, extraList, tx, cancellationToken: ct));
                }

                await tx.CommitAsync(ct);
                return rentalId;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<SimulatedRental> GetRentalWithExtrasAsync(int id, CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);

            var rental = await conn.QueryFirstOrDefaultAsync<SimulatedRental>(
                new CommandDefinition("SELECT * FROM `SimulatedRentals` WHERE `Id` = @id;",
                    new { id }, cancellationToken: ct));
            if (rental == null)
                return null;

            var extras = await conn.QueryAsync<SimulatedRentalExtra>(
                new CommandDefinition(
                    "SELECT * FROM `SimulatedRentalExtras` WHERE `SimulatedRentalId` = @id ORDER BY `Id`;",
                    new { id }, cancellationToken: ct));
            rental.Extras = extras.ToList();
            return rental;
        }

        public async Task<int> RentalCountByDateAsync(DateTime date, CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT COUNT(*) FROM `SimulatedRentals` WHERE DATE(`CreatedAt`) = @d;",
                    new { d = date.Date }, cancellationToken: ct));
        }
    }
}
