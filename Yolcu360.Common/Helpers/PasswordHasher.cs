using System.Security.Cryptography;

namespace Yolcu360.Common.Helpers
{
    /// <summary>
    /// Şifreleri güvenli şekilde saklamak için PBKDF2 (SHA-256) tabanlı hash yardımcısı.
    /// Saklanan biçim: <c>pbkdf2$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;</c>.
    /// Şifreler ASLA düz metin saklanmaz; doğrulama sabit-zamanlı karşılaştırmayla yapılır.
    /// </summary>
    public static class PasswordHasher
    {
        private const string Prefix = "pbkdf2";
        private const int SaltSize = 16;       // 128-bit salt
        private const int KeySize = 32;        // 256-bit türetilmiş anahtar
        private const int Iterations = 100_000;

        /// <summary>Düz metin şifreden saklanabilir hash üretir.</summary>
        public static string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(
                password ?? string.Empty, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        /// <summary>Verilen değer bu yardımcının ürettiği bir hash biçiminde mi?</summary>
        public static bool IsHashed(string stored)
            => !string.IsNullOrEmpty(stored) && stored.StartsWith(Prefix + "$", StringComparison.Ordinal);

        /// <summary>
        /// Düz metin şifrenin saklanan hash ile eşleşip eşleşmediğini döner. Saklanan değer geçerli
        /// bir hash değilse (ör. eski düz metin kayıt) <c>false</c> döner — yani giriş başarısız olur.
        /// </summary>
        public static bool Verify(string password, string stored)
        {
            if (!IsHashed(stored))
                return false;

            var parts = stored.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
                return false;

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(
                    password ?? string.Empty, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
