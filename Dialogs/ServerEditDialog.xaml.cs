using System;
using System.Windows;
using Y0daiiIRC.Models;

namespace Y0daiiIRC
{
    public partial class ServerEditDialog : Window
    {
        public ServerInfo ServerInfo { get; private set; }

        public ServerEditDialog(ServerInfo? existingServer = null)
        {
            InitializeComponent();
            ServerInfo = existingServer ?? new ServerInfo();
            LoadServerInfo();
        }

        private void LoadServerInfo()
        {
            NameTextBox.Text = ServerInfo.Name;
            HostTextBox.Text = ServerInfo.Host;
            PortTextBox.Text = ServerInfo.Port.ToString();
            UseSSLCheckBox.IsChecked = ServerInfo.UseSSL;
            PasswordTextBox.Text = ServerInfo.Password ?? "";
            NicknameTextBox.Text = ServerInfo.Nickname ?? "";
            UsernameTextBox.Text = ServerInfo.Username ?? "";
            RealNameTextBox.Text = ServerInfo.RealName ?? "";
            IdentServerTextBox.Text = ServerInfo.IdentServer ?? "";
            IdentPortTextBox.Text = ServerInfo.IdentPort.ToString();
            AutoConnectCheckBox.IsChecked = ServerInfo.AutoConnect;
            IsFavoriteCheckBox.IsChecked = ServerInfo.IsFavorite;
            AutoJoinChannelsTextBox.Text = ServerInfo.AutoJoinChannels ?? "";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(HostTextBox.Text))
            {
                MessageBox.Show("Please enter a host address.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(PortTextBox.Text, out int port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("Please enter a valid port number (1-65535).", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(IdentPortTextBox.Text, out int identPort) || identPort <= 0 || identPort > 65535)
            {
                MessageBox.Show("Please enter a valid ident port number (1-65535).", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ServerInfo.Name = NameTextBox.Text.Trim();
            ServerInfo.Host = HostTextBox.Text.Trim();
            ServerInfo.Port = port;
            ServerInfo.UseSSL = UseSSLCheckBox.IsChecked == true;
            ServerInfo.Password = string.IsNullOrEmpty(PasswordTextBox.Text) ? null : PasswordTextBox.Text;
            ServerInfo.Nickname = string.IsNullOrEmpty(NicknameTextBox.Text) ? null : NicknameTextBox.Text;
            ServerInfo.Username = string.IsNullOrEmpty(UsernameTextBox.Text) ? null : UsernameTextBox.Text;
            ServerInfo.RealName = string.IsNullOrEmpty(RealNameTextBox.Text) ? null : RealNameTextBox.Text;
            ServerInfo.IdentServer = string.IsNullOrEmpty(IdentServerTextBox.Text) ? null : IdentServerTextBox.Text;
            ServerInfo.IdentPort = identPort;
            ServerInfo.AutoConnect = AutoConnectCheckBox.IsChecked == true;
            ServerInfo.IsFavorite = IsFavoriteCheckBox.IsChecked == true;
            ServerInfo.AutoJoinChannels = string.IsNullOrEmpty(AutoJoinChannelsTextBox.Text) ? null : AutoJoinChannelsTextBox.Text;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
