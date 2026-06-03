namespace Yolcu360.DataAccessLayer.Abstract
{
    /// <summary>
    /// Tüm entity'ler için ortak CRUD sözleşmesi. Somut uygulama Dapper kullanır.
    /// </summary>
    public interface IGenericRepository<T> where T : class
    {
        Task<List<T>> GetAllAsync(CancellationToken ct = default);
        Task<T> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>Yeni kayıt ekler ve oluşan otomatik artan Id'yi döndürür.</summary>
        Task<int> InsertAsync(T entity, CancellationToken ct = default);

        Task UpdateAsync(T entity, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
