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
        /// Seçili filtreleri YALNIZCA site üzerinde uygular (uygulama içi/local filtreleme yoktur).
        /// Boş filtre, sitedeki tüm filtre kutularını/radyolarını temizler.
        /// </summary>
        Task<bool> ApplyFiltersOnWebsiteAsync(FilterRequestDto filter, CancellationToken ct = default);

        /// <summary>Mevcut sonuç sayfasını yeniden kazır (site filtresi uygulandıktan sonra).</summary>
        Task<List<ResultCarDto>> ScrapeCurrentResultsAsync(SearchRequestDto request, CancellationToken ct = default);

        /// <summary>
        /// Sonuç sayfasındaki filtre panelini siteden dinamik okur (marka/şirket/model/... bölümleri
        /// ve seçenekleri). UI dropdown'larını site verisiyle doldurmak için kullanılır.
        /// </summary>
        Task<List<SiteFilterSectionDto>> ScrapeSiteFiltersAsync(CancellationToken ct = default);
    }
}
