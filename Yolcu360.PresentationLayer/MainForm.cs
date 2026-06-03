using Guna.UI2.WinForms;
using Yolcu360.BusinessLayer;
using Yolcu360.Common.Constants;
using Yolcu360.Common.Logging;
using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.DtoLayer.ReportDto;
using Yolcu360.DtoLayer.SearchDto;
using Yolcu360.PresentationLayer.Helpers;
using Yolcu360.PresentationLayer.UI;

namespace Yolcu360.PresentationLayer
{
    public class MainForm : Form
    {
        private readonly AppServices _services = new();
        private readonly HttpClient _http = new();
        private readonly List<Control> _actionButtons = new();

        private List<ResultCarDto> _allCars = new();
        private List<ResultCarDto> _displayedCars = new();
        private CancellationTokenSource _searchCts;
        private CancellationTokenSource _imageCts;
        private OtpLoginForm _otpLoginForm;
        private bool _browserVisible = true;
        private string _sortProperty;
        private bool _sortAsc = true;

        private SplitContainer _split;
        private Guna2TextBox _txtPickup;
        private Guna2DateTimePicker _dtPickup;
        private Guna2DateTimePicker _dtReturn;
        private Guna2ComboBox _cmbPickupHour;
        private Guna2ComboBox _cmbReturnHour;
        private Guna2ComboBox _cmbProfiles;
        private Guna2TextBox _txtSegment;
        private Guna2TextBox _txtBrand;
        private Guna2TextBox _txtCompany;
        private Guna2TextBox _txtMinPrice;
        private Guna2TextBox _txtMaxPrice;
        private Guna2CheckBox _chkAutomatic;
        private Guna2CheckBox _chkManual;
        private Guna2CheckBox _chkGasoline;
        private Guna2CheckBox _chkDiesel;
        private Guna2CheckBox _chkHybrid;
        private Guna2CheckBox _chkElectric;
        private NumericUpDown _numHighlight;
        private DataGridView _dgv;
        private Label _lblStatus;
        private Label _lblLoginStatus;
        private Label _lblDbStatus;
        private Label _lblLastOp;
        private Label _lblLastUpdate;
        private Label _lblCarCount;
        private Label _lblStats;
        private Label _statTotal;
        private Label _statMin;
        private Label _statAvg;
        private Label _statMedian;
        private Label _statMax;
        private Label _statCheapest;
        private Label _lblCarTitle;
        private PictureBox _picCar;
        private Panel _browserHost;

        public MainForm()
        {
            Text = "Yolcu360 - Arac Kiralama Otomasyonu ve Raporlama Paneli";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1280, 760);
            WindowState = FormWindowState.Maximized;
            BackColor = ThemeColors.Background;
            Font = UiStyleHelper.BaseFont;

