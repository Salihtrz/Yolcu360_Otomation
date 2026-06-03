using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.DtoLayer.SearchDto;

namespace Yolcu360.BusinessLayer.Abstract
{
    /// <summary>
    /// Yolcu360 üzerinde arama otomasyonunu ve veri kazımayı yürüten servis.
    /// İşlem ilerlemesini bildirmek için bir durum callback'i alır (UI label güncellemesi).
    /// </summary>
    public interface IYolcu360AutomationService
    {
        /// <summary>
        /// Arama formunu doldurur, aramayı tetikler, sonuçları bekler ve kazır.
        /// </summary>
        /// <param name="request">Kullanıcının girdiği arama bilgileri.</param>
        /// <param name="progress">Durum mesajlarını UI'a iletmek için (örn. "Arama yapılıyor...").</param>
        Task<List<ResultCarDto>> SearchAsync(
            SearchRequestDto request,
            IProgress<string> progress,
            CancellationToken ct = default);

        /// <summary>
        /// Filtreyi site üzerinde uygulamayı dener. Site filtre elemanları bulunamazsa
        /// false döner (bu durumda çağıran yerel filtreye düşmelidir).
        /// </summary>
        Task<bool> ApplyFiltersOnWebsiteAsync(FilterRequestDto filter, CancellationToken ct = default);

        /// <summary>Eldeki sonuç listesini C# tarafında filtreler (site filtresi yoksa).</summary>
        List<ResultCarDto> ApplyFiltersLocally(List<ResultCarDto> cars, FilterRequestDto filter);

        /// <summary>Mevcut sonuç sayfasını yeniden kazır (site filtresi uygulandıktan sonra).</summary>
        Task<List<ResultCarDto>> ScrapeCurrentResultsAsync(SearchRequestDto request, CancellationToken ct = default);
    }
}
