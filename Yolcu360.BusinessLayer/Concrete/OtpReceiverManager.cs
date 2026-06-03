using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.Common.Logging;
using Yolcu360.DataAccessLayer.Config;

namespace Yolcu360.BusinessLayer.Concrete
{
    /// <summary>
    /// Telefondaki MacroDroid'in gönderdiği 6 haneli OTP kodunu yerel ağ üzerinden alır.
    ///
    /// Önce <see cref="HttpListener"/> denenir; yönetici/URL ACL gerektirip başlatılamazsa
    /// yönetici gerektirmeyen <see cref="TcpListener"/>'a düşülür (telefon aynı Wi-Fi'dan
    /// bilgisayarın IP'sine erişebilir).
    ///
    /// GÜVENLİK: Yalnızca /otp endpoint'i, yalnızca doğru token ve yalnızca 6 haneli sayısal
    /// kod kabul edilir. Kod log'a/DB'ye yazılmaz; bellekte kısa süre tutulur, kullanılınca silinir.
    /// </summary>
    public class OtpReceiverManager : IOtpReceiverService
    {
        private static readonly Regex SixDigits = new(@"^\d{6}$", RegexOptions.Compiled);
        // Serbest metin içinde tek başına duran 6 haneli kodu bulur (7+ haneli sayılara takılmaz).
        private static readonly Regex SixDigitsInText = new(@"(?<!\d)\d{6}(?!\d)", RegexOptions.Compiled);

        private readonly object _gate = new();
        private HttpListener _httpListener;
        private TcpListener _tcpListener;
        private CancellationTokenSource _serverCts;
        private TaskCompletionSource<string> _waiter;
        private string _lastCode;
        private string _token;

        public bool IsListening { get; private set; }
        public int Port { get; private set; }
        public bool UsingTcpFallback { get; private set; }

        public event Action<string> OtpReceived;

        public async Task StartAsync(int? port = null, string token = null, CancellationToken ct = default)
        {
            if (IsListening) return;

            Port = port ?? OtpReceiverOptions.Port;
            _token = token ?? OtpReceiverOptions.Token;
            UsingTcpFallback = false;
            _serverCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            lock (_gate) { _lastCode = null; }

            // 1) HttpListener dene (http://+:port/). Yönetici/URL ACL hatası olursa TcpListener'a geç.
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://+:{Port}/");
                _httpListener.Start();
                IsListening = true;
                _ = Task.Run(() => HttpListenLoopAsync(_serverCts.Token));
                LogHelper.Info($"OTP dinleyici (HttpListener) başlatıldı. Port={Port}.");
                return;
            }
            catch (Exception ex)
            {
                LogHelper.Warning("HttpListener başlatılamadı (muhtemelen yönetici/URL ACL gerekiyor); " +
                                  "TcpListener'a geçiliyor. Detay: " + ex.Message);
                try { _httpListener?.Close(); } catch { }
                _httpListener = null;
            }

