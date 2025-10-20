using System;
using System.Linq;
using System.Windows;
using Y0daiiIRC.Models;
using Y0daiiIRC.Services;

namespace Y0daiiIRC
{
    public partial class ServerListDialog : Window
    {
        private readonly ServerListService _serverListService;
        public ServerInfo? SelectedServer { get; private set; }

        public ServerListDialog(ServerListService serverListService)
        {
            InitializeComponent();
            _serverListService = serverListService;
            LoadServers();
        }

        private void LoadServers()
        {
            var servers = _serverListService.GetServers();
            ServerDataGrid.ItemsSource = servers;
        }

        private void AddServerButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ServerEditDialog();
            if (dialog.ShowDialog() == true)
            {
                _serverListService.AddServer(dialog.ServerInfo);
                LoadServers();
            }
        }

        private void EditServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServerDataGrid.SelectedItem is ServerInfo server)
            {
                var dialog = new ServerEditDialog(server);
                if (dialog.ShowDialog() == true)
                {
                    _serverListService.UpdateServer(dialog.ServerInfo);
                    LoadServers();
                }
            }
            else
            {
                MessageBox.Show("Please select a server to edit.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RemoveServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServerDataGrid.SelectedItem is ServerInfo server)
            {
                var result = MessageBox.Show($"Are you sure you want to remove '{server.Name}'?", 
                    "Remove Server", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _serverListService.RemoveServer(server.Name);
                    LoadServers();
                }
            }
            else
            {
                MessageBox.Show("Please select a server to remove.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServerDataGrid.SelectedItem is ServerInfo server)
            {
                SelectedServer = server;
                _serverListService.UpdateConnectionStats(server.Name);
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please select a server to connect to.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
