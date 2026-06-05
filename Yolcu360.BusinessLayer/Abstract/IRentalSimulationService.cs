using Yolcu360.DtoLayer.SimulationDto;

namespace Yolcu360.BusinessLayer.Abstract
{
    /// <summary>
    /// Araç kiralama SİMÜLASYONU iş mantığı. Gerçek rezervasyon/ödeme YAPMAZ; yalnızca uygulama
    /// içinde simüle edilmiş kiralama kayıtları üretir, hesaplar, listeler ve siler.
    /// </summary>
    public interface IRentalSimulationService
    {
        /// <summary>
        /// Girdilerden gün sayısı, ek hizmet toplamı, genel toplam ve simülasyon kodunu hesaplar,
        /// kaydı (ek hizmetlerle) DB'ye yazar ve özet (PNG/ekran için) döndürür.
        /// </summary>
        Task<RentalSimulationSummaryDto> CreateAsync(CreateSimulatedRentalDto dto, CancellationToken ct = default);

        /// <summary>Tüm simülasyonları (özet, ek hizmet hariç) en yeniden eskiye listeler.</summary>
        Task<List<ResultSimulatedRentalDto>> GetAllAsync(CancellationToken ct = default);

        /// <summary>Tek simülasyon (ek hizmet hariç); yoksa null.</summary>
        Task<ResultSimulatedRentalDto> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>Tek simülasyon, ek hizmetleriyle birlikte (detay/özet için); yoksa null.</summary>
        Task<ResultSimulatedRentalDto> GetWithExtrasAsync(int id, CancellationToken ct = default);

        /// <summary>Bir simülasyon kaydını (ve ek hizmetlerini) siler.</summary>
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
