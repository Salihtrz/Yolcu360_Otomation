using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.DataAccessLayer.Abstract
{
    public interface ISearchProfileRepository : IGenericRepository<SearchProfile>
    {
        Task<SearchProfile> GetByNameAsync(string profileName, CancellationToken ct = default);
    }
}
