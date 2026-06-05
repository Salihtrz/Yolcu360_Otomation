using System.Globalization;
using System.Text;
using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.Common.Logging;
using Yolcu360.DtoLayer.CarResultDto;

namespace Yolcu360.BusinessLayer.Concrete
{
    public class CsvReportManager : ICsvReportService
    {
        private static readonly string[] Headers =
        {
            "Araç Modeli", "Kiralama Şirketi", "Vites Tipi", "Yakıt Tipi", "Segment",
            "Fiyat", "Para Birimi", "Alış Lokasyonu", "Alış Tarihi", "Dönüş Tarihi", "Çekilme Tarihi"
        };

        public Task ExportAsync(string filePath, List<ResultCarDto> cars, CancellationToken ct = default)
        {
            cars ??= new List<ResultCarDto>();

            return Task.Run(async () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Join(";", Headers.Select(Escape)));

                foreach (var c in cars)
                {
                    ct.ThrowIfCancellationRequested();
                    var fields = new[]
                    {
                        c.CarModel,
                        c.RentalCompany,
                        c.TransmissionType,
                        c.FuelType,
                        c.Segment,
                        c.Price.ToString("0.00", CultureInfo.GetCultureInfo("tr-TR")),
                        c.Currency,
                        c.PickupLocation,
                        c.PickupDateTime.ToString("dd.MM.yyyy HH:mm"),
                        c.ReturnDateTime.ToString("dd.MM.yyyy HH:mm"),
                        c.ScrapedAt.ToString("dd.MM.yyyy HH:mm")
                    };
                    sb.AppendLine(string.Join(";", fields.Select(Escape)));
                }

                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(true), ct);
                LogHelper.Info($"CSV raporu oluşturuldu: {filePath} ({cars.Count} araç).");
            }, ct);
        }

        private static string Escape(string value)
        {
            value ??= "";
            if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
