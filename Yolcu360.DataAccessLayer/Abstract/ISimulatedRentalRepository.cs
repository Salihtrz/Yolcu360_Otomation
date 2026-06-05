using Yolcu360.EntityLayer.Entities;

namespace Yolcu360.DataAccessLayer.Abstract
{
    /// <summary>
    /// Simülasyon kiralama kayıtları için veri erişimi. Kayıt + ek hizmetler tek transaction'da
    /// yazılır; silme FK CASCADE ile ek hizmetleri de siler.
    /// </summary>
    public interface ISimulatedRentalRepository : IGenericRepository<SimulatedRental>
    {
        /// <summary>Simülasyonu ve ek hizmetlerini tek transaction içinde kaydeder; yeni Id'yi döndürür.</summary>
        Task<int> CreateRentalWithExtrasAsync(SimulatedRental rental, IEnumerable<SimulatedRentalExtra> extras, CancellationToken ct = default);

        /// <summary>Bir simülasyonu ek hizmetleriyle birlikte getirir (yoksa null).</summary>
        Task<SimulatedRental> GetRentalWithExtrasAsync(int id, CancellationToken ct = default);

        /// <summary>Belirli bir günde oluşturulmuş simülasyon sayısı (simülasyon kodu sıra numarası için).</summary>
        Task<int> RentalCountByDateAsync(DateTime date, CancellationToken ct = default);
    }
}
