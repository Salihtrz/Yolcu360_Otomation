using Dapper;
using Yolcu360.DataAccessLayer.Abstract;
using Yolcu360.DataAccessLayer.Concrete;
using Yolcu360.DataAccessLayer.Context;
using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.DataAccessLayer.Repositories
{
    public class SearchProfileRepository : GenericRepository<SearchProfile>, ISearchProfileRepository
    {
        public SearchProfileRepository(MySqlConnectionFactory factory)
            : base(factory, "SearchProfiles") { }

        public async Task<SearchProfile> GetByNameAsync(string profileName, CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            return await conn.QueryFirstOrDefaultAsync<SearchProfile>(
                new CommandDefinition(
                    "SELECT * FROM `SearchProfiles` WHERE `ProfileName` = @profileName LIMIT 1;",
                    new { profileName }, cancellationToken: ct));
        }
    }
}
