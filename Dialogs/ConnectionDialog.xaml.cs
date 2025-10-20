using System;
using System.Windows;

namespace Y0daiiIRC
{
    public partial class ConnectionDialog : Window
    {
        public string Server => ServerTextBox.Text.Trim();
        public int Port => int.TryParse(PortTextBox.Text, out int port) ? port : 6667;
        public string Nickname => NicknameTextBox.Text.Trim();
        public string Username => UsernameTextBox.Text.Trim();
        public string RealName => RealNameTextBox.Text.Trim();
        public bool UseSSL => UseSslCheckBox.IsChecked == true;

        public ConnectionDialog()
        {
            InitializeComponent();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Server) || string.IsNullOrEmpty(Nickname) || 
                string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(RealName))
            {
                MessageBox.Show("Please fill in all required fields.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
