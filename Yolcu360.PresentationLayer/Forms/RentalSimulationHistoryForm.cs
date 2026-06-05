using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.DtoLayer.SimulationDto;
using Yolcu360.PresentationLayer.Helpers;
using Yolcu360.PresentationLayer.UI;

namespace Yolcu360.PresentationLayer.Forms
{
    /// <summary>
    /// Geçmiş araç kiralama simülasyonlarını listeler. Detay görme, silme ve PNG indirme sağlar.
    /// (Gerçek rezervasyon kaydı değildir; tümü simülasyondur.)
    /// </summary>
    public class RentalSimulationHistoryForm : Form
    {
        private readonly IRentalSimulationService _service;
        private readonly ISimulationPngService _pngService;
        private DataGridView _grid;
        private Label _lblInfo;

        public RentalSimulationHistoryForm(IRentalSimulationService service, ISimulationPngService pngService)
        {
            _service = service;
            _pngService = pngService;
            BuildUi();
            Load += async (s, e) => await LoadAsync();
        }

        private void BuildUi()
        {
            Text = "Simülasyon Geçmişi";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(940, 540);
            MinimizeBox = false;
            BackColor = ThemeColors.Background;
            Font = new Font("Segoe UI", 9.5f);

            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = ThemeColors.Surface };
            header.Controls.Add(new Label { Text = "Araç Kiralama Simülasyon Geçmişi", AutoSize = true, Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = ThemeColors.TextDark, Location = new Point(18, 12) });
            _lblInfo = new Label { AutoSize = true, ForeColor = ThemeColors.TextMuted, Location = new Point(20, 40), Text = "Tüm kayıtlar simülasyondur; gerçek rezervasyon değildir." };
            header.Controls.Add(_lblInfo);

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
                new DataGridViewTextBoxColumn { HeaderText = "Simülasyon Kodu", DataPropertyName = "SimulationCode", FillWeight = 150 },
                new DataGridViewTextBoxColumn { HeaderText = "Araç Modeli", DataPropertyName = "CarModel", FillWeight = 130 },
                new DataGridViewTextBoxColumn { HeaderText = "Sürücü", DataPropertyName = "DriverFullName", FillWeight = 120 },
                new DataGridViewTextBoxColumn { HeaderText = "Alış Lokasyonu", DataPropertyName = "PickupLocation", FillWeight = 120 },
                new DataGridViewTextBoxColumn { HeaderText = "Alış", DataPropertyName = "PickupDateTime", FillWeight = 105 },
                new DataGridViewTextBoxColumn { HeaderText = "Dönüş", DataPropertyName = "ReturnDateTime", FillWeight = 105 },
                new DataGridViewTextBoxColumn { HeaderText = "Genel Toplam", DataPropertyName = "GrandTotal", FillWeight = 95 },
                new DataGridViewTextBoxColumn { HeaderText = "Oluşturulma", DataPropertyName = "CreatedAt", FillWeight = 110 });
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) ShowDetail(); };
            DataGridViewStyleHelper.Apply(_grid);
            card.Controls.Add(_grid);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = ThemeColors.Background, Padding = new Padding(12, 10, 12, 10) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            var btnDetail = UiStyleHelper.Button("Detay Gör", ButtonVariant.Primary, 120);
            var btnPng = UiStyleHelper.Button("PNG Olarak İndir", ButtonVariant.Secondary, 160);
            var btnDelete = UiStyleHelper.Button("Sil", ButtonVariant.Danger, 90);
            var btnClose = UiStyleHelper.Button("Kapat", ButtonVariant.Secondary, 90);
            btnDetail.Click += (s, e) => ShowDetail();
            btnPng.Click += async (s, e) => await ExportSelectedPngAsync();
            btnDelete.Click += async (s, e) => await DeleteSelectedAsync();
            btnClose.Click += (s, e) => Close();
            foreach (var b in new[] { btnDetail, btnPng, btnDelete, btnClose }) b.Margin = new Padding(0, 0, 8, 0);
            flow.Controls.AddRange(new Control[] { btnDetail, btnPng, btnDelete, btnClose });
            bottom.Controls.Add(flow);

            Controls.Add(card);
            Controls.Add(bottom);
            Controls.Add(header);
        }

        private async Task LoadAsync()
        {
            try
            {
                var rows = await _service.GetAllRentalAsync();
                _grid.DataSource = null;
                _grid.DataSource = rows;
                _lblInfo.Text = $"{rows.Count} simülasyon kaydı. Çift tıkla = detay.";
            }
            catch (Exception ex)
            {
                UiHelper.Error("Simülasyon geçmişi getirilemedi.\n\n" + ex.Message);
            }
        }

        private ResultSimulatedRentalDto Current() => _grid.CurrentRow?.DataBoundItem as ResultSimulatedRentalDto;

        private async void ShowDetail()
        {
            var row = Current();
            if (row == null) { UiHelper.Warn("Lütfen bir kayıt seçin."); return; }
            try
            {
                var full = await _service.GetRentalWithExtrasAsync(row.Id) ?? row;
                var extras = full.Extras.Count == 0 ? "  - Yok" :
                    string.Join("\n", full.Extras.Select(e => $"  • {e.ExtraName}: {e.ExtraPrice:N2} TL"));
                UiHelper.Info(
                    $"Simülasyon Kodu: {full.SimulationCode}\n" +
                    $"Durum: {full.SimulationStatus}\n\n" +
                    $"ARAÇ: {full.CarModel} · {full.RentalCompany}\n" +
                    $"  {full.TransmissionType} / {full.FuelType} / {full.Segment}\n" +
                    $"  {full.PickupLocation}\n" +
                    $"  {full.PickupDateTime:dd.MM.yyyy HH:mm} → {full.ReturnDateTime:dd.MM.yyyy HH:mm}  ({full.RentalDays} gün)\n\n" +
                    $"SÜRÜCÜ: {full.DriverFullName}  |  {full.DriverPhone}  |  {full.DriverEmail}\n\n" +
                    $"EK HİZMETLER:\n{extras}\n\n" +
                    $"Araç Fiyatı: {full.BasePrice:N2} TL\n" +
                    $"Ek Hizmetler: {full.ExtraServicesTotal:N2} TL\n" +
                    $"GENEL TOPLAM: {full.GrandTotal:N2} TL\n" +
                    $"Ödeme: {full.PaymentMethod}\n" +
                    $"Oluşturulma: {full.CreatedAt:dd.MM.yyyy HH:mm}",
                    "Simülasyon Detayı");
            }
            catch (Exception ex)
            {
                UiHelper.Error("Detay getirilemedi.\n\n" + ex.Message);
            }
        }

        private async Task ExportSelectedPngAsync()
        {
            var row = Current();
            if (row == null) { UiHelper.Warn("Lütfen PNG için bir kayıt seçin."); return; }
            try
            {
                var full = await _service.GetRentalWithExtrasAsync(row.Id) ?? row;
                using var dlg = new SaveFileDialog { Filter = "PNG (*.png)|*.png", FileName = $"{full.SimulationCode}.png" };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                await _pngService.ExportAsync(dlg.FileName, ToSummary(full));
                UiHelper.Info("Simülasyon özeti PNG olarak kaydedildi.");
            }
            catch (Exception ex)
            {
                UiHelper.Error("PNG oluşturulamadı.\n\n" + ex.Message);
            }
        }

        private async Task DeleteSelectedAsync()
        {
            var row = Current();
            if (row == null) { UiHelper.Warn("Lütfen silinecek kaydı seçin."); return; }
            if (!UiHelper.Confirm($"'{row.SimulationCode}' simülasyonu kalıcı olarak silinecek.\nDevam edilsin mi?", "Simülasyon Sil"))
                return;
            try
            {
                await _service.DeleteRentalAsync(row.Id);
                UiHelper.Info("Simülasyon silindi.");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                UiHelper.Error("Simülasyon silinemedi.\n\n" + ex.Message);
            }
        }

        private static RentalSimulationSummaryDto ToSummary(ResultSimulatedRentalDto r) => new()
        {
            SimulationCode = r.SimulationCode,
            CarModel = r.CarModel,
            RentalCompany = r.RentalCompany,
            TransmissionType = r.TransmissionType,
            FuelType = r.FuelType,
            Segment = r.Segment,
            PickupLocation = r.PickupLocation,
            PickupDateTime = r.PickupDateTime,
            ReturnDateTime = r.ReturnDateTime,
            RentalDays = r.RentalDays,
            Driver = new DriverInfoDto
            {
                FirstName = r.DriverFirstName,
                LastName = r.DriverLastName,
                Phone = r.DriverPhone,
                Email = r.DriverEmail,
                IdentityNo = r.DriverIdentityNo,
                BirthDate = r.DriverBirthDate,
                LicenseNo = r.DriverLicenseNo,
                LicenseDate = r.DriverLicenseDate
            },
            Extras = r.Extras,
            BasePrice = r.BasePrice,
            ExtraServicesTotal = r.ExtraServicesTotal,
            GrandTotal = r.GrandTotal,
            PaymentMethod = r.PaymentMethod,
            SimulationDate = r.CreatedAt
        };
    }
}
