using Newtonsoft.Json.Linq;
using Yolcu360.Common.Logging;

namespace Yolcu360.DataAccessLayer.Config
{
    /// <summary>
    /// MySQL bağlantı ayarlarının tek tutulduğu yer. Varsayılan değerler localhost
    /// içindir; uygulama dizinine konan <c>appsettings.json</c> ile override edilebilir.
    ///
    /// appsettings.json örneği:
    /// {
    ///   "MySql": {
    ///     "Server": "localhost",
    ///     "Port": 3306,
    ///     "UserId": "root",
    ///     "Password": "",
    ///     "Database": "yolcu360_automation"
    ///   }
    /// }
    /// </summary>
    public static class AppSettings
    {
        public static string Server { get; private set; } = "localhost";
        public static uint Port { get; private set; } = 3306;
        public static string UserId { get; private set; } = "root";
        public static string Password { get; private set; } = "";
        public static string Database { get; private set; } = "yolcu360_automation";

        static AppSettings()
        {
            TryLoadFromJson();
        }

        /// <summary>Hedef veritabanına bağlanmak için kullanılan connection string.</summary>
        public static string ConnectionString =>
            $"Server={Server};Port={Port};User ID={UserId};Password={Password};" +
            $"Database={Database};Connection Timeout=10;Default Command Timeout=30;";

        /// <summary>
        /// Veritabanı henüz oluşturulmadan sunucuya bağlanmak için kullanılır
        /// (DatabaseInitializer "CREATE DATABASE" çalıştırırken).
        /// </summary>
        public static string ServerConnectionString =>
            $"Server={Server};Port={Port};User ID={UserId};Password={Password};" +
            $"Connection Timeout=10;Default Command Timeout=30;";

        private static void TryLoadFromJson()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (!File.Exists(path))
                    return;

                var root = JObject.Parse(File.ReadAllText(path));
                var mysql = root["MySql"];
                if (mysql == null)
                    return;

                Server = (string)mysql["Server"] ?? Server;
                if (mysql["Port"] != null) Port = (uint)mysql["Port"];
                UserId = (string)mysql["UserId"] ?? UserId;
                Password = (string)mysql["Password"] ?? Password;
                Database = (string)mysql["Database"] ?? Database;

                LogHelper.Info($"appsettings.json yüklendi (Server={Server}, Database={Database}).");
            }
            catch (Exception ex)
            {
                LogHelper.Warning($"appsettings.json okunamadı, varsayılan ayarlar kullanılacak: {ex.Message}");
            }
        }
    }
}
