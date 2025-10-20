using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Y0daiiIRC.Models;
using Y0daiiIRC.Services;

namespace Y0daiiIRC
{
    public partial class ServerListDialog : Window
    {
        private readonly ServerListService _serverListService;
        private readonly List<ServerInfo> _servers = new();
        private ServerInfo? _selectedServer;

        public ServerInfo? SelectedServer => _selectedServer;

        public ServerListDialog(ServerListService serverListService)
        {
            InitializeComponent();
            _serverListService = serverListService;
            LoadServers();
        }

        private void LoadServers()
        {
            _servers.Clear();
            _servers.AddRange(_serverListService.GetServers());
            RefreshServerList();
        }

        private void RefreshServerList()
        {
            ServerListPanel.Children.Clear();
            
            foreach (var server in _servers)
            {
                var serverItem = CreateServerItem(server);
                ServerListPanel.Children.Add(serverItem);
            }
        }

        private Border CreateServerItem(ServerInfo server)
        {
            var border = new Border
            {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8),
                Tag = server
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            var nameText = new TextBlock
            {
                Text = server.Name,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumn(nameText, 0);

            var hostText = new TextBlock
            {
                Text = server.Host,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(hostText, 1);

            var portText = new TextBlock
            {
                Text = server.Port.ToString(),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(portText, 2);

            var sslText = new TextBlock
            {
                Text = server.UseSSL ? "Yes" : "No",
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sslText, 3);

            grid.Children.Add(nameText);
            grid.Children.Add(hostText);
            grid.Children.Add(portText);
            grid.Children.Add(sslText);

            border.Child = grid;

            // Add click handler for selection
            border.MouseLeftButtonDown += (s, e) => SelectServer(server, border);

            return border;
        }

        private void SelectServer(ServerInfo server, Border border)
        {
            // Clear previous selection
            foreach (Border item in ServerListPanel.Children)
            {
                item.Background = System.Windows.Media.Brushes.Transparent;
            }

            // Select new item
            border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 248, 255));
            _selectedServer = server;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ServerEditDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _serverListService.AddServer(dialog.ServerInfo);
                LoadServers();
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedServer != null)
            {
                var dialog = new ServerEditDialog(_selectedServer);
                dialog.Owner = this;
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

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedServer != null)
            {
                var result = MessageBox.Show($"Are you sure you want to remove '{_selectedServer.Name}'?", 
                    "Remove Server", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _serverListService.RemoveServer(_selectedServer.Name);
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
            if (_selectedServer != null)
            {
                _serverListService.UpdateConnectionStats(_selectedServer.Name);
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

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}