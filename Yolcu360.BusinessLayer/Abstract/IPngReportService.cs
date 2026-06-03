using Yolcu360.DtoLayer.CarResultDto;

namespace Yolcu360.BusinessLayer.Abstract
{
    /// <summary>
    /// DataGridView'deki sonuçları başlıklı, tablolu bir PNG raporuna dönüştürür.
    /// </summary>
    public interface IPngReportService
    {
        /// <summary>
        /// Araç listesini bir PNG dosyasına çizer.
        /// </summary>
        /// <param name="filePath">Kaydedilecek tam dosya yolu (SaveFileDialog'dan).</param>
        /// <param name="reportName">Rapor başlığında gösterilecek ad.</param>
        /// <param name="pickupLocation">Alış lokasyonu.</param>
        /// <param name="pickupDateTime">Alış tarihi.</param>
        /// <param name="returnDateTime">Dönüş tarihi.</param>
        /// <param name="cars">Tabloya yazılacak araçlar.</param>
        Task ExportAsync(
            string filePath,
            string reportName,
            string pickupLocation,
            DateTime pickupDateTime,
            DateTime returnDateTime,
            List<ResultCarDto> cars,
            CancellationToken ct = default);
    }
}
