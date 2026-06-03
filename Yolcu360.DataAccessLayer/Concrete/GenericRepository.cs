using System.Collections;
using System.Reflection;
using Dapper;
using MySqlConnector;
using Yolcu360.DataAccessLayer.Abstract;
using Yolcu360.DataAccessLayer.Context;

namespace Yolcu360.DataAccessLayer.Concrete
{
    /// <summary>
    /// Reflection + Dapper tabanlı genel CRUD repository'si. Tablo adı türetilen
    /// sınıfta belirtilir. Koleksiyon (navigation) özellikleri ve hesaplanan
    /// (salt-okunur) özellikler SQL'e dahil edilmez.
    /// </summary>
    public abstract class GenericRepository<T> : IGenericRepository<T> where T : class, new()
    {
        protected readonly MySqlConnectionFactory Factory;
        protected readonly string TableName;

        // Id dışındaki, veritabanına yazılabilen sütunlar.
        private static readonly PropertyInfo[] WritableColumns = GetMappedProperties()
            .Where(p => !string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        protected GenericRepository(MySqlConnectionFactory factory, string tableName)
        {
            Factory = factory;
            TableName = tableName;
        }

        public virtual async Task<List<T>> GetAllAsync(CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            var rows = await conn.QueryAsync<T>(
                new CommandDefinition($"SELECT * FROM `{TableName}`;", cancellationToken: ct));
            return rows.ToList();
        }

        public virtual async Task<T> GetByIdAsync(int id, CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            return await conn.QueryFirstOrDefaultAsync<T>(
                new CommandDefinition($"SELECT * FROM `{TableName}` WHERE Id = @id;",
                    new { id }, cancellationToken: ct));
        }

        public virtual async Task<int> InsertAsync(T entity, CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            return await InsertWithConnectionAsync(conn, null, entity, ct);
        }

        public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
        {
            var setClause = string.Join(", ", WritableColumns.Select(c => $"`{c.Name}` = @{c.Name}"));
            var sql = $"UPDATE `{TableName}` SET {setClause} WHERE Id = @Id;";

            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct));
        }

        public virtual async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            await using var conn = await Factory.CreateOpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                $"DELETE FROM `{TableName}` WHERE Id = @id;", new { id }, cancellationToken: ct));
        }

        /// <summary>
        /// Var olan bir bağlantı/transaction üzerinde insert yapar (toplu kayıtlar için).
        /// Oluşan Id'yi döndürür.
        /// </summary>
        protected async Task<int> InsertWithConnectionAsync(
            MySqlConnection conn, MySqlTransaction tx, T entity, CancellationToken ct)
        {
            var columns = string.Join(", ", WritableColumns.Select(c => $"`{c.Name}`"));
            var values = string.Join(", ", WritableColumns.Select(c => $"@{c.Name}"));
            var sql = $"INSERT INTO `{TableName}` ({columns}) VALUES ({values}); SELECT LAST_INSERT_ID();";

            return await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, entity, tx, cancellationToken: ct));
        }

        /// <summary>
        /// Basit (string olmayan) ve koleksiyon olmayan, okunup yazılabilen özellikler.
        /// Hesaplanan özellikler (set'i olmayan) hariç tutulur.
        /// </summary>
        private static PropertyInfo[] GetMappedProperties()
        {
            return typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Where(p => !IsCollection(p.PropertyType))
                .ToArray();
        }

        private static bool IsCollection(Type type)
        {
            if (type == typeof(string))
                return false;
            return typeof(IEnumerable).IsAssignableFrom(type);
        }
    }
}