            // 2) TcpListener (yönetici gerektirmez, LAN'dan erişilebilir).
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, Port);
                _tcpListener.Start();
                UsingTcpFallback = true;
                IsListening = true;
                _ = Task.Run(() => TcpListenLoopAsync(_serverCts.Token));
                LogHelper.Info($"OTP dinleyici (TcpListener) başlatıldı. Port={Port}.");
            }
            catch (Exception ex)
            {
                IsListening = false;
                LogHelper.Error("OTP dinleyici başlatılamadı (TcpListener).", ex);
                throw new InvalidOperationException(
                    $"OTP dinleyici {Port} portunda başlatılamadı. Port başka bir uygulama tarafından " +
                    "kullanılıyor olabilir veya güvenlik duvarı engelliyor olabilir.\n\nDetay: " + ex.Message);
            }

            await Task.CompletedTask;
        }

        public void Stop()
        {
            try { _serverCts?.Cancel(); } catch { }
            try { if (_httpListener != null) { _httpListener.Stop(); _httpListener.Close(); } } catch { }
            try { _tcpListener?.Stop(); } catch { }
            _httpListener = null;
            _tcpListener = null;

            lock (_gate)
            {
                _lastCode = null;
                if (_waiter != null && !_waiter.Task.IsCompleted) _waiter.TrySetCanceled();
                _waiter = null;
            }
            IsListening = false;
            LogHelper.Info("OTP dinleyici durduruldu.");
        }

        public async Task<string> WaitForOtpAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            TaskCompletionSource<string> tcs;
            lock (_gate)
            {
                if (_lastCode != null) { var c = _lastCode; _lastCode = null; return c; }
                tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiter = tcs;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            using (timeoutCts.Token.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    return await tcs.Task;
                }
                catch (OperationCanceledException)
                {
                    lock (_gate) { if (_waiter == tcs) _waiter = null; }
                    if (ct.IsCancellationRequested) throw;   // dış iptal
                    return null;                              // zaman aşımı
                }
            }
        }

        public bool ValidateOtpCode(string code)
            => !string.IsNullOrEmpty(code) && SixDigits.IsMatch(code);

        public IReadOnlyList<string> GetLocalIPv4Addresses()
        {
            // Gerçek (Wi-Fi/Ethernet) adresler önce, sanal adaptörler (VMware/VirtualBox/Hyper-V
            // vb.) en sona. Böylece telefondan erişilebilen IP listenin başında gösterilir.
            var preferred = new List<string>();
            var virtualOnes = new List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    var tag = (ni.Name + " " + ni.Description).ToLowerInvariant();
                    bool isVirtual = tag.Contains("vmware") || tag.Contains("virtualbox")
                        || tag.Contains("hyper-v") || tag.Contains("vethernet")
                        || tag.Contains("vpn") || tag.Contains("tap")
                        || tag.Contains("tunnel") || tag.Contains("virtual");

                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        var ip = ua.Address.ToString();
                        if (ip.StartsWith("169.254")) continue; // APIPA
                        var target = isVirtual ? virtualOnes : preferred;
                        if (!target.Contains(ip)) target.Add(ip);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Warning("Yerel IP adresleri alınamadı: " + ex.Message);
            }

            var list = new List<string>();
            list.AddRange(preferred);
            list.AddRange(virtualOnes);
            if (list.Count == 0) list.Add("127.0.0.1");
            return list;
        }

        // ---- HttpListener yolu ----

        private async Task HttpListenLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _httpListener is { IsListening: true })
            {
                HttpListenerContext context;
                try { context = await _httpListener.GetContextAsync(); }
                catch { break; } // dinleyici durduruldu
                _ = Task.Run(() => HandleHttpContextAsync(context));
            }
        }

        private async Task HandleHttpContextAsync(HttpListenerContext context)
        {
            try
            {
                var req = context.Request;
                string body = string.Empty;
                if (req.HasEntityBody)
                {
                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                    body = await reader.ReadToEndAsync();
                }

                var query = (req.Url?.Query ?? string.Empty).TrimStart('?');
                var (status, message) = ProcessOtpRequest(req.HttpMethod, req.Url?.AbsolutePath ?? "/", query, body);

                var payload = BuildJsonBody(status, message);
                var buffer = Encoding.UTF8.GetBytes(payload);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                LogHelper.Warning("OTP HTTP isteği işlenemedi: " + ex.Message);
                try { context.Response.Abort(); } catch { }
            }
        }

        // ---- TcpListener yolu (basit HTTP ayrıştırma) ----

        private async Task TcpListenLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _tcpListener.AcceptTcpClientAsync(ct); }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch { break; }
                _ = Task.Run(() => HandleTcpClientAsync(client, ct));
            }
        }

        private async Task HandleTcpClientAsync(TcpClient client, CancellationToken ct)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var (method, path, query, body) = await ReadHttpRequestAsync(stream, ct);
                    var (status, message) = ProcessOtpRequest(method, path, query, body);

                    var payload = BuildJsonBody(status, message);
                    var statusText = status switch
                    {
                        200 => "OK",
                        400 => "Bad Request",
                        401 => "Unauthorized",
                        404 => "Not Found",
                        _ => "Error"
                    };
                    var response =
                        $"HTTP/1.1 {status} {statusText}\r\n" +
                        "Content-Type: application/json; charset=utf-8\r\n" +
                        $"Content-Length: {Encoding.UTF8.GetByteCount(payload)}\r\n" +
                        "Connection: close\r\n\r\n" +
                        payload;

                    var bytes = Encoding.UTF8.GetBytes(response);
                    await stream.WriteAsync(bytes, ct);
                    await stream.FlushAsync(ct);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Warning("OTP TCP isteği işlenemedi: " + ex.Message);
            }
        }

        private static async Task<(string method, string path, string query, string body)> ReadHttpRequestAsync(
            NetworkStream stream, CancellationToken ct)
        {
            var header = new List<byte>(512);
            var one = new byte[1];
            int contentLength = 0;

            // Başlıkları \r\n\r\n görene kadar oku.
            while (true)
            {
                int read = await stream.ReadAsync(one.AsMemory(0, 1), ct);
                if (read == 0) break;
                header.Add(one[0]);
                int n = header.Count;
                if (n >= 4 && header[n - 4] == 13 && header[n - 3] == 10 && header[n - 2] == 13 && header[n - 1] == 10)
                    break;
                if (n > 16384) break; // güvenlik sınırı
            }

            var headerText = Encoding.UTF8.GetString(header.ToArray());
            var lines = headerText.Split("\r\n", StringSplitOptions.None);

            string method = "GET", target = "/";
            if (lines.Length > 0)
            {
                var parts = lines[0].Split(' ');
                if (parts.Length >= 2) { method = parts[0]; target = parts[1]; }
            }
            foreach (var line in lines)
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(line["Content-Length:".Length..].Trim(), out contentLength);
            }

            string path = target, query = string.Empty;
            int q = target.IndexOf('?');
            if (q >= 0) { path = target[..q]; query = target[(q + 1)..]; }

            string body = string.Empty;
            if (contentLength > 0 && contentLength < 1_000_000)
            {
                var buf = new byte[contentLength];
                int got = 0;
                while (got < contentLength)
                {
                    int r = await stream.ReadAsync(buf.AsMemory(got, contentLength - got), ct);
                    if (r == 0) break;
                    got += r;
                }
                body = Encoding.UTF8.GetString(buf, 0, got);
            }

            return (method, path, query, body);
        }

        // ---- Ortak işleme ----

        /// <summary>İsteği doğrular ve (HTTP durum kodu, mesaj) döner. Kod/token log'lanmaz.</summary>
        private (int status, string message) ProcessOtpRequest(string method, string path, string query, string body)
        {
            path = string.IsNullOrEmpty(path) ? "/" : path.TrimEnd('/');
            if (path.Length == 0) path = "/";
            if (!path.Equals("/otp", StringComparison.OrdinalIgnoreCase))
                return (404, "Endpoint bulunamadı. Yalnızca /otp kabul edilir.");

            // Gövde (JSON) + query parametrelerini tek (büyük/küçük harf duyarsız) sözlükte topla.
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var obj = JObject.Parse(body);
                    foreach (var p in obj.Properties())
                        fields[p.Name] = p.Value?.ToString() ?? string.Empty;
                }
                catch { /* gövde JSON değil; ham gövde aşağıda kod taraması için kullanılır */ }
            }
            if (!string.IsNullOrEmpty(query))
                foreach (var kv in ParseQuery(query))
                    fields[kv.Key] = kv.Value;

            // Token doğrulaması (zorunlu).
            fields.TryGetValue("token", out var token);
            if (string.IsNullOrWhiteSpace(_token) || !string.Equals(token, _token, StringComparison.Ordinal))
            {
                LogHelper.Warning("OTP isteği reddedildi: token doğrulanamadı.");
                return (401, "Token geçersiz.");
            }

            // Kod: önce 'code' alanı; yoksa herhangi bir metin alanından (ör. tüm SMS) 6 haneyi çıkar.
            string code = null;
            if (fields.TryGetValue("code", out var direct) && ValidateOtpCode(direct?.Trim()))
            {
                code = direct.Trim();
            }
            else
            {
                foreach (var v in fields.Values)
                {
                    var m = SixDigitsInText.Match(v ?? string.Empty);
                    if (m.Success) { code = m.Value; break; }
                }
                if (code == null && !string.IsNullOrEmpty(body))
                {
                    var m = SixDigitsInText.Match(body);
                    if (m.Success) code = m.Value;
                }
            }

            if (!ValidateOtpCode(code))
            {
                LogHelper.Warning("OTP isteği reddedildi: gönderilen veride 6 haneli kod bulunamadı.");
                return (400, "6 haneli kod bulunamadı. 'code' alanında kodu ya da SMS metnini gönderin.");
            }

            DeliverCode(code);
            LogHelper.Info("Geçerli OTP kodu alındı (kaynak: harici istek). Kod log'a yazılmaz.");
            return (200, "Kod alındı.");
        }

        private void DeliverCode(string code)
        {
            try { OtpReceived?.Invoke(code); } catch { /* UI tarafı hatası dinleyiciyi düşürmesin */ }
            lock (_gate)
            {
                if (_waiter != null && !_waiter.Task.IsCompleted) { _waiter.TrySetResult(code); _waiter = null; }
                else _lastCode = code;
            }
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = pair.IndexOf('=');
                if (idx < 0) continue;
                var key = Uri.UnescapeDataString(pair[..idx]);
                var val = Uri.UnescapeDataString(pair[(idx + 1)..]);
                dict[key] = val;
            }
            return dict;
        }

        private static string BuildJsonBody(int status, string message)
            => "{\"status\":" + status + ",\"message\":" + JsonConvert.SerializeObject(message) + "}";
    }
}
