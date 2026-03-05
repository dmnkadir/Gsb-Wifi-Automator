using System.Windows;

namespace GsbWifiOtomasyon
{
    public partial class EditNicknameDialog : Window
    {
        public string Nickname { get; set; }

        public EditNicknameDialog(string currentNickname)
        {
            InitializeComponent();

            // Metni parantez içindeki eski isimle güncelliyoruz
            lblInstruction.Text = $"Yeni hesap ismini girin: ({currentNickname})";

            // Düzenleme kutusuna eski ismi yazıyoruz
            txtNickname.Text = currentNickname;

            txtNickname.Focus();
            txtNickname.SelectAll();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            Nickname = txtNickname.Text;
            DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}