using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.DtoLayer.ReportDto;
using Yolcu360.PresentationLayer.Helpers;
using Yolcu360.PresentationLayer.UI;
using Guna.UI2.WinForms;

namespace Yolcu360.PresentationLayer
{
    /// <summary>
    /// İki geçmiş raporu yan yana karşılaştırır: rapor özetleri (adet, min/ort/max fiyat) ve
    /// ortak araç modellerinin fiyat farkları.
    /// </summary>
    public class ReportCompareForm : Form
    {
        private readonly IReportService _reportService;
        private Guna2ComboBox _cmbA, _cmbB;
        private Guna2Button _btnCompare;
        private Label _lblSummary;
        private DataGridView _grid;

        public ReportCompareForm(IReportService reportService)
        {
            _reportService = reportService;
            BuildUi();
            Load += async (s, e) => await LoadReportsAsync();
        }

        private void BuildUi()
        {
            Text = "Rapor Karşılaştırma";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(940, 600);
            MinimizeBox = false;
            BackColor = ThemeColors.Background;
            Font = new Font("Segoe UI", 9.5f);

            // Üst seçim barı
            var top = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = ThemeColors.Surface, Padding = new Padding(16, 12, 16, 8) };
            top.Controls.Add(new Label { Text = "Rapor Karşılaştırma", AutoSize = true, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = ThemeColors.TextDark, Location = new Point(16, 10) });
            var selFlow = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.LeftToRight };
            _cmbA = UiStyleHelper.Combo(280);
            _cmbB = UiStyleHelper.Combo(280);
            _btnCompare = UiStyleHelper.Button("Karşılaştır", ButtonVariant.Primary, 120, 32);
            _btnCompare.Click += async (s, e) => await CompareAsync();
            selFlow.Controls.Add(new Label { Text = "Rapor A:", AutoSize = true, ForeColor = ThemeColors.TextMuted, Margin = new Padding(0, 6, 4, 0) });
            selFlow.Controls.Add(_cmbA);
            selFlow.Controls.Add(new Label { Text = "Rapor B:", AutoSize = true, ForeColor = ThemeColors.TextMuted, Margin = new Padding(14, 6, 4, 0) });
            selFlow.Controls.Add(_cmbB);
            _btnCompare.Margin = new Padding(14, 2, 0, 0);
            selFlow.Controls.Add(_btnCompare);
            top.Controls.Add(selFlow);

