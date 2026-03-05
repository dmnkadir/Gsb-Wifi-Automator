using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GsbWifiOtomasyon
{
    public class NetworkService
    {
        // Client ve CookieContainer artık 'readonly' değil çünkü sıfırlayıp yeniden yaratacağız.
        private HttpClient _client;
        private CookieContainer _cookies;

        private string _currentViewState = "";
        private string _startButtonId = "";
        private string _stopButtonId = "";

        public bool IsStartDisabled { get; private set; }
        public bool IsStopDisabled { get; private set; }

        public NetworkService()
        {
            istemciyiHazirla(); // İlk açılışta motoru kur
        }

        // --- MOTORU SIFIRLAMA VE HAZIRLAMA FONKSİYONU ---
        // Bu fonksiyon, tarayıcıyı kapatıp yeniden açmışız gibi her şeyi temizler.
        private void istemciyiHazirla()
        {
            _cookies = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookies,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                UseCookies = true,
                AllowAutoRedirect = true,
                UseProxy = false,
                Proxy = null
            };

            _client = new HttpClient(handler);
            _client.BaseAddress = new Uri("https://10.106.7.45/");
            _client.Timeout = TimeSpan.FromSeconds(30);

            // GSB'nin sevdiği başlıklar
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("Host", "wifi.gsb.gov.tr");
            _client.DefaultRequestHeaders.Add("Origin", "https://wifi.gsb.gov.tr");
            _client.DefaultRequestHeaders.Add("Referer", "https://wifi.gsb.gov.tr/index.html");
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        // --- 1. LOGIN VE KESİN ŞUTLAMA (AJAX KICK + MEMORY WIPE) ---
        public async Task<(bool basarili, string mesaj)> LoginOl(string kAdi, string sifre, int denemeSayisi = 0)
        {
            if (denemeSayisi > 3) return (false, "Otomatik sonlandırma başarısız. Lütfen elle kapatmayı deneyin.");

            try
            {
                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("j_username", kAdi),
                    new KeyValuePair<string, string>("j_password", sifre),
                    new KeyValuePair<string, string>("submit", "Giriş")
                };

                // Giriş İsteği
                await _client.PostAsync("j_spring_security_check", new FormUrlEncodedContent(formData));
                var html = await _client.GetStringAsync("index.html");
                SayfaVerileriniGuncelle(html);

                bool girisBasarili = html.Contains("Toplam Kalan Kota") ||
                                     html.Contains("Total Remaining Quota") ||
                                     html.Contains("Oturumu Sonlandır") ||
                                     html.Contains("End Session");

                if (girisBasarili) return (true, "Giriş Başarılı!");

                // HATA: MAKSİMUM CİHAZ UYARISI MI?
                else if (html.Contains("Maksimum") || html.Contains("Maximum"))
                {
                    // ViewState'i al
                    var matchVS = Regex.Match(html, @"name=""javax\.faces\.ViewState"".*?value=""([^""]+)""", RegexOptions.Singleline);
                    if (matchVS.Success) _currentViewState = matchVS.Groups[1].Value;

                    // "Sonlandır" Butonunu Bul
                    var matchKickBtn = Regex.Match(html, @"<button id=""([^""]+)""[^>]*>.*?Sonlandır.*?<\/button>", RegexOptions.Singleline);

                    if (matchKickBtn.Success)
                    {
                        string kickBtnId = matchKickBtn.Groups[1].Value; // Örn: j_idt20:0:j_idt28:j_idt29
                        string formId = kickBtnId.Substring(0, kickBtnId.LastIndexOf(':')); // Örn: j_idt20:0:j_idt28

                        // --- AJAX PAYLOAD (Senin SS'inle Birebir Aynı Sıra) ---
                        var values = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("javax.faces.partial.ajax", "true"),
                            new KeyValuePair<string, string>("javax.faces.source", kickBtnId),
                            new KeyValuePair<string, string>("javax.faces.partial.execute", "@all"),
                            new KeyValuePair<string, string>("javax.faces.partial.render", "@all"),
                            new KeyValuePair<string, string>(kickBtnId, kickBtnId), // Anahtar ve Değer Eşit!
                            new KeyValuePair<string, string>(formId, formId),
                            new KeyValuePair<string, string>("javax.faces.ViewState", _currentViewState)
                        };

                        var request = new HttpRequestMessage(HttpMethod.Post, "maksimumCihazHakkiDolu.html");

                        // Ajax Headerları
                        request.Headers.Add("Faces-Request", "partial/ajax");
                        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                        request.Headers.Referrer = new Uri("https://wifi.gsb.gov.tr/maksimumCihazHakkiDolu.html");

                        request.Content = new FormUrlEncodedContent(values);

                        // Şutlama paketini yolla
                        var response = await _client.SendAsync(request);

                        // İŞTE SİHİRLİ DOKUNUŞ BURADA:
                        // Şutlama başarılı olduysa (200 OK), sunucu diğer cihazı attı.
                        // Ama bizim şu anki Session'ımız "Hatalı Giriş" modunda kaldı.
                        // O yüzden tarayıcıyı RESETLİYORUZ (Cookie'leri siliyoruz).

                        if (response.IsSuccessStatusCode)
                        {
                            // 1.5 saniye bekle (Sunucu işlemi bitirsin)
                            await Task.Delay(1500);

                            // İstemciyi SIFIRLA (Çerezleri sil, tertemiz sayfa aç)
                            istemciyiHazirla();

                            // Tertemiz sayfayla tekrar giriş yap
                            return await LoginOl(kAdi, sifre, denemeSayisi + 1);
                        }
                    }

                    // Eğer Ajax başarısız olursa veya buton yoksa klasik Logout ile dene
                    // (Yine de reset atalım garanti olsun)
                    await _client.GetAsync("/logout");
                    await Task.Delay(1000);
                    istemciyiHazirla(); // Temizle
                    return await LoginOl(kAdi, sifre, denemeSayisi + 1);
                }
                else if (html.Contains("j_username") || html.Contains("Giriş Yap"))
                {
                    return (false, "Kullanıcı adı veya şifre hatalı.");
                }
                else
                {
                    return (false, "Bağlantı var ama sayfa yapısı farklı.");
                }
            }
            catch (Exception ex)
            {
                return (false, "Hata: " + ex.Message);
            }
        }

        private void SayfaVerileriniGuncelle(string icerik)
        {
            try
            {
                // 1. ViewState Yakalama (Sadece taze HTML'den)
                var matchVS = Regex.Match(icerik, @"name=""javax\.faces\.ViewState"".*?value=""([^""]+)""", RegexOptions.Singleline);
                if (matchVS.Success) _currentViewState = matchVS.Groups[1].Value;

                // 2. Buton Tespit ve Durum Kontrolü
                var butonlar = Regex.Matches(icerik, @"<button[^>]*>.*?</button>", RegexOptions.Singleline);
                foreach (Match btn in butonlar)
                {
                    string btnKod = btn.Value;
                    var idMatch = Regex.Match(btnKod, @"id=""([^""]+)""");
                    if (!idMatch.Success) continue;
                    string id = idMatch.Groups[1].Value;

                    // KRİTİK: Sadece 'ui-state-disabled' varsa buton kapalıdır.
                    // 'disabled' kelimesine bakmıyoruz çünkü 'aria-disabled="false"' içinde de geçiyor.
                    bool isOff = btnKod.Contains("ui-state-disabled");

                    if (btnKod.Contains("Başlat") || btnKod.Contains("Start"))
                    {
                        _startButtonId = id;
                        IsStartDisabled = isOff;
                    }
                    else if (btnKod.Contains("Durdur") || btnKod.Contains("Stop"))
                    {
                        _stopButtonId = id;
                        IsStopDisabled = isOff;
                    }
                }
            }
            catch { }
        }

        public async Task<string> InternetDurumuDegistir(bool baslat)
        {
            try
            {
                if (string.IsNullOrEmpty(_startButtonId) || string.IsNullOrEmpty(_stopButtonId))
                {
                    SayfaVerileriniGuncelle(await _client.GetStringAsync("index.html"));
                }

                string hedefId = baslat ? _startButtonId : _stopButtonId;

                var request = new HttpRequestMessage(HttpMethod.Post, "index.html");
                request.Headers.Add("Faces-Request", "partial/ajax");
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                request.Headers.Referrer = new Uri("https://wifi.gsb.gov.tr/index.html");

                var values = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("javax.faces.partial.ajax", "true"),
                    new KeyValuePair<string, string>("javax.faces.source", hedefId),
                    new KeyValuePair<string, string>("javax.faces.partial.execute", "@all"),
                    new KeyValuePair<string, string>("javax.faces.partial.render", "servisUpdateForm"),
                    new KeyValuePair<string, string>(hedefId, hedefId),
                    new KeyValuePair<string, string>("servisUpdateForm", "servisUpdateForm"),
                    new KeyValuePair<string, string>("javax.faces.ViewState", _currentViewState)
                };

                request.Content = new FormUrlEncodedContent(values);
                var response = await _client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    SayfaVerileriniGuncelle(await _client.GetStringAsync("index.html"));
                    return baslat ? "İnternet Başlatıldı!" : "İnternet Durduruldu!";
                }
                return "İşlem Başarısız.";
            }
            catch (Exception ex) { return "Hata: " + ex.Message; }
        }

        public async Task<string> KotaBilgisiGetir()
        {
            try
            {
                var html = await _client.GetStringAsync("https://10.106.7.45/index.html");
                SayfaVerileriniGuncelle(html);

                // Metin üzerinden arama yapan kararlı Regex
                var matchQuota = Regex.Match(html, @"(?:Toplam Kalan Kota|Total Remaining Quota) \(MB\):[^<]*?<\/label>[\s\S]*?<label[^>]*>([\d\.]+)", RegexOptions.Singleline);
                var matchTime = Regex.Match(html, @"(?:Kalan Kota Zamanı|Remaining Quota Time):[^<]*?<\/label>[\s\S]*?<label[^>]*>(.*?)<\/label>", RegexOptions.Singleline);

                if (matchQuota.Success)
                {
                    string mbHam = matchQuota.Groups[1].Value.Trim();
                    string sure = matchTime.Success ? matchTime.Groups[1].Value.Trim() : "--";

                    if (double.TryParse(mbHam, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double mb))
                        return mb > 1024 ? $"{mb / 1024.0:F2} GB\n({sure})" : $"{mb:F0} MB\n({sure})";

                    return $"{mbHam} MB\n({sure})";
                }
                return "Kota okunamadı.";
            }
            catch { return "Bağlantı Sorunu"; }
        }

        public async Task<(bool basarili, string mesaj)> OturumuKapat()
        {
            try
            {
                // Sadece logout sayfasına git, sunucunun yönlendirmesini takip etme
                await _client.GetAsync("logout");
                return (true, "Oturum yerel olarak sonlandırıldı.");
            }
            catch (Exception ex)
            {
                return (false, "Bağlantı Hatası: " + ex.Message);
            }
            finally
            {
                istemciyiHazirla(); // Hafızayı ve çerezleri her halükarda temizle
            }
        }



    }
}