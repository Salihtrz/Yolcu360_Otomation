namespace Yolcu360.DtoLayer.BrowserDto
{
    /// <summary>
    /// Tarayıcı motorları arası oturum çerezi taşıma nesnesi. WebView2'de (gerçek Edge ile,
    /// reCAPTCHA geçilerek) yapılan girişin oturum çerezleri buraya alınıp CefSharp'a aktarılır.
    /// Böylece otomasyon tarayıcısı (CefSharp) da girişli olur; YENİDEN giriş/captcha YAPILMAZ.
    /// </summary>
    public class BrowserCookieDto
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Domain { get; set; }
        public string Path { get; set; }
        public bool Secure { get; set; }
        public bool HttpOnly { get; set; }

        /// <summary>Bitiş zamanı (UTC). null = oturum (session) çerezi.</summary>
        public DateTime? Expires { get; set; }
    }
}
