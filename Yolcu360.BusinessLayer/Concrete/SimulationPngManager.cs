using System.Drawing;
using System.Drawing.Imaging;
using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.Common.Logging;
using Yolcu360.DtoLayer.SimulationDto;

namespace Yolcu360.BusinessLayer.Concrete
{
    public class SimulationPngManager : ISimulationPngService
    {
        private const int Width = 720;
        private const int Margin = 32;

        public Task ExportAsync(string filePath, RentalSimulationSummaryDto s, CancellationToken ct = default)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));

            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                var carLines = new[]
                {
                    ("Araç", $"{Safe(s.CarModel)}  ({Safe(s.RentalCompany)})"),
                    ("Vites / Yakıt / Segment", $"{Safe(s.TransmissionType)} · {Safe(s.FuelType)} · {Safe(s.Segment)}"),
                    ("Alış Lokasyonu", Safe(s.PickupLocation)),
                    ("Alış / Dönüş", $"{s.PickupDateTime:dd.MM.yyyy HH:mm}  →  {s.ReturnDateTime:dd.MM.yyyy HH:mm}"),
                    ("Gün Sayısı", $"{s.RentalDays} gün"),
                };
                var driver = s.Driver ?? new DriverInfoDto();
                var driverLines = new[]
                {
                    ("Ad Soyad", Safe(driver.FullName)),
                    ("Telefon", Safe(driver.Phone)),
                    ("E-posta", Safe(driver.Email)),
                    ("Kimlik/Pasaport No", Safe(driver.IdentityNo)),
                    ("Doğum Tarihi", driver.BirthDate == DateTime.MinValue ? "-" : driver.BirthDate.ToString("dd.MM.yyyy")),
                    ("Ehliyet No", Safe(driver.LicenseNo)),
                };
                var extras = s.Extras ?? new();
                var priceLines = new[]
                {
                    ("Araç Fiyatı", $"{s.BasePrice:N2} TL"),
                    ("Ek Hizmetler Toplamı", $"{s.ExtraServicesTotal:N2} TL"),
                    ("GENEL TOPLAM", $"{s.GrandTotal:N2} TL"),
                    ("Ödeme Yöntemi", Safe(s.PaymentMethod)),
                };

                int lineH = 26, sectionGap = 16, headerH = 132;
                int height = Margin + headerH
                    + SectionHeight(carLines.Length, lineH) + sectionGap
                    + SectionHeight(driverLines.Length, lineH) + sectionGap
                    + SectionHeight(Math.Max(extras.Count, 1) + 1, lineH) + sectionGap
                    + SectionHeight(priceLines.Length, lineH) + sectionGap
                    + 60
                    + Margin;

                using var bmp = new Bitmap(Width, height);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.White);

                    float y = DrawHeader(g, s);

                    y = DrawSection(g, "Araç Bilgileri", carLines, y, lineH) + sectionGap;
                    y = DrawSection(g, "Sürücü Bilgileri (Simülasyon)", driverLines, y, lineH) + sectionGap;
                    y = DrawExtras(g, extras, y, lineH) + sectionGap;
                    y = DrawSection(g, "Fiyat Özeti", priceLines, y, lineH) + sectionGap;

                    DrawDisclaimer(g, y, height);
                }

                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                bmp.Save(filePath, ImageFormat.Png);
                LogHelper.Info($"Simülasyon PNG çıktısı oluşturuldu: {filePath} (kod={s.SimulationCode}).");
            }, ct);
        }

        private static int SectionHeight(int rows, int lineH) => 34 + rows * lineH + 8;

        private static float DrawHeader(Graphics g, RentalSimulationSummaryDto s)
        {
            using var accent = new SolidBrush(Color.FromArgb(0x25, 0x63, 0xEB));
            using var titleBrush = new SolidBrush(Color.FromArgb(0x11, 0x18, 0x27));
            using var subBrush = new SolidBrush(Color.FromArgb(0x6B, 0x72, 0x80));
            using var titleFont = new Font("Segoe UI", 18, FontStyle.Bold);
            using var metaFont = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            using var codeFont = new Font("Segoe UI", 12, FontStyle.Bold);

            g.FillRectangle(accent, 0, 0, Width, 8);
            float y = Margin;
            g.DrawString("Yolcu360 Araç Kiralama Simülasyon Özeti", titleFont, titleBrush, Margin, y);
            y += 38;
            g.DrawString($"Simülasyon Kodu: {Safe(s.SimulationCode)}", codeFont, accent, Margin, y);
            y += 28;
            g.DrawString($"Simülasyon Tarihi: {s.SimulationDate:dd.MM.yyyy HH:mm}", metaFont, subBrush, Margin, y);
            y += 26;
            return y;
        }

        private static float DrawSection(Graphics g, string title, (string Label, string Value)[] rows, float y, int lineH)
        {
            y = DrawSectionTitle(g, title, y);
            using var labelBrush = new SolidBrush(Color.FromArgb(0x6B, 0x72, 0x80));
            using var valueBrush = new SolidBrush(Color.FromArgb(0x11, 0x18, 0x27));
            using var labelFont = new Font("Segoe UI", 10, FontStyle.Regular);
            using var valueFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            foreach (var (label, value) in rows)
            {
                g.DrawString(label, labelFont, labelBrush, Margin + 4, y);
                g.DrawString(value, valueFont, valueBrush, Margin + 250, y);
                y += lineH;
            }
            return y + 8;
        }

        private static float DrawExtras(Graphics g, List<SimulatedRentalExtraDto> extras, float y, int lineH)
        {
            y = DrawSectionTitle(g, "Ek Hizmetler", y);
            using var labelBrush = new SolidBrush(Color.FromArgb(0x37, 0x41, 0x51));
            using var valueBrush = new SolidBrush(Color.FromArgb(0x11, 0x18, 0x27));
            using var font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            if (extras == null || extras.Count == 0)
            {
                g.DrawString("Ek hizmet seçilmedi.", font, labelBrush, Margin + 4, y);
                return y + lineH + 8;
            }
            foreach (var ex in extras)
            {
                g.DrawString($"• {Safe(ex.ExtraName)}", font, labelBrush, Margin + 4, y);
                g.DrawString($"{ex.ExtraPrice:N2} TL", font, valueBrush, Margin + 250, y);
                y += lineH;
            }
            return y + 8;
        }

        private static float DrawSectionTitle(Graphics g, string title, float y)
        {
            using var barBrush = new SolidBrush(Color.FromArgb(0x25, 0x63, 0xEB));
            using var titleBrush = new SolidBrush(Color.FromArgb(0x11, 0x18, 0x27));
            using var titleFont = new Font("Segoe UI", 12, FontStyle.Bold);
            g.FillRectangle(barBrush, Margin, y + 2, 4, 18);
            g.DrawString(title, titleFont, titleBrush, Margin + 12, y);
            return y + 34;
        }

        private static void DrawDisclaimer(Graphics g, float y, int height)
        {
            using var bg = new SolidBrush(Color.FromArgb(0xFE, 0xF3, 0xC7));
            using var border = new Pen(Color.FromArgb(0xF5, 0x9E, 0x0B));
            using var textBrush = new SolidBrush(Color.FromArgb(0x92, 0x40, 0x0E));
            using var font = new Font("Segoe UI", 10, FontStyle.Bold);
            var rect = new Rectangle(Margin, (int)y, Width - Margin * 2, 44);
            g.FillRectangle(bg, rect);
            g.DrawRectangle(border, rect);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("Bu belge SİMÜLASYON amaçlıdır. Gerçek rezervasyon veya ödeme değildir.",
                font, textBrush, rect, sf);
        }

        private static string Safe(string s) => string.IsNullOrWhiteSpace(s) ? "-" : s;
    }
}
