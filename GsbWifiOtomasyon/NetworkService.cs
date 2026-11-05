using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
        private string _logoutButtonId = "";

        private readonly string _sessionFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "GsbAccounts",
            "session.dat");

        public string SonKotaBilgisi { get; private set; } = "Kota okunamadı.";

        public NetworkService()
        {
            istemciyiHazirla(); // İlk açılışta motoru kur
        }

        // --- MOTORU SIFIRLAMA VE HAZIRLAMA FONKSİYONU ---
        // Bu fonksiyon, tarayıcıyı kapatıp yeniden açmışız gibi her şeyi temizler.
        private void istemciyiHazirla()
        {
            ServicePointManager.DnsRefreshTimeout = 0;
            ServicePointManager.MaxServicePointIdleTime = 0;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            _cookies = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookies,
                UseCookies = true,
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            _client = new HttpClient(handler);
            _client.BaseAddress = new Uri("https://wifi.gsb.gov.tr/");
            _client.Timeout = TimeSpan.FromSeconds(20);

            // GSB'nin sevdiği başlıklar
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Add("Origin", "https://wifi.gsb.gov.tr");
            _client.DefaultRequestHeaders.Add("Referer", "https://wifi.gsb.gov.tr/login.html");
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _client.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");

            _currentViewState = "";
            _logoutButtonId = "";
            SonKotaBilgisi = "Kota okunamadı.";
        }

        private void OturumCereziniKaydet()
        {
            try
            {
                var cookies = _cookies.GetCookies(new Uri("https://wifi.gsb.gov.tr/"));
                var jsessionCookie = cookies["JSESSIONID"];
                if (jsessionCookie != null && !string.IsNullOrWhiteSpace(jsessionCookie.Value))
                {
                    string dir = Path.GetDirectoryName(_sessionFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllText(_sessionFilePath, jsessionCookie.Value);
                }
            }
            catch { }
        }

        private void OturumCereziniSil()
        {
            try
            {
                if (File.Exists(_sessionFilePath))
                {
                    File.Delete(_sessionFilePath);
                }
            }
            catch { }
        }

        public bool KayitliOturumVarMi()
        {
            try
            {
                return File.Exists(_sessionFilePath) && !string.IsNullOrWhiteSpace(File.ReadAllText(_sessionFilePath));
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> MevcutOturumuKontrolEt()
        {
            try
            {
                if (!File.Exists(_sessionFilePath)) return false;

                string jsessionId = File.ReadAllText(_sessionFilePath);
                if (string.IsNullOrWhiteSpace(jsessionId)) return false;

                _cookies.Add(new Uri("https://wifi.gsb.gov.tr/"), new Cookie("JSESSIONID", jsessionId.Trim()));

                var html = await _client.GetStringAsync("index.html");
                SayfaVerileriniGuncelle(html);

                bool oturumAktif = html.Contains("Toplam Kalan Kota") ||
                                   html.Contains("Total Remaining Quota");

                if (oturumAktif)
                {
                    return true;
                }

                OturumCereziniSil();
                istemciyiHazirla();
                return false;
            }
            catch
            {
                return false;
            }
        }

        // --- 1. LOGIN VE KESİN ŞUTLAMA (AJAX KICK + MEMORY WIPE) ---
        public async Task<(bool basarili, string mesaj)> LoginOl(string kAdi, string sifre, int denemeSayisi = 0, string atilanCihazBilgisi = null)
        {
            if (denemeSayisi > 2) return (false, "Otomatik sonlandırma başarısız. Lütfen tekrar deneyin.");

            try
            {
                if (_cookies.GetCookies(new Uri("https://wifi.gsb.gov.tr/"))["JSESSIONID"] == null)
                {
                    try { await _client.GetAsync("login.html"); } catch { }
                }

                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("j_username", kAdi),
                    new KeyValuePair<string, string>("j_password", sifre),
                    new KeyValuePair<string, string>("submit", "Giriş")
                };

                // Giriş İsteği (AllowAutoRedirect = true sayesinde yönlendirmeyi otomatik takip eder)
                var loginResponse = await _client.PostAsync("j_spring_security_check", new FormUrlEncodedContent(formData));
                var html = await loginResponse.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(html))
                {
                    html = await _client.GetStringAsync("");
                }

                SayfaVerileriniGuncelle(html);

                bool girisBasarili = html.Contains("Toplam Kalan Kota") ||
                                     html.Contains("Total Remaining Quota");

                if (girisBasarili)
                {
                    OturumCereziniKaydet();
                    if (denemeSayisi > 0 || !string.IsNullOrEmpty(atilanCihazBilgisi))
                    {
                        var nameMatch = Regex.Match(html, @"<span class=""myinfo"">\s*([^<]+?)\s*</span>");
                        string kisiBilgi = nameMatch.Success ? $"{nameMatch.Groups[1].Value.Trim()} - " : "";
                        return (true, $"{kisiBilgi}Açık oturum ({atilanCihazBilgisi ?? "Diğer Cihaz"}) atılarak giriş yapıldı!");
                    }
                    return (true, "Giriş Başarılı!");
                }

                // HATA: MAKSİMUM CİHAZ UYARISI MI?
                else if (html.Contains("Maksimum") || html.Contains("Maximum") || html.Contains("maksimumCihazHakkiDolu"))
                {
                    // Atılacak oturumun Başlangıç Zamanı ve MAC Adresi bilgisini çek
                    var rowMatch = Regex.Match(html, @"<tbody[^>]*id=""[^""]*data""[^>]*>.*?<td[^>]*>([^<]+)</td>\s*<td[^>]*>([^<]+)</td>", RegexOptions.Singleline);
                    if (rowMatch.Success)
                    {
                        string baslangic = rowMatch.Groups[1].Value.Trim();
                        string mac = rowMatch.Groups[2].Value.Trim();
                        atilanCihazBilgisi = $"MAC: {mac}, Giriş: {baslangic}";
                    }
                    else if (string.IsNullOrEmpty(atilanCihazBilgisi))
                    {
                        atilanCihazBilgisi = "Diğer Cihaz";
                    }

                    // ViewState'i al
                    var matchVS = Regex.Match(html, @"name=""javax\.faces\.ViewState""[^>]*value=""([^""]+)""", RegexOptions.Singleline);
                    if (matchVS.Success) _currentViewState = matchVS.Groups[1].Value;

                    // "Sonlandır" Butonunu Bul
                    string kickBtnId = "";
                    var butonlar = Regex.Matches(html, @"<button\b[^>]*>.*?</button>", RegexOptions.Singleline);
                    foreach (Match btn in butonlar)
                    {
                        string btnHtml = btn.Value;
                        if (btnHtml.Contains("Sonlandır") || btnHtml.Contains("Terminate") || btnHtml.Contains("End"))
                        {
                            var idMatch = Regex.Match(btnHtml, @"id=""([^""]+)""");
                            if (idMatch.Success)
                            {
                                kickBtnId = idMatch.Groups[1].Value;
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(kickBtnId) && kickBtnId.Contains(":"))
                    {
                        string formId = kickBtnId.Substring(0, kickBtnId.LastIndexOf(':')); // Örn: j_idt20:0:j_idt26

                        var values = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("javax.faces.partial.ajax", "true"),
                            new KeyValuePair<string, string>("javax.faces.source", kickBtnId),
                            new KeyValuePair<string, string>("javax.faces.partial.execute", "@all"),
                            new KeyValuePair<string, string>("javax.faces.partial.render", formId),
                            new KeyValuePair<string, string>(kickBtnId, kickBtnId),
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

                        if (response.IsSuccessStatusCode)
                        {
                            await _client.GetAsync("logout");
                            await Task.Delay(500);
                            istemciyiHazirla();
                            return await LoginOl(kAdi, sifre, denemeSayisi + 1, atilanCihazBilgisi);
                        }
                    }

                    // Eğer Ajax başarısız olursa veya buton yoksa klasik Logout ile dene
                    await _client.GetAsync("logout");
                    await Task.Delay(500);
                    istemciyiHazirla();
                    return await LoginOl(kAdi, sifre, denemeSayisi + 1, atilanCihazBilgisi);
                }
                else if (html.Contains("Oturumu Sonlandır") || html.Contains("End Session") || html.Contains("servisUpdateForm"))
                {
                    // Oturum arafta kalmış (eski oturum tam kapanmamış veya servis durmuş)
                    await OturumuKapat();
                    await Task.Delay(1200);
                    return await LoginOl(kAdi, sifre, denemeSayisi + 1, atilanCihazBilgisi);
                }
                else if (html.Contains("j_username") || html.Contains("Giriş"))
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
                if (string.IsNullOrEmpty(icerik)) return;

                // 1. ViewState Yakalama
                var matchVS = Regex.Match(icerik, @"name=""javax\.faces\.ViewState""[^>]*value=""([^""]+)""", RegexOptions.Singleline);
                if (matchVS.Success) _currentViewState = matchVS.Groups[1].Value;

                // 2. Oturumu Sonlandır Butonu Tespiti
                var butonlar = Regex.Matches(icerik, @"<button\b[^>]*>.*?</button>", RegexOptions.Singleline);
                foreach (Match btn in butonlar)
                {
                    string btnKod = btn.Value;
                    if (btnKod.Contains("Oturumu Sonlandır") || btnKod.Contains("End Session"))
                    {
                        var idMatch = Regex.Match(btnKod, @"id=""([^""]+)""");
                        if (idMatch.Success)
                        {
                            _logoutButtonId = idMatch.Groups[1].Value;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(_logoutButtonId))
                {
                    _logoutButtonId = "servisUpdateForm:j_idt127";
                }

                // 3. Kota Bilgisini Güncelle
                string kota = KotaMetniAyikla(icerik);
                if (kota != "Kota okunamadı.")
                {
                    SonKotaBilgisi = kota;
                }
            }
            catch { }
        }

        private string FormatMb(string mbHam)
        {
            if (string.IsNullOrWhiteSpace(mbHam)) return "0 MB (Kota Bitti)";
            mbHam = mbHam.Replace(",", ".");

            if (double.TryParse(mbHam, NumberStyles.Any, CultureInfo.InvariantCulture, out double mb))
            {
                if (mb <= 0) return "0 MB (Kota Bitti)";
                if (mb > 1024) return $"{mb / 1024.0:F2} GB";
                return $"{mb:F0} MB";
            }
            return "0 MB";
        }

        private string KotaMetniAyikla(string html)
        {
            if (string.IsNullOrEmpty(html)) return "Kota okunamadı.";

            var matchesQuota = Regex.Matches(html, @"(?:Toplam Kalan Kota|Total Remaining Quota) \(MB\):[^<]*?<\/label>\s*<\/td>\s*<td[^>]*>\s*<label[^>]*>([^<]*)<\/label>", RegexOptions.Singleline);
            var matchTime = Regex.Match(html, @"(?:Kalan Kota Zamanı|Remaining Quota Time):[^<]*?<\/label>\s*<\/td>\s*<td[^>]*>\s*<label[^>]*>([^<]*)<\/label>", RegexOptions.Singleline);

            if (matchesQuota.Count > 0)
            {
                string toplamMbHam = matchesQuota[matchesQuota.Count - 1].Groups[1].Value.Trim();
                string toplamFormatli = FormatMb(toplamMbHam);
                string sure = matchTime.Success ? matchTime.Groups[1].Value.Trim() : "--";

                string saat = DateTime.Now.ToString("HH.mm");
                return $"{toplamFormatli} ({saat})\n({sure})";
            }

            return "Kota okunamadı.";
        }

        public async Task<string> KotaBilgisiGetir()
        {
            try
            {
                var html = await _client.GetStringAsync("index.html");
                SayfaVerileriniGuncelle(html);
                return SonKotaBilgisi;
            }
            catch
            {
                return "Bağlantı Sorunu";
            }
        }

        public async Task<(bool basarili, string mesaj)> OturumuKapat()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentViewState) || string.IsNullOrEmpty(_logoutButtonId))
                {
                    var html = await _client.GetStringAsync("index.html");
                    SayfaVerileriniGuncelle(html);
                }

                if (string.IsNullOrEmpty(_logoutButtonId))
                {
                    _logoutButtonId = "servisUpdateForm:j_idt127";
                }

                var request = new HttpRequestMessage(HttpMethod.Post, "index.html");
                request.Headers.Add("Faces-Request", "partial/ajax");
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                request.Headers.Referrer = new Uri("https://wifi.gsb.gov.tr/index.html");

                var values = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("javax.faces.partial.ajax", "true"),
                    new KeyValuePair<string, string>("javax.faces.source", _logoutButtonId),
                    new KeyValuePair<string, string>("javax.faces.partial.execute", "@all"),
                    new KeyValuePair<string, string>(_logoutButtonId, _logoutButtonId),
                    new KeyValuePair<string, string>("servisUpdateForm", "servisUpdateForm"),
                    new KeyValuePair<string, string>("javax.faces.ViewState", _currentViewState)
                };

                request.Content = new FormUrlEncodedContent(values);
                var response = await _client.SendAsync(request);

                await _client.GetAsync("logout");
                return (true, "Oturum başarıyla sonlandırıldı.");
            }
            catch (Exception ex)
            {
                return (false, "Bağlantı Hatası: " + ex.Message);
            }
            finally
            {
                OturumCereziniSil();
                istemciyiHazirla(); // Hafızayı ve çerezleri her halükarda temizle
            }
        }
    }
}
