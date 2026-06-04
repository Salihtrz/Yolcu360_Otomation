namespace Yolcu360.BusinessLayer.Concrete
{
    /// <summary>
    /// Yerel uygulama girişi (e-posta/şifre doğrulaması) sırasında oluşan, kullanıcıya
    /// doğrudan gösterilebilecek anlamlı hataları temsil eder. Message alanı her zaman
    /// son kullanıcıya uygundur (teknik detay içermez). Bkz. AutomationException deseni.
    /// </summary>
    public class AuthenticationException : Exception
    {
        public AuthenticationException(string message) : base(message) { }
    }
}
