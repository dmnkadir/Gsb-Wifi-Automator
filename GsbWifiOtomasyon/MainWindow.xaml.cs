using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace GsbWifiOtomasyon
{
    public class SavedAccount
    {
        public string Nickname { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public partial class MainWindow : Window
    {
        // Değişkenleri teke düşürdük
        private ObservableCollection<SavedAccount> _savedAccounts = new ObservableCollection<SavedAccount>();
        private string _accountFilePath;
        private NetworkService _networkService = new NetworkService();
        private bool _isBypassInProgress = false;

        public MainWindow()
        {
            InitializeComponent();

            // Belgelerim klasörünün yolunu al
            string belgelerim = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // GsbAccounts klasör yolunu oluştur
            string klasorYolu = Path.Combine(belgelerim, "GsbAccounts");

            // Klasör yoksa oluştur (System.IO sayesinde)
            if (!Directory.Exists(klasorYolu))
            {
                Directory.CreateDirectory(klasorYolu);
            }

            // Tam dosya yolunu ata
            _accountFilePath = Path.Combine(klasorYolu, "accounts.txt");

            LoadSavedAccounts();
            GuncelleDpiSwitchDurumu();
            MevcutOturumuDenetle();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (!_isBypassInProgress)
            {
                GuncelleDpiSwitchDurumu();
            }
        }

        private void GuncelleDpiSwitchDurumu()
        {
            bool aktif = GoodbyeDpiService.IsRunning();
            chkGoodbyeDpi.IsChecked = aktif;
            txtDpiStatus.Text = aktif ? "Aktif (Çalışıyor)" : "Kapalı";
            txtDpiStatus.Foreground = aktif
                ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2E7D32"))
                : System.Windows.Media.Brushes.Gray;
        }

        private async void chkGoodbyeDpi_Click(object sender, RoutedEventArgs e)
        {
            if (_isBypassInProgress) return;
            bool hedefDurum = chkGoodbyeDpi.IsChecked == true;
            chkGoodbyeDpi.IsEnabled = false;
            txtDpiStatus.Text = hedefDurum ? "Başlatılıyor..." : "Durduruluyor...";
            try
            {
                if (hedefDurum)
                    await GoodbyeDpiService.StartAsync();
                else
                    await GoodbyeDpiService.StopAsync();
            }
            finally
            {
                chkGoodbyeDpi.IsEnabled = true;
                GuncelleDpiSwitchDurumu();
            }
        }

        private async Task<T> ExecuteWithDpiBypassAsync<T>(Func<Task<T>> networkAction, string islemMesaji = null)
        {
            bool dpiAcikMiydi = GoodbyeDpiService.IsRunning();
            _isBypassInProgress = true;
            chkGoodbyeDpi.IsEnabled = false;
            try
            {
                if (dpiAcikMiydi)
                {
                    txtDpiStatus.Text = "GSB işlemi için duraklatıldı...";
                    if (!string.IsNullOrEmpty(islemMesaji))
                        txtStatus.Text = islemMesaji;
                    await GoodbyeDpiService.StopAsync();
                }
                return await networkAction();
            }
            finally
            {
                if (dpiAcikMiydi)
                {
                    txtDpiStatus.Text = "Yeniden başlatılıyor...";
                    await GoodbyeDpiService.StartAsync();
                }
                _isBypassInProgress = false;
                chkGoodbyeDpi.IsEnabled = true;
                GuncelleDpiSwitchDurumu();
            }
        }

        private async void MevcutOturumuDenetle()
        {
            if (!_networkService.KayitliOturumVarMi())
                return;

            bool aktif = await ExecuteWithDpiBypassAsync(() => _networkService.MevcutOturumuKontrolEt());
            if (aktif)
            {
                txtStatus.Text = "Mevcut aktif oturum yüklendi.";
                panelControls.Visibility = Visibility.Visible;
                panelControls.Opacity = 1;
                ((System.Windows.Media.TranslateTransform)panelControls.RenderTransform).Y = 0;
                panelLogin.Visibility = Visibility.Collapsed;
                txtQuota.Text = _networkService.SonKotaBilgisi;
            }
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (_isBypassInProgress) return;

            string kadi = txtUsername.Text;
            string sifre = txtPassword.Visibility == Visibility.Visible ? txtPassword.Password : txtVisiblePassword.Text;

            if (string.IsNullOrEmpty(kadi) || string.IsNullOrEmpty(sifre))
            {
                txtStatus.Text = "Lütfen bilgileri doldurun.";
                return;
            }

            btnLogin.IsEnabled = false;
            txtStatus.Text = "Giriş yapılıyor...";

            bool oturumZatenAcikti = panelControls.Visibility == Visibility.Visible;
            var sonuc = await ExecuteWithDpiBypassAsync(async () =>
            {
                if (oturumZatenAcikti)
                {
                    await _networkService.OturumuKapat();
                    await Task.Delay(300);
                }
                return await _networkService.LoginOl(kadi, sifre);
            }, oturumZatenAcikti ? "Mevcut oturum kapatılıp yeni hesaba geçiliyor..." : "GoodbyeDPI duraklatılıyor ve giriş yapılıyor...");
            btnLogin.IsEnabled = true;

            if (sonuc.basarili)
            {
                txtStatus.Text = sonuc.mesaj;
                panelControls.Visibility = Visibility.Visible;
                panelLogin.Visibility = Visibility.Collapsed;

                // Görünürlük (Opacity) animasyonu: 0'dan 1'e 0.6 saniyede çıksın
                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.6));

                // Aşağıdan yukarı kayma animasyonu: 30 birim aşağıdan 0'a (kendi yerine) gelsin
                var slideUp = new System.Windows.Media.Animation.DoubleAnimation(30, 0, TimeSpan.FromSeconds(0.6));

                panelControls.BeginAnimation(OpacityProperty, fadeIn);
                ((System.Windows.Media.TranslateTransform)panelControls.RenderTransform).BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideUp);

                txtQuota.Text = _networkService.SonKotaBilgisi;

                if (chkSaveAccount.IsChecked == true)
                {
                    panelSaveNickname.Visibility = Visibility.Visible;
                }
            }
            else
            {
                txtStatus.Text = sonuc.mesaj;
            }
        }

        private void btnShowPassword_Click(object sender, RoutedEventArgs e)
        {
            if (txtPassword.Visibility == Visibility.Visible)
            {
                // Şifreyi GÖSTER
                txtVisiblePassword.Text = txtPassword.Password;
                txtPassword.Visibility = Visibility.Collapsed;
                txtVisiblePassword.Visibility = Visibility.Visible;
                btnShowPassword.Content = "\uED1A";
            }
            else
            {
                // Şifreyi GİZLE
                txtPassword.Password = txtVisiblePassword.Text;
                txtVisiblePassword.Visibility = Visibility.Collapsed;
                txtPassword.Visibility = Visibility.Visible;
                btnShowPassword.Content = "\uE7B3";
            }
        }

        private async void btnExitSession_Click(object sender, RoutedEventArgs e)
        {
            if (_isBypassInProgress) return;

            btnExitSession.IsEnabled = false;
            try
            {
                txtStatus.Text = "Çıkış isteği doğrulanıyor...";
                var (basarili, mesaj) = await ExecuteWithDpiBypassAsync(() => _networkService.OturumuKapat(), "GoodbyeDPI duraklatılıyor ve çıkış yapılıyor...");

                if (basarili)
                {
                    panelControls.Visibility = Visibility.Collapsed;
                    panelSaveNickname.Visibility = Visibility.Collapsed;
                    panelLogin.Visibility = Visibility.Visible;
                    btnLogin.IsEnabled = true;
                    txtStatus.Text = "Oturum sonlandırıldı.";
                }
                else
                {
                    // SUNUCUNUN GERÇEK YANITINI BURADA GÖRECEĞİZ
                    MessageBox.Show(mesaj, "Sunucu Logout Hatası");
                    txtStatus.Text = "Hata: Oturum hala açık!";
                }
            }
            finally
            {
                btnExitSession.IsEnabled = true;
            }
        }

        private async void btnQuotaRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_isBypassInProgress) return;

            txtStatus.Text = "Kota bilgisi güncelleniyor...";
            txtQuota.Text = await ExecuteWithDpiBypassAsync(() => _networkService.KotaBilgisiGetir(), "Kota için GoodbyeDPI geçici duraklatılıyor...");
            txtStatus.Text = "Kota güncellendi.";
        }

        private void LoadSavedAccounts()
        {
            if (File.Exists(_accountFilePath))
            {
                var lines = File.ReadAllLines(_accountFilePath);
                _savedAccounts.Clear();
                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length == 3)
                        _savedAccounts.Add(new SavedAccount { Nickname = parts[0], Username = parts[1], Password = parts[2] });
                }
            }
            listSavedAccounts.ItemsSource = _savedAccounts;
        }

        private void btnDeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            var acc = (sender as System.Windows.Controls.Button).Tag as SavedAccount;
            if (acc != null)
            {
                _savedAccounts.Remove(acc);
                File.WriteAllLines(_accountFilePath, _savedAccounts.Select(a => $"{a.Nickname}|{a.Username}|{a.Password}"));
            }
        }

        private void btnQuickLogin_Click(object sender, RoutedEventArgs e)
        {
            if (_isBypassInProgress) return;

            var acc = (sender as System.Windows.Controls.Button).Tag as SavedAccount;
            if (acc != null)
            {
                txtUsername.Text = acc.Username;
                txtPassword.Password = acc.Password;
                txtVisiblePassword.Text = acc.Password;
                btnLogin_Click(null, null); // Mevcut girişi tetikle
            }
        }

        private void btnConfirmSave_Click(object sender, RoutedEventArgs e)
        {
            string nick = string.IsNullOrEmpty(txtNewNickname.Text) ? "İsimsiz Hesap" : txtNewNickname.Text;
            string sifre = txtPassword.Visibility == Visibility.Visible ? txtPassword.Password : txtVisiblePassword.Text;

            _savedAccounts.Add(new SavedAccount
            {
                Nickname = nick,
                Username = txtUsername.Text,
                Password = sifre
            });

            File.WriteAllLines(_accountFilePath, _savedAccounts.Select(a => $"{a.Nickname}|{a.Username}|{a.Password}"));

            panelSaveNickname.Visibility = Visibility.Collapsed;
            txtNewNickname.Clear();
            chkSaveAccount.IsChecked = false;
            LogEkle("Hesap kaydedildi.");
        }

        private void btnCancelSave_Click(object sender, RoutedEventArgs e)
        {
            panelSaveNickname.Visibility = Visibility.Collapsed;
            chkSaveAccount.IsChecked = false;
        }

        private void btnEditNickname_Click(object sender, RoutedEventArgs e)
        {
            // Tıklanan butonun hangi hesaba ait olduğunu alıyoruz
            var acc = (sender as System.Windows.Controls.Button).Tag as SavedAccount;

            if (acc != null)
            {
                // Kendi oluşturduğun pencereyi (EditNicknameDialog) çağırıyoruz
                // İçine mevcut ismi (acc.Nickname) gönder
                var dialog = new EditNicknameDialog(acc.Nickname);

                // Bu pencerenin ana uygulamanın ortasında açılmasını sağlıyoruz
                dialog.Owner = this;

                // 3. Pencereyi aç ve "KAYDET" butonuna basılıp basılmadığını kontrol et
                if (dialog.ShowDialog() == true)
                {
                    // Eğer kullanıcı yeni bir isim girip Kaydet dediyse (dialog.Nickname boş değilse)
                    if (!string.IsNullOrEmpty(dialog.Nickname))
                    {
                        // Listemizi ve dosyamızı güncelle
                        acc.Nickname = dialog.Nickname;

                        // Değişiklikleri accounts.txt dosyasına kaydet
                        System.IO.File.WriteAllLines(_accountFilePath,
                            _savedAccounts.Select(a => $"{a.Nickname}|{a.Username}|{a.Password}"));

                        // Arayüzdeki listeyi yenile
                        LoadSavedAccounts();
                    }
                }
            }
        }

        private void LogEkle(string mesaj)
        {
            // Hata veren kısımları buraya yönlendiriyoruz
            txtStatus.Text = mesaj;
        }

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            string donateUrl = "https://www.shopier.com/helpdeveloper/42616719";

            try
            {
                // Linki varsayılan internet tarayıcısında güvenli bir şekilde açar
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = donateUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Nadiren de olsa tarayıcı bulunamazsa program çökmesin
                MessageBox.Show("Link açılırken bir hata oluştu:\n" + ex.Message);
            }
        }

        private void CustomTitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        // Kapatma butonu için
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Simge durumuna küçültme için
        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
    }
}
