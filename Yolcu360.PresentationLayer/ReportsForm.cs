using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.DtoLayer.ReportDto;
using Yolcu360.PresentationLayer.Helpers;
using Yolcu360.PresentationLayer.UI;
using Guna.UI2.WinForms;

namespace Yolcu360.PresentationLayer
{
    /// <summary>
    /// Geçmiş raporları listeler. Kullanıcı bir rapora çift tıklayınca o rapor seçilir
    /// (MainForm araçlarını yükler) veya seçili raporu kalıcı olarak silebilir.
    /// </summary>
    public class ReportsForm : Form
    {
        private readonly IReportService _reportService;
        private DataGridView _grid;
        private Guna2Button _btnLoad;
        private Guna2Button _btnDelete;
        private Guna2Button _btnRefresh;
        private Label _lblInfo;

        /// <summary>Çift tıklanan / yüklenmek üzere seçilen rapor.</summary>
        public ResultReportDto SelectedReport { get; private set; }

        public ReportsForm(IReportService reportService)
        {
            _reportService = reportService;
            BuildUi();
            Load += async (s, e) => await LoadReportsAsync();
        }

        private void BuildUi()
        {
            Text = "Geçmiş Raporlar";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 520);
            MinimizeBox = false;
            BackColor = ThemeColors.Background;
            Font = new Font("Segoe UI", 9.5f);

            // Üst başlık
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = ThemeColors.Surface };
            header.Controls.Add(new Label { Text = "Geçmiş Raporlar", AutoSize = true, Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = ThemeColors.TextDark, Location = new Point(18, 12) });
            _lblInfo = new Label { AutoSize = true, ForeColor = ThemeColors.TextMuted, Location = new Point(20, 40), Text = "Bir rapora çift tıklayarak araçlarını ana ekrana yükleyebilirsiniz." };
            header.Controls.Add(_lblInfo);

            // Grid kartı
            var card = UiStyleHelper.NewCard();
            card.Dock = DockStyle.Fill; card.Margin = new Padding(12); card.Padding = new Padding(10);
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            _grid.Columns.AddRange(
                new DataGridViewTextBoxColumn { HeaderText = "Id", DataPropertyName = "Id", Visible = false },
                new DataGridViewTextBoxColumn { HeaderText = "Rapor Adı", DataPropertyName = "ReportName", FillWeight = 150 },
                new DataGridViewTextBoxColumn { HeaderText = "Alış Lokasyonu", DataPropertyName = "PickupLocation", FillWeight = 130 },
                new DataGridViewTextBoxColumn { HeaderText = "Alış Tarihi", DataPropertyName = "PickupDateTime", FillWeight = 110 },
                new DataGridViewTextBoxColumn { HeaderText = "Dönüş Tarihi", DataPropertyName = "ReturnDateTime", FillWeight = 110 },
                new DataGridViewTextBoxColumn { HeaderText = "Kayıt Tarihi", DataPropertyName = "CreatedAt", FillWeight = 110 },
                new DataGridViewTextBoxColumn { HeaderText = "Araç Sayısı", DataPropertyName = "CarCount", FillWeight = 80 });
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) LoadSelected(); };
            DataGridViewStyleHelper.Apply(_grid);
            card.Controls.Add(_grid);

            // Alt buton barı
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = ThemeColors.Background, Padding = new Padding(12, 10, 12, 10) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            _btnLoad = UiStyleHelper.Button("Seçili Raporu Yükle", ButtonVariant.Primary, 170);
            _btnDelete = UiStyleHelper.Button("Seçili Raporu Sil", ButtonVariant.Danger, 150);
            _btnRefresh = UiStyleHelper.Button("Yenile", ButtonVariant.Secondary, 90);
            _btnLoad.Click += (s, e) => LoadSelected();
            _btnDelete.Click += async (s, e) => await DeleteSelectedAsync();
            _btnRefresh.Click += async (s, e) => await LoadReportsAsync();
            foreach (var b in new[] { _btnLoad, _btnDelete, _btnRefresh }) b.Margin = new Padding(0, 0, 8, 0);
            flow.Controls.AddRange(new Control[] { _btnLoad, _btnDelete, _btnRefresh });
            bottom.Controls.Add(flow);

            Controls.Add(card);
            Controls.Add(bottom);
            Controls.Add(header);
        }

        private async Task LoadReportsAsync()
        {
            try
            {
                var reports = await _reportService.GetAllReportsAsync();
                _grid.DataSource = null;
                _grid.DataSource = reports;
                _lblInfo.Text = $"{reports.Count} rapor listelendi. Çift tıkla = yükle.";
            }
            catch (Exception ex)
            {
                UiHelper.Error("Raporlar getirilemedi. Veritabanı bağlantısını kontrol edin.\n\n" + ex.Message);
            }
        }

        private ResultReportDto CurrentRow()
            => _grid.CurrentRow?.DataBoundItem as ResultReportDto;

        private void LoadSelected()
        {
            var report = CurrentRow();
            if (report == null)
            {
                UiHelper.Warn("Lütfen bir rapor seçin.");
                return;
            }
            SelectedReport = report;
            DialogResult = DialogResult.OK;
            Close();
        }

        private async Task DeleteSelectedAsync()
        {
            var report = CurrentRow();
            if (report == null)
            {
                UiHelper.Warn("Lütfen silinecek raporu seçin.");
                return;
            }

            if (!UiHelper.Confirm($"'{report.ReportName}' raporu ve içindeki tüm araçlar kalıcı olarak silinecek.\nDevam edilsin mi?", "Rapor Sil"))
                return;

            try
            {
                await _reportService.DeleteReportAsync(report.Id);
                UiHelper.Info("Rapor silindi.");
                await LoadReportsAsync();
            }
            catch (Exception ex)
            {
                UiHelper.Error("Rapor silinemedi.\n\n" + ex.Message);
            }
        }
    }
}
