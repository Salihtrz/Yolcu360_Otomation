using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.Common.Logging;
using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.DtoLayer.SimulationDto;
using Yolcu360.PresentationLayer.Helpers;
using Yolcu360.PresentationLayer.UI;

namespace Yolcu360.PresentationLayer.Forms
{
    /// <summary>
    /// Araç Kiralama Simülasyonu sihirbazı (5 adım). Kullanıcı seçili araçla sanki kiralıyormuş gibi
    /// adım adım ilerler; SON adımda GERÇEK rezervasyon/ödeme YAPILMADAN bir simülasyon kaydı oluşur.
    /// </summary>
    public class RentalSimulationForm : Form
    {
        private readonly IRentalSimulationService _service;
        private readonly ISimulationPngService _pngService;
        private readonly ResultCarDto _car;
        private static readonly HttpClient _http = new();

        private const int StepCount = 5;
        private int _step;

        // Navigasyon / başlık
        private readonly Panel _host = new() { Dock = DockStyle.Fill, BackColor = ThemeColors.Background, Padding = new Padding(20, 12, 20, 12) };
        private readonly Label _lblStepTitle = new();
        private readonly FlowLayoutPanel _stepper = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent };
        private readonly List<Label> _stepChips = new();
        private Guna2Button _btnBack, _btnNext, _btnFinish, _btnCancel;

        // Adım panelleri
        private readonly Panel[] _panels = new Panel[StepCount];

        // 1: araç + fiyat
        private decimal _basePrice;
        private Guna2TextBox _txtManualPrice;
        private Panel _manualPriceRow;
        private PictureBox _picCar;

        // 2: sürücü
        private Guna2TextBox _txtFirst, _txtLast, _txtPhone, _txtEmail, _txtIdentity, _txtLicenseNo;
        private DateTimePicker _dtBirth, _dtLicense;

        // 3: ek hizmetler
        private readonly List<(Guna2CheckBox cb, SimulatedRentalExtraDto extra)> _extraChecks = new();
        private Label _lblExtrasTotal;

        // 4: ödeme
        private RadioButton _rbOffice, _rbCredit, _rbDebit;
        private Panel _cardPanel;
        private Guna2TextBox _txtCardName;

        // 5: özet
        private Label _lblSummary;

        /// <summary>Tamamlanan simülasyonun özeti (MainForm mesajı için). Null = tamamlanmadı.</summary>
        public RentalSimulationSummaryDto CompletedSummary { get; private set; }

        public RentalSimulationForm(IRentalSimulationService service, ISimulationPngService pngService, ResultCarDto car)
        {
            _service = service;
            _pngService = pngService;
            _car = car ?? new ResultCarDto();
            _basePrice = _car.Price;
            BuildUi();
            ShowStep(0);
            Load += async (s, e) => await LoadCarImageAsync(_car.ImageUrl);
            LogHelper.Info($"Kiralama simülasyonu ekranı açıldı (araç='{_car.CarModel}').");
        }

        // ---------------------------------------------------------------- UI iskeleti

        private void BuildUi()
        {
            Text = "Araç Kiralama Simülasyonu";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(760, 660);
            MinimumSize = new Size(720, 600);
            BackColor = ThemeColors.Background;
            Font = new Font("Segoe UI", 9.5f);
            MaximizeBox = false;

            // ----- Üst başlık + kalıcı uyarı şeridi -----
            var header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = ThemeColors.Surface };
            header.Controls.Add(new Label
            {
                Text = "Araç Kiralama Simülasyonu",
                AutoSize = true,
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = ThemeColors.TextDark,
                Location = new Point(20, 12)
            });
            var warn = new Label
            {
                Text = "⚠  Bu işlem simülasyon amaçlıdır. Yolcu360 üzerinde gerçek rezervasyon veya ödeme yapılmaz.",
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(0xFE, 0xF3, 0xC7),
                ForeColor = Color.FromArgb(0x92, 0x40, 0x0E),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            header.Controls.Add(warn);

            // ----- Stepper + adım başlığı -----
            var stepBar = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = ThemeColors.Background, Padding = new Padding(20, 10, 20, 4) };
            string[] names = { "1  Araç", "2  Sürücü", "3  Ek Hizmet", "4  Ödeme", "5  Özet" };
            foreach (var n in names)
            {
                var chip = new Label
                {
                    Text = n,
                    AutoSize = false,
                    Size = new Size(128, 28),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Margin = new Padding(0, 0, 8, 0),
                    ForeColor = ThemeColors.TextMuted,
                    BackColor = ThemeColors.Surface
                };
                _stepChips.Add(chip);
                _stepper.Controls.Add(chip);
            }
            var stepperHost = new Panel { Dock = DockStyle.Top, Height = 34 };
            stepperHost.Controls.Add(_stepper);
            _lblStepTitle.Dock = DockStyle.Bottom;
            _lblStepTitle.Height = 24;
            _lblStepTitle.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            _lblStepTitle.ForeColor = ThemeColors.Primary;
            stepBar.Controls.Add(_lblStepTitle);
            stepBar.Controls.Add(stepperHost);

