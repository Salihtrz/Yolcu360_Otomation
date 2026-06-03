namespace Yolcu360.DtoLayer.LoginDto
{
    /// <summary>
    /// MacroDroid'in (veya manuel testin) yerel OTP endpoint'ine gönderdiği gövde.
    /// JSON alan adları (code/source/token) Newtonsoft tarafında büyük/küçük harf
    /// duyarsız eşlenir.
    /// GÜVENLİK: Code asla log'a/DB'ye yazılmaz; yalnızca bellekte kısa süre tutulur.
    /// </summary>
    public class OtpReceiveDto
    {
        public string Code { get; set; }
        public string Source { get; set; }
        public string Token { get; set; }
        public DateTime ReceivedAt { get; set; } = DateTime.Now;
    }
}
