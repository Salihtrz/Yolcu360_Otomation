using Yolcu360.BusinessLayer.Abstract;
using Yolcu360.BusinessLayer.Concrete;
using Yolcu360.DataAccessLayer.Abstract;
using Yolcu360.DataAccessLayer.Context;
using Yolcu360.DataAccessLayer.Repositories;

namespace Yolcu360.BusinessLayer
{
    /// <summary>
    /// Basit composition root. Tüm nesne grafiği (repository -> manager) burada
    /// kurulur; böylece PresentationLayer veritabanı/altyapı detaylarıyla uğraşmaz,
    /// yalnızca servis arayüzlerini kullanır.
    /// </summary>
    public class AppServices
    {
        public IReportService ReportService { get; }
        public ICarResultService CarResultService { get; }
        public IUserService UserService { get; }
        public ICefSharpBrowserService BrowserService { get; }
        /// <summary>Yalnızca GİRİŞ (login) için ayrı WebView2 tarayıcısı (reCAPTCHA gerçek Edge'de geçer).</summary>
        public ICefSharpBrowserService LoginBrowserService { get; }
        public IYolcu360AutomationService AutomationService { get; }
        /// <summary>Seçili aracın firmasının site üzerindeki değerlendirmelerini OKUR (salt-okunur).</summary>
        public ISupplierReviewService ReviewService { get; }
        public IPngReportService PngReportService { get; }
        public ICsvReportService CsvReportService { get; }
        public IExcelReportService ExcelReportService { get; }
        public ISearchProfileService SearchProfileService { get; }
        public IOtpReceiverService OtpReceiverService { get; }
        public ILoginAutomationService LoginAutomationService { get; }
        public ISimulatedRentalService SimulatedRentalService { get; }
        public ISimulationPngService SimulationPngService { get; }
        public DatabaseInitializer DatabaseInitializer { get; }

        public AppServices()
        {
            // DataAccess
            var connectionFactory = new MySqlConnectionFactory();
            DatabaseInitializer = new DatabaseInitializer(connectionFactory);

            IReportRepository reportRepository = new ReportRepository(connectionFactory);
            ICarResultRepository carResultRepository = new CarResultRepository(connectionFactory);
            IUserRepository userRepository = new UserRepository(connectionFactory);
            ISearchProfileRepository searchProfileRepository = new SearchProfileRepository(connectionFactory);
            ISimulatedRentalRepository simulatedRentalRepository = new SimulatedRentalRepository(connectionFactory);

            // Business
            CarResultService = new CarResultManager();
            ReportService = new ReportManager(reportRepository, carResultRepository, CarResultService);
            UserService = new UserManager(userRepository);

            // Aktif tarayıcı motoru: CefSharp (Chromium). Viewport kilidi + CDP gerçek tıklama
            // DevTools (CEF UI thread'ine marshal edilerek) ile karşılanır (bkz. CefSharpBrowserManager).
            BrowserService = new CefSharpBrowserManager();
            AutomationService = new Yolcu360AutomationManager(BrowserService);
            // Firma değerlendirme okuma: otomasyon tarayıcısının (CefSharp) o anki sonuç sayfasını kullanır.
            ReviewService = new SupplierReviewManager(BrowserService);
            PngReportService = new PngReportManager();
            CsvReportService = new CsvReportManager();
            ExcelReportService = new ExcelReportManager();
            SearchProfileService = new SearchProfileManager(searchProfileRepository);

            // Telefon + SMS/OTP giriş akışı (yalnızca kullanıcının kendi hesabı içindir).
            // GİRİŞ AYRI bir WebView2 tarayıcısında yapılır: gerçek Edge runtime reCAPTCHA'yı geçer
            // (CefSharp gömülü olduğu için düşük puan alıp engelleniyordu). Giriş başarılı olunca
            // oturum çerezleri CefSharp otomasyon tarayıcısına köprülenir (OtpLoginForm).
            OtpReceiverService = new OtpReceiverManager();
            LoginBrowserService = new WebView2BrowserManager();
            LoginAutomationService = new LoginAutomationManager(LoginBrowserService);

            // Araç kiralama SİMÜLASYONU (gerçek rezervasyon/ödeme yok).
            SimulatedRentalService = new SimulatedRentalManager(simulatedRentalRepository);
            SimulationPngService = new SimulationPngManager();
        }
    }
}
