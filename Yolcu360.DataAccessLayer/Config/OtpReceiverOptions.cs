using Newtonsoft.Json.Linq;
using Yolcu360.Common.Logging;

namespace Yolcu360.DataAccessLayer.Config
{
    /// <summary>
    /// MacroDroid → masaüstü OTP aktarımı için ayarlar. <c>appsettings.json</c> içindeki
    /// "OtpReceiver" bölümünden okunur; yoksa güvenli varsayılanlar kullanılır.
    ///
    /// appsettings.json örneği:
    /// {
    ///   "OtpReceiver": {
    ///     "Enabled": true,
    ///     "Port": 5055,
    ///     "Token": "CHANGE_ME_SECRET_TOKEN",
    ///     "TimeoutSeconds": 120
    ///   }
    /// }
    /// </summary>
    public static class OtpReceiverOptions
    {
        public const string DefaultToken = "CHANGE_ME_SECRET_TOKEN";

        public static bool Enabled { get; private set; } = true;
        public static int Port { get; private set; } = 5055;
        public static string Token { get; private set; } = DefaultToken;
        public static int TimeoutSeconds { get; private set; } = 120;

        /// <summary>Token hâlâ varsayılan (güvensiz) değerde mi?</summary>
        public static bool IsDefaultToken =>
            string.IsNullOrWhiteSpace(Token) || Token == DefaultToken;

        static OtpReceiverOptions()
        {
            TryLoadFromJson();
        }

        private static void TryLoadFromJson()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (!File.Exists(path))
                    return;

                var root = JObject.Parse(File.ReadAllText(path));
                var otp = root["OtpReceiver"];
                if (otp == null)
                    return;

                if (otp["Enabled"] != null) Enabled = (bool)otp["Enabled"];
                if (otp["Port"] != null) Port = (int)otp["Port"];
                Token = (string)otp["Token"] ?? Token;
                if (otp["TimeoutSeconds"] != null) TimeoutSeconds = (int)otp["TimeoutSeconds"];

                // Not: Token log'a YAZILMAZ.
                LogHelper.Info($"OtpReceiver ayarları yüklendi (Port={Port}, TimeoutSeconds={TimeoutSeconds}).");
            }
            catch (Exception ex)
            {
                LogHelper.Warning($"OtpReceiver ayarları okunamadı, varsayılanlar kullanılacak: {ex.Message}");
            }
        }
    }
}