            // Özet kartı
            _lblSummary = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.75f),
                ForeColor = ThemeColors.TextDark,
                BackColor = Color.Transparent,
                Text = "İki rapor seçip 'Karşılaştır'a basın."
            };
            var summaryCard = UiStyleHelper.Card("Özet", _lblSummary);
            summaryCard.Dock = DockStyle.Top; summaryCard.Height = 132; summaryCard.Margin = new Padding(12, 12, 12, 6);

            // Karşılaştırma tablosu kartı
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _grid.Columns.AddRange(
                new DataGridViewTextBoxColumn { HeaderText = "Araç Modeli", DataPropertyName = "Model", FillWeight = 160 },
                new DataGridViewTextBoxColumn { HeaderText = "A Ort. Fiyat", DataPropertyName = "PriceADisplay", FillWeight = 90 },
                new DataGridViewTextBoxColumn { HeaderText = "B Ort. Fiyat", DataPropertyName = "PriceBDisplay", FillWeight = 90 },
                new DataGridViewTextBoxColumn { HeaderText = "Fark (B-A)", DataPropertyName = "DiffDisplay", FillWeight = 90 },
                new DataGridViewTextBoxColumn { HeaderText = "Durum", DataPropertyName = "Note", FillWeight = 110 });
            _grid.CellFormatting += Grid_CellFormatting;
            DataGridViewStyleHelper.Apply(_grid);
            var gridCard = UiStyleHelper.Card("Model Bazında Fiyat Farkları", _grid);
            gridCard.Dock = DockStyle.Fill; gridCard.Margin = new Padding(12, 6, 12, 12);

            Controls.Add(gridCard);
            Controls.Add(summaryCard);
            Controls.Add(top);
        }

        private async Task LoadReportsAsync()
        {
            try
            {
                var reports = await _reportService.GetAllReportsAsync();
                _cmbA.DisplayMember = "ReportName";
                _cmbB.DisplayMember = "ReportName";
                _cmbA.ValueMember = "Id";
                _cmbB.ValueMember = "Id";
                _cmbA.DataSource = new List<ResultReportDto>(reports);
                _cmbB.DataSource = new List<ResultReportDto>(reports);
                if (reports.Count > 1) _cmbB.SelectedIndex = 1;
            }
            catch (Exception ex)
            {
                UiHelper.Error("Raporlar getirilemedi.\n\n" + ex.Message);
            }
        }

        private async Task CompareAsync()
        {
            if (_cmbA.SelectedItem is not ResultReportDto a || _cmbB.SelectedItem is not ResultReportDto b)
            {
                UiHelper.Warn("Lütfen iki rapor seçin.");
                return;
            }
            if (a.Id == b.Id)
            {
                UiHelper.Warn("Lütfen farklı iki rapor seçin.");
                return;
            }

            try
            {
                _btnCompare.Enabled = false;
                var carsA = await _reportService.GetReportCarsAsync(a.Id);
                var carsB = await _reportService.GetReportCarsAsync(b.Id);

                _lblSummary.Text =
                    $"A: {a.ReportName}  →  {Summary(carsA)}\n" +
                    $"B: {b.ReportName}  →  {Summary(carsB)}\n" +
                    $"Ortalama fiyat farkı (B-A): {AvgDiff(carsA, carsB):+#,##0;-#,##0;0} {Currency(carsA, carsB)}";

                _grid.DataSource = BuildComparison(carsA, carsB);
            }
            catch (Exception ex)
            {
                UiHelper.Error("Karşılaştırma yapılamadı.\n\n" + ex.Message);
            }
            finally
            {
                _btnCompare.Enabled = true;
            }
        }

        private static string Summary(List<ResultCarDto> cars)
        {
            var priced = cars.Where(c => c.Price > 0).ToList();
            if (priced.Count == 0) return $"{cars.Count} araç (fiyat yok)";
            return $"{cars.Count} araç | min {priced.Min(c => c.Price):N0} | ort {priced.Average(c => c.Price):N0} | max {priced.Max(c => c.Price):N0}";
        }

        private static decimal AvgDiff(List<ResultCarDto> a, List<ResultCarDto> b)
        {
            var pa = a.Where(c => c.Price > 0).Select(c => c.Price).DefaultIfEmpty(0).Average();
            var pb = b.Where(c => c.Price > 0).Select(c => c.Price).DefaultIfEmpty(0).Average();
            return Math.Round(pb - pa, 0);
        }

        private static string Currency(List<ResultCarDto> a, List<ResultCarDto> b)
            => a.FirstOrDefault()?.Currency ?? b.FirstOrDefault()?.Currency ?? "TL";

        private static List<CompareRow> BuildComparison(List<ResultCarDto> a, List<ResultCarDto> b)
        {
            decimal? Avg(IEnumerable<ResultCarDto> src, string model)
            {
                var list = src.Where(c => string.Equals(c.CarModel, model, StringComparison.OrdinalIgnoreCase) && c.Price > 0)
                    .Select(c => c.Price).ToList();
                return list.Count > 0 ? Math.Round(list.Average(), 0) : (decimal?)null;
            }

            var models = a.Select(c => c.CarModel).Concat(b.Select(c => c.CarModel))
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m);

            var rows = new List<CompareRow>();
            foreach (var m in models)
            {
                var pa = Avg(a, m);
                var pb = Avg(b, m);
                string note;
                decimal? diff = null;
                if (pa == null) note = "Sadece B'de";
                else if (pb == null) note = "Sadece A'da";
                else { diff = pb - pa; note = diff == 0 ? "Aynı" : (diff < 0 ? "B daha ucuz" : "B daha pahalı"); }

                rows.Add(new CompareRow
                {
                    Model = m,
                    PriceA = pa,
                    PriceB = pb,
                    Diff = diff,
                    Note = note
                });
            }
            return rows;
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_grid.Rows[e.RowIndex].DataBoundItem is not CompareRow row) return;
            if (_grid.Columns[e.ColumnIndex].DataPropertyName == "DiffDisplay" && row.Diff.HasValue)
                e.CellStyle.ForeColor = row.Diff.Value < 0 ? Color.Green : (row.Diff.Value > 0 ? Color.Firebrick : Color.Black);
        }

        private class CompareRow
        {
            public string Model { get; set; }
            public decimal? PriceA { get; set; }
            public decimal? PriceB { get; set; }
            public decimal? Diff { get; set; }
            public string Note { get; set; }

            public string PriceADisplay => PriceA.HasValue ? PriceA.Value.ToString("N0") : "-";
            public string PriceBDisplay => PriceB.HasValue ? PriceB.Value.ToString("N0") : "-";
            public string DiffDisplay => Diff.HasValue ? Diff.Value.ToString("+#,##0;-#,##0;0") : "-";
        }
    }
}
