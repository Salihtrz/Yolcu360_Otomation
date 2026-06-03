namespace Yolcu360.BusinessLayer.Helpers
{
    /// <summary>
    /// Asenkron bekleme yardımcıları. Thread.Sleep kesinlikle kullanılmaz; bunun
    /// yerine <see cref="Task.Delay(int, CancellationToken)"/> ile UI'ı bloklamayan
    /// polling yapılır.
    /// </summary>
    public static class BrowserWaitHelper
    {
        /// <summary>
        /// <paramref name="condition"/> true dönene kadar (veya zaman aşımına kadar)
        /// belirli aralıklarla kontrol eder.
        /// </summary>
        /// <returns>Koşul zamanında sağlandıysa true, zaman aşımına uğradıysa false.</returns>
        public static async Task<bool> PollUntilAsync(
            Func<Task<bool>> condition,
            int timeoutSeconds,
            int pollIntervalMs = 300,
            CancellationToken ct = default)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (await condition())
                        return true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Geçici JS/DOM hatalarını yut; bir sonraki denemede tekrar bak.
                }

                await Task.Delay(pollIntervalMs, ct);
            }
            return false;
        }
    }
}
