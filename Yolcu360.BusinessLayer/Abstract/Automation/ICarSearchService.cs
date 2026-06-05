using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.DtoLayer.SearchDto;

namespace Yolcu360.BusinessLayer.Abstract.Automation
{
    /// <summary>
    /// Yolcu360 arama formunu doldurur, aramayı tetikler ve sonuçların kazınmasını sağlar (kazıma
    /// işini <see cref="ICarScrapingService"/>'e devreder). Site üzerinde filtre uygulamayı da yönetir.
    /// İşlem ilerlemesini bildirmek için bir durum callback'i (UI label) alır.
    /// </summary>
    public interface ICarSearchService
    {
        /// <summary>
        /// Arama formunu doldurur, aramayı tetikler, sonuçları bekler ve kazınmış araç listesini döner.
        /// </summary>
        /// <param name="request">Kullanıcının girdiği arama bilgileri (lokasyon + tarih/saat).</param>
        /// <param name="progress">Durum mesajlarını UI'a iletmek için (örn. "Arama yapılıyor...").</param>
        Task<List<ResultCarDto>> SearchCarsAsync(
            SearchRequestDto request, IProgress<string> progress, CancellationToken ct = default);

        /// <summary>
        /// Seçili filtreleri YALNIZCA site üzerinde uygular (uygulama içi/local filtreleme yoktur).
        /// Boş filtre, sitedeki tüm filtre kutularını/radyolarını temizler.
        /// </summary>
        Task<bool> ApplyFiltersOnWebsiteAsync(FilterRequestDto filter, CancellationToken ct = default);
    }
}
