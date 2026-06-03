# Yolcu360 Araç Kiralama Otomasyonu ve Raporlama Sistemi

Windows Forms tabanlı, **katmanlı mimariye** sahip bir masaüstü otomasyon/raporlama uygulaması.
Yolcu360 sitesi arka planda **CefSharp (Chromium)** ile açılır; kullanıcı masaüstü uygulamasından
arama yapar, sonuçlar kazınıp `DataGridView`'de listelenir, **MySQL**'e rapor olarak kaydedilir ve
**PNG** olarak dışa aktarılabilir.

> ⚠️ **Etik/Güvenlik:** Bu proje okul ödevi/simülasyon amaçlıdır. Uygulama yalnızca normal kullanıcı
> etkilesimlerini taklit eder. **SMS/OTP/captcha bypass edilmez, yogun istek atilmaz.** SMS girisi yalnizca kullanicinin kendi telefonu ve MacroDroid ile kendi kodunu masaustu uygulamasina aktarmasi icindir.

---

## Gereksinimler

- **.NET 9 SDK** (kurulu SDK'lar: 6/9/10)
- **Windows x64** (CefSharp x64 zorunlu)
- **MySQL Server** (rapor kaydetme/geçmiş için — yoksa uygulama yine açılır, sadece DB özellikleri devre dışı kalır)
- **Visual C++ 2019+ Redistributable** (CefSharp için)

## Katman Yapısı

```
Yolcu360_Otomation.slnx
├── Yolcu360.EntityLayer        (net9.0)          Entity sınıfları (User, Report, CarResult)
├── Yolcu360.Common             (net9.0)          LogHelper, JsHelper, Yolcu360Selectors, sabitler
├── Yolcu360.DtoLayer           (net9.0)          DTO'lar (Search/Filter/Report/Car/User)
├── Yolcu360.DataAccessLayer    (net9.0)          MySQL bağlantı, DatabaseInitializer, Repository'ler (Dapper)
├── Yolcu360.BusinessLayer      (net9.0-windows)  Servis/Manager'lar + CefSharp + otomasyon + PNG
└── Yolcu360.PresentationLayer  (net9.0-windows)  WinForms UI (MainForm, ReportsForm) — STARTUP
```

Referans yönü tek yönlüdür (döngüsel referans yok). `EntityLayer` en altta, `PresentationLayer` en üstte.

> **Mimari not:** `UiHelper` (WinForms) PresentationLayer'da, `BrowserWaitHelper` (CefSharp) BusinessLayer'da
> tutulur. Nedeni: `Common` katmanının bağımsız (net9.0, WinForms/CefSharp bağımlılığı olmayan) kalması ve
> `DataAccessLayer` tarafından referans alınabilmesi gerektiğidir.

## Kurulum & Çalıştırma

1. **MySQL ayarları** (opsiyonel): `Yolcu360.PresentationLayer/appsettings.json` dosyasını düzenleyin:
   ```json
   {
     "MySql": { "Server": "localhost", "Port": 3306, "UserId": "root", "Password": "", "Database": "yolcu360_automation" }
   }
   ```
   Veritabanı ve tablolar uygulama açılışında **otomatik oluşturulur** (`DatabaseInitializer`).

2. **Çalıştırma:**
   ```powershell
   dotnet run --project Yolcu360.PresentationLayer
   ```
   veya derlenen exe:
   `Yolcu360.PresentationLayer/bin/Debug/net9.0-windows/win-x64/Yolcu360.PresentationLayer.exe`

## Kullanım Akışı

1. (Gerekirse) **Giris Yap (SMS)** ile telefon + MacroDroid OTP penceresini acin; kod telefonunuzdan uygulamaya aktarilir.
2. Alış yeri, tarih/saat, dönüş tarih/saat girin → **Ara**.
3. Sonuçlar `DataGridView`'de listelenir (sütun başlığına tıklayıp sıralayabilirsiniz).
4. **Filtreleri Uygula**: önce site üzerinde, bulunamazsa yerel (C#) filtreleme yapılır.
5. **Sonuçları Kaydet**: rapor adı sorulur (aynı ad varsa: üzerine yaz / yeni isim / iptal).
6. **Geçmiş Raporları Getir**: rapora çift tıkla → araçlar yüklenir; oradan silebilirsiniz.
7. **PNG Olarak İndir**: başlıklı, tablolu PNG üretir.

## Teknik Notlar

- **Thread.Sleep kullanılmaz.** Tüm beklemeler `async/await`, `LoadingStateChanged`,
  `TaskCompletionSource` ve `Task.Delay` ile yapılır → UI donmaz.
- **Selector'lar** `Yolcu360.Common/Constants/Yolcu360Selectors.cs` içinde tek yerde, alternatif
  listeler halinde tutulur. Site değişirse sadece bu dosya güncellenir. Selector'lar **gerçek
  Yolcu360 DOM'una (2026) göre doğrulanmıştır** ve uçtan uca test edilmiştir (20 araç çekildi):
  - Alış yeri: `#inputPickUpLocation`, öneri: `.location-item`
  - Takvim: `.month-header` (ör. "Haziran 2026") + `.day` gün hücreleri (aralık seçici)
  - Ara butonu: `#search`
  - Araç kartı: `.car-card`; model `.text-lg.font-bold`; vites/yakıt/segment
    `[data-cms-key^='filter_transmission'/'filter_fuel'/'filter_class_type']`; fiyat `#car_total_price`
  - Firma site'de logo görseli olarak gösterilir (metin ad yok), bu yüzden "Kiralama Şirketi"
    sütununda tedarikçi logo URL'i tutulur.
- **Otomasyon akışı:** lokasyonu gerçek klavye olaylarıyla (keydown/input/keyup) yazar (React
  autocomplete'i tetiklemek için), açılan öneriye tıklar, takvimden alış/dönüş günlerini tıklar
  saat seçicisinden (li.hour-li) saati yarım saate yuvarlayarak best-effort seçer (tutmazsa
  varsayılan 10:00), `#search`'e basar, `.car-card`'ların XHR ile yüklenmesini bekler ve kazır.
  SPA hidrasyonu için yazmadan önce kısa bir bekleme (Task.Delay) vardır.
- **JS enjeksiyonu** `JsHelper` ile yönetilir; modern formlar için `setNativeValue` + input/change/blur
  event dispatch kullanılır.
- **Loglar:** `logs/app-log.txt` (exe dizininde).

## CefSharp Native Dosya Notu (önemli)

CefSharp.WinForms.NETCore native ikilileri varsayılan olarak `runtimes/win-x64` altında bırakır; bu
durumda mixed-mode `CefSharp.Core.Runtime.dll` native bağımlılığını (libcef.dll) bulamaz. Çözüm
`PresentationLayer.csproj` içinde uygulanmıştır:
`<RuntimeIdentifier>win-x64</RuntimeIdentifier>` + `chromiumembeddedframework.runtime.win-x64` paketi +
`CefRedist64CopyResources` hedefi. Bu sayede tüm CEF dosyaları exe dizinine düzleşir.
