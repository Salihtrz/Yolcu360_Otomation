using Yolcu360.DtoLayer.ProfileDto;
using Yolcu360.DtoLayer.SearchDto;

namespace Yolcu360.BusinessLayer.Abstract
{
    /// <summary>
    /// Sık kullanılan arama profillerini (lokasyon + tarih/saat) yönetir.
    /// </summary>
    public interface ISearchProfileService
    {
        Task<int> SaveAsync(string profileName, SearchRequestDto request, CancellationToken ct = default);
        Task<int> OverwriteAsync(string profileName, SearchRequestDto request, CancellationToken ct = default);
        Task<bool> ExistsAsync(string profileName, CancellationToken ct = default);
        Task<List<SearchProfileDto>> GetAllAsync(CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
