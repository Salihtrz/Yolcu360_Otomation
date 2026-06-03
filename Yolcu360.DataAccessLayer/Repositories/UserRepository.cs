using Dapper;
using Yolcu360.DataAccessLayer.Abstract;
using Yolcu360.DataAccessLayer.Concrete;
using Yolcu360.DataAccessLayer.Context;
using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.DataAccessLayer.Repositories
{
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        public UserRepository(MySqlConnectionFactory factory)
            : base(factory, "Users") { }

        public async Task<User> GetByEmailAsync(string email, CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            return await conn.QueryFirstOrDefaultAsync<User>(
                new CommandDefinition(
                    "SELECT * FROM `Users` WHERE `Email` = @email LIMIT 1;",
                    new { email }, cancellationToken: ct));
        }

        public async Task<User> GetFirstAsync(CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            return await conn.QueryFirstOrDefaultAsync<User>(
                new CommandDefinition(
                    "SELECT * FROM `Users` ORDER BY `Id` ASC LIMIT 1;",
                    cancellationToken: ct));
        }
    }
}
