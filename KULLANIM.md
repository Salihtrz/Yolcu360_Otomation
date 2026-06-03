# Yolcu360 Otomasyon — Kullanım Kılavuzu

Bu belge uygulamanın kurulumu, çalıştırılması ve adım adım kullanımını anlatır.
(Teknik mimari için `README.md` dosyasına bakınız.)

---

## 1. Gereksinimler

| Bileşen | Not |
|---------|-----|
| .NET 9 SDK | Kurulu (`dotnet --version` ile kontrol) |
| Windows x64 | CefSharp x64 zorunlu |
| MySQL Server | Rapor kaydetme/geçmiş için (yoksa uygulama yine açılır) |
| Visual C++ 2019+ Redistributable | CefSharp için gerekli |

MySQL ayarları `Yolcu360.PresentationLayer/appsettings.json` içindedir
(varsayılan: `localhost / root / boş şifre / yolcu360_automation`).
Veritabanı ve tablolar **ilk açılışta otomatik oluşturulur**. Mevcut bir kurulumda `Users`
tablosuna **`PhoneNumber`** kolonu (SMS girişi için) açılışta güvenli şekilde eklenir.
Aynı dosyadaki **`OtpReceiver`** bölümü MacroDroid OTP aktarımını ayarlar (bkz. §4.1.b).

---

## 2. Çalıştırma

### Visual Studio ile
1. `Yolcu360_Otomation.slnx` çözümünü aç.
2. Solution Explorer → **`Yolcu360.PresentationLayer`** üzerine sağ tık →
   **"Set as Startup Project"** (kalın yazı bu projede olmalı).
3. **F5** (veya yeşil ▶ Start).

> Çözümü ilk açışta "unknown configuration mappings" uyarısı çıkarsa: çözümü kapatıp
> tekrar açın (File → Close Solution → yeniden aç). Uyarı kaybolur.

### Terminal ile
```powershell
dotnet run --project Yolcu360.PresentationLayer
```

Çalışan exe: `Yolcu360.PresentationLayer\bin\Debug\net9.0-windows\win-x64\Yolcu360.PresentationLayer.exe`

---

## 3. Ekran bölümleri

- **Arama Bilgileri:** Alış yeri, alış/dönüş tarih ve saati, **Ara** / **Temizle**. Ayrıca
  **arama profilleri**: sık kullanılan lokasyon+tarih kombinasyonlarını kaydedip (**Profili Kaydet**)
  açılır listeden seçip **Profili Yükle & Ara** ile tek tıkla tekrar arayabilirsiniz (**Profili Sil**).
- **Filtreler:** Vites (Otomatik/Manuel), Yakıt (Benzin/Dizel/Hibrit/Elektrik),
  Segment, **Şirket** (gerçek firma listesi), Min/Max fiyat, **Filtreleri Uygula**.
- **Sonuçlar:** Araç listesi (DataGridView). Arama sonrası **fiyata göre otomatik artan sıralanır**;
  **en ucuz N araç yeşil vurgulanır** (N değerini özet panelindeki kutudan ayarlayabilirsiniz).
  Sütun başlığına tıklayarak yeniden sıralayabilirsiniz. Listenin altında **özet istatistik paneli**
  (toplam araç, min/ortalama/medyan/max fiyat, en uygun araç, vites/yakıt/segment/firma dağılımı) ve
  sağda **seçili aracın görsel önizlemesi** vardır.
- **Alt butonlar:** Sonuclari Kaydet, Gecmis Raporlar, Secili Raporu Sil, **Rapor Karsilastir**,
  PNG / **CSV** / **Excel** Olarak Indir, Tarayiciyi Goster/Gizle.
- **Header (sag ust) login butonlari:** **Giris Yap (SMS)** (telefon + MacroDroid OTP ile giris penceresini acar),
  **Tarayiciyi Goster/Gizle**.
- **Durum çubuğu:** İşlem sürerken adımları ve **ilerleme çubuğunu** gösterir.
- **Sağ panel:** CefSharp tarayıcısı (varsayılan gizli; manuel login ve hata ayıklama için).

---

## 4. Adım adım kullanım

### 4.1. Telefon + SMS (MacroDroid OTP) ile yarı otomatik giriş

