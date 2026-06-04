namespace Yolcu360.Common.Helpers
{
    /// <summary>
    /// Telefon numaralarını loglarken gizlilik için maskeler. OTP kodları HİÇBİR ZAMAN
    /// loglanmaz; telefon numarası gerekiyorsa yalnızca maskeli biçimde loglanmalıdır.
    /// Örnek: "+905321231234" → "+90******1234".
    /// </summary>
    public static class PhoneMaskHelper
    {
        public static string Mask(string phone)
        {
            phone = (phone ?? string.Empty).Trim();
            if (phone.Length <= 4)
                return new string('*', phone.Length);

            // Baştaki ülke kodunu (+90 gibi) ve son 4 haneyi koru, ortayı yıldızla.
            var prefixLen = phone.StartsWith("+") ? 3 : 2;
            if (prefixLen >= phone.Length - 4)
                prefixLen = 0;

            var prefix = phone.Substring(0, prefixLen);
            var suffix = phone.Substring(phone.Length - 4);
            var stars = new string('*', phone.Length - prefixLen - 4);
            return $"{prefix}{stars}{suffix}";
        }
    }
}
