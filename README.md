# GSB Wi-Fi Kontrol Pro

KYK yurtlarındaki GSB Wi-Fi (Captive Portal) ağına tarayıcı açmadan, doğrudan HTTP paketleriyle giriş ve çıkış yapmayı sağlayan masaüstü otomasyon uygulaması.

Yurt internetinin yoğun olduğu saatlerde giriş sayfasının (`wifi.gsb.gov.tr`) yüklenmeme sorununu aşmak, başka cihazda açık kalan oturumları tek tıkla düşürüp bağlanmak ve GSB portalı ile çakışan GoodbyeDPI servisini giriş/çıkış işlemleri sırasında otomatik yönetmek amacıyla geliştirilmiştir.

## İndirme ve Kullanım

Uygulamayı kaynak kodunu derlemeden doğrudan kullanmak için:

1. Sağ taraftaki [Releases](https://github.com/dmnkadir/Gsb-Wifi-Automator/releases) bölümünden en güncel `GsbWifiOtomasyon.exe` dosyasını indirin.
2. Uygulama tek bir `.exe` dosyası olarak çalışır; yanında ek bir klasör veya kurulum gerektirmez.

## Temel Özellikler

- **Paket Tabanlı Doğrudan Giriş ve Çıkış:** Tarayıcı arayüzünü yüklemeden doğrudan arka planda kimlik doğrulama ve oturum sonlandırma paketlerini gönderir.
- **Açık Oturumu Otomatik Sonlandırma:** Giriş yapılan hesap başka bir cihazda (örneğin telefonda) açıksa, maksimum cihaz sınırı ekranını otomatik geçerek diğer cihazdaki oturumu kapatır, hangi cihazın (MAC adresi ve giriş saati) düşürüldüğünü bilgi satırında gösterir ve bağlantıyı kurar.
- **Dahili GoodbyeDPI Yönetimi ve Otomatik Bypass:**
  - GoodbyeDPI dosyaları doğrudan `.exe` içerisine gömülüdür; bilgisayarda GoodbyeDPI kurulu olmasa bile uygulama içindeki anahtar (Switch) üzerinden açılıp kapatılabilir.
  - Bilgisayarda halihazırda çalışan bir GoodbyeDPI Windows servisi varsa durumu otomatik algılar ve senkronize olur.
  - GoodbyeDPI açıkken GSB portalına erişilemediği için; giriş yapma, çıkış yapma ve kota yenileme işlemleri sırasında GoodbyeDPI'ı geçici olarak duraklatır, işlemi tamamlar ve işlem sonunda tekrar eski durumuna döndürür.
- **Kota ve Güncelleme Saati Takibi:** Kalan ana internet kotasını (GB), geçerlilik süresini ve kotanın en son güncellendiği saati gösterir.
- **Çoklu Hesap Kaydı ve Hızlı Geçiş:** Kaydedilen hesapları `Belgelerim\GsbAccounts\accounts.txt` altında saklar. Bir hesapta oturum açıkken listedeki başka bir hesaba tıklandığında mevcut oturumu güvenli şekilde kapatıp yeni hesaba geçiş yapar.

## Teknik Bilgiler

- **Platform:** Windows (.NET Framework 4.8, WPF)
- **Ağ Katmanı:** `HttpClient` (Spring Security & JSF ViewState AJAX oturum yönetimi)
- **Servis Yönetimi:** Windows Service Control (`sc.exe`) üzerinden GoodbyeDPI ve WinDivert yönetimi (Yönetici izni gerektirir)