            // ----- Alt buton barı -----
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = ThemeColors.Surface, Padding = new Padding(16, 12, 16, 12) };
            _btnCancel = UiStyleHelper.Button("İptal", ButtonVariant.Secondary, 100);
            _btnCancel.Dock = DockStyle.Left;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            var right = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoSize = true };
            _btnFinish = UiStyleHelper.Button("Simülasyonu Tamamla", ButtonVariant.Success, 200);
            _btnNext = UiStyleHelper.Button("İleri", ButtonVariant.Primary, 120);
            _btnBack = UiStyleHelper.Button("Geri", ButtonVariant.Secondary, 100);
            _btnNext.Margin = _btnBack.Margin = _btnFinish.Margin = new Padding(6, 0, 0, 0);
            _btnNext.Click += (s, e) => GoNext();
            _btnBack.Click += (s, e) => ShowStep(_step - 1);
            _btnFinish.Click += async (s, e) => await CompleteAsync();
            right.Controls.AddRange(new Control[] { _btnFinish, _btnNext, _btnBack });
            bottom.Controls.Add(right);
            bottom.Controls.Add(_btnCancel);

            // ----- Adım panelleri -----
            _panels[0] = BuildCarStep();
            _panels[1] = BuildDriverStep();
            _panels[2] = BuildExtrasStep();
            _panels[3] = BuildPaymentStep();
            _panels[4] = BuildSummaryStep();
            foreach (var p in _panels) { p.Dock = DockStyle.Fill; p.Visible = false; _host.Controls.Add(p); }

            Controls.Add(_host);
            Controls.Add(bottom);
            Controls.Add(stepBar);
            Controls.Add(header);
        }

        // ---------------------------------------------------------------- adımlar

        private Panel BuildCarStep()
        {
            var p = new Panel { BackColor = ThemeColors.Background };
            var card = UiStyleHelper.NewCard();
            card.Dock = DockStyle.Fill; card.Padding = new Padding(18);

            _picCar = new PictureBox { Size = new Size(220, 140), Location = new Point(18, 18), SizeMode = PictureBoxSizeMode.Zoom, BackColor = ThemeColors.Background };

            var info = new Label
            {
                AutoSize = false,
                Location = new Point(260, 16),
                Size = new Size(440, 250),
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = ThemeColors.TextDark
            };
            int days = SimulationCalculator.RentalDays(_car.PickupDateTime, _car.ReturnDateTime);
            info.Text =
                $"Araç Modeli      : {Safe(_car.CarModel)}\n" +
                $"Kiralama Şirketi : {Safe(_car.RentalCompany)}\n" +
                $"Vites Tipi       : {Safe(_car.TransmissionType)}\n" +
                $"Yakıt Tipi       : {Safe(_car.FuelType)}\n" +
                $"Segment          : {Safe(_car.Segment)}\n" +
                $"Alış Lokasyonu   : {Safe(_car.PickupLocation)}\n" +
                $"Alış             : {_car.PickupDateTime:dd.MM.yyyy HH:mm}\n" +
                $"Dönüş            : {_car.ReturnDateTime:dd.MM.yyyy HH:mm}\n" +
                $"Gün Sayısı       : {days} gün\n" +
                $"Fiyat            : {(_car.Price > 0 ? $"{_car.Price:N2} {Safe(_car.Currency)}" : "okunamadı")}";

            // Manuel fiyat satırı (yalnızca araç fiyatı 0/boşsa görünür).
            _manualPriceRow = new Panel { Location = new Point(18, 176), Size = new Size(680, 70), Visible = _basePrice <= 0 };
            var warn = new Label
            {
                Text = "Seçili aracın fiyat bilgisi okunamadı. Simülasyon için manuel fiyat girin (TL):",
                AutoSize = true, ForeColor = ThemeColors.Danger, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Location = new Point(0, 0)
            };
            _txtManualPrice = UiStyleHelper.TextBox(180);
            _txtManualPrice.Location = new Point(0, 28);
            _txtManualPrice.PlaceholderText = "örn. 4500";
            _manualPriceRow.Controls.Add(warn);
            _manualPriceRow.Controls.Add(_txtManualPrice);

            card.Controls.Add(_picCar);
            card.Controls.Add(info);
            card.Controls.Add(_manualPriceRow);
            p.Controls.Add(card);
            return p;
        }

        private Panel BuildDriverStep()
        {
            var p = new Panel { BackColor = ThemeColors.Background };
            var card = UiStyleHelper.NewCard();
            card.Dock = DockStyle.Fill; card.Padding = new Padding(18);

            // Üstten aşağı dikey yığın: önce form ızgarası, sonra not. (Fill yerine TopDown akış
            // kullanılır; böylece satırlar uzayıp etiketler kaymaz.)
            var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };

            var grid = new TableLayoutPanel
            {
                ColumnCount = 4,
                RowCount = 4,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 14)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            for (int r = 0; r < 4; r++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            _txtFirst = UiStyleHelper.TextBox(220);
            _txtLast = UiStyleHelper.TextBox(220);
            _txtPhone = UiStyleHelper.TextBox(220);
            _txtEmail = UiStyleHelper.TextBox(220);
            _txtIdentity = UiStyleHelper.TextBox(220);
            _txtLicenseNo = UiStyleHelper.TextBox(220);
            _dtBirth = NewDate(1990, 1, 1);
            _dtLicense = NewDate(2015, 1, 1);

            AddRow(grid, 0, "Ad", _txtFirst, "Soyad", _txtLast);
            AddRow(grid, 1, "Telefon", _txtPhone, "E-posta", _txtEmail);
            AddRow(grid, 2, "TC / Pasaport No", _txtIdentity, "Doğum Tarihi", _dtBirth);
            AddRow(grid, 3, "Ehliyet No", _txtLicenseNo, "Ehliyet Alış Tarihi", _dtLicense);

            var note = new Label
            {
                Text = "Bu bilgiler yalnızca simülasyon içindir; gerçek siteye gönderilmez.",
                AutoSize = true, ForeColor = ThemeColors.TextMuted, Font = new Font("Segoe UI", 8.5f), Margin = new Padding(2, 4, 0, 0)
            };

            stack.Controls.Add(grid);
            stack.Controls.Add(note);
            card.Controls.Add(stack);
            p.Controls.Add(card);
            return p;
        }

        private Panel BuildExtrasStep()
        {
            var p = new Panel { BackColor = ThemeColors.Background };
            var card = UiStyleHelper.NewCard();
            card.Dock = DockStyle.Fill; card.Padding = new Padding(18);

            int y = 14;
            foreach (var extra in SimulationCatalog.Extras)
            {
                var cb = new Guna2CheckBox
                {
                    Text = $"{extra.ExtraName}   (+{extra.ExtraPrice:N0} TL)",
                    AutoSize = true,
                    Location = new Point(8, y),
                    Font = new Font("Segoe UI", 10.5f),
                    ForeColor = ThemeColors.TextDark
                };
                cb.CheckedState.FillColor = ThemeColors.Primary;
                cb.CheckedState.BorderColor = ThemeColors.Primary;
                cb.UncheckedState.BorderColor = ThemeColors.TextMuted;
                cb.CheckedChanged += (s, e) => UpdateExtrasTotal();
                _extraChecks.Add((cb, extra));
                card.Controls.Add(cb);
                y += 36;
            }
            _lblExtrasTotal = new Label
            {
                Text = "Ek Hizmet Toplamı: 0,00 TL",
                AutoSize = true, Location = new Point(8, y + 8),
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold), ForeColor = ThemeColors.Primary
            };
            card.Controls.Add(_lblExtrasTotal);
            p.Controls.Add(card);
            return p;
        }

        private Panel BuildPaymentStep()
        {
            var p = new Panel { BackColor = ThemeColors.Background };
            var card = UiStyleHelper.NewCard();
            card.Dock = DockStyle.Fill; card.Padding = new Padding(18);

            _rbOffice = NewRadio(SimulationCatalog.PaymentMethods[0], 14, true);
            _rbCredit = NewRadio(SimulationCatalog.PaymentMethods[1], 50, false);
            _rbDebit = NewRadio(SimulationCatalog.PaymentMethods[2], 86, false);
            _rbCredit.CheckedChanged += (s, e) => _cardPanel.Visible = _rbCredit.Checked || _rbDebit.Checked;
            _rbDebit.CheckedChanged += (s, e) => _cardPanel.Visible = _rbCredit.Checked || _rbDebit.Checked;
            _rbOffice.CheckedChanged += (s, e) => _cardPanel.Visible = _rbCredit.Checked || _rbDebit.Checked;

            // Sahte kart alanları (gerçek kart numarası/CVV ALINMAZ).
            _cardPanel = new Panel { Location = new Point(14, 128), Size = new Size(680, 170), Visible = false };
            var lblName = new Label { Text = "Kart Sahibi Adı", AutoSize = true, Location = new Point(0, 6), ForeColor = ThemeColors.TextMuted };
            _txtCardName = UiStyleHelper.TextBox(260); _txtCardName.Location = new Point(0, 28);
            var lblNo = new Label { Text = "Kart Numarası", AutoSize = true, Location = new Point(300, 6), ForeColor = ThemeColors.TextMuted };
            var fakeNo = ReadOnlyBox("**** **** **** 0000 (test)", 300, 28, 240);
            var lblExp = new Label { Text = "Son Kullanma", AutoSize = true, Location = new Point(0, 74), ForeColor = ThemeColors.TextMuted };
            var fakeExp = ReadOnlyBox("12 / 30", 0, 96, 100);
            var lblCvv = new Label { Text = "CVV", AutoSize = true, Location = new Point(140, 74), ForeColor = ThemeColors.TextMuted };
            var fakeCvv = ReadOnlyBox("***", 140, 96, 80);
            var simNote = new Label
            {
                Text = "Bu ekran ödeme simülasyonudur. Gerçek ödeme alınmaz; gerçek kart numarası/CVV istenmez.",
                AutoSize = true, ForeColor = ThemeColors.Danger, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Location = new Point(0, 138)
            };
            _cardPanel.Controls.AddRange(new Control[] { lblName, _txtCardName, lblNo, fakeNo, lblExp, fakeExp, lblCvv, fakeCvv, simNote });

            card.Controls.AddRange(new Control[] { _rbOffice, _rbCredit, _rbDebit, _cardPanel });
            p.Controls.Add(card);
            return p;
        }

        private Panel BuildSummaryStep()
        {
            var p = new Panel { BackColor = ThemeColors.Background };
            var card = UiStyleHelper.NewCard();
            card.Dock = DockStyle.Fill; card.Padding = new Padding(18);
            _lblSummary = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10f), ForeColor = ThemeColors.TextDark, AutoSize = false };
            card.Controls.Add(_lblSummary);
            p.Controls.Add(card);
            return p;
        }

        // ---------------------------------------------------------------- navigasyon

        private void ShowStep(int index)
        {
            _step = Math.Clamp(index, 0, StepCount - 1);
            for (int i = 0; i < StepCount; i++) _panels[i].Visible = (i == _step);

            string[] titles =
            {
                "Adım 1 / 5 — Araç Bilgileri",
                "Adım 2 / 5 — Sürücü Bilgileri",
                "Adım 3 / 5 — Ek Hizmetler",
                "Adım 4 / 5 — Ödeme Simülasyonu",
                "Adım 5 / 5 — Kiralama Özeti"
            };
            _lblStepTitle.Text = titles[_step];

            for (int i = 0; i < _stepChips.Count; i++)
            {
                bool active = i == _step, done = i < _step;
                _stepChips[i].BackColor = active ? ThemeColors.Primary : (done ? ThemeColors.Success : ThemeColors.Surface);
                _stepChips[i].ForeColor = (active || done) ? Color.White : ThemeColors.TextMuted;
            }

            _btnBack.Visible = _step > 0;
            bool last = _step == StepCount - 1;
            _btnNext.Visible = !last;
            _btnFinish.Visible = last;

            if (last) BuildSummaryText();
        }

        private void GoNext()
        {
            var (ok, msg) = ValidateStep(_step);
            if (!ok) { UiHelper.Warn(msg); return; }
            if (_step == 1) LogHelper.Info("Sürücü bilgileri doğrulandı.");
            ShowStep(_step + 1);
        }

        private (bool ok, string msg) ValidateStep(int step)
        {
            switch (step)
            {
                case 0:
                    if (_basePrice <= 0)
                    {
                        if (!decimal.TryParse((_txtManualPrice.Text ?? "").Replace(".", "").Replace(",", "."),
                                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var mp) || mp <= 0)
                            return (false, "Lütfen geçerli bir manuel araç fiyatı girin.");
                        _basePrice = mp;
                    }
                    return (true, null);

                case 1:
                    if (string.IsNullOrWhiteSpace(_txtFirst.Text) || string.IsNullOrWhiteSpace(_txtLast.Text))
                        return (false, "Ad ve Soyad boş olamaz.");
                    if (string.IsNullOrWhiteSpace(_txtPhone.Text))
                        return (false, "Telefon boş olamaz.");
                    if (Regex.Replace(_txtPhone.Text, @"\D", "").Length < 10)
                        return (false, "Telefon numarası geçersiz görünüyor (en az 10 hane).");
                    if (!Regex.IsMatch(_txtEmail.Text ?? "", @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                        return (false, "Geçerli bir e-posta adresi girin.");
                    if (_dtLicense.Value.Date > DateTime.Today)
                        return (false, "Ehliyet alış tarihi bugünden büyük olamaz.");
                    if (_dtBirth.Value.Date > DateTime.Today)
                        return (false, "Doğum tarihi bugünden büyük olamaz.");
                    if (AgeOn(_dtBirth.Value, DateTime.Today) < 18)
                        return (false, "Sürücü 18 yaşından küçük olamaz.");
                    return (true, null);

                default:
                    return (true, null);
            }
        }

        private void BuildSummaryText()
        {
            var extras = SelectedExtras();
            var extrasTotal = SimulationCalculator.ExtrasTotal(extras);
            var grand = SimulationCalculator.GrandTotal(_basePrice, extrasTotal);
            int days = SimulationCalculator.RentalDays(_car.PickupDateTime, _car.ReturnDateTime);
            var extrasText = extras.Count == 0 ? "  - Ek hizmet seçilmedi" :
                string.Join("\n", extras.Select(e => $"  • {e.ExtraName}: {e.ExtraPrice:N2} TL"));

            _lblSummary.Text =
                "ARAÇ\n" +
                $"  {Safe(_car.CarModel)} · {Safe(_car.RentalCompany)} · {Safe(_car.TransmissionType)} / {Safe(_car.FuelType)} / {Safe(_car.Segment)}\n" +
                $"  {Safe(_car.PickupLocation)}  |  {_car.PickupDateTime:dd.MM.yyyy HH:mm} → {_car.ReturnDateTime:dd.MM.yyyy HH:mm}  |  {days} gün\n\n" +
                "SÜRÜCÜ\n" +
                $"  {_txtFirst.Text} {_txtLast.Text}  |  Tel: {_txtPhone.Text}  |  {_txtEmail.Text}\n\n" +
                "EK HİZMETLER\n" +
                $"{extrasText}\n\n" +
                "ÖDEME\n" +
                $"  Yöntem: {SelectedPayment()}\n\n" +
                "FİYAT ÖZETİ\n" +
                $"  Araç Fiyatı        : {_basePrice:N2} TL\n" +
                $"  Ek Hizmetler       : {extrasTotal:N2} TL\n" +
                $"  GENEL TOPLAM       : {grand:N2} TL\n\n" +
                $"Simülasyon Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}\n\n" +
                "⚠  Simülasyonu Tamamla, GERÇEK rezervasyon/ödeme yapmaz; yalnızca uygulama içi kayıt oluşturur.";
        }

        private async Task CompleteAsync()
        {
            var (ok, msg) = ValidateStep(1); // sürücü bilgileri yine de geçerli olmalı
            if (!ok) { ShowStep(1); UiHelper.Warn(msg); return; }

            var dto = new CreateSimulatedRentalDto
            {
                CarModel = _car.CarModel,
                RentalCompany = _car.RentalCompany,
                TransmissionType = _car.TransmissionType,
                FuelType = _car.FuelType,
                Segment = _car.Segment,
                PickupLocation = _car.PickupLocation,
                PickupDateTime = _car.PickupDateTime,
                ReturnDateTime = _car.ReturnDateTime,
                ImageUrl = _car.ImageUrl,
                BasePrice = _basePrice,
                PaymentMethod = SelectedPayment(),
                Driver = new DriverInfoDto
                {
                    FirstName = _txtFirst.Text.Trim(),
                    LastName = _txtLast.Text.Trim(),
                    Phone = _txtPhone.Text.Trim(),
                    Email = _txtEmail.Text.Trim(),
                    IdentityNo = _txtIdentity.Text.Trim(),
                    BirthDate = _dtBirth.Value.Date,
                    LicenseNo = _txtLicenseNo.Text.Trim(),
                    LicenseDate = _dtLicense.Value.Date
                },
                Extras = SelectedExtras()
            };

            try
            {
                SetBusy(true);
                CompletedSummary = await _service.CreateAsync(dto);

                if (UiHelper.Confirm(
                        $"Kiralama simülasyonu tamamlandı.\nSimülasyon Kodu: {CompletedSummary.SimulationCode}\n\nSimülasyon özetini PNG olarak kaydetmek ister misiniz?",
                        "Simülasyon Tamamlandı"))
                {
                    await ExportPngAsync(CompletedSummary);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                LogHelper.Error("Simülasyon kaydedilemedi.", ex);
                UiHelper.Error("Simülasyon kaydedilemedi:\n" + ex.Message);
            }
            finally { SetBusy(false); }
        }

        private async Task ExportPngAsync(RentalSimulationSummaryDto summary)
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "PNG (*.png)|*.png",
                FileName = $"{summary.SimulationCode}.png"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            await _pngService.ExportAsync(dlg.FileName, summary);
            UiHelper.Info("Simülasyon özeti PNG olarak kaydedildi.");
        }

        // ---------------------------------------------------------------- yardımcılar

        private List<SimulatedRentalExtraDto> SelectedExtras()
            => _extraChecks.Where(x => x.cb.Checked)
                .Select(x => new SimulatedRentalExtraDto { ExtraName = x.extra.ExtraName, ExtraPrice = x.extra.ExtraPrice })
                .ToList();

        private string SelectedPayment()
            => _rbCredit.Checked ? SimulationCatalog.PaymentMethods[1]
             : _rbDebit.Checked ? SimulationCatalog.PaymentMethods[2]
             : SimulationCatalog.PaymentMethods[0];

        private void UpdateExtrasTotal()
            => _lblExtrasTotal.Text = $"Ek Hizmet Toplamı: {SimulationCalculator.ExtrasTotal(SelectedExtras()):N2} TL";

        private void SetBusy(bool busy)
        {
            _btnFinish.Enabled = _btnBack.Enabled = _btnNext.Enabled = _btnCancel.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private async Task LoadCarImageAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
                using var ms = new MemoryStream(bytes);
                _picCar.Image = Image.FromStream(ms);
            }
            catch { /* görsel yüklenemezse boş kalır */ }
        }

        private static int AgeOn(DateTime birth, DateTime on)
        {
            var age = on.Year - birth.Year;
            if (birth.Date > on.AddYears(-age)) age--;
            return age;
        }

        private static DateTimePicker NewDate(int y, int m, int d) => new()
        {
            Format = DateTimePickerFormat.Short,
            Width = 220,
            Value = new DateTime(y, m, d),
            Font = new Font("Segoe UI", 9.75f)
        };

        private RadioButton NewRadio(string text, int y, bool check) => new()
        {
            Text = text,
            AutoSize = true,
            Checked = check,
            Location = new Point(14, y),
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = ThemeColors.TextDark
        };

        private static Guna2TextBox ReadOnlyBox(string text, int x, int y, int w)
        {
            var t = UiStyleHelper.TextBox(w);
            t.Location = new Point(x, y);
            t.Text = text;
            t.ReadOnly = true;
            t.FillColor = ThemeColors.Background;
            return t;
        }

        private static void AddRow(TableLayoutPanel grid, int row, string l1, Control c1, string l2, Control c2)
        {
            // Etiketler ve girişler hücre içinde dikey ORTALANIR (Anchor = Left) → aynı hizada dururlar.
            c1.Anchor = c2.Anchor = AnchorStyles.Left;
            grid.Controls.Add(new Label { Text = l1, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(2, 3, 6, 3), ForeColor = ThemeColors.TextMuted }, 0, row);
            grid.Controls.Add(c1, 1, row);
            grid.Controls.Add(new Label { Text = l2, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(10, 3, 6, 3), ForeColor = ThemeColors.TextMuted }, 2, row);
            grid.Controls.Add(c2, 3, row);
        }

        private static string Safe(string s) => string.IsNullOrWhiteSpace(s) ? "-" : s;
    }
}
