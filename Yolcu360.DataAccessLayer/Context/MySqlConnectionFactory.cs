using MySqlConnector;
using Yolcu360.DataAccessLayer.Config;

namespace Yolcu360.DataAccessLayer.Context
{
    /// <summary>
    /// MySQL bağlantı nesnelerini üreten merkezi fabrika. Repository'ler doğrudan
    /// connection string ile uğraşmaz; her işlem için buradan bağlantı alır.
    /// </summary>
    public class MySqlConnectionFactory
    {
        /// <summary>Hedef veritabanına (yolcu360_automation) açık bir bağlantı döndürür.</summary>
        public async Task<MySqlConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
        {
            var connection = new MySqlConnection(AppSettings.ConnectionString);
            await connection.OpenAsync(ct);
            return connection;
        }

        /// <summary>
        /// Veritabanı henüz yokken sunucuya bağlanmak için kullanılır
        /// (DatabaseInitializer içindir).
        /// </summary>
        public async Task<MySqlConnection> CreateServerConnectionAsync(CancellationToken ct = default)
        {
            var connection = new MySqlConnection(AppSettings.ServerConnectionString);
            await connection.OpenAsync(ct);
            return connection;
        }
    }
}
