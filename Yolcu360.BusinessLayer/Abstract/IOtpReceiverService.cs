namespace Yolcu360.BusinessLayer.Abstract
{
    /// <summary>
    /// MacroDroid'in telefondan gönderdiği 6 haneli OTP kodunu yerel bir HTTP dinleyici ile
    /// alır. Yalnızca login sürecinde aktif edilir; kod kullanıldıktan sonra bellekten silinir.
    ///
    /// GÜVENLİK: Token doğrulanmadan ve kod 6 haneli sayısal değilse istek reddedilir.
    /// Kod log'a/DB'ye yazılmaz.
    /// </summary>
    public interface IOtpReceiverService
    {
        bool IsListening { get; }

        /// <summary>Dinlemenin yapıldığı port.</summary>
        int Port { get; }

        /// <summary>HttpListener yerine TcpListener'a düşüldüyse true (yönetici gerektirmeyen yol).</summary>
        bool UsingTcpFallback { get; }

        /// <summary>Geçerli (6 haneli) bir kod geldiğinde tetiklenir. Kod parametre olarak gelir.</summary>
        event Action<string> OtpReceived;

        /// <summary>
        /// Dinleyiciyi başlatır. Port/token verilmezse appsettings.json'daki değerler kullanılır.
        /// </summary>
        Task StartAsync(int? port = null, string token = null, CancellationToken ct = default);

        /// <summary>Dinleyiciyi durdurur ve bellekteki kodu temizler.</summary>
        void Stop();

        /// <summary>
        /// Bir OTP kodu gelene kadar (veya zaman aşımına kadar) bekler. Zaman aşımında null döner.
        /// Dışarıdan iptal edilirse OperationCanceledException fırlatır.
        /// </summary>
        Task<string> WaitForOtpAsync(TimeSpan timeout, CancellationToken ct = default);

        /// <summary>Kod tam 6 haneli sayısal mı?</summary>
        bool ValidateOtpCode(string code);

        /// <summary>Bu makinenin yerel IPv4 adresleri (telefon ayarı için gösterilir).</summary>
        IReadOnlyList<string> GetLocalIPv4Addresses();
    }
}
