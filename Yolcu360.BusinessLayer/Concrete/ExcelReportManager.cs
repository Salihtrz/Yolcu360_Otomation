using ClosedXML.Excel;
using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.Common.Logging;
using Yolcu360.DtoLayer.CarResultDto;

namespace Yolcu360.BusinessLayer.Concrete
{
    /// <summary>
    /// Araç sonuçlarını biçimlendirilmiş bir Excel (.xlsx) dosyasına yazar (ClosedXML).
    /// Başlık satırı, otomatik genişlik, fiyat sütununda sayı biçimi ve özet satırı içerir.
    /// </summary>
    public class ExcelReportManager : IExcelReportService
    {
        private static readonly string[] Headers =
        {
            "Araç Modeli", "Kiralama Şirketi", "Vites Tipi", "Yakıt Tipi", "Segment",
            "Fiyat", "Para Birimi", "Alış Lokasyonu", "Alış Tarihi", "Dönüş Tarihi", "Çekilme Tarihi"
        };

        public Task ExportAsync(string filePath, string reportName, List<ResultCarDto> cars, CancellationToken ct = default)
        {
            cars ??= new List<ResultCarDto>();

            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Araçlar");

                // Başlık (rapor adı)
                ws.Cell(1, 1).Value = $"Yolcu360 Araç Kiralama Raporu - {reportName}";
                ws.Range(1, 1, 1, Headers.Length).Merge();
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Font.FontSize = 14;
                ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0, 123, 255);
                ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
                ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Sütun başlıkları (3. satır)
                const int headerRow = 3;
                for (int c = 0; c < Headers.Length; c++)
                {
                    var cell = ws.Cell(headerRow, c + 1);
                    cell.Value = Headers[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(222, 235, 255);
                    cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                }

                // Veri satırları
                int row = headerRow + 1;
                foreach (var car in cars)
                {
                    ct.ThrowIfCancellationRequested();
                    ws.Cell(row, 1).Value = car.CarModel;
                    ws.Cell(row, 2).Value = car.RentalCompany;
                    ws.Cell(row, 3).Value = car.TransmissionType;
                    ws.Cell(row, 4).Value = car.FuelType;
                    ws.Cell(row, 5).Value = car.Segment;
                    ws.Cell(row, 6).Value = car.Price;
                    ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 7).Value = car.Currency;
                    ws.Cell(row, 8).Value = car.PickupLocation;
                    ws.Cell(row, 9).Value = car.PickupDateTime;
                    ws.Cell(row, 9).Style.DateFormat.Format = "dd.mm.yyyy hh:mm";
                    ws.Cell(row, 10).Value = car.ReturnDateTime;
                    ws.Cell(row, 10).Style.DateFormat.Format = "dd.mm.yyyy hh:mm";
                    ws.Cell(row, 11).Value = car.ScrapedAt;
                    ws.Cell(row, 11).Style.DateFormat.Format = "dd.mm.yyyy hh:mm";
                    row++;
                }

                // Özet satırı
                if (cars.Count > 0)
                {
                    ws.Cell(row + 1, 1).Value = "Toplam araç:";
                    ws.Cell(row + 1, 2).Value = cars.Count;
                    ws.Cell(row + 1, 1).Style.Font.Bold = true;

                    var priced = cars.Where(c => c.Price > 0).ToList();
                    if (priced.Count > 0)
                    {
                        ws.Cell(row + 2, 1).Value = "En ucuz / Ortalama / En pahalı:";
                        ws.Cell(row + 2, 1).Style.Font.Bold = true;
                        ws.Cell(row + 2, 2).Value = priced.Min(c => c.Price);
                        ws.Cell(row + 2, 3).Value = Math.Round(priced.Average(c => c.Price), 2);
                        ws.Cell(row + 2, 4).Value = priced.Max(c => c.Price);
                    }
                }

                ws.Columns().AdjustToContents();

                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                wb.SaveAs(filePath);
                LogHelper.Info($"Excel raporu oluşturuldu: {filePath} ({cars.Count} araç).");
            }, ct);
        }
    }
}
