using Guna.UI2.WinForms;
using Yolcu360.BusinessLayer;
using Yolcu360.Common.Constants;
using Yolcu360.Common.Logging;
using Yolcu360.DtoLayer.CarResultDto;
using Yolcu360.DtoLayer.ReportDto;
using Yolcu360.DtoLayer.ReviewDto;
using Yolcu360.DtoLayer.SearchDto;
using Yolcu360.PresentationLayer.Forms;
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
        private List<ResultCarDto> _gridView = new();   // grid'de o an bağlı (ada göre filtrelenmiş) liste
        private CancellationTokenSource _searchCts;
        private CancellationTokenSource _imageCts;
        private OtpLoginForm _otpLoginForm;
        private bool _browserVisible = false;
        private bool _appClosing;
        private string _sortProperty;
        private bool _sortAsc = true;

        private Form _browserWindow;
        private Guna2TextBox _txtPickup;
        private Guna2DateTimePicker _dtPickup;
        private Guna2DateTimePicker _dtReturn;
        private Guna2ComboBox _cmbPickupHour;
        private Guna2ComboBox _cmbReturnHour;
        private Guna2ComboBox _cmbProfiles;
        private Guna2ComboBox _cmbBrand;
        private Guna2ComboBox _cmbModel;
        private Guna2ComboBox _cmbCompany;
        // Son aramadan siteden okunan filtre bölümleri (marka/şirket/model dropdown'larını doldurur).
        private List<SiteFilterSectionDto> _siteFilters = new();
        private Guna2CheckBox _chkAutomatic;
        private Guna2CheckBox _chkManual;
        private Guna2CheckBox _chkGasoline;
        private Guna2CheckBox _chkDiesel;
        private Guna2CheckBox _chkHybrid;
        private Guna2CheckBox _chkElectric;
        private Guna2CheckBox _chkLpg;
        private Guna2ComboBox _cmbSeat;
        private Guna2ComboBox _cmbKm;
        private Guna2ComboBox _cmbDelivery;
        private Guna2ComboBox _cmbDeposit;
        private NumericUpDown _numHighlight;
        private Guna2TextBox _txtGridSearch;
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
        private Label _statMax;
        private Label _statCheapest;
        private Label _lblCarTitle;
        private PictureBox _picCar;
        private Panel _browserHost;

        // ---- Firma değerlendirme / misafir yorumu paneli (Seçili Araç kartı) ----
        private Label _lblRvOverall;     // büyük genel puan (ör. 4.6)
        private Label _lblRvStars;       // ★★★★☆
        private Label _lblRvCount;       // "4.6 / 5 · 400 yorum"
        private Label _lblRvSupplier;    // firma adı
        private Label _lblRvClean, _lblRvDelivery, _lblRvStaff;     // alt puan değerleri
        private Panel _barRvClean, _barRvDelivery, _barRvStaff;     // bar dolgu panelleri
        private Panel _trackRvClean, _trackRvDelivery, _trackRvStaff; // bar arka planı
        private Guna2Panel _rvCardBox;   // yorum kartı kutusu
        private Control _rvNav;          // ◀ x/y ▶ navigasyon çubuğu
        private Label _lblRvState;       // boş/yükleniyor durum metni (kartı kaplar)
        private Label _lblRvAuthor, _lblRvDate, _lblRvRating, _lblRvIndex;
        private TextBox _txtRvComment;
        private Guna2Button _btnRvPrev, _btnRvNext;
        private List<SupplierReviewDto> _currentReviews = new();
        private int _rvIndex;
        private CancellationTokenSource _reviewCts;
        private readonly Dictionary<string, SupplierReviewSummaryDto> _supplierReviewCache = new(StringComparer.OrdinalIgnoreCase);

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

            // Tarayıcı artık MainForm içinde değil, ayrı bir pencerede gösterilir (içerik
            // alanı daralıp kartlar sıkışmasın diye). Pencere baştan gizli kurulur.
            BuildBrowserWindow();
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
            menu.Controls.Add(SideButton("🔍   Arac Ara", (s, e) => SetStatus("Arama paneli hazir."), true));
            menu.Controls.Add(SideButton("🕘   Gecmis Raporlar", async (s, e) => await OpenReportsAsync()));
            menu.Controls.Add(SideButton("🚗   Simulasyon Gecmisi", (s, e) => new RentalSimulationHistoryForm(_services.SimulatedRentalService, _services.SimulationPngService).ShowDialog(this)));
            menu.Controls.Add(SideButton("📊   Rapor Karsilastir", (s, e) => new ReportCompareForm(_services.ReportService).Show(this)));
            menu.Controls.Add(SideButton("🌐   Tarayici", (s, e) => RevealBrowser(!_browserVisible)));
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

            // İçerik tüm genişliği kullanır; tarayıcı ayrı pencerede.
            main.Controls.Add(BuildContent(), 0, 1);

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
            var btnLogin = UiStyleHelper.Button("Giris Yap", ButtonVariant.Accent, 120);
            btnLogin.Click += OnAutoLoginClick;
            var btnLogout = UiStyleHelper.Button("Cikis Yap", ButtonVariant.Danger, 120);
            btnLogout.Click += OnLogoutClick;
            _actionButtons.Add(btnLogin);
            // RightToLeft akış: önce eklenen en sağda. Sıra (soldan sağa): Çıkış | Giriş | Göster/Gizle
            actions.Controls.Add(btnToggle);
            actions.Controls.Add(btnLogin);
            actions.Controls.Add(btnLogout);
            header.Controls.Add(actions);
            return header;
        }

        private Control BuildContent()
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ThemeColors.Background, Padding = new Padding(0, 0, 10, 0) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 4, Padding = new Padding(4) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 410));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 430));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
            layout.Controls.Add(UiStyleHelper.Card("Arac Arama Bilgileri", BuildSearchPanel()), 0, 0);
            layout.Controls.Add(UiStyleHelper.Card("Filtreler", BuildFilterPanel()), 1, 0);
            layout.Controls.Add(UiStyleHelper.Card("Arac Sonuclari", BuildGridPanel()), 0, 1);
            layout.SetColumnSpan(layout.GetControlFromPosition(0, 1), 2);
            layout.Controls.Add(UiStyleHelper.Card("Ozet Istatistikler", BuildStatsPanel()), 0, 2);
            var selCard = UiStyleHelper.Card("Secili Arac & Firma Degerlendirmesi", BuildSelectedCarPanel());
            layout.Controls.Add(selCard, 1, 2);
            layout.SetRowSpan(selCard, 2); // değerlendirme paneli için 2 satır yüksekliği (250+220)
            scroll.Controls.Add(layout);
            return scroll;
        }

        private Control BuildSearchPanel()
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, RowCount = 6, AutoSize = true, BackColor = Color.Transparent };
            for (var i = 0; i < 4; i++) p.ColumnStyles.Add(new ColumnStyle(i % 2 == 0 ? SizeType.Absolute : SizeType.Percent, i % 2 == 0 ? 90 : 50));

            _txtPickup = UiStyleHelper.TextBox(260);
            _txtPickup.PlaceholderText = "Orn: Istanbul Havalimani";
            _dtPickup = UiStyleHelper.DatePicker(160);
            _dtPickup.Value = DateTime.Today.AddDays(1);
            _dtReturn = UiStyleHelper.DatePicker(160);
            _dtReturn.Value = DateTime.Today.AddDays(4);
            _cmbPickupHour = HourCombo();
            _cmbReturnHour = HourCombo();

            AddRow(p, 0, "Alis Yeri", _txtPickup, "Alis Tarihi", _dtPickup);
            AddRow(p, 1, "Alis Saati", _cmbPickupHour, "Donus Tarihi", _dtReturn);
            // Dönüş Saati solda; sağ taraf boş.
            p.Controls.Add(UiStyleHelper.FieldLabel("Donus Saati"), 0, 2);
            p.Controls.Add(_cmbReturnHour, 1, 2);

            // Profil: kart altında kendi geniş satırında (ad kesilmesin diye geniş combo + dropdown).
            _cmbProfiles = UiStyleHelper.Combo(320);
            _cmbProfiles.DropDownWidth = 320;
            p.Controls.Add(UiStyleHelper.FieldLabel("Profil"), 0, 3);
            p.Controls.Add(_cmbProfiles, 1, 3);
            p.SetColumnSpan(_cmbProfiles, 3);

            var btnSearch = UiStyleHelper.Button("Ara", ButtonVariant.Primary, 110);
            btnSearch.Click += async (s, e) => await SearchAsync();
            var btnClear = UiStyleHelper.Button("Temizle", ButtonVariant.Secondary, 100);
            btnClear.Click += (s, e) => ClearSearch();
            var btnProfile = UiStyleHelper.Button("Yukle ve Ara", ButtonVariant.Accent, 140);
            btnProfile.Click += async (s, e) => await LoadSelectedProfileAndSearchAsync();
            var btnSaveProfile = UiStyleHelper.Button("Profili Kaydet", ButtonVariant.Success, 140);
            btnSaveProfile.Click += async (s, e) => await SaveCurrentProfileAsync();
            var btnDeleteProfile = UiStyleHelper.Button("Profili Sil", ButtonVariant.Danger, 100);
            btnDeleteProfile.Click += async (s, e) => await DeleteSelectedProfileAsync();
            _actionButtons.AddRange(new Control[] { btnSearch, btnProfile, btnSaveProfile });

            // Arama aksiyonları (row 4) ve profil aksiyonları (row 5) ayrı satırlarda → sığar, kesilmez.
            var searchBtns = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, Margin = new Padding(0, 6, 0, 0) };
            searchBtns.Controls.AddRange(new Control[] { btnSearch, btnClear });
            p.Controls.Add(searchBtns, 1, 4);
            p.SetColumnSpan(searchBtns, 3);

            var profileBtns = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, Margin = new Padding(0, 2, 0, 0) };
            profileBtns.Controls.AddRange(new Control[] { btnProfile, btnSaveProfile, btnDeleteProfile });
            p.Controls.Add(profileBtns, 1, 5);
            p.SetColumnSpan(profileBtns, 3);
            return p;
        }

        private Control BuildFilterPanel()
        {
            // Kaydırılabilir: içerik karttan uzun olsa bile hiçbir alan (ör. Depozito) kesilmez.
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };

            var checks = new FlowLayoutPanel { Width = 450, Height = 92, BackColor = Color.Transparent };
            _chkAutomatic = UiStyleHelper.Check("Otomatik");
            _chkManual = UiStyleHelper.Check("Manuel");
            _chkGasoline = UiStyleHelper.Check("Benzin");
            _chkDiesel = UiStyleHelper.Check("Dizel");
            _chkHybrid = UiStyleHelper.Check("Hibrit");
            _chkElectric = UiStyleHelper.Check("Elektrik");
            _chkLpg = UiStyleHelper.Check("LPG");
            checks.Controls.AddRange(new Control[] { _chkAutomatic, _chkManual, _chkGasoline, _chkDiesel, _chkHybrid, _chkElectric, _chkLpg });
            p.Controls.Add(checks);

            var grid = new TableLayoutPanel { Width = 450, AutoSize = true, ColumnCount = 3, RowCount = 8, BackColor = Color.Transparent };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));

            // TÜM filtre dropdown'ları SİTEDEN dinamik dolar (sabit liste YOK). Başlangıçta yalnızca
            // "Hepsi"; arama/filtre sonrası site filtre panelindeki gerçek seçeneklerle (ve adetlerle)
            // güncellenir. Segment YOK: sitenin filtre panelinde sınıf/segment filtresi yok ve uygulama
            // içi (local) filtreleme yapmıyoruz — yalnızca sitedeki filtrelere göre liste gösterilir.
            _cmbBrand = OptionCombo(140, ("Hepsi", ""));
            _cmbModel = OptionCombo(140, ("Hepsi", ""));
            _cmbCompany = OptionCombo(140, ("Hepsi", ""));
            _cmbSeat = OptionCombo(140, ("Hepsi", ""));
            _cmbKm = OptionCombo(140, ("Hepsi", ""));
            _cmbDelivery = OptionCombo(140, ("Hepsi", ""));
            _cmbDeposit = OptionCombo(140, ("Hepsi", ""));

            AddField(grid, 0, 0, "Marka", _cmbBrand);
            AddField(grid, 1, 0, "Model", _cmbModel);
            AddField(grid, 2, 0, "Sirket", _cmbCompany);
            AddField(grid, 0, 2, "Koltuk Sayisi", _cmbSeat);
            AddField(grid, 1, 2, "KM Siniri", _cmbKm);
            AddField(grid, 2, 2, "Teslim Sekli", _cmbDelivery);
            AddField(grid, 0, 4, "Depozito", _cmbDeposit);
            p.Controls.Add(grid);

            var hint = new Label { Text = "Marka / Model / Sirket secenekleri arama sonrasi siteden yuklenir.", AutoSize = true, ForeColor = ThemeColors.TextMuted, Font = new Font("Segoe UI", 8.25f), Margin = new Padding(2, 2, 0, 4) };
            p.Controls.Add(hint);

            var btnApply = UiStyleHelper.Button("Filtreleri Uygula", ButtonVariant.Accent, 170);
            btnApply.Click += async (s, e) => await ApplyFiltersAsync();
            var btnClear = UiStyleHelper.Button("Filtreleri Temizle", ButtonVariant.Secondary, 180);
            btnClear.Click += async (s, e) => await ClearFiltersAsync();
            var actions = new FlowLayoutPanel { Width = 450, Height = 42, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent };
            actions.Controls.Add(btnApply);
            actions.Controls.Add(btnClear);
            p.Controls.Add(actions);
            return p;
        }

        private Control BuildGridPanel()
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent };
            p.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 0) };
            var btnSave = UiStyleHelper.Button("Raporu Kaydet", ButtonVariant.Primary, 140);
            btnSave.Margin = new Padding(0, 0, 6, 0);
            btnSave.Click += async (s, e) => await SaveReportAsync();
            var btnDelete = UiStyleHelper.Button("Listeyi Temizle", ButtonVariant.Danger, 140);
            btnDelete.Margin = new Padding(0, 0, 6, 0);
            btnDelete.Click += (s, e) => { _displayedCars.Clear(); _allCars.Clear(); BindGrid(); };
            var btnPng = UiStyleHelper.Button("PNG", ButtonVariant.Secondary, 70);
            btnPng.Margin = new Padding(0, 0, 6, 0);
            btnPng.Click += async (s, e) => await ExportPngAsync();
            var btnCsv = UiStyleHelper.Button("CSV", ButtonVariant.Secondary, 70);
            btnCsv.Margin = new Padding(0, 0, 6, 0);
            btnCsv.Click += async (s, e) => await ExportCsvAsync();
            var btnExcel = UiStyleHelper.Button("Excel", ButtonVariant.Secondary, 80);
            btnExcel.Margin = new Padding(0, 0, 14, 0);
            btnExcel.Click += async (s, e) => await ExportExcelAsync();

            // Seçili araçla kiralama SİMÜLASYONU (gerçek rezervasyon/ödeme yok).
            var btnSimulate = UiStyleHelper.Button("Kiralamayı Simüle Et", ButtonVariant.Accent, 190);
            btnSimulate.Margin = new Padding(0, 0, 14, 0);
            btnSimulate.Click += (s, e) => OpenRentalSimulation();

            // Araç adına göre grid'de arama (yalnızca tabloyu filtreler; siteye gitmez).
            _txtGridSearch = UiStyleHelper.TextBox(200);
            _txtGridSearch.PlaceholderText = "Arac adina gore ara...";
            _txtGridSearch.Margin = new Padding(0, 0, 10, 0);
            _txtGridSearch.TextChanged += (s, e) => BindGrid();

            _lblCarCount = new Label
            {
                Text = "0 arac",
                AutoSize = false,
                Width = 90,
                Height = 36,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = ThemeColors.TextMuted
            };
            actions.Controls.AddRange(new Control[] { btnSave, btnDelete, btnPng, btnCsv, btnExcel, btnSimulate, _txtGridSearch, _lblCarCount });
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
                RowHeadersVisible = true,        // sol başlık: sıra numarası (1,2,3...)
                RowHeadersWidth = 52,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            // Sıra numarasını satır başlığına çiz (DataSource değişse de doğru kalır).
            _dgv.RowPostPaint += (s, e) =>
            {
                var num = (e.RowIndex + 1).ToString();
                var rect = new Rectangle(e.RowBounds.Location.X, e.RowBounds.Location.Y, _dgv.RowHeadersWidth - 6, e.RowBounds.Height);
                TextRenderer.DrawText(e.Graphics, num, _dgv.Font, rect, _dgv.RowHeadersDefaultCellStyle.ForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
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
            // Apply, satır başlığını gizler; sıra numarası için tekrar aç ve stillendir.
            _dgv.RowHeadersVisible = true;
            _dgv.RowHeadersWidth = 52;
            _dgv.RowHeadersDefaultCellStyle.BackColor = ThemeColors.GridAltRow;
            _dgv.RowHeadersDefaultCellStyle.ForeColor = ThemeColors.TextMuted;
            _dgv.RowHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _dgv.RowHeadersDefaultCellStyle.SelectionBackColor = ThemeColors.GridAltRow;
            _dgv.AllowUserToResizeColumns = true;
            p.Controls.Add(_dgv, 0, 1);
            return p;
        }

        private Control BuildStatsPanel()
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Color.Transparent };
            p.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            p.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // İstatistik kutuları: 5 eşit sütun; her biri yuvarlak köşeli kart (başlık + vurgulu değer).
            var boxes = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 5, BackColor = Color.Transparent };
            for (var i = 0; i < 5; i++) boxes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 5));
            _statTotal = StatTile(boxes, 0, "Toplam Arac", ThemeColors.Primary);
            _statMin = StatTile(boxes, 1, "En Dusuk", ThemeColors.Success);
            _statAvg = StatTile(boxes, 2, "Ortalama", ThemeColors.Primary);
            _statMax = StatTile(boxes, 3, "En Yuksek", ThemeColors.Danger);
            _statCheapest = StatTile(boxes, 4, "En Ucuz Arac", ThemeColors.Success);
            p.Controls.Add(boxes, 0, 0);

            // Vurgulama kontrolü ayrı, kısa bir satırda.
            var highlight = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent };
            highlight.Controls.Add(new Label { Text = "En ucuz N vurgula:", AutoSize = true, ForeColor = ThemeColors.TextMuted, Margin = new Padding(0, 6, 6, 0) });
            _numHighlight = new NumericUpDown { Width = 54, Minimum = 0, Maximum = 20, Value = 3, Margin = new Padding(0, 3, 0, 0), BorderStyle = BorderStyle.FixedSingle };
            _numHighlight.ValueChanged += (s, e) => ApplyRowHighlighting();
            highlight.Controls.Add(_numHighlight);
            p.Controls.Add(highlight, 0, 1);

            _lblStats = new Label { Dock = DockStyle.Fill, ForeColor = ThemeColors.TextMuted, Font = new Font("Segoe UI", 9f), Text = "Henuz sonuc yok." };
            p.Controls.Add(_lblStats, 0, 2);
            return p;
        }

        /// <summary>Başlık (üstte küçük/gri) + değer (altta kalın/renkli) içeren yuvarlak köşeli istatistik kartı.</summary>
        private Label StatTile(TableLayoutPanel parent, int col, string title, Color valueColor)
        {
            var tile = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 0),
                FillColor = Color.White,
                BorderColor = ThemeColors.Border,
                BorderThickness = 1,
                BorderRadius = 10,
                Padding = new Padding(6, 8, 6, 8)
            };
            var value = new Label
            {
                Text = "-",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = valueColor,
                AutoEllipsis = true
            };
            var header = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 18,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8f),
                ForeColor = ThemeColors.TextMuted
            };
            tile.Controls.Add(value);
            tile.Controls.Add(header);
            parent.Controls.Add(tile, col, 0);
            return value;
        }

        /// <summary>İstatistik kartının değerini günceller (başlık ayrı etikettedir).</summary>
        private static void SetStatValue(Label valueLabel, string value) => valueLabel.Text = value;

        private Control BuildSelectedCarPanel()
        {
            // Dikey yerleşim: [araç görseli+başlık] / [genel puan] / [alt puanlar] / [yorum carousel]
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));   // araç görseli + başlık
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));   // genel puan başlığı
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));   // alt puanlar
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // yorum carousel

            // --- Üst: araç görseli + başlık ---
            var top = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _picCar = new PictureBox { Dock = DockStyle.Left, Width = 110, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
            _lblCarTitle = new Label { Dock = DockStyle.Fill, Text = "-", Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = ThemeColors.TextDark, Padding = new Padding(12, 4, 4, 4), TextAlign = ContentAlignment.MiddleLeft };
            top.Controls.Add(_lblCarTitle); // Fill önce
            top.Controls.Add(_picCar);

            // --- Genel puan başlığı ---
            var head = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _lblRvOverall = new Label { Dock = DockStyle.Left, Width = 88, Text = "-", Font = new Font("Segoe UI", 19f, FontStyle.Bold), ForeColor = ThemeColors.Primary, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = false };
            var headRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _lblRvCount = new Label { Dock = DockStyle.Top, Height = 16, Text = "", Font = new Font("Segoe UI", 8.5f), ForeColor = ThemeColors.TextMuted };
            _lblRvStars = new Label { Dock = DockStyle.Top, Height = 20, Text = "", Font = new Font("Segoe UI", 13f), ForeColor = ThemeColors.Warning };
            _lblRvSupplier = new Label { Dock = DockStyle.Top, Height = 18, Text = "-", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = ThemeColors.TextDark };
            headRight.Controls.Add(_lblRvCount);   // Top dock: son eklenen en üstte → ters sırada ekle
            headRight.Controls.Add(_lblRvStars);
            headRight.Controls.Add(_lblRvSupplier);
            head.Controls.Add(headRight);          // Fill önce
            head.Controls.Add(_lblRvOverall);

            // --- Alt puanlar (temizlik / teslimat / personel) ---
            var subs = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 4) };
            var rStaff = BuildRatingRow("Personel", out _lblRvStaff, out _barRvStaff, out _trackRvStaff);
            var rDelivery = BuildRatingRow("Teslimat Hizi", out _lblRvDelivery, out _barRvDelivery, out _trackRvDelivery);
            var rClean = BuildRatingRow("Temizlik", out _lblRvClean, out _barRvClean, out _trackRvClean);
            subs.Controls.Add(rStaff);     // Top dock → ters sırada ekle (Temizlik en üstte)
            subs.Controls.Add(rDelivery);
            subs.Controls.Add(rClean);

            // --- Yorum carousel ---
            var carousel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 6, 0, 0) };

            _rvNav = BuildReviewNav();

            _rvCardBox = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = ThemeColors.Background,
                BorderRadius = 10,
                BorderColor = ThemeColors.Border,
                BorderThickness = 1,
                Padding = new Padding(12, 8, 12, 8)
            };
            _txtRvComment = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = ThemeColors.Background,
                ForeColor = ThemeColors.TextDark,
                Font = new Font("Segoe UI", 9.75f),
                ScrollBars = ScrollBars.Vertical,
                Cursor = Cursors.Default,
                TabStop = false
            };
            var rvHdr = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = Color.Transparent };
            _lblRvRating = new Label { Dock = DockStyle.Right, Width = 70, Text = "", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = ThemeColors.Warning, TextAlign = ContentAlignment.MiddleRight };
            _lblRvDate = new Label { Dock = DockStyle.Right, Width = 120, Text = "", Font = new Font("Segoe UI", 8.5f), ForeColor = ThemeColors.TextMuted, TextAlign = ContentAlignment.MiddleRight };
            _lblRvAuthor = new Label { Dock = DockStyle.Fill, Text = "", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = ThemeColors.TextDark, TextAlign = ContentAlignment.MiddleLeft };
            rvHdr.Controls.Add(_lblRvRating); // Right: ilk eklenen en sağda
            rvHdr.Controls.Add(_lblRvDate);
            rvHdr.Controls.Add(_lblRvAuthor); // Fill
            _rvCardBox.Controls.Add(_txtRvComment); // Fill önce
            _rvCardBox.Controls.Add(rvHdr);

            _lblRvState = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Bir arac secince firma degerlendirmeleri burada gorunur.",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ThemeColors.TextMuted,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = ThemeColors.Surface
            };

            carousel.Controls.Add(_rvCardBox); // Fill (kalan)
            carousel.Controls.Add(_rvNav);     // Bottom
            carousel.Controls.Add(_lblRvState);// Fill (üstte; görünürken kaplar)

            root.Controls.Add(top, 0, 0);
            root.Controls.Add(head, 0, 1);
            root.Controls.Add(subs, 0, 2);
            root.Controls.Add(carousel, 0, 3);

            ShowReviewState("Bir arac secince firma degerlendirmeleri burada gorunur.");
            return root;
        }

        /// <summary>Alt puan satırı: etiket + mini ilerleme çubuğu + sayısal değer.</summary>
        private Panel BuildRatingRow(string caption, out Label valueLabel, out Panel fill, out Panel track)
        {
            var row = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.Transparent };
            var cap = new Label { Text = caption, AutoSize = false, Width = 96, Dock = DockStyle.Left, ForeColor = ThemeColors.TextMuted, Font = UiStyleHelper.LabelFont, TextAlign = ContentAlignment.MiddleLeft };
            valueLabel = new Label { Text = "-", AutoSize = false, Width = 42, Dock = DockStyle.Right, ForeColor = ThemeColors.TextDark, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight };
            var holder = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(4, 10, 8, 10) };
            track = new Panel { Dock = DockStyle.Fill, BackColor = ThemeColors.Background };
            fill = new Panel { Dock = DockStyle.Left, Width = 0, BackColor = ThemeColors.Success, Tag = 0.0 };
            var fillRef = fill; var trackRef = track;
            track.Controls.Add(fill);
            track.Resize += (s, e) => ApplyBar(fillRef, trackRef);
            holder.Controls.Add(track);
            row.Controls.Add(holder);      // Fill (kalan)
            row.Controls.Add(valueLabel);  // Right
            row.Controls.Add(cap);         // Left
            return row;
        }

        private static void ApplyBar(Panel fill, Panel track)
        {
            var v = fill.Tag is double d ? d : 0.0;
            fill.Width = (int)(track.ClientSize.Width * Math.Clamp(v, 0, 5) / 5.0);
        }

        private void SetBar(Panel fill, Panel track, Label valueLabel, double value)
        {
            fill.Tag = value;
            valueLabel.Text = value > 0 ? value.ToString("0.0") : "-";
            ApplyBar(fill, track);
        }

        /// <summary>◀ "x / y" ▶ navigasyon çubuğu.</summary>
        private Control BuildReviewNav()
        {
            var nav = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 36, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent };
            nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            nav.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            _btnRvPrev = UiStyleHelper.Button("◀", ButtonVariant.Secondary, 40, 28);
            _btnRvPrev.Click += (s, e) => ShowReviewAt(_rvIndex - 1);
            _btnRvNext = UiStyleHelper.Button("▶", ButtonVariant.Secondary, 40, 28);
            _btnRvNext.Click += (s, e) => ShowReviewAt(_rvIndex + 1);
            _lblRvIndex = new Label { Dock = DockStyle.Fill, Text = "0 / 0", TextAlign = ContentAlignment.MiddleCenter, ForeColor = ThemeColors.TextMuted, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            nav.Controls.Add(_btnRvPrev, 0, 0);
            nav.Controls.Add(_lblRvIndex, 1, 0);
            nav.Controls.Add(_btnRvNext, 2, 0);
            return nav;
        }

        private static string StarString(double rating)
        {
            var r = (int)Math.Round(Math.Clamp(rating, 0, 5), MidpointRounding.AwayFromZero);
            return new string('★', r) + new string('☆', 5 - r);
        }

        /// <summary>Carousel'i gizleyip ortada durum metni gösterir (boş / yükleniyor / hata).</summary>
        private void ShowReviewState(string message)
        {
            if (_lblRvState == null) return;
            _lblRvState.Text = message;
            _lblRvState.Visible = true;
            _lblRvState.BringToFront();
        }

        private void ShowReviewLoading() => ShowReviewState("Degerlendirmeler yukleniyor...");

        /// <summary>Firma değerlendirme özetini panele basar (genel puan, alt puanlar, yorumlar).</summary>
        private void RenderReviewSummary(SupplierReviewSummaryDto s)
        {
            if (s == null) { ShowReviewState("Degerlendirme bilgisi bulunamadi."); return; }

            _lblRvOverall.Text = s.OverallRating > 0 ? s.OverallRating.ToString("0.0") : "-";
            _lblRvStars.Text = StarString(s.OverallRating);
            var supplier = string.IsNullOrWhiteSpace(s.SupplierName) ? "Firma" : s.SupplierName;
            _lblRvSupplier.Text = s.IsSampleData ? supplier + "  ·  ornek veri" : supplier;
            _lblRvCount.Text = s.OverallRating > 0
                ? $"{s.OverallRating:0.0} / 5" + (s.ReviewCount > 0 ? $"  ·  {s.ReviewCount} yorum" : "")
                : (s.ReviewCount > 0 ? $"{s.ReviewCount} yorum" : "");

            SetBar(_barRvClean, _trackRvClean, _lblRvClean, s.CleanlinessRating);
            SetBar(_barRvDelivery, _trackRvDelivery, _lblRvDelivery, s.DeliverySpeedRating);
            SetBar(_barRvStaff, _trackRvStaff, _lblRvStaff, s.StaffRating);

            _currentReviews = s.Reviews ?? new List<SupplierReviewDto>();
            _rvIndex = 0;
            if (_currentReviews.Count == 0)
            {
                ShowReviewState("Bu firma icin yorum bulunamadi.");
            }
            else
            {
                _lblRvState.Visible = false;
                ShowReviewAt(0);
            }
        }

        /// <summary>Carousel'de belirtilen indeksli yorumu gösterir.</summary>
        private void ShowReviewAt(int index)
        {
            if (_currentReviews.Count == 0) { ShowReviewState("Bu firma icin yorum bulunamadi."); return; }
            _rvIndex = Math.Clamp(index, 0, _currentReviews.Count - 1);
            var r = _currentReviews[_rvIndex];
            _lblRvAuthor.Text = string.IsNullOrWhiteSpace(r.AuthorName) ? "Misafir" : r.AuthorName;
            _lblRvDate.Text = r.ReviewDate ?? "";
            _lblRvRating.Text = r.Rating > 0 ? $"{r.Rating:0.0} ★" : "";
            _txtRvComment.Text = string.IsNullOrWhiteSpace(r.Comment) ? "(Yorum metni yok)" : r.Comment.Trim();
            _lblRvIndex.Text = $"{_rvIndex + 1} / {_currentReviews.Count}";
            _btnRvPrev.Enabled = _rvIndex > 0;
            _btnRvNext.Enabled = _rvIndex < _currentReviews.Count - 1;
            _lblRvState.Visible = false;
        }

        /// <summary>
        /// Tarayıcıyı barındıran ayrı (top-level) pencereyi kurar. Aynı CefSharp tarayıcı kontrolü
        /// buraya yerleştirilir; otomasyon yine aynı tarayıcıyı sürdürür. Pencere kapatılırsa
        /// (uygulama kapanmıyorsa) yok edilmez, yalnızca gizlenir.
        /// </summary>
        private void BuildBrowserWindow()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = ThemeColors.SidebarDark };
            top.Controls.Add(new Label { Text = "Yolcu360 Tarayici", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(14, 10) });
            var hide = UiStyleHelper.Button("Gizle", ButtonVariant.Danger, 90, 28);
            hide.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            hide.Top = 6;
            hide.Click += (s, e) => RevealBrowser(false);
            top.Controls.Add(hide);
            void placeHide() => hide.Left = Math.Max(0, top.Width - hide.Width - 10);
            top.Resize += (s, e) => placeHide();

            _browserHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _browserHost.Controls.Add(new Label
            {
                Text = "Tarayici baslatiliyor...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ThemeColors.TextMuted
            });

            _browserWindow = new Form
            {
                Text = "Yolcu360 Tarayici",
                StartPosition = FormStartPosition.CenterScreen,
                Size = new Size(1100, 820),
                MinimumSize = new Size(420, 520),
                BackColor = ThemeColors.SidebarDark,
                ShowInTaskbar = true,
                // Tam ekran açılır; viewport CDP ile sabit (1000px tablet) olduğundan pencere boyutu
                // ne olursa olsun site DOM'u değişmez, selector'lar bozulmaz.
                WindowState = FormWindowState.Maximized,
                Icon = Icon
            };
            _browserWindow.Controls.Add(_browserHost);
            _browserWindow.Controls.Add(top);
            placeHide();

            // Kullanıcı X'e basarsa: uygulama kapanmıyorsa pencereyi kapatma, gizle.
            _browserWindow.FormClosing += (s, e) =>
            {
                if (_appClosing) return;
                e.Cancel = true;
                RevealBrowser(false);
            };
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
            _appClosing = true;
            try { _searchCts?.Cancel(); } catch { }
            try { _imageCts?.Cancel(); } catch { }
            try { _http.Dispose(); } catch { }
            try { _browserWindow?.Close(); } catch { }
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
            var request = BuildSearchRequest();
            // Geçmiş alış zamanı: site "bu saat diliminde arama yapılamaz" uyarısı verir ve sonuç
            // gelmez. Kullanıcıyı baştan uyar.
            if (request.PickupDateTime <= DateTime.Now.AddMinutes(30))
            {
                UiHelper.Warn("Alis tarihi/saati gecmiste veya cok yakin. Lutfen ileri bir tarih/saat secin.");
                return;
            }
            if (request.ReturnDateTime <= request.PickupDateTime)
            {
                UiHelper.Warn("Donus zamani alis zamanindan sonra olmali.");
                return;
            }

            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            SetBusy(true);
            try
            {
                var progress = new Progress<string>(SetStatus);
                _allCars = await _services.AutomationService.SearchAsync(request, progress, _searchCts.Token);
                _displayedCars = _allCars.ToList();
                SortDisplayedByPriceAscending();
                BindGrid();
                await PopulateFiltersFromSiteAsync(_searchCts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogHelper.Error("Arama basarisiz.", ex);
                UiHelper.Error("Arama basarisiz:\n" + ex.Message);
            }
            finally { SetBusy(false); }
        }

        /// <summary>
        /// Arama sonrası filtre dropdown'larını doldurur: Marka/Şirket/Model siteden dinamik okunur;
        /// Segment, kazınan araçların farklı segmentlerinden üretilir. Önceki seçim korunmaya çalışılır.
        /// </summary>
        private async Task PopulateFiltersFromSiteAsync(CancellationToken ct)
        {
            try
            {
                _siteFilters = await _services.AutomationService.ScrapeSiteFiltersAsync(ct);
            }
            catch (Exception ex)
            {
                LogHelper.Warning("Site filtreleri okunamadi: " + ex.Message);
                _siteFilters = new List<SiteFilterSectionDto>();
            }

            // Teşhis: sitedeki gerçek filtre bölüm anahtarlarını logla (eşleme doğru mu görelim).
            LogHelper.Info("Site filtre bölümleri: " + string.Join(" | ",
                (_siteFilters ?? new()).Select(s => $"{s.Key}({s.Options?.Count ?? 0})")));

            // TÜM dropdown'lar site filtre panelinden DİNAMİK dolar. Site hangi seçenekleri sunuyorsa
            // (ve kaç adet) onları gösterir; örn. KM Sınırı yalnızca o arama için mevcut aralıkları.
            FillComboFromSection(_cmbBrand, "filter_brand");
            FillComboFromSection(_cmbCompany, "filter_vendor");
            FillComboFromSection(_cmbModel, "filter_model");
            FillComboFromSection(_cmbSeat, "filter_seat");
            FillComboFromSection(_cmbKm, "filter_distance_limit");
            FillComboFromSection(_cmbDelivery, "filter_delivery_type");
            FillComboFromSection(_cmbDeposit, "filter_provision");
        }

        /// <summary>Bir comboyu site filtre bölümünün seçenekleriyle doldurur (id son ekini değer yapar).</summary>
        private void FillComboFromSection(Guna2ComboBox cmb, string sectionKey)
        {
            var keep = ComboValue(cmb);
            var section = _siteFilters?.FirstOrDefault(s => string.Equals(s.Key, sectionKey, StringComparison.OrdinalIgnoreCase));
            cmb.Items.Clear();
            cmb.Items.Add(new ComboOption("Hepsi", ""));
            if (section != null)
            {
                foreach (var opt in section.Options.OrderBy(o => o.Label, StringComparer.CurrentCulture))
                {
                    // opt.Id = "filter-brand.63" → değer olarak yalnızca "63" (id son eki) tutulur.
                    var dot = opt.Id?.IndexOf('.') ?? -1;
                    var value = dot >= 0 ? opt.Id.Substring(dot + 1) : opt.Id;
                    cmb.Items.Add(new ComboOption(opt.Label, value));
                }
            }
            SelectComboValue(cmb, keep);
        }

        /// <summary>Combo'da verilen değere sahip öğeyi seçer; yoksa ilk öğeye (Hepsi) döner.</summary>
        private static void SelectComboValue(Guna2ComboBox cmb, string value)
        {
            for (var i = 0; i < cmb.Items.Count; i++)
                if (cmb.Items[i] is ComboOption o && o.Value == (value ?? ""))
                { cmb.SelectedIndex = i; return; }
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
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
                // Marka/Şirket/Model: dropdown'ların değeri = sitenin filter-* id son eki (dinamik).
                BrandId = ComboValue(_cmbBrand),
                VendorId = ComboValue(_cmbCompany),
                ModelId = ComboValue(_cmbModel),
                SeatCount = ComboValue(_cmbSeat),
                KmLimit = ComboValue(_cmbKm),
                DeliveryType = ComboValue(_cmbDelivery),
                Deposit = ComboValue(_cmbDeposit)
            };
            // Vites/yakıt: site filter-* id son ekleri (sabit, doğrulanmış).
            if (_chkAutomatic.Checked) filter.TransmissionIds.Add("2");
            if (_chkManual.Checked) filter.TransmissionIds.Add("1");
            if (_chkGasoline.Checked) filter.FuelIds.Add("1");
            if (_chkDiesel.Checked) filter.FuelIds.Add("2");
            if (_chkHybrid.Checked) filter.FuelIds.Add("7");
            if (_chkElectric.Checked) filter.FuelIds.Add("11");
            if (_chkLpg.Checked) filter.FuelIds.Add("5");
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
                // Filtreler YALNIZCA site üzerinde uygulanır (uygulama içi/local filtre YOK). Seçili
                // filtreler siteye yansıtılır, liste sitede güncellenir ve yeniden kazınır; Grid sadece
                // siteden geleni gösterir. Boş filtre = site filtrelerini temizler.
                await _services.AutomationService.ApplyFiltersOnWebsiteAsync(filter);
                _allCars = await _services.AutomationService.ScrapeCurrentResultsAsync(BuildSearchRequest());
                _displayedCars = _allCars.ToList();
                SortDisplayedByPriceAscending();
                BindGrid();
                await PopulateFiltersFromSiteAsync(CancellationToken.None);
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

        /// <summary>
        /// Grid'de seçili araçla Araç Kiralama Simülasyonu sihirbazını açar. Araç seçili değilse uyarır.
        /// Gerçek rezervasyon/ödeme YAPILMAZ; yalnızca uygulama içi simülasyon kaydı oluşur.
        /// </summary>
        private void OpenRentalSimulation()
        {
            if (_dgv.CurrentRow?.DataBoundItem is not ResultCarDto car)
            {
                UiHelper.Warn("Lütfen simülasyon için bir araç seçin.");
                return;
            }

            using var form = new RentalSimulationForm(_services.SimulatedRentalService, _services.SimulationPngService, car);
            if (form.ShowDialog(this) == DialogResult.OK && form.CompletedSummary != null)
            {
                SetStatus($"Simulasyon tamamlandi. Kod: {form.CompletedSummary.SimulationCode}");
                UiHelper.Info($"Kiralama simülasyonu tamamlandı.\nSimülasyon kodu: {form.CompletedSummary.SimulationCode}");
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
                SortDisplayedByPriceAscending();
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

        /// <summary>Mevcut arama bilgilerini (lokasyon + tarih/saat) yeni bir profil olarak kaydeder.</summary>
        private async Task SaveCurrentProfileAsync()
        {
            var name = PromptDialog.Show("Profil adi", "Profil adini girin:");
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            try
            {
                var req = BuildSearchRequest();
                if (await _services.SearchProfileService.ExistsAsync(name))
                {
                    if (!UiHelper.Confirm("Bu isimde profil var. Uzerine yazilsin mi?")) return;
                    await _services.SearchProfileService.OverwriteAsync(name, req);
                }
                else
                {
                    await _services.SearchProfileService.SaveAsync(name, req);
                }
                await LoadProfilesAsync();
                SetStatus("Profil kaydedildi.");
            }
            catch (Exception ex)
            {
                LogHelper.Error("Profil kaydedilemedi.", ex);
                UiHelper.Error("Profil kaydedilemedi:\n" + ex.Message);
            }
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

        /// <summary>
        /// Site oturumunu kapatır (YUMUŞAK ÇIKIŞ): yalnızca Yolcu360 çerezleri + localStorage/
        /// sessionStorage silinir, Google/reCAPTCHA güven çerezleri KORUNUR. Böylece tekrar girişte
        /// reCAPTCHA "şüpheli yeni tarayıcı" deyip engellemez.
        /// </summary>
        private async void OnLogoutClick(object sender, EventArgs e)
        {
            if (!UiHelper.Confirm("Site oturumu kapatılsın mı? (logout)\nYolcu360 çerezleri silinir; reCAPTCHA güveni korunur.", "Çıkış Yap"))
                return;
            try
            {
                Cursor = Cursors.WaitCursor;
                // HER İKİ tarayıcının da Yolcu360 oturumunu temizle: CefSharp (otomasyon) + WebView2
                // (login). Aksi halde WebView2 girişli kalıp "Giriş Yap"ta otomatik köprülüyor.
                await _services.BrowserService.ClearSiteSessionAsync();
                try
                {
                    if (!_services.LoginBrowserService.IsInitialized)
                        await _services.LoginBrowserService.InitializeAsync(Yolcu360Constants.LoginUrl);
                    await _services.LoginBrowserService.ClearSiteSessionAsync();
                }
                catch (Exception lex) { LogHelper.Warning("WebView2 login oturumu temizlenemedi: " + lex.Message); }

                await _services.BrowserService.LoadUrlAsync(Yolcu360Constants.LoginUrl);
                _lblLoginStatus.Text = "• Giris: yapilmadi";
                _lblLoginStatus.ForeColor = ThemeColors.SidebarText;
                SetStatus("Cikis yapildi (her iki tarayici oturumu temizlendi).");
            }
            catch (Exception ex)
            {
                LogHelper.Error("Cikis (logout) hatasi.", ex);
                UiHelper.Error("Çıkış yapılırken hata oluştu.\n\n" + ex.Message);
            }
            finally { Cursor = Cursors.Default; }
        }

        private void RevealBrowser(bool show) => UiHelper.RunOnUi(this, () =>
        {
            _browserVisible = show;
            if (_browserWindow == null || _browserWindow.IsDisposed) BuildBrowserWindow();
            if (show)
            {
                _browserWindow.Show();
                if (_browserWindow.WindowState == FormWindowState.Minimized)
                    _browserWindow.WindowState = FormWindowState.Maximized;
                _browserWindow.BringToFront();
                _browserWindow.Activate();
            }
            else
            {
                _browserWindow.Hide();
            }
            _services.BrowserService.SetBrowserVisible(show);
            SetStatus(show ? "Tarayici ayri pencerede gosteriliyor." : "Tarayici gizlendi.");
        });

        private void MarkLoggedIn(bool ok) => UiHelper.RunOnUi(this, () =>
        {
            _lblLoginStatus.Text = ok ? "• Giris: yapildi" : "• Giris: dogrulanamadi";
            _lblLoginStatus.ForeColor = ok ? ThemeColors.Success : ThemeColors.Warning;
        });

        private void BindGrid()
        {
            // Grid'de gösterilecek görünüm = sonuç listesi, araç adı arama kutusuyla filtrelenmiş.
            var term = _txtGridSearch?.Text?.Trim();
            _gridView = string.IsNullOrEmpty(term)
                ? (_displayedCars ?? new List<ResultCarDto>()).ToList()
                : (_displayedCars ?? new List<ResultCarDto>())
                    .Where(c => !string.IsNullOrEmpty(c.CarModel) &&
                                c.CarModel.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            _dgv.DataSource = null;
            _dgv.DataSource = _gridView;
            UpdateStatistics();
            ApplyRowHighlighting();
            SetStatus(_gridView.Count == 0 ? "Liste bos." : $"{_gridView.Count} arac listeleniyor.");
        }

        private void UpdateStatistics()
        {
            var cars = _gridView ?? new List<ResultCarDto>();
            if (cars.Count == 0)
            {
                _lblStats.Text = "Henuz sonuc yok.";
                foreach (var l in new[] { _statTotal, _statMin, _statAvg, _statMax, _statCheapest })
                    SetStatValue(l, "-");
                _lblCarCount.Text = "0 arac";
                return;
            }
            var priced = cars.Where(c => c.Price > 0).ToList();
            var cur = priced.FirstOrDefault()?.Currency ?? "TL";
            var min = priced.Count > 0 ? priced.Min(c => c.Price) : 0;
            var max = priced.Count > 0 ? priced.Max(c => c.Price) : 0;
            var avg = priced.Count > 0 ? priced.Average(c => c.Price) : 0;
            var cheapest = priced.OrderBy(c => c.Price).FirstOrDefault();
            SetStatValue(_statTotal, cars.Count.ToString());
            SetStatValue(_statMin, $"{min:N0} {cur}");
            SetStatValue(_statAvg, $"{avg:N0} {cur}");
            SetStatValue(_statMax, $"{max:N0} {cur}");
            SetStatValue(_statCheapest, cheapest != null ? $"{cheapest.CarModel} {cheapest.Price:N0} {cur}" : "-");
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
            BindGrid();
        }

        /// <summary>Sonuç listesini fiyata göre küçükten büyüğe sıralar (fiyatsızlar en sona).</summary>
        private void SortDisplayedByPriceAscending()
        {
            _displayedCars = _displayedCars
                .OrderBy(c => c.Price <= 0 ? decimal.MaxValue : c.Price)
                .ToList();
            _sortProperty = "Price";
            _sortAsc = true;
        }

        private void ApplyRowHighlighting()
        {
            if (_dgv == null || _dgv.Rows.Count == 0) return;
            foreach (DataGridViewRow row in _dgv.Rows) row.DefaultCellStyle.BackColor = Color.White;
            var n = (int)(_numHighlight?.Value ?? 0);
            if (n <= 0) return;
            var cheapest = (_gridView ?? new List<ResultCarDto>()).Where(c => c.Price > 0).OrderBy(c => c.Price).Take(n).ToHashSet();
            foreach (DataGridViewRow row in _dgv.Rows)
                if (row.DataBoundItem is ResultCarDto car && cheapest.Contains(car))
                    row.DefaultCellStyle.BackColor = ThemeColors.RowHighlight;
        }

        private void ShowSelectedRowDetail()
        {
            if (_dgv.CurrentRow?.DataBoundItem is not ResultCarDto car) return;
            _lblCarTitle.Text = string.IsNullOrWhiteSpace(car.CarModel) ? "-" : $"{car.CarModel}\n{car.RentalCompany}\n{car.Price:N0} {car.Currency}";
            _ = LoadCarImageAsync(car.ImageUrl);
            _ = LoadReviewsForSelectedAsync(car);
        }

        /// <summary>
        /// Seçili aracın firmasına ait değerlendirmeleri yükler. Aynı firma daha önce çekildiyse
        /// runtime cache'ten gösterilir (siteye tekrar gidilmez). Kullanıcı hızlı satır değiştirirse
        /// önceki yükleme iptal edilir (CancellationToken). UI donmaz: async/await, Thread.Sleep yok.
        ///
        /// PHASE 1 (şu an): UI'ı doğrulamak için örnek/dummy veri üretilir.
        /// PHASE 2: bu metodun gövdesi CefSharp ile gerçek site DOM'undan okuyacak şekilde değişecek;
        /// panel/cache/iptal akışı aynı kalır.
        /// </summary>
        private async Task LoadReviewsForSelectedAsync(ResultCarDto car)
        {
            _reviewCts?.Cancel();
            _reviewCts = new CancellationTokenSource();
            var ct = _reviewCts.Token;

            if (car == null || string.IsNullOrWhiteSpace(car.RentalCompany))
            {
                ShowReviewState("Bu arac icin firma bilgisi yok.");
                return;
            }

            var key = car.RentalCompany.Trim();
            if (_supplierReviewCache.TryGetValue(key, out var cached))
            {
                LogHelper.Info($"Degerlendirme cache'ten gosterildi: {key}");
                RenderReviewSummary(cached);
                return;
            }

            ShowReviewLoading();
            try
            {
                // Gerçek site DOM'undan oku (CefSharp): kartı bul → modal aç → puan+yorum oku → kapat.
                var summary = await _services.ReviewService.GetSupplierReviewsAsync(car, 30, ct);
                if (ct.IsCancellationRequested) return;

                if (summary == null)
                {
                    ShowReviewState("Degerlendirme bilgisi bulunamadi.");
                    return;
                }

                // Çekilemeyen (boş) sonuçları cache'leme; firma tekrar seçilince yeniden denensin.
                if (summary.Reviews.Count > 0 || summary.OverallRating > 0)
                    _supplierReviewCache[key] = summary;

                RenderReviewSummary(summary);
            }
            catch (OperationCanceledException) { /* kullanıcı hızlı geçti: sessizce iptal */ }
            catch (Exception ex)
            {
                LogHelper.Error("Firma degerlendirmesi yuklenemedi.", ex);
                ShowReviewState("Degerlendirme bilgisi bulunamadi.");
            }
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

        private async Task ClearFiltersAsync()
        {
            // UI reset
            foreach (var c in new[] { _chkAutomatic, _chkManual, _chkGasoline, _chkDiesel, _chkHybrid, _chkElectric, _chkLpg }) c.Checked = false;
            foreach (var cmb in new[] { _cmbBrand, _cmbModel, _cmbCompany, _cmbSeat, _cmbKm, _cmbDelivery, _cmbDeposit })
                if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;

            if (_allCars.Count == 0)
            {
                _displayedCars = new List<ResultCarDto>();
                BindGrid();
                return;
            }

            // SİTEYİ de temizle: boş filtre uygulamak tüm site kutularını/radio'larını sıfırlar;
            // ardından liste yeniden kazınır ve grid güncellenir.
            SetBusy(true);
            try
            {
                await _services.AutomationService.ApplyFiltersOnWebsiteAsync(new FilterRequestDto());
                _allCars = await _services.AutomationService.ScrapeCurrentResultsAsync(BuildSearchRequest());
                _displayedCars = _allCars.ToList();
                SortDisplayedByPriceAscending();
                BindGrid();
                await PopulateFiltersFromSiteAsync(CancellationToken.None);
                SetStatus("Filtreler temizlendi.");
            }
            catch (Exception ex)
            {
                LogHelper.Error("Filtreler temizlenemedi.", ex);
                UiHelper.Error("Filtreler temizlenemedi:\n" + ex.Message);
            }
            finally { SetBusy(false); }
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

        private static Guna2ComboBox HourCombo()
        {
            var cmb = UiStyleHelper.Combo(90);
            for (var h = 0; h < 24; h++) cmb.Items.Add($"{h:00}:00");
            cmb.SelectedItem = "10:00";
            return cmb;
        }

        /// <summary>Görünen etiket + (site id son eki) değeri taşıyan seçenek combobox'ı üretir.</summary>
        private static Guna2ComboBox OptionCombo(int width, params (string label, string value)[] items)
        {
            var cmb = UiStyleHelper.Combo(width);
            foreach (var it in items) cmb.Items.Add(new ComboOption(it.label, it.value));
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
            return cmb;
        }

        /// <summary>Combobox seçili değerini (site id son eki) döner; boşsa null (filtre uygulanmaz).</summary>
        private static string ComboValue(Guna2ComboBox cmb)
            => (cmb?.SelectedItem as ComboOption)?.Value is { Length: > 0 } v ? v : null;

        /// <summary>Combobox öğesi: kullanıcıya gösterilen etiket + siteye gönderilen değer.</summary>
        private sealed class ComboOption
        {
            public string Label { get; }
            public string Value { get; }
            public ComboOption(string label, string value) { Label = label; Value = value; }
            public override string ToString() => Label;
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
