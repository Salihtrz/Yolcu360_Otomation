using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.DataAccessLayer.Abstract
{
    public interface IUserRepository : IGenericRepository<User>
    {
        /// <summary>E-postaya göre kullanıcı getirir; yoksa null döner.</summary>
        Task<User> GetByEmailAsync(string email, CancellationToken ct = default);

        /// <summary>
        /// Sistemdeki ilk (varsayılan/aktif) kullanıcıyı getirir; yoksa null döner.
        /// Tek kullanıcılı bu yerel uygulamada "kayıtlı kullanıcı" olarak kullanılır.
        /// </summary>
        Task<User> GetFirstAsync(CancellationToken ct = default);
    }
}
