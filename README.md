# 🌐 GSB Wi-Fi Kontrol Pro (Otomasyon & GoodbyeDPI)

KYK / GSB Yurt Wi-Fi (Captive Portal) ağına tarayıcı açmadan, doğrudan HTTP paketleriyle saniyeler içinde giriş/çıkış yapmayı sağlayan, çoklu hesap destekli ve dahili GoodbyeDPI entegrasyonuna sahip masaüstü otomasyon uygulaması.

## 🚀 Özellikler
- **⚡ Paket Tabanlı Hızlı Giriş & Çıkış:** Yoğun saatlerde GSB portal sayfasının yüklenmesini beklemeden doğrudan arka plan paketleriyle anında oturum açar ve kapatır.
- **🔄 Otomatik Cihaz Düşürme (Auto-Kick):** Hesap başka bir cihazda (ör. telefonda) açıksa, maksimum cihaz uyarısını otomatik aşarak diğer cihazdaki oturumu sonlandırır, hangi MAC adresli cihazın atıldığını ekranda bildirir ve anında giriş yapar.
- **🛡️ Dahili GoodbyeDPI & Akıllı Bypass:**
  - **Tek .exe Taşınabilirliği:** GoodbyeDPI dosyaları doğrudan uygulama içerisine gömülüdür (`EmbeddedResource`), ekstra klasör gerektirmez.
  - **Windows Servis Senkronizasyonu:** Bilgisayardaki mevcut GoodbyeDPI servisiyle tam senkronize çalışır.
  - **Otomatik Duraklat-İşlem Yap-Tekrar Aç:** GoodbyeDPI açıkken GSB işlemleri (Giriş, Çıkış, Kota Yenileme) yapılacağı zaman GoodbyeDPI'ı geçici olarak duraklatır, işlemi tamamlar ve ardından otomatik olarak eski durumuna döndürür.
- **📊 Kota ve Son Güncelleme Saati Takibi:** 32 GB ana kotayı, kalan süreyi ve kotanın en son hangi saatte güncellendiğini gösterir.
- **👥 Çoklu Hesap Yönetimi:** Sınırsız sayıda hesabı isimlendirerek (`Belgelerim\GsbAccounts\accounts.txt`) kaydetme, düzenleme ve oturum açıkken bile tek tıkla hesaplar arası geçiş yapma imkanı sunar.

## 📦 Kurulum ve Kullanım (Release)
1. Sağ taraftaki **Releases** bölümünden en güncel `GsbWifiOtomasyon.exe` dosyasını indirin.
2. Uygulama hiçbir ek klasöre ihtiyaç duymadan tek bir `.exe` olarak çalışır.

## 🛠 Teknik Altyapı
- **Dil & Arayüz:** C# / WPF (.NET Framework 4.8)
- **Ağ & Doğrulama:** `HttpClient` (JSF ViewState & Spring Security Paket Otomasyonu)
- **DPI Yönetimi:** Gömülü GoodbyeDPI & WinDivert Windows Servis Yöneticisi