> **Etik not:** Bu özellik **yalnızca kendi hesabınız ve kendi telefon numaranız** içindir.
> SMS/OTP veya captcha **bypass edilmez**. Yapılan tek şey, **size ait** OTP kodunun **kendi
> cihazlarınız arasında** (telefon → bilgisayar) aktarılmasıdır. Kod log'a/veritabanına yazılmaz.

Akış: numara siteye yazılır → site **kendi telefonunuza** SMS kod gönderir → telefondaki
**MacroDroid** kodu masaüstü uygulamasına iletir → uygulama kodu siteye yazıp girişi tamamlar.
MacroDroid çalışmazsa kodu **elle** de girebilirsiniz.

**Adımlar:**
1. Header'da **Giriş Yap (SMS)** → giriş penceresi açılır.
2. **Telefon Numarası** alanına numaranızı yazın (örn. `+905xxxxxxxxx`) → **Numarayı Kaydet**
   (veritabanına kaydedilir; sonraki açılışta otomatik dolar).
3. **Dinleyici Başlat** → uygulama, telefondan kod almak için yerel bir HTTP dinleyici açar.
   Durum: `● Dinleyici: açık (port 5055, TCP/HTTP)`.
4. **Giriş Yap (SMS bekle)** → numara siteye yazılır, "Kod Gönder" tetiklenir ve uygulama
   `SMS kodu bekleniyor...` durumuna geçer (varsayılan **2 dakika**).
5. Telefonunuza gelen SMS'i **MacroDroid** otomatik okur ve koddan `\b\d{6}\b` ile 6 haneyi
   alıp bilgisayara gönderir. Kod gelince uygulama otomatik siteye yazar ve **Giriş Yap**'a basar.
6. Başarılıysa: `● Giriş: yapıldı`, tarayıcı gizlenir. Başarısızsa tarayıcı görünür kalır ve
   manuel müdahale edebilirsiniz.

**MacroDroid çalışmazsa (manuel kod):** Telefonunuza gelen 6 haneli kodu giriş penceresindeki
**Kodu Manuel Gir** kutusuna yazıp **Kodu Gönder**'e basın.

#### MacroDroid kurulumu (telefon tarafı)

Giriş penceresindeki **MacroDroid Ayarları** kutusunda, bilgisayarınızın **IP'si**, **port**'u ve
**token**'ı hazır gösterilir. Telefonda:

1. **Trigger:** SMS Received (tercihen gönderen Yolcu360 olacak şekilde sınırlandırın).
2. SMS metninden 6 haneli kodu **regex** ile alın: `\b\d{6}\b`
3. **Action:** HTTP Request
   - **Method:** POST
   - **URL:** `http://BILGISAYAR_IP:5055/otp`
   - **Header:** `Content-Type: application/json`
   - **Body:**
     ```json
     {
       "code": "{sms_içinden_regex_ile_alınan_6_haneli_kod}",
       "source": "MacroDroid",
       "token": "appsettings.json içindeki token"
     }
     ```
   - (Opsiyonel GET: `http://BILGISAYAR_IP:5055/otp?code=123456&token=...`)

> **Not:** Telefon ve bilgisayar **aynı Wi-Fi ağında** olmalıdır. İlk istekte Windows Güvenlik
> Duvarı bu porta erişim için izin isteyebilir — **izin verin**.

#### Güvenlik token'ı

`Yolcu360.PresentationLayer/appsettings.json` içindeki `OtpReceiver` bölümünden ayarlanır:

```json
"OtpReceiver": {
  "Enabled": true,
  "Port": 5055,
  "Token": "CHANGE_ME_SECRET_TOKEN",
  "TimeoutSeconds": 120
}
```

- **Token yanlışsa** istek reddedilir (401). Kod **6 haneli sayısal değilse** reddedilir (400).
- Token hâlâ `CHANGE_ME_SECRET_TOKEN` ise uygulama **uyarı** gösterir — lütfen kendi gizli
  token'ınızı belirleyin. Token ve kod **log'a yazılmaz**.

#### Hızlı test (telefon olmadan)

PowerShell ile dinleyiciye sahte bir istek atarak akışı test edebilirsiniz (önce **Dinleyici Başlat**):

```powershell
Invoke-RestMethod -Uri "http://localhost:5055/otp" -Method Post `
  -ContentType "application/json" `
  -Body '{"code":"123456","source":"Test","token":"CHANGE_ME_SECRET_TOKEN"}'
```

