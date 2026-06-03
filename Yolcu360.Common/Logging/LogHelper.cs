namespace Yolcu360.Common.Logging
{
    /// <summary>
    /// Basit, thread-safe dosya tabanlı log sistemi. Tüm kayıtlar
    /// uygulama dizinindeki <c>logs/app-log.txt</c> dosyasına eklenir.
    /// </summary>
    public static class LogHelper
    {
        private static readonly object _lock = new();
        private static readonly string _logDirectory =
            Path.Combine(AppContext.BaseDirectory, "logs");
        private static readonly string _logFile =
            Path.Combine(_logDirectory, "app-log.txt");

        public static string LogFilePath => _logFile;

        public static void Info(string message) => Write("INFO", message);

        public static void Warning(string message) => Write("WARN", message);

        public static void Error(string message, Exception ex = null)
        {
            var full = ex == null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
            Write("ERROR", full);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    if (!Directory.Exists(_logDirectory))
                        Directory.CreateDirectory(_logDirectory);

                    var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(_logFile, line);
                }
            }
            catch
            {
                // Loglama hiçbir zaman uygulamayı çökertmemeli; sessizce yut.
            }
        }
    }
}
