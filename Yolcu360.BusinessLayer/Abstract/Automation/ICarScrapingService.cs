using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.DtoLayer.SearchDto;

namespace Yolcu360.BusinessLayer.Abstract.Automation
{
    /// <summary>
    /// Yolcu360 sonuç sayfasının DOM'unu OKUR: araç kartlarını DTO'lara çevirir ve filtre panelini
    /// dinamik okur. Form doldurma / arama tetikleme burada DEĞİL, <see cref="ICarSearchService"/>'tedir.
    /// </summary>
    public interface ICarScrapingService
    {
        /// <summary>
        /// Mevcut sonuç sayfasındaki tüm araç kartlarını (lazy-load ile hepsi yüklenerek) kazır ve
        /// <see cref="ResultCarDto"/> listesine çevirir.
        /// </summary>
        Task<List<ResultCarDto>> ScrapeCarsAsync(SearchRequestDto request, CancellationToken ct = default);

        /// <summary>
        /// Sonuç sayfasındaki filtre panelini siteden dinamik okur (marka/şirket/model/... bölümleri
        /// ve seçenekleri). UI dropdown'larını site verisiyle doldurmak için kullanılır.
        /// </summary>
        Task<List<SiteFilterSectionDto>> ScrapeSiteFiltersAsync(CancellationToken ct = default);
    }
}
