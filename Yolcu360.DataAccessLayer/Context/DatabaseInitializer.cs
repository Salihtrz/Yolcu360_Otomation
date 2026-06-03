using MySqlConnector;
using Yolcu360.Common.Logging;
using Yolcu360.DataAccessLayer.Config;

namespace Yolcu360.DataAccessLayer.Context
{
    /// <summary>
    /// Uygulama açılırken çalışır. Veritabanı ve tablolar yoksa oluşturur.
    /// Bağlantı hatalarını yakalar ve kullanıcıya gösterilebilecek anlamlı bir
    /// sonuç döndürür (uygulamayı çökertmez).
    /// </summary>
    public class DatabaseInitializer
    {
        private readonly MySqlConnectionFactory _factory;

        public DatabaseInitializer(MySqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public class InitResult
        {
            public bool Success { get; set; }
            public string UserMessage { get; set; }
        }

        public async Task<InitResult> InitializeAsync(CancellationToken ct = default)
        {
            try
            {
                // 1) Veritabanını oluştur (henüz yoksa). Bunun için DB belirtmeyen
                //    sunucu bağlantısı kullanılır.
                await using (var serverConn = await _factory.CreateServerConnectionAsync(ct))
                {
                    var createDbSql =
                        $"CREATE DATABASE IF NOT EXISTS `{AppSettings.Database}` " +
                        "CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                    await using var cmd = new MySqlCommand(createDbSql, serverConn);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // 2) Tabloları oluştur (henüz yoksa).
                await using (var conn = await _factory.CreateOpenConnectionAsync(ct))
                {
                    await using var cmd = new MySqlCommand(TableScripts, conn);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                // 3) Şema göçleri: eksik kolonları ekler. Kolon zaten varsa MySQL 1060
                //    (ER_DUP_FIELDNAME) döner; bu durum güvenle yok sayılır (idempotent).
                await using (var conn = await _factory.CreateOpenConnectionAsync(ct))
                {
                    await TryRunMigrationAsync(conn,
                        "ALTER TABLE `Users` ADD COLUMN `PhoneNumber` VARCHAR(30) NOT NULL DEFAULT '' AFTER `Password`", ct);
                    await TryRunMigrationAsync(conn,
                        "UPDATE `Users` SET `PhoneNumber` = '' WHERE `PhoneNumber` IS NULL", ct);
                    await TryRunMigrationAsync(conn,
                        "ALTER TABLE `Users` MODIFY COLUMN `PhoneNumber` VARCHAR(30) NOT NULL DEFAULT ''", ct);
                }

                LogHelper.Info("Veritabanı ve tablolar hazır (DatabaseInitializer).");
                return new InitResult { Success = true, UserMessage = "Veritabanı bağlantısı başarılı." };
            }
            catch (MySqlException ex)
            {
                LogHelper.Error("Veritabanı başlatma hatası (MySQL).", ex);
                return new InitResult
                {
                    Success = false,
                    UserMessage =
                        "Veritabanı bağlantısı kurulamadı. Lütfen MySQL sunucusunun çalıştığından ve " +
                        "appsettings.json içindeki kullanıcı/şifre bilgilerinin doğru olduğundan emin olun.\n\n" +
                        $"Teknik detay: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                LogHelper.Error("Veritabanı başlatma hatası (genel).", ex);
                return new InitResult
                {
                    Success = false,
                    UserMessage = "Veritabanı hazırlanırken beklenmeyen bir hata oluştu. Detaylar log dosyasına yazıldı."
                };
            }
        }

        /// <summary>
        /// Tüm tablolar tek script içinde. Hepsi IF NOT EXISTS olduğu için tekrar
        /// çalıştırmak güvenlidir.
        /// </summary>
        private const string TableScripts = @"
CREATE TABLE IF NOT EXISTS Users (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    Email VARCHAR(150) NOT NULL,
    Password VARCHAR(150) NOT NULL,
    PhoneNumber VARCHAR(30) NOT NULL DEFAULT '',
    CreatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Reports (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    ReportName VARCHAR(200) NOT NULL,
    PickupLocation VARCHAR(200) NULL,
    PickupDateTime DATETIME NULL,
    ReturnDateTime DATETIME NULL,
    CreatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS SearchProfiles (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    ProfileName VARCHAR(200) NOT NULL,
    PickupLocation VARCHAR(200) NULL,
    PickupDateTime DATETIME NULL,
    ReturnDateTime DATETIME NULL,
    CreatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS CarResults (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    ReportId INT NOT NULL,
    CarModel VARCHAR(200) NULL,
    RentalCompany VARCHAR(200) NULL,
    TransmissionType VARCHAR(100) NULL,
    FuelType VARCHAR(100) NULL,
    Segment VARCHAR(100) NULL,
    Price DECIMAL(18,2) NOT NULL DEFAULT 0,
    Currency VARCHAR(20) NULL,
    PickupLocation VARCHAR(200) NULL,
    PickupDateTime DATETIME NULL,
    ReturnDateTime DATETIME NULL,
    SourceUrl TEXT NULL,
    ImageUrl TEXT NULL,
    ScrapedAt DATETIME NOT NULL,
    CONSTRAINT FK_CarResults_Reports FOREIGN KEY (ReportId)
        REFERENCES Reports(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS SimulatedRentals (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    CarResultId INT NULL,
    CarModel VARCHAR(200) NULL,
    RentalCompany VARCHAR(200) NULL,
    TransmissionType VARCHAR(100) NULL,
    FuelType VARCHAR(100) NULL,
    Segment VARCHAR(100) NULL,
    PickupLocation VARCHAR(200) NULL,
    PickupDateTime DATETIME NULL,
    ReturnDateTime DATETIME NULL,
    RentalDays INT NOT NULL DEFAULT 1,
    BasePrice DECIMAL(18,2) NOT NULL DEFAULT 0,
    ExtraServicesTotal DECIMAL(18,2) NOT NULL DEFAULT 0,
    GrandTotal DECIMAL(18,2) NOT NULL DEFAULT 0,
    PaymentMethod VARCHAR(100) NULL,
    DriverFirstName VARCHAR(100) NULL,
    DriverLastName VARCHAR(100) NULL,
    DriverPhone VARCHAR(40) NULL,
    DriverEmail VARCHAR(150) NULL,
    DriverIdentityNo VARCHAR(50) NULL,
    DriverBirthDate DATETIME NULL,
    DriverLicenseNo VARCHAR(50) NULL,
    DriverLicenseDate DATETIME NULL,
    SimulationStatus VARCHAR(50) NULL,
    SimulationCode VARCHAR(60) NULL,
    CreatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS SimulatedRentalExtras (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    SimulatedRentalId INT NOT NULL,
    ExtraName VARCHAR(120) NULL,
    ExtraPrice DECIMAL(18,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_SimExtras_SimRentals FOREIGN KEY (SimulatedRentalId)
        REFERENCES SimulatedRentals(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
";

        /// <summary>
        /// Tek bir şema göçü (ör. ADD COLUMN) çalıştırır. Kolon/öğe zaten varsa MySQL'in
        /// döndürdüğü "zaten var" hataları (1060 ER_DUP_FIELDNAME, 1061 dup key, 1054)
        /// yutulur; böylece her açılışta güvenle çalıştırılabilir. Kullanıcı değişkeni
        /// (@var) kullanılmaz — MySqlConnector'da varsayılan kapalıdır.
        /// </summary>
        private static async Task TryRunMigrationAsync(MySqlConnection conn, string sql, CancellationToken ct)
        {
            try
            {
                await using var cmd = new MySqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (MySqlException ex) when (ex.Number is 1060 or 1061 or 1054 or 1050)
            {
                // Kolon/anahtar/tablo zaten var — göç daha önce uygulanmış, yok say.
                LogHelper.Info($"Şema göçü atlandı (zaten uygulanmış): {ex.Message}");
            }
        }
    }
}
