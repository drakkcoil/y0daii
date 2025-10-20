using System.Windows;

namespace Y0daiiIRC
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Load current settings from configuration
            // This would typically load from a settings file or registry
            ThemeComboBox.SelectedIndex = 0; // Dark theme
            PrimaryColorComboBox.SelectedIndex = 0; // Deep Purple
            SecondaryColorComboBox.SelectedIndex = 0; // Lime
            
            AutoConnectCheckBox.IsChecked = false;
            ShowTimestampsCheckBox.IsChecked = true;
            ShowJoinPartCheckBox.IsChecked = true;
            PlaySoundCheckBox.IsChecked = true;
            
            DefaultServerTextBox.Text = "irc.libera.chat";
            DefaultPortTextBox.Text = "6667";
            UseSSLByDefaultCheckBox.IsChecked = false;
            PingIntervalTextBox.Text = "60";
            
            EnableNotificationsCheckBox.IsChecked = true;
            NotifyOnMentionCheckBox.IsChecked = true;
            NotifyOnPrivateMessageCheckBox.IsChecked = true;
            NotifyOnChannelMessageCheckBox.IsChecked = false;
            NotificationVolumeSlider.Value = 50;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save settings to configuration
            // This would typically save to a settings file or registry
            
            MessageBox.Show("Settings saved successfully!", "Settings", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to reset all settings to defaults?", 
                "Reset Settings", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                LoadSettings();
            }
        }

        private void CheckForUpdatesNowButton_Click(object sender, RoutedEventArgs e)
        {
            var updateDialog = new UpdateDialog();
            updateDialog.Owner = this;
            updateDialog.ShowDialog();
        }

        private void ViewUpdateHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/drakkcoil/y0daii/releases",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open update history: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
