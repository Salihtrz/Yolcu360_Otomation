using Yolcu360.DtoLayer.SimulationDto;

namespace Yolcu360.BusinessLayer.Abstract
{
    /// <summary>
    /// Bir kiralama simülasyonu özetini başlıklı, bölümlü bir PNG belgesine çizer.
    /// Belgede açıkça "simülasyon amaçlıdır, gerçek rezervasyon değildir" notu yer alır.
    /// </summary>
    public interface ISimulationPngService
    {
        Task ExportAsync(string filePath, RentalSimulationSummaryDto summary, CancellationToken ct = default);
    }
}