Uygulama, kod sanki MacroDroid'den gelmiş gibi davranır (kodu **Kodu Manuel Gir** kutusuna yansıtır).

### 4.2. Arama
1. **Alış Yeri:** açık yazın, örn. `İstanbul Havalimanı` (otomatik tamamlama için).
2. Alış/Dönüş tarih ve saatlerini seçin.
3. **Ara**. Durum çubuğunda sırasıyla:
   `Alış yeri giriliyor → Lokasyon önerisi seçiliyor → Tarihler seçiliyor →
   Arama yapılıyor → Sonuçlar bekleniyor → Araç bilgileri çekiliyor → İşlem tamamlandı`.
4. Araçlar tabloda listelenir.

> Uygulama gerçek site davranışını taklit eder: lokasyonu **gerçek klavye olaylarıyla** yazar,
> açılan öneriye tıklar, **takvimden** alış/dönüş günlerini seçer, **Ara**'ya basar, sonuçların
> AJAX ile yüklenmesini bekler ve sayfayı aşağı kaydırarak daha fazla aracı yükleyip kazır.

### 4.3. Filtreleme
- Filtre alanları: **Vites** (Otomatik/Manuel), **Yakıt** (Benzin/Dizel/Hibrit/Elektrik),
  **Segment**, **Marka** (Fiat, Renault, Opel...), **Şirket** (gerçek firma listesi),
  **Min/Max fiyat**.
