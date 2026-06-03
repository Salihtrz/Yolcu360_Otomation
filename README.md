# Yolcu360 Araç Kiralama Otomasyonu, Raporlama ve Simülasyon Paneli

Windows Forms tabanlı, **katmanlı mimariye (N-Tier)** sahip bir masaüstü uygulamasıdır. [Yolcu360](https://www.yolcu360.com/) araç kiralama sitesini gömülü bir **WebView2 (Edge/Chromium)** tarayıcısı üzerinden sürerek; arama yapar, sonuçları kazıyıp `DataGridView`'de listeler, site filtrelerini uygular, sonuçları **MySQL** veritabanına rapor olarak kaydeder, **PNG / CSV / Excel** çıktısı üretir, raporları karşılaştırır ve gerçek rezervasyon yapmadan **araç kiralama simülasyonu** sunar.

> ⚠️ **Etik / Güvenlik**
> Bu proje bir **okul projesi** olup yalnızca normal kullanıcı etkileşimlerini taklit eder.
> - SMS / OTP / captcha **bypass edilmez**, yoğun istek atılmaz.
> - SMS girişi yalnızca kullanıcının **kendi telefonu** ve **kendi OTP kodu** içindir (MacroDroid ile cihazlar arası aktarım).
> - **Araç Kiralama Simülasyonu** tamamen uygulama içidir: gerçek rezervasyon, gerçek ödeme, gerçek kart işlemi **yapılmaz**; sitede ödeme/rezervasyon tamamlayan butonlara basılmaz.

---

## İçindekiler

1. [Özellikler](#özellikler)
2. [Mimari ve Teknoloji](#mimari-ve-teknoloji)
3. [Proje Yapısı](#proje-yapısı)
4. [Gereksinimler](#gereksinimler)
5. [Kurulum ve Çalıştırma](#kurulum-ve-çalıştırma)
6. [Kullanım Akışı](#kullanım-akışı)
7. [Giriş (Telefon + SMS/OTP) ve reCAPTCHA](#giriş-telefon--smsotp-ve-recaptcha)
8. [Araç Kiralama Simülasyonu](#araç-kiralama-simülasyonu)
9. [Veritabanı Şeması](#veritabanı-şeması)
10. [Teknik Notlar](#teknik-notlar)
11. [Loglama ve Sorun Giderme](#loglama-ve-sorun-giderme)

---

## Özellikler

### Arama ve Sonuçlar
- **Araç arama:** Alış yeri (autocomplete), alış/dönüş tarihi ve **saati** site üzerinde gerçek olarak ayarlanır; arama tetiklenir.
- **Sonuç kazıma:** Tüm araç kartları (lazy-load tamamlanana kadar kaydırılarak) kazınır; model, kiralama şirketi, vites, yakıt, segment, fiyat, lokasyon, tarihler `DataGridView`'de listelenir.
- **Grid içi araç adına göre arama** (yalnızca tabloyu filtreler, siteye gitmez).
- **Fiyata göre otomatik sıralama** (küçükten büyüğe) + sütun başlığına tıklayarak sıralama.
- **En ucuz N araç** açık yeşil ile vurgulanır; satır numaraları (1, 2, 3…) solda gösterilir.
- **Özet istatistik paneli** (araç sayısı, en ucuz/ortalama/en pahalı vb.).
- **Araç görsel önizleme** (seçili aracın görseli `HttpClient` ile yüklenir).

### Filtreleme (tamamen siteye dayalı)
- Marka, Model, Şirket, Koltuk Sayısı, KM Sınırı, Teslim Şekli, Depozito **dropdown'ları siteden DİNAMİK doldurulur** — sabit liste yok; o arama için sitede hangi seçenekler varsa (ve kaç adet) onlar gösterilir.
- Vites / Yakıt tipi onay kutuları.
- **Uygulama içi (local) filtreleme yoktur:** seçilen filtreler yalnızca Yolcu360 üzerinde uygulanır, liste sitede güncellenip yeniden kazınır; Grid yalnızca siteden geleni gösterir. Böylece uygulama ile sitedeki araç sayısı birebir eşleşir.

### Raporlama ve Çıktı
- **Rapor kaydetme:** Sonuçlar MySQL'e isimlendirilmiş rapor olarak kaydedilir (aynı ad: üzerine yaz / iptal).
- **Geçmiş raporlar:** Kaydedilmiş raporları listele, yükle, sil.
- **Rapor karşılaştırma** (`ReportCompareForm`).
- **Dışa aktarma:** PNG (başlıklı tablolu görsel), CSV, Excel (ClosedXML).
- **Arama profilleri:** Sık kullanılan arama bilgilerini kaydet → "Yükle ve Ara" ile tek tıkla tekrar ara → sil.

### Giriş / Çıkış
- **Sade giriş:** "Giriş Yap" → numara seçme/yazma dropdown'ı → "Onayla" → tarayıcıda numara yazılır, "Devam Et"e kullanıcı basar, SMS kodu MacroDroid ile otomatik girilir.
- **Çıkış (yumuşak logout):** Yalnızca Yolcu360 çerezleri + localStorage temizlenir; **Google/reCAPTCHA güven çerezleri korunur**.

### Araç Kiralama Simülasyonu
- Seçili araçla 5 adımlı sihirbaz: Araç → Sürücü → Ek Hizmetler → Ödeme (simülasyon) → Özet.
- Simülasyon kaydı + benzersiz kod (`Y360-SIM-YYYYMMDD-####`) + PNG özet çıktısı.
- Simülasyon geçmişi (detay / sil / PNG).

---

## Mimari ve Teknoloji

- **Dil/Platform:** C# / .NET 9 (SDK .NET 10 ile derlenir), Windows Forms.
- **Tarayıcı motoru:** **WebView2 (Microsoft Edge/Chromium)** — aktif motor. CefSharp paketi projede mevcut ama kullanılmıyor (alternatif `CefSharpBrowserManager` ölü kod olarak durur).
- **Veritabanı:** MySQL (MySqlConnector + Dapper, mikro-ORM).
- **UI kütüphanesi:** Guna.UI2.WinForms (modern butonlar, combobox, panel; designer dosyası yok — UI kod ile kurulur).
- **Mimari:** Katmanlı (N-Tier), tek yönlü referans, döngüsel bağımlılık yok.

### NuGet paketleri
| Paket | Sürüm | Kullanım |
|---|---|---|
| Microsoft.Web.WebView2 | — | Gömülü tarayıcı (aktif) |
| CefSharp.WinForms.NETCore | 147.0.100 | (mevcut, kullanılmıyor) |
| MySqlConnector | 2.4.0 | MySQL bağlantısı |
| Dapper | 2.1.66 | ORM |
| Newtonsoft.Json | 13.0.3 | JS sonuçları / JSON |
| ClosedXML | — | Excel çıktısı |
| Guna.UI2.WinForms | — | Modern UI |

---

## Proje Yapısı

```
Yolcu360_Otomation.slnx                         (.NET 10 SDK / slnx formatı)
├── Yolcu360.EntityLayer        (net9.0)          Entity: User, Report, CarResult, SearchProfile,
│                                                  SimulatedRental, SimulatedRentalExtra
├── Yolcu360.Common             (net9.0)          LogHelper, JsHelper, Yolcu360Constants,
│                                                  Yolcu360Selectors, Yolcu360LoginSelectors, Suppliers
├── Yolcu360.DtoLayer           (net9.0)          DTO'lar: Search/Filter/Site, Report, Car, User,
│                                                  Login, Profile, Simulation (+Catalog/Calculator)
├── Yolcu360.DataAccessLayer    (net9.0)          MySqlConnectionFactory, DatabaseInitializer,
│                                                  GenericRepository + Repository'ler (Dapper), Config
├── Yolcu360.BusinessLayer      (net9.0-windows)  Servis/Manager'lar + WebView2/CefSharp tarayıcı +
│                                                  otomasyon + OTP + PNG/CSV/Excel
└── Yolcu360.PresentationLayer  (net9.0-windows)  WinForms UI — STARTUP
    ├── MainForm.cs                                Ana panel (arama/filtre/grid/istatistik/giriş-çıkış)
    ├── OtpLoginForm.cs                            Sade giriş dialog'u
    ├── ReportsForm.cs / ReportCompareForm.cs      Geçmiş raporlar / karşılaştırma
    └── Forms/
        ├── RentalSimulationForm.cs                5 adımlı kiralama simülasyon sihirbazı
        └── RentalSimulationHistoryForm.cs         Simülasyon geçmişi
```

**Referans yönü:** `EntityLayer` en altta, `PresentationLayer` en üstte. `Common` katmanı bağımsızdır (WinForms/tarayıcı bağımlılığı yoktur) ki `DataAccessLayer` onu referans alabilsin. `UiHelper` (WinForms) Presentation'da, `BrowserWaitHelper` Business'tedir.

**Servis kurulumu (composition root):** `Yolcu360.BusinessLayer/AppServices.cs` — tüm repository → manager grafiği burada kurulur; UI yalnızca servis arayüzlerini kullanır.

---

## Gereksinimler

- **.NET 9 SDK** (derleme .NET 10 SDK ile yapılır; runtime net9.0)
- **Windows x64**
- **Microsoft Edge WebView2 Runtime** (Windows 11'de genelde kuruludur)
- **MySQL Server** (rapor/geçmiş/profil/simülasyon kayıtları için — yoksa uygulama yine açılır, sadece DB özellikleri devre dışı kalır)
- (İsteğe bağlı) **MacroDroid** yüklü bir Android telefon — SMS kodunu otomatik aktarmak için

---

## Kurulum ve Çalıştırma

### 1) MySQL ayarları
`Yolcu360.PresentationLayer/appsettings.json`:
```json
{
  "MySql": {
    "Server": "localhost", "Port": 3306,
    "UserId": "root", "Password": "", "Database": "yolcu360_automation"
  },
  "OtpReceiver": {
    "Enabled": true, "Port": 5055,
    "Token": "Yolcu360SecretKey", "TimeoutSeconds": 120
  }
}
```
Veritabanı ve tüm tablolar uygulama açılışında **otomatik oluşturulur** (`DatabaseInitializer`, `CREATE TABLE IF NOT EXISTS` + idempotent göçler). Mevcut tablolar bozulmaz.

### 2) Derleme ve çalıştırma
```powershell
# Derleme
dotnet build Yolcu360_Otomation.slnx

# Çalıştırma
dotnet run --project Yolcu360.PresentationLayer
```
Derlenen exe:
`Yolcu360.PresentationLayer/bin/Debug/net9.0-windows/win-x64/Yolcu360.PresentationLayer.exe`

> **Not:** "Build failed" mesajı çoğu zaman gerçek derleme hatası değil, **uygulama açıkken DLL'lerin kilitli olmasıdır** (MSB3027/MSB3021). Bu durumda çalışan uygulamayı (ve Visual Studio derlemesini) kapatıp tekrar derleyin.

---

## Kullanım Akışı

1. **(Gerekirse) Giriş yapın:** Üst sağdaki **"Giriş Yap"** → numara seçin/yazın → **Onayla** → tarayıcıda "Devam Et"e basın → SMS kodu otomatik girilir.
2. **Arama bilgilerini girin:** Alış yeri, alış/dönüş tarihi ve saati → **Ara**.
3. **Sonuçlar** `DataGridView`'de listelenir (fiyata göre sıralı, en ucuzlar vurgulu, satır numaralı). Sütun başlığına tıklayarak sıralayabilir, arama kutusuyla araç adına göre filtreleyebilirsiniz.
4. **Filtreler:** Dropdown'lar siteden dolar. İstediğiniz filtreleri seçip **Filtreleri Uygula** → site üzerinde uygulanır, liste yenilenir. **Filtreleri Temizle** sitedeki filtreleri de sıfırlar.
5. **Profil:** Mevcut arama bilgilerini **Profili Kaydet** ile saklayın; sonra **Yükle ve Ara** ile tek tıkla tekrar arayın.
6. **Rapor:** **Raporu Kaydet** (MySQL). **Geçmiş Raporlar** (sidebar) → rapora çift tıkla = yükle, ya da sil.
7. **Çıktı:** **PNG / CSV / Excel** butonları ile dışa aktarın.
8. **Simülasyon:** Bir araç seçip **Kiralamayı Simüle Et** → sihirbaz. Geçmiş için sidebar'daki **Simülasyon Geçmişi**.
9. **Çıkış:** **Çıkış Yap** → Yolcu360 oturumu temizlenir (reCAPTCHA güveni korunur).

---

## Giriş (Telefon + SMS/OTP) ve reCAPTCHA

**Akış:** Numara siteye yazılır → kullanıcı **"Devam Et"e elle basar** → site kullanıcının telefonuna SMS gönderir → telefondaki **MacroDroid** kodu bilgisayardaki yerel dinleyiciye (HTTP/TCP, port 5055) gönderir → uygulama kodu OTP alanına otomatik yazıp girişi tamamlar.

**Neden "Devam Et"e kullanıcı basıyor?** Yolcu360 login adımında **görünmez reCAPTCHA v3** vardır. Bu, koda değil; çerez geçmişi, IP itibarı ve **insan davranışına** bakarak bir güven puanı verir. "Devam Et"e gerçek bir kullanıcının basması puanı yükseltir; otomatik tıklama düşük puan (`recaptcha_score_too_low`) ile reddedilebilir. Etik kural gereği captcha **bypass edilmez**.

**Çıkış (yumuşak logout):** "Çıkış Yap" tüm çerezleri silmez — yalnızca **Yolcu360 alan adının** çerezlerini ve localStorage/sessionStorage'ını siler. Google'ın reCAPTCHA güven çerezi (`_GRECAPTCHA`, farklı alan adında) **korunur**. Böylece çıkıştan hemen sonra tekrar girişte reCAPTCHA sizi "şüpheli yeni tarayıcı" sayıp engellemez.

**MacroDroid kurulumu (telefon):**
- Trigger: SMS Received
- Action: HTTP Request (POST) → `http://<bilgisayar-IP>:5055/otp?token=Yolcu360SecretKey`
- Body (JSON): `{ "smsMetni": "{sms_message}" }`
- Telefon ve bilgisayar **aynı Wi-Fi ağında** olmalı. Kod/token loglanmaz, DB'ye yazılmaz; kullanılınca bellekten silinir.

---

## Araç Kiralama Simülasyonu

Seçili bir araçla, gerçek rezervasyon/ödeme **yapmadan** uçtan uca kiralama deneyimi simüle eder.

**Adımlar (sihirbaz):**
1. **Araç Bilgileri** — seçili aracın tüm bilgileri + görseli. Fiyat okunamadıysa manuel fiyat alanı açılır.
2. **Sürücü Bilgileri** — ad/soyad/telefon/e-posta/TC-pasaport/doğum tarihi/ehliyet. Validasyon: boş alan, e-posta formatı, telefon, ehliyet tarihi ≤ bugün, **yaş ≥ 18**.
3. **Ek Hizmetler** — Ek sürücü (500), Bebek koltuğu (300), Navigasyon (250), Kış lastiği (400), Tam sigorta (1000), Yol yardım (350), HGS/OGS (200) TL. Anlık toplam gösterilir.
4. **Ödeme (Simülasyon)** — Ofiste / Kredi kartı / Banka kartı. Kart seçilirse **sahte alanlar** (kart no `**** 0000`, CVV `***`) + açık uyarı. Gerçek kart bilgisi alınmaz, ödeme yapılmaz.
5. **Özet** — tüm bilgiler + fiyat dökümü. **Simülasyonu Tamamla** → DB'ye kayıt + benzersiz kod (`Y360-SIM-YYYYMMDD-####`) + isteğe bağlı **PNG özet** çıktısı.

**Geçmiş:** Sidebar → **Simülasyon Geçmişi** → kayıtları listele, detay gör, sil, PNG indir.

**Hesaplama:** Gün sayısı = ⌈dönüş − alış⌉ (min 1). Genel toplam = araç fiyatı + ek hizmetler. Aynı katalog/hesaplama hem UI'da hem serviste kullanılır (`SimulationCatalog`, `SimulationCalculator`).

**Güvenlik:** Kişisel bilgiler (TC/telefon/e-posta) **loga açık yazılmaz**; log yalnızca kod/araç/toplam/adım olaylarını tutar.

---

## Veritabanı Şeması

Tümü açılışta otomatik oluşturulur (InnoDB, utf8mb4):

| Tablo | Açıklama | İlişki |
|---|---|---|
| `Users` | Yerel kullanıcı + kayıtlı telefon numaraları | — |
| `Reports` | Kaydedilmiş arama raporu başlığı | 1—N `CarResults` |
| `CarResults` | Rapora bağlı araç sonuçları | FK → Reports (CASCADE) |
| `SearchProfiles` | Sık kullanılan arama profilleri | — |
| `SimulatedRentals` | Kiralama simülasyon kayıtları | 1—N `SimulatedRentalExtras` |
| `SimulatedRentalExtras` | Simülasyona eklenen ek hizmetler | FK → SimulatedRentals (CASCADE) |

---

## Teknik Notlar

- **Thread.Sleep kullanılmaz.** Tüm beklemeler `async/await`, `NavigationCompleted`, `TaskCompletionSource`, `Task.Delay` ile yapılır → UI donmaz.
- **WebView2 viewport kilidi:** Yolcu360 responsive bir SPA'dır; DOM (ve selector'lar) pencere genişliğiyle değişir. CDP `Emulation.setDeviceMetricsOverride` ile **sabit bir CSS viewport (1000×980, tablet layout)** zorlanır; böylece pencere boyutu ne olursa olsun site hep aynı, selector'ların çalıştığı layout'u render eder.
- **Gerçek (trusted) fare tıklaması:** Bazı Vue widget'ları JS ile gönderilen (`isTrusted=false`) tıklamalara tepki vermez. CDP `Input.dispatchMouseEvent` ile **gerçek** tıklama gönderilir (ör. saat seçici menüsü yalnızca böyle açılır).
- **Saat seçimi:** Sitenin saat menüsü body'ye taşınan (Teleport) `<li>HH:MM</li>` öğeleridir; menü yalnızca gerçek tıklamayla açılır. Tetikleyiciler **indeksle** (0=Alış, 1=Dönüş) bulunur; hedef saat görünür yapılıp gerçek tıklanır ve gösterilen değer **doğrulanana kadar** denenir.
- **Lazy-load:** Sonuçlar kart sayısı **artmayana kadar** kaydırılarak yüklenir; her kazımadan önce çalışır → uygulama ile sitedeki araç sayısı eşleşir.
- **Selector'lar** tek yerde, alternatif listeler halinde: `Yolcu360.Common/Constants/Yolcu360Selectors.cs` ve `Yolcu360LoginSelectors.cs`. Gerçek 2026 DOM'una göre doğrulanmıştır.
- **Kiralama şirketi:** Kartta firma metni yoktur (yalnızca logo). Logo UUID'si `Yolcu360Suppliers` haritasıyla firma adına çevrilir.
- **JS enjeksiyonu** `JsHelper` ile yönetilir; modern formlar için native value setter + input/change/blur event dispatch kullanılır. Tüm değerler JSON ile güvenli gömülür.

---

## Loglama ve Sorun Giderme

- **Log dosyası:** Uygulamanın çalıştığı klasörde `logs/app-log.txt` (zaman damgalı, seviyeli). Kişisel/gizli bilgi yazılmaz.
- **MySQL bağlantı hatası:** Uygulama yine açılır; rapor/geçmiş/profil/simülasyon devre dışı kalır. `appsettings.json` ve MySQL servisini kontrol edin.
- **"Build failed" (MSB3027/MSB3021):** Çalışan uygulamayı/Visual Studio derlemesini kapatıp tekrar derleyin (dosya kilidi).
- **reCAPTCHA reddi:** Çıkıştan hemen sonra tekrar girişte takılırsa birkaç saniye bekleyin; "Devam Et"e elle basın. Yumuşak logout bu sorunu büyük ölçüde azaltır.

---

> Bu uygulama eğitim/okul projesi amaçlıdır. Yolcu360 üzerinde gerçek rezervasyon veya ödeme işlemi yapmaz.
