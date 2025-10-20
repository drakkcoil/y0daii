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
            ServerNameTextBox.Text = ServerInfo.Name;
            HostTextBox.Text = ServerInfo.Host;
            PortTextBox.Text = ServerInfo.Port.ToString();
            UseSslCheckBox.IsChecked = ServerInfo.UseSSL;
            NicknameTextBox.Text = ServerInfo.Nickname ?? "";
            UsernameTextBox.Text = ServerInfo.Username ?? "";
            RealNameTextBox.Text = ServerInfo.RealName ?? "";
            IdentServerTextBox.Text = ServerInfo.IdentServer ?? "";
            IdentPortTextBox.Text = ServerInfo.IdentPort.ToString();
            UseIdentCheckBox.IsChecked = !string.IsNullOrEmpty(ServerInfo.IdentServer);
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

            ServerInfo.Name = ServerNameTextBox.Text.Trim();
            ServerInfo.Host = HostTextBox.Text.Trim();
            ServerInfo.Port = port;
            ServerInfo.UseSSL = UseSslCheckBox.IsChecked == true;
            ServerInfo.Nickname = string.IsNullOrEmpty(NicknameTextBox.Text) ? null : NicknameTextBox.Text;
            ServerInfo.Username = string.IsNullOrEmpty(UsernameTextBox.Text) ? null : UsernameTextBox.Text;
            ServerInfo.RealName = string.IsNullOrEmpty(RealNameTextBox.Text) ? null : RealNameTextBox.Text;
            ServerInfo.IdentServer = UseIdentCheckBox.IsChecked == true ? IdentServerTextBox.Text.Trim() : null;
            ServerInfo.IdentPort = identPort;
            ServerInfo.AutoJoinChannels = string.IsNullOrEmpty(AutoJoinChannelsTextBox.Text) ? null : AutoJoinChannelsTextBox.Text;

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
