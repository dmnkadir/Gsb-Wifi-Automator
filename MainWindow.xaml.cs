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
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string kadi = txtUsername.Text;
            string sifre = txtPassword.Visibility == Visibility.Visible ? txtPassword.Password : txtVisiblePassword.Text;

            if (string.IsNullOrEmpty(kadi) || string.IsNullOrEmpty(sifre))
            {
                txtStatus.Text = "Lütfen bilgileri doldurun.";
                return;
            }

            btnLogin.IsEnabled = false;
            txtStatus.Text = "Giriş yapılıyor...";

            var sonuc = await _networkService.LoginOl(kadi, sifre);

            if (sonuc.basarili)
            {
                txtStatus.Text = "Giriş Başarılı!";
                panelControls.Visibility = Visibility.Visible;
                panelLogin.Visibility = Visibility.Collapsed;

                // Görünürlük (Opacity) animasyonu: 0'dan 1'e 0.6 saniyede çıksın
                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.6));

                // Aşağıdan yukarı kayma animasyonu: 30 birim aşağıdan 0'a (kendi yerine) gelsin
                var slideUp = new System.Windows.Media.Animation.DoubleAnimation(30, 0, TimeSpan.FromSeconds(0.6));

                panelControls.BeginAnimation(OpacityProperty, fadeIn);
                ((System.Windows.Media.TranslateTransform)panelControls.RenderTransform).BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideUp);

                txtQuota.Text = "Sorgulanıyor...";
                txtQuota.Text = await _networkService.KotaBilgisiGetir();
                ButonlariGuncelle();
                if (chkSaveAccount.IsChecked == true)
                {
                    panelSaveNickname.Visibility = Visibility.Visible;
                    panelLogin.Visibility = Visibility.Visible; // Panelin görünmesi için login panelini geçici açıyoruz
                }
            }
            else
            {
                txtStatus.Text = sonuc.mesaj;
                btnLogin.IsEnabled = true;
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

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "İnternet başlatılıyor...";
            txtStatus.Text = await _networkService.InternetDurumuDegistir(true);
            ButonlariGuncelle();
        }

        private async void btnStop_Click(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "İnternet durduruluyor...";
            txtStatus.Text = await _networkService.InternetDurumuDegistir(false);
            ButonlariGuncelle();
        }

        private async void btnExitSession_Click(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "Çıkış isteği doğrulanıyor...";
            var (basarili, mesaj) = await _networkService.OturumuKapat();

            if (basarili)
            {
                panelControls.Visibility = Visibility.Collapsed;
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

        private async void btnQuotaRefresh_Click(object sender, RoutedEventArgs e)
        {
            txtQuota.Text = await _networkService.KotaBilgisiGetir();
            ButonlariGuncelle();
        }

        private void ButonlariGuncelle()
        {
            btnStart.IsEnabled = !_networkService.IsStartDisabled;
            btnStop.IsEnabled = !_networkService.IsStopDisabled;
            btnStart.Opacity = btnStart.IsEnabled ? 1.0 : 0.5;
            btnStop.Opacity = btnStop.IsEnabled ? 1.0 : 0.5;
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

            _savedAccounts.Add(new SavedAccount
            {
                Nickname = nick,
                Username = txtUsername.Text,
                Password = txtPassword.Visibility == Visibility.Visible ? txtPassword.Password : txtVisiblePassword.Text
            });

            File.WriteAllLines(_accountFilePath, _savedAccounts.Select(a => $"{a.Nickname}|{a.Username}|{a.Password}"));

            panelSaveNickname.Visibility = Visibility.Collapsed;
            panelLogin.Visibility = Visibility.Collapsed; // İşlem bitince tamamen kapat
            txtNewNickname.Clear();
            chkSaveAccount.IsChecked = false;
            LogEkle("Hesap kaydedildi.");
        }

        private void btnCancelSave_Click(object sender, RoutedEventArgs e)
        {
            panelSaveNickname.Visibility = Visibility.Collapsed;
            panelLogin.Visibility = Visibility.Collapsed;
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