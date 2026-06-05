using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.DataAccessLayer.Abstract
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User> GetByEmailAsync(string email, CancellationToken ct = default);
        Task<User> GetFirstAsync(CancellationToken ct = default);
    }
}