- **Filtreleri Uygula**: Vites, yakıt, **marka** ve **şirket** site üzerindeki gerçek filtre
  kutularına (idempotent) uygulanır; segment ve fiyat aralığı yerel (C#) filtrelenir. Site filtre
  kutuları **durum-bazlı** ayarlanır: ikinci kez "Uygula" dediğinizde önceki seçili filtreler
  kapanmaz (örn. Benzin+Dizel seçiliyken şirket eklerseniz Benzin+Dizel açık kalır).
- **Filtreleri Temizle**: Filtre kutularını boşaltır ve **filtresiz tüm sonuçlara** geri döner
  (yeniden arama yapmadan). "Temizle" (arama panelindeki) ise her şeyi sıfırlar.

### 4.4. Rapor kaydetme
1. **Sonuçları Kaydet** → rapor adı isteyen pencere (örn. `İstanbulHavalimani-Haftasonu`).
2. Aynı adda rapor varsa: **Evet** = üzerine yaz, **Hayır** = yeni isimle kaydet, **İptal**.
3. Rapor + araçlar MySQL'e tek işlemde (transaction) kaydedilir.

### 4.5. Geçmiş raporlar
1. **Geçmiş Raporları Getir** → açılan pencerede rapor adı, lokasyon, tarihler,
   kayıt tarihi ve **araç sayısı** listelenir.
2. Bir rapora **çift tıkla** → araçları ana ekrana yüklenir.
3. **Seçili Raporu Sil** → onaydan sonra kalıcı silinir (araçlar da cascade ile silinir).
4. **Rapor Karşılaştır** → iki geçmiş raporu seçip karşılaştırır: her raporun özeti (adet,
   min/ort/max fiyat) ve ortak araç modellerinin **fiyat farkları** (B daha ucuz/pahalı, renkli).

### 4.6. PNG / CSV çıktısı
1. **PNG Olarak İndir** → başlıklı, tablolu bir PNG üretilir
   (Yolcu360 Araç Kiralama Raporu + rapor bilgileri + tablo).
2. **CSV Olarak İndir** → Excel ile açılabilen `.csv` (UTF-8, noktalı virgül ayraçlı,
   Türkçe karakter uyumlu) üretilir; veriyi başka programlarda işlemek için.
3. **Excel Olarak İndir** → biçimlendirilmiş `.xlsx` (başlık, sayı biçimi, özet satırı) üretilir.

### 4.7. Özet ve görsel önizleme
- Sonuç listesinin altındaki **özet panelinde** toplam araç sayısı, en ucuz / en pahalı /
  ortalama fiyat ve vites/yakıt dağılımı görünür.
- Bir satır seçtiğinizde sağdaki kutuda **aracın görseli** indirilip gösterilir.

### 4.8. Tarayıcı görünürlüğü
- **Tarayıcıyı Göster/Gizle**: sağ paneldeki Chromium'u açıp kapatır (hata ayıklama için).

---

## 5. Etik / güvenlik kuralları

- **SMS/OTP/captcha bypass edilmez.** Site güvenlik sistemi aşılmaz, yoğun istek atılmaz.
- Site yalnızca normal kullanıcı etkileşimleri taklit edilerek kullanılır.
- Giriş gerekiyorsa kullanıcı kendi hesabıyla girer. **Telefon + SMS (MacroDroid)** akışı bir
  bypass **değildir**: yalnızca **kullanıcıya ait** OTP kodunun **kendi cihazları arasında**
  aktarılmasıdır (bkz. §4.1.b). Kod log'a/DB'ye yazılmaz, kullanıldıktan sonra bellekten silinir.

---

## 6. Bilinen sınırlamalar

1. **Firma adı:** Yolcu360 araç kartında firma yalnızca **logo görseli** (UUID) olarak gösterilir;
   metin firma adı DOM'da yoktur. Uygulama, her firma site üzerinde tek tek filtrelenerek çıkarılan
   bir **UUID → firma adı haritası** ile gerçek adı gösterir (örn. Greenmotion, Garenta, Avis, Sixt).
   Haritada olmayan (yeni/nadir) bir tedarikçi için **"Diğer"** yazılır. Bu ad grid, CSV, PNG ve
   Excel çıktılarının tümünde kullanılır.
2. **Saat:** Uygulama, sitenin yarım saat aralıklı saat seçicisinden (li.hour-li) seçtiğiniz
   saati **en yakın yarım saate yuvarlayarak** uygulamayı dener (best-effort). Sitenin Vue
   tabanlı saat widget'ı otomasyona her zaman tutarlı yanıt vermediğinden, seçilemezse **site
   varsayılan saatine (10:00)** düşülür ve arama yine geçerli olur. **Tarihler her zaman kesin
   uygulanır.** (Saat seçimi otomatik aramayı bozmaz; sadece saat varsayılan kalabilir.)
3. **Selector bağımlılığı:** Tüm CSS selector'lar `Yolcu360.Common/Constants/Yolcu360Selectors.cs`
   içindedir. Site arayüzü değişirse yalnızca bu dosyanın güncellenmesi yeterlidir.
4. **Sonuç sayısı:** Liste lazy-load ile gelir; uygulama sayfayı birkaç kez kaydırarak yüzlerce
   aracı yükler, ancak çok büyük listelerde tüm sonuçları getirmeyebilir.

---

## 7. Sorun giderme

| Belirti | Çözüm |
|---------|-------|
| "Veritabanı bağlantısı kurulamadı" | MySQL çalışıyor mu? `appsettings.json` kullanıcı/şifre doğru mu? |
| "Lokasyon önerisi açılmadı" | Alış yerini daha açık yazın (örn. tam "İstanbul Havalimanı"). |
| "Sonuç bulunamadı" | İlk denemede çerez/ağ gecikmesi olabilir; **Ara**'ya tekrar basın. |
| Tarayıcı açılmıyor | Visual C++ Redistributable kurulu mu? `logs/app-log.txt` dosyasına bakın. |
| Telefondan kod gelmiyor | Telefon ve PC **aynı Wi-Fi**'da mı? Güvenlik Duvarı 5055 portuna izin verdi mi? MacroDroid URL'inde **PC IP'si** doğru mu? Önce **Dinleyici Başlat**. |
| "Token geçersiz" (401) | MacroDroid body'sindeki `token`, `appsettings.json`'daki `OtpReceiver.Token` ile aynı olmalı. |
| "Kod formatı geçersiz" (400) | Yalnızca **6 haneli sayısal** kod kabul edilir; MacroDroid regex'i `\b\d{6}\b` olmalı. |
| Giris alani bulunamadi | Site login DOMu degismis olabilir; **Giris Yap (SMS)** penceresinden telefonu yeniden yazdirin veya tarayicida normal kullanici etkilesimiyle devam edin. Selectorlar `Yolcu360LoginSelectors.cs` icinde. |

**Log dosyası:** çalışan exe'nin yanındaki `logs\app-log.txt` — her adım ve hata buraya yazılır.
