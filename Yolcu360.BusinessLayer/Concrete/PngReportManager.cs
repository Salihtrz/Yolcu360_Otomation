using System.Drawing;
using System.Drawing.Imaging;
using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.Common.Logging;
using Yolcu360.DtoLayer.CarResultDto;

namespace Yolcu360.BusinessLayer.Concrete
{
    /// <summary>
    /// Araç sonuçlarını başlıklı, çizgili bir tablo halinde PNG'ye çizer (System.Drawing).
    /// Çizim UI thread'ini bloklamamak için Task.Run içinde yapılır.
    /// </summary>
    public class PngReportManager : IPngReportService
    {
        // Tablo sütunları: başlık + oransal genişlik.
        private static readonly (string Header, int Width)[] Columns =
        {
            ("Araç Modeli",      240),
            ("Kiralama Şirketi", 200),
            ("Vites",            110),
            ("Yakıt",            110),
            ("Segment",          130),
            ("Fiyat",            150),
        };

        private const int Margin = 30;
        private const int HeaderBlockHeight = 150;
        private const int RowHeight = 34;
        private const int TableHeaderHeight = 40;

        public Task ExportAsync(
            string filePath, string reportName, string pickupLocation,
            DateTime pickupDateTime, DateTime returnDateTime,
            List<ResultCarDto> cars, CancellationToken ct = default)
        {
            cars ??= new List<ResultCarDto>();

            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                int tableWidth = Columns.Sum(c => c.Width);
                int width = tableWidth + Margin * 2;
                int height = Margin * 2 + HeaderBlockHeight + TableHeaderHeight + Math.Max(cars.Count, 1) * RowHeight;

                using var bmp = new Bitmap(width, height);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.White);

                    DrawHeaderBlock(g, width, reportName, pickupLocation, pickupDateTime, returnDateTime, cars.Count);
                    DrawTable(g, cars, ct);
                }

                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                bmp.Save(filePath, ImageFormat.Png);
                LogHelper.Info($"PNG raporu oluşturuldu: {filePath} ({cars.Count} araç).");
            }, ct);
        }

        private static void DrawHeaderBlock(
            Graphics g, int width, string reportName, string pickupLocation,
            DateTime pickupDateTime, DateTime returnDateTime, int carCount)
        {
            using var titleBrush = new SolidBrush(Color.FromArgb(33, 37, 41));
            using var subBrush = new SolidBrush(Color.FromArgb(90, 90, 90));
            using var accent = new SolidBrush(Color.FromArgb(0, 123, 255));
            using var titleFont = new Font("Segoe UI", 20, FontStyle.Bold);
            using var metaFont = new Font("Segoe UI", 11, FontStyle.Regular);

            // Üst renkli şerit.
            g.FillRectangle(accent, 0, 0, width, 8);

            float y = Margin;
            g.DrawString("Yolcu360 Araç Kiralama Raporu", titleFont, titleBrush, Margin, y);
            y += 44;

            var lines = new[]
            {
                $"Rapor Adı: {Safe(reportName)}",
                $"Alış Lokasyonu: {Safe(pickupLocation)}",
                $"Alış Tarihi: {pickupDateTime:dd.MM.yyyy HH:mm}    Dönüş Tarihi: {returnDateTime:dd.MM.yyyy HH:mm}",
                $"Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}    Araç Sayısı: {carCount}",
            };
            foreach (var line in lines)
            {
                g.DrawString(line, metaFont, subBrush, Margin, y);
                y += 22;
            }
        }

        private static void DrawTable(Graphics g, List<ResultCarDto> cars, CancellationToken ct)
        {
            using var headerBg = new SolidBrush(Color.FromArgb(0, 123, 255));
            using var headerText = new SolidBrush(Color.White);
            using var cellText = new SolidBrush(Color.FromArgb(33, 37, 41));
            using var altRowBg = new SolidBrush(Color.FromArgb(245, 248, 252));
            using var gridPen = new Pen(Color.FromArgb(220, 220, 220));
            using var headerFont = new Font("Segoe UI", 10, FontStyle.Bold);
            using var cellFont = new Font("Segoe UI", 10, FontStyle.Regular);

            var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

            int x0 = Margin;
            int y = Margin + HeaderBlockHeight;
            int tableWidth = Columns.Sum(c => c.Width);

            // Tablo başlık satırı.
            g.FillRectangle(headerBg, x0, y, tableWidth, TableHeaderHeight);
            int cx = x0;
            foreach (var col in Columns)
            {
                var rect = new RectangleF(cx + 8, y, col.Width - 12, TableHeaderHeight);
                g.DrawString(col.Header, headerFont, headerText, rect, sf);
                cx += col.Width;
            }
            y += TableHeaderHeight;

            if (cars.Count == 0)
            {
                var rect = new RectangleF(x0 + 8, y, tableWidth - 12, RowHeight);
                g.DrawString("Sonuç bulunamadı.", cellFont, cellText, rect, sf);
                g.DrawRectangle(gridPen, x0, Margin + HeaderBlockHeight, tableWidth, TableHeaderHeight + RowHeight);
                return;
            }

            // Veri satırları.
            for (int i = 0; i < cars.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var car = cars[i];

                if (i % 2 == 1)
                    g.FillRectangle(altRowBg, x0, y, tableWidth, RowHeight);

                var values = new[]
                {
                    Safe(car.CarModel),
                    Safe(car.RentalCompany),
                    Safe(car.TransmissionType),
                    Safe(car.FuelType),
                    Safe(car.Segment),
                    $"{car.Price:N2} {car.Currency}",
                };

                cx = x0;
                for (int c = 0; c < Columns.Length; c++)
                {
                    var rect = new RectangleF(cx + 8, y, Columns[c].Width - 12, RowHeight);
                    g.DrawString(values[c], cellFont, cellText, rect, sf);
                    cx += Columns[c].Width;
                }
                y += RowHeight;
            }

            // Izgara çizgileri.
            int tableTop = Margin + HeaderBlockHeight;
            int tableBottom = y;
            cx = x0;
            for (int c = 0; c <= Columns.Length; c++)
            {
                g.DrawLine(gridPen, cx, tableTop, cx, tableBottom);
                if (c < Columns.Length) cx += Columns[c].Width;
            }
            int ry = tableTop;
            g.DrawLine(gridPen, x0, ry, x0 + tableWidth, ry);
            ry += TableHeaderHeight;
            for (int r = 0; r <= cars.Count; r++)
            {
                g.DrawLine(gridPen, x0, ry, x0 + tableWidth, ry);
                ry += RowHeight;
            }
        }

        private static string Safe(string s) => string.IsNullOrWhiteSpace(s) ? "-" : s;
    }
}