            BuildUi();
            Load += OnLoadAsync;
            FormClosing += OnClosing;
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = ThemeColors.Background
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildSidebar(), 0, 0);
            root.Controls.Add(BuildMainArea(), 1, 0);
            Controls.Add(root);
        }

        private Control BuildSidebar()
        {
            var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = ThemeColors.SidebarDark, Padding = new Padding(12, 20, 12, 16) };
            sidebar.Controls.Add(new Label
            {
                Text = "Yolcu360",
                AutoSize = true,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(14, 18)
            });
            sidebar.Controls.Add(new Label
            {
                Text = "Otomasyon Paneli",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = ThemeColors.Accent,
                Location = new Point(16, 58)
            });

            var menu = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Location = new Point(0, 105),
                Size = new Size(208, 300),
                BackColor = Color.Transparent
            };
            menu.Controls.Add(SideButton("Arac Ara", (s, e) => SetStatus("Arama paneli hazir."), true));
            menu.Controls.Add(SideButton("Sonuclar", (s, e) => _dgv.Focus()));
            menu.Controls.Add(SideButton("Gecmis Raporlar", async (s, e) => await OpenReportsAsync()));
            menu.Controls.Add(SideButton("Rapor Karsilastir", (s, e) => new ReportCompareForm(_services.ReportService).Show(this)));
            menu.Controls.Add(SideButton("Tarayici", (s, e) => RevealBrowser(!_browserVisible)));
            sidebar.Controls.Add(menu);

            _lblLoginStatus = SideStatus("Giris: yapilmadi", 890, ThemeColors.SidebarText);
            _lblDbStatus = SideStatus("Veritabani: bekleniyor", 916, ThemeColors.SidebarText);
            _lblLastOp = SideStatus("Son islem: hazir", 942, ThemeColors.SidebarText);
            sidebar.Controls.Add(_lblLoginStatus);
            sidebar.Controls.Add(_lblDbStatus);
            sidebar.Controls.Add(_lblLastOp);
            return sidebar;
        }

        private Guna2Button SideButton(string text, EventHandler onClick, bool active = false)
        {
            var btn = UiStyleHelper.Button(text, ButtonVariant.Sidebar, 200, 42);
            btn.Margin = new Padding(0, 0, 0, 6);
            btn.Click += onClick;
            UiStyleHelper.SetSidebarActive(btn, active);
            return btn;
        }

        private static Label SideStatus(string text, int y, Color color) => new()
        {
            Text = "• " + text,
            AutoSize = false,
            Size = new Size(205, 24),
            Location = new Point(18, y),
            ForeColor = color,
            Font = new Font("Segoe UI", 9f),
            BackColor = Color.Transparent
        };

        private Control BuildMainArea()
        {
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = ThemeColors.Background };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            main.Controls.Add(BuildHeader(), 0, 0);

            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterWidth = 6,
                BackColor = ThemeColors.Border,
                Panel2Collapsed = false
            };
            _split.Panel1.Controls.Add(BuildContent());
            _split.Panel2.Controls.Add(BuildBrowserPanel());
            main.Controls.Add(_split, 0, 1);

            var statusBar = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _lblStatus = new Label { Text = "Hazir.", AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0), ForeColor = ThemeColors.TextDark };
            _lblLastUpdate = new Label { Text = "-", AutoSize = false, Dock = DockStyle.Right, Width = 190, TextAlign = ContentAlignment.MiddleRight, ForeColor = ThemeColors.TextMuted, Padding = new Padding(0, 0, 12, 0) };
            statusBar.Controls.Add(_lblStatus);
            statusBar.Controls.Add(_lblLastUpdate);
            main.Controls.Add(statusBar, 0, 2);
            return main;
        }

        private Control BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(26, 16, 12, 10) };
            header.Controls.Add(new Label
            {
                Text = "Arac Kiralama Otomasyonu",
                AutoSize = true,
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = ThemeColors.TextDark,
                Location = new Point(26, 18)
            });
            header.Controls.Add(new Label
            {
                Text = "Yolcu360 arama, filtreleme ve raporlama paneli",
                AutoSize = true,
                ForeColor = ThemeColors.TextMuted,
                Location = new Point(28, 48)
            });

            var actions = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Right,
                Width = 650,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 12, 0, 0)
            };
            var btnToggle = UiStyleHelper.Button("Tarayiciyi Goster/Gizle", ButtonVariant.Secondary, 210);
            btnToggle.Click += (s, e) => RevealBrowser(!_browserVisible);
            var btnSms = UiStyleHelper.Button("Giris Yap (SMS)", ButtonVariant.Accent, 170);
            btnSms.Click += OnAutoLoginClick;
            _actionButtons.Add(btnSms);
            actions.Controls.Add(btnToggle);
            actions.Controls.Add(btnSms);
            header.Controls.Add(actions);
            return header;
        }

        private Control BuildContent()
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ThemeColors.Background, Padding = new Padding(0, 0, 10, 0) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 4, Padding = new Padding(4) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 355));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 430));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
            layout.Controls.Add(UiStyleHelper.Card("Arac Arama Bilgileri", BuildSearchPanel()), 0, 0);
            layout.Controls.Add(UiStyleHelper.Card("Filtreler", BuildFilterPanel()), 1, 0);
            layout.Controls.Add(UiStyleHelper.Card("Arac Sonuclari", BuildGridPanel()), 0, 1);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, 1), 2);
            layout.Controls.Add(UiStyleHelper.Card("Ozet Istatistikler", BuildStatsPanel()), 0, 2);
            layout.Controls.Add(UiStyleHelper.Card("Secili Arac", BuildSelectedCarPanel()), 1, 2);
            scroll.Controls.Add(layout);
            return scroll;
        }

        private Control BuildSearchPanel()
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, RowCount = 5, AutoSize = true, BackColor = Color.Transparent };
            for (var i = 0; i < 4; i++) p.ColumnStyles.Add(new ColumnStyle(i % 2 == 0 ? SizeType.Absolute : SizeType.Percent, i % 2 == 0 ? 90 : 50));

            _txtPickup = UiStyleHelper.TextBox(260);
            _txtPickup.PlaceholderText = "Orn: Istanbul Havalimani";
            _dtPickup = UiStyleHelper.DatePicker(160);
            _dtPickup.Value = DateTime.Today.AddDays(1);
            _dtReturn = UiStyleHelper.DatePicker(160);
            _dtReturn.Value = DateTime.Today.AddDays(4);
            _cmbPickupHour = HourCombo();
            _cmbReturnHour = HourCombo();
            _cmbProfiles = UiStyleHelper.Combo(160);

            AddRow(p, 0, "Alis Yeri", _txtPickup, "Alis Tarihi", _dtPickup);
            AddRow(p, 1, "Alis Saati", _cmbPickupHour, "Donus Tarihi", _dtReturn);
            AddRow(p, 2, "Donus Saati", _cmbReturnHour, "Profil", _cmbProfiles);

            var btnSearch = UiStyleHelper.Button("Ara", ButtonVariant.Primary, 110);
            btnSearch.Click += async (s, e) => await SearchAsync();
            var btnClear = UiStyleHelper.Button("Temizle", ButtonVariant.Secondary, 100);
            btnClear.Click += (s, e) => ClearSearch();
            var btnProfile = UiStyleHelper.Button("Yukle ve Ara", ButtonVariant.Accent, 140);
            btnProfile.Click += async (s, e) => await LoadSelectedProfileAndSearchAsync();
            var btnDeleteProfile = UiStyleHelper.Button("Sil", ButtonVariant.Danger, 70);
            btnDeleteProfile.Click += async (s, e) => await DeleteSelectedProfileAsync();
            _actionButtons.AddRange(new Control[] { btnSearch, btnProfile });

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            buttons.Controls.AddRange(new Control[] { btnSearch, btnClear, btnProfile, btnDeleteProfile });
            p.Controls.Add(buttons, 1, 3);
            p.SetColumnSpan(buttons, 3);
            return p;
        }

        private Control BuildFilterPanel()
        {
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
            var checks = new FlowLayoutPanel { Width = 460, Height = 68, BackColor = Color.Transparent };
            _chkAutomatic = UiStyleHelper.Check("Otomatik");
            _chkManual = UiStyleHelper.Check("Manuel");
            _chkGasoline = UiStyleHelper.Check("Benzin");
            _chkDiesel = UiStyleHelper.Check("Dizel");
            _chkHybrid = UiStyleHelper.Check("Hibrit");
            _chkElectric = UiStyleHelper.Check("Elektrik");
            checks.Controls.AddRange(new Control[] { _chkAutomatic, _chkManual, _chkGasoline, _chkDiesel, _chkHybrid, _chkElectric });
            p.Controls.Add(checks);

            var grid = new TableLayoutPanel { Width = 470, AutoSize = true, ColumnCount = 3, RowCount = 4, BackColor = Color.Transparent };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            _txtSegment = UiStyleHelper.TextBox(120);
            _txtBrand = UiStyleHelper.TextBox(120);
            _txtCompany = UiStyleHelper.TextBox(140);
            _txtMinPrice = UiStyleHelper.TextBox(100);
            _txtMaxPrice = UiStyleHelper.TextBox(100);
            AddField(grid, 0, 0, "Segment", _txtSegment);
            AddField(grid, 1, 0, "Marka", _txtBrand);
            AddField(grid, 2, 0, "Sirket", _txtCompany);
            AddField(grid, 0, 2, "Min Fiyat", _txtMinPrice);
            AddField(grid, 1, 2, "Max Fiyat", _txtMaxPrice);
            p.Controls.Add(grid);

            var btnApply = UiStyleHelper.Button("Filtreleri Uygula", ButtonVariant.Accent, 170);
            btnApply.Click += async (s, e) => await ApplyFiltersAsync();
            var btnClear = UiStyleHelper.Button("Filtreleri Temizle", ButtonVariant.Secondary, 180);
            btnClear.Click += (s, e) => ClearFilters();
            var actions = new FlowLayoutPanel { Width = 470, Height = 42, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent };
            actions.Controls.Add(btnApply);
            actions.Controls.Add(btnClear);
            p.Controls.Add(actions);
            return p;
        }

        private Control BuildGridPanel()
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent };
            p.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent };
            var btnSave = UiStyleHelper.Button("Sonuclari Kaydet", ButtonVariant.Primary, 155);
            btnSave.Click += async (s, e) => await SaveReportAsync();
            var btnDelete = UiStyleHelper.Button("Secili Raporu Sil", ButtonVariant.Danger, 150);
            btnDelete.Click += (s, e) => { _displayedCars.Clear(); _allCars.Clear(); BindGrid(); };
            var btnPng = UiStyleHelper.Button("PNG", ButtonVariant.Secondary, 80);
            btnPng.Click += async (s, e) => await ExportPngAsync();
            var btnCsv = UiStyleHelper.Button("CSV", ButtonVariant.Secondary, 80);
            btnCsv.Click += async (s, e) => await ExportCsvAsync();
            var btnExcel = UiStyleHelper.Button("Excel", ButtonVariant.Secondary, 90);
            btnExcel.Click += async (s, e) => await ExportExcelAsync();
            _lblCarCount = new Label
            {
                Text = "0 arac",
                AutoSize = false,
                Width = 90,
                Height = 34,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = ThemeColors.TextMuted
            };
            actions.Controls.AddRange(new Control[] { btnSave, btnDelete, btnPng, btnCsv, btnExcel, _lblCarCount });
            p.Controls.Add(actions, 0, 0);

            _dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _dgv.Columns.AddRange(
                Col("Arac Modeli", "CarModel", 145),
                Col("Kiralama Sirketi", "RentalCompany", 110),
                Col("Vites Tipi", "TransmissionType", 85),
                Col("Yakit Tipi", "FuelType", 80),
                Col("Segment", "Segment", 80),
                Col("Fiyat", "Price", 80),
                Col("Para Birimi", "Currency", 70),
                Col("Alis Lokasyonu", "PickupLocation", 130),
                Col("Alis Tarihi", "PickupDateTime", 110));
            _dgv.CellMouseClick += OnGridHeaderClick;
            _dgv.SelectionChanged += (s, e) => ShowSelectedRowDetail();
            DataGridViewStyleHelper.Apply(_dgv);
            p.Controls.Add(_dgv, 0, 1);
            return p;
        }

        private Control BuildStatsPanel()
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent };
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 74, BackColor = Color.Transparent };
            _statTotal = Stat(top, "Toplam Arac");
            _statMin = Stat(top, "En Dusuk Fiyat");
            _statAvg = Stat(top, "Ortalama Fiyat");
            _statMedian = Stat(top, "Medyan Fiyat");
            _statMax = Stat(top, "En Yuksek Fiyat");
            _statCheapest = Stat(top, "En ucuz N vurgula:");
            _numHighlight = new NumericUpDown { Width = 54, Minimum = 0, Maximum = 20, Value = 3 };
            _numHighlight.ValueChanged += (s, e) => ApplyRowHighlighting();
            top.Controls.Add(_numHighlight);
            _lblStats = new Label { Dock = DockStyle.Fill, ForeColor = ThemeColors.TextMuted, Font = new Font("Segoe UI", 9f), Text = "Henuz sonuc yok." };
            p.Controls.Add(top, 0, 0);
            p.Controls.Add(_lblStats, 0, 1);
            return p;
        }

        private Label Stat(FlowLayoutPanel parent, string title)
        {
            var box = new Label
            {
                Text = "-",
                AutoSize = false,
                Width = 118,
                Height = 52,
                Margin = new Padding(0, 0, 8, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = ThemeColors.TextDark,
                BackColor = Color.White
            };
            box.Tag = title;
            parent.Controls.Add(box);
            return box;
        }

        private Control BuildSelectedCarPanel()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _picCar = new PictureBox { Dock = DockStyle.Left, Width = 180, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
            _lblCarTitle = new Label { Dock = DockStyle.Fill, Text = "-", Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = ThemeColors.TextDark, Padding = new Padding(18) };
            p.Controls.Add(_lblCarTitle);
            p.Controls.Add(_picCar);
            return p;
        }

        private Control BuildBrowserPanel()
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = ThemeColors.SidebarDark };
            var top = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = ThemeColors.SidebarDark };
            top.Controls.Add(new Label { Text = "Yolcu360 Tarayici Onizleme", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(16, 11) });
            var hide = UiStyleHelper.Button("Gizle", ButtonVariant.Danger, 80, 30);
            hide.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            hide.Location = new Point(470, 6);
            hide.Click += (s, e) => RevealBrowser(false);
            top.Controls.Add(hide);

            _browserHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _browserHost.Controls.Add(new Label
            {
                Text = "Tarayici baslatiliyor...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ThemeColors.TextMuted
            });
            p.Controls.Add(_browserHost);
            p.Controls.Add(top);
            return p;
        }

        private async void OnLoadAsync(object sender, EventArgs e)
        {
            SetBusy(true);
            try
            {
                await _services.DatabaseInitializer.InitializeAsync();
                _lblDbStatus.Text = "• Veritabani: bagli";
                _lblDbStatus.ForeColor = ThemeColors.Success;
                await _services.BrowserService.InitializeAsync(Yolcu360Constants.HomeUrl);
                AttachBrowserControl();
                await LoadProfilesAsync();
                SetBrowserSplit();
                SetStatus("Hazir.");
            }
            catch (Exception ex)
            {
                LogHelper.Error("Uygulama baslatilamadi.", ex);
                UiHelper.Error("Uygulama baslatilamadi:\n" + ex.Message);
                SetStatus("Baslatma hatasi.");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            try { _searchCts?.Cancel(); } catch { }
            try { _imageCts?.Cancel(); } catch { }
            try { _http.Dispose(); } catch { }
        }

        private void AttachBrowserControl()
        {
            var browser = _services.BrowserService.GetBrowserControl();
            if (browser == null)
            {
                LogHelper.Warning("Tarayici kontrolu initialize sonrasi null dondu.");
                return;
            }

            browser.Dock = DockStyle.Fill;
            _browserHost.Controls.Clear();
            _browserHost.Controls.Add(browser);
        }

        private async Task SearchAsync()
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            SetBusy(true);
            try
            {
                var request = BuildSearchRequest();
                var progress = new Progress<string>(SetStatus);
                _allCars = await _services.AutomationService.SearchAsync(request, progress, _searchCts.Token);
                _displayedCars = _allCars.ToList();
                BindGrid();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogHelper.Error("Arama basarisiz.", ex);
                UiHelper.Error("Arama basarisiz:\n" + ex.Message);
            }
            finally { SetBusy(false); }
        }

        private SearchRequestDto BuildSearchRequest()
        {
            return new SearchRequestDto
            {
                PickupLocation = string.IsNullOrWhiteSpace(_txtPickup.Text) ? "Istanbul Havalimani" : _txtPickup.Text.Trim(),
                PickupDate = _dtPickup.Value.Date,
                ReturnDate = _dtReturn.Value.Date,
                PickupTime = TimeSpan.Parse(_cmbPickupHour.Text),
                ReturnTime = TimeSpan.Parse(_cmbReturnHour.Text)
            };
        }

        private FilterRequestDto BuildFilterRequest()
        {
            var filter = new FilterRequestDto
            {
                Segment = _txtSegment.Text?.Trim(),
                Brand = _txtBrand.Text?.Trim(),
                RentalCompany = _txtCompany.Text?.Trim()
            };
            if (_chkAutomatic.Checked) filter.TransmissionTypes.Add("Otomatik");
            if (_chkManual.Checked) filter.TransmissionTypes.Add("Manuel");
            if (_chkGasoline.Checked) filter.FuelTypes.Add("Benzin");
            if (_chkDiesel.Checked) filter.FuelTypes.Add("Dizel");
            if (_chkHybrid.Checked) filter.FuelTypes.Add("Hibrit");
            if (_chkElectric.Checked) filter.FuelTypes.Add("Elektrik");
            if (decimal.TryParse(_txtMinPrice.Text, out var min)) filter.MinPrice = min;
            if (decimal.TryParse(_txtMaxPrice.Text, out var max)) filter.MaxPrice = max;
            return filter;
        }

        private async Task ApplyFiltersAsync()
        {
            if (_allCars.Count == 0)
            {
                UiHelper.Warn("Filtrelemek icin once arama yapin.");
                return;
            }
            SetBusy(true);
            try
            {
                var filter = BuildFilterRequest();
                var usedWebsite = filter.HasAnyFilter && await _services.AutomationService.ApplyFiltersOnWebsiteAsync(filter);
                _displayedCars = usedWebsite
                    ? await _services.AutomationService.ScrapeCurrentResultsAsync(BuildSearchRequest())
                    : _services.AutomationService.ApplyFiltersLocally(_allCars, filter);
                BindGrid();
            }
            catch (Exception ex)
            {
                LogHelper.Error("Filtreleme basarisiz.", ex);
                UiHelper.Error("Filtreleme basarisiz:\n" + ex.Message);
            }
            finally { SetBusy(false); }
        }

        private async Task SaveReportAsync()
        {
            if (_displayedCars.Count == 0)
            {
                UiHelper.Warn("Kaydedilecek sonuc yok.");
                return;
            }
            var name = PromptDialog.Show("Rapor adi", "Rapor adini girin:");
            if (string.IsNullOrWhiteSpace(name)) return;
            var req = BuildSearchRequest();
            var dto = new CreateReportDto
            {
                ReportName = name.Trim(),
                PickupLocation = req.PickupLocation,
                PickupDateTime = req.PickupDateTime,
                ReturnDateTime = req.ReturnDateTime,
                Cars = _displayedCars.ToList()
            };
            try
            {
                if (await _services.ReportService.ReportNameExistsAsync(dto.ReportName))
                {
                    if (!UiHelper.Confirm("Bu isimde rapor var. Uzerine yazilsin mi?")) return;
                    await _services.ReportService.OverwriteReportAsync(dto.ReportName, dto);
                }
                else
                {
                    await _services.ReportService.SaveReportAsync(dto);
                }
                SetStatus("Rapor kaydedildi.");
            }
            catch (Exception ex)
            {
                LogHelper.Error("Rapor kaydedilemedi.", ex);
                UiHelper.Error("Rapor kaydedilemedi:\n" + ex.Message);
            }
        }

        private async Task OpenReportsAsync()
        {
            using var form = new ReportsForm(_services.ReportService);
            if (form.ShowDialog(this) != DialogResult.OK || form.SelectedReport == null) return;
            try
            {
                _allCars = await _services.ReportService.GetReportCarsAsync(form.SelectedReport.Id);
                _displayedCars = _allCars.ToList();
                BindGrid();
                SetStatus("Gecmis rapor yuklendi.");
            }
            catch (Exception ex)
            {
                UiHelper.Error("Rapor yuklenemedi:\n" + ex.Message);
            }
        }

        private async Task ExportPngAsync()
        {
            if (_displayedCars.Count == 0) { UiHelper.Warn("Aktarilacak sonuc yok."); return; }
            using var dlg = new SaveFileDialog { Filter = "PNG (*.png)|*.png", FileName = "yolcu360-rapor.png" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var req = BuildSearchRequest();
            await _services.PngReportService.ExportAsync(dlg.FileName, "Yolcu360 Raporu", req.PickupLocation, req.PickupDateTime, req.ReturnDateTime, _displayedCars);
            SetStatus("PNG olusturuldu.");
        }

        private async Task ExportCsvAsync()
        {
            if (_displayedCars.Count == 0) { UiHelper.Warn("Aktarilacak sonuc yok."); return; }
            using var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "yolcu360-rapor.csv" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            await _services.CsvReportService.ExportAsync(dlg.FileName, _displayedCars);
            SetStatus("CSV olusturuldu.");
        }

        private async Task ExportExcelAsync()
        {
            if (_displayedCars.Count == 0) { UiHelper.Warn("Aktarilacak sonuc yok."); return; }
            using var dlg = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = "yolcu360-rapor.xlsx" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            await _services.ExcelReportService.ExportAsync(dlg.FileName, "Yolcu360 Raporu", _displayedCars);
            SetStatus("Excel olusturuldu.");
        }

        private async Task LoadProfilesAsync()
        {
            try
            {
                var profiles = await _services.SearchProfileService.GetAllAsync();
                _cmbProfiles.DataSource = profiles;
                _cmbProfiles.DisplayMember = "ProfileName";
                _cmbProfiles.ValueMember = "Id";
            }
            catch (Exception ex) { LogHelper.Warning("Profiller yuklenemedi: " + ex.Message); }
        }

        private async Task LoadSelectedProfileAndSearchAsync()
        {
            dynamic profile = _cmbProfiles.SelectedItem;
            if (profile == null) return;
            try
            {
                _txtPickup.Text = profile.PickupLocation;
                _dtPickup.Value = profile.PickupDateTime;
                _dtReturn.Value = profile.ReturnDateTime;
                _cmbPickupHour.Text = profile.PickupDateTime.ToString("HH\\:mm");
                _cmbReturnHour.Text = profile.ReturnDateTime.ToString("HH\\:mm");
                await SearchAsync();
            }
            catch { UiHelper.Warn("Profil yuklenemedi."); }
        }

        private async Task DeleteSelectedProfileAsync()
        {
            dynamic profile = _cmbProfiles.SelectedItem;
            if (profile == null) return;
            if (!UiHelper.Confirm("Secili profil silinsin mi?")) return;
            await _services.SearchProfileService.DeleteAsync((int)profile.Id);
            await LoadProfilesAsync();
        }

        private void OnAutoLoginClick(object sender, EventArgs e)
        {
            if (_otpLoginForm != null && !_otpLoginForm.IsDisposed)
            {
                _otpLoginForm.Show();
                _otpLoginForm.Activate();
                return;
            }
            _otpLoginForm = new OtpLoginForm(_services, RevealBrowser, MarkLoggedIn);
            _otpLoginForm.FormClosed += (s, args) => _otpLoginForm = null;
            _otpLoginForm.Show(this);
        }

        private void RevealBrowser(bool show) => UiHelper.RunOnUi(this, () =>
        {
            _browserVisible = show;
            _split.Panel2Collapsed = !show;
            if (show) SetBrowserSplit();
            _services.BrowserService.SetBrowserVisible(show);
            SetStatus(show ? "Tarayici gosteriliyor." : "Tarayici gizlendi.");
        });

        private void MarkLoggedIn(bool ok) => UiHelper.RunOnUi(this, () =>
        {
            _lblLoginStatus.Text = ok ? "• Giris: yapildi" : "• Giris: dogrulanamadi";
            _lblLoginStatus.ForeColor = ok ? ThemeColors.Success : ThemeColors.Warning;
        });

        private void BindGrid()
        {
            _dgv.DataSource = null;
            _dgv.DataSource = _displayedCars;
            UpdateStatistics();
            ApplyRowHighlighting();
            SetStatus(_displayedCars.Count == 0 ? "Liste bos." : $"{_displayedCars.Count} arac listeleniyor.");
        }

        private void UpdateStatistics()
        {
            var cars = _displayedCars ?? new List<ResultCarDto>();
            if (cars.Count == 0)
            {
                _lblStats.Text = "Henuz sonuc yok.";
                foreach (var l in new[] { _statTotal, _statMin, _statAvg, _statMedian, _statMax, _statCheapest })
                    l.Text = "-";
                _lblCarCount.Text = "0 arac";
                return;
            }
            var priced = cars.Where(c => c.Price > 0).ToList();
            var cur = priced.FirstOrDefault()?.Currency ?? "TL";
            var min = priced.Count > 0 ? priced.Min(c => c.Price) : 0;
            var max = priced.Count > 0 ? priced.Max(c => c.Price) : 0;
            var avg = priced.Count > 0 ? priced.Average(c => c.Price) : 0;
            var median = Median(priced.Select(c => c.Price).ToList());
            var cheapest = priced.OrderBy(c => c.Price).FirstOrDefault();
            _statTotal.Text = cars.Count.ToString();
            _statMin.Text = $"{min:N0} {cur}";
            _statAvg.Text = $"{avg:N0} {cur}";
            _statMedian.Text = $"{median:N0} {cur}";
            _statMax.Text = $"{max:N0} {cur}";
            _statCheapest.Text = cheapest != null ? $"{cheapest.CarModel} {cheapest.Price:N0} {cur}" : "-";
            _lblCarCount.Text = $"{cars.Count} arac";
            _lblStats.Text = $"Firma: {GroupSummary(cars, c => c.RentalCompany)}\nYakit: {GroupSummary(cars, c => c.FuelType)}\nVites: {GroupSummary(cars, c => c.TransmissionType)}";
        }

        private static string GroupSummary(IEnumerable<ResultCarDto> cars, Func<ResultCarDto, string> selector)
            => string.Join(", ", cars.GroupBy(c => string.IsNullOrWhiteSpace(selector(c)) ? "Diger" : selector(c)).OrderByDescending(g => g.Count()).Take(6).Select(g => $"{g.Key}: {g.Count()}"));

        private void OnGridHeaderClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0 || e.ColumnIndex < 0 || _displayedCars.Count == 0) return;
            var prop = _dgv.Columns[e.ColumnIndex].DataPropertyName;
            if (string.IsNullOrEmpty(prop)) return;
            _sortAsc = _sortProperty == prop ? !_sortAsc : true;
            _sortProperty = prop;
            var pi = typeof(ResultCarDto).GetProperty(prop);
            if (pi == null) return;
            _displayedCars = (_sortAsc ? _displayedCars.OrderBy(c => pi.GetValue(c)) : _displayedCars.OrderByDescending(c => pi.GetValue(c))).ToList();
            _dgv.DataSource = null;
            _dgv.DataSource = _displayedCars;
            ApplyRowHighlighting();
        }

        private void ApplyRowHighlighting()
        {
            if (_dgv == null || _dgv.Rows.Count == 0) return;
            foreach (DataGridViewRow row in _dgv.Rows) row.DefaultCellStyle.BackColor = Color.White;
            var n = (int)(_numHighlight?.Value ?? 0);
            if (n <= 0) return;
            var cheapest = _displayedCars.Where(c => c.Price > 0).OrderBy(c => c.Price).Take(n).ToHashSet();
            foreach (DataGridViewRow row in _dgv.Rows)
                if (row.DataBoundItem is ResultCarDto car && cheapest.Contains(car))
                    row.DefaultCellStyle.BackColor = ThemeColors.RowHighlight;
        }

        private static decimal Median(List<decimal> values)
        {
            if (values.Count == 0) return 0;
            values.Sort();
            return values.Count % 2 == 1 ? values[values.Count / 2] : (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2m;
        }

        private void ShowSelectedRowDetail()
        {
            if (_dgv.CurrentRow?.DataBoundItem is not ResultCarDto car) return;
            _lblCarTitle.Text = string.IsNullOrWhiteSpace(car.CarModel) ? "-" : $"{car.CarModel}\n{car.RentalCompany}\n{car.Price:N0} {car.Currency}";
            _ = LoadCarImageAsync(car.ImageUrl);
        }

        private async Task LoadCarImageAsync(string url)
        {
            _imageCts?.Cancel();
            _imageCts = new CancellationTokenSource();
            var ct = _imageCts.Token;
            if (string.IsNullOrWhiteSpace(url))
            {
                _picCar.Image?.Dispose();
                _picCar.Image = null;
                return;
            }
            try
            {
                var bytes = await _http.GetByteArrayAsync(url, ct);
                using var ms = new MemoryStream(bytes);
                var img = new Bitmap(Image.FromStream(ms));
                if (ct.IsCancellationRequested) { img.Dispose(); return; }
                _picCar.Image?.Dispose();
                _picCar.Image = img;
            }
            catch { }
        }

        private void ClearSearch()
        {
            _txtPickup.Clear();
            _dtPickup.Value = DateTime.Today.AddDays(1);
            _dtReturn.Value = DateTime.Today.AddDays(4);
            _cmbPickupHour.SelectedItem = "10:00";
            _cmbReturnHour.SelectedItem = "10:00";
        }

        private void ClearFilters()
        {
            foreach (var c in new[] { _chkAutomatic, _chkManual, _chkGasoline, _chkDiesel, _chkHybrid, _chkElectric }) c.Checked = false;
            _txtSegment.Clear();
            _txtBrand.Clear();
            _txtCompany.Clear();
            _txtMinPrice.Clear();
            _txtMaxPrice.Clear();
            _displayedCars = _allCars.ToList();
            BindGrid();
        }

        private void SetStatus(string text) => UiHelper.RunOnUi(this, () =>
        {
            _lblStatus.Text = text;
            _lblLastUpdate.Text = "Son guncelleme: " + DateTime.Now.ToString("HH:mm:ss");
            _lblLastOp.Text = "• Son islem: " + (text.Length > 26 ? text[..26] + "..." : text);
        });

        private void SetBusy(bool busy)
        {
            UiHelper.SetControlsEnabled(!busy, _actionButtons.ToArray());
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private void SetBrowserSplit()
        {
            try
            {
                const int browserWidth = 560;
                var target = Math.Max(520, _split.Width - browserWidth - _split.SplitterWidth);
                _split.SplitterDistance = Math.Min(target, _split.Width - _split.SplitterWidth - 1);
            }
            catch { }
        }

        private static Guna2ComboBox HourCombo()
        {
            var cmb = UiStyleHelper.Combo(90);
            for (var h = 0; h < 24; h++) cmb.Items.Add($"{h:00}:00");
            cmb.SelectedItem = "10:00";
            return cmb;
        }

        private static void AddRow(TableLayoutPanel p, int row, string leftLabel, Control left, string rightLabel, Control right)
        {
            p.Controls.Add(UiStyleHelper.FieldLabel(leftLabel), 0, row);
            p.Controls.Add(left, 1, row);
            p.Controls.Add(UiStyleHelper.FieldLabel(rightLabel), 2, row);
            p.Controls.Add(right, 3, row);
        }

        private static void AddField(TableLayoutPanel p, int col, int row, string label, Control control)
        {
            p.Controls.Add(UiStyleHelper.FieldLabel(label), col, row);
            p.Controls.Add(control, col, row + 1);
        }

        private static DataGridViewTextBoxColumn Col(string header, string prop, float weight) => new()
        {
            HeaderText = header,
            DataPropertyName = prop,
            FillWeight = weight
        };
    }
}
