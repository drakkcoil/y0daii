using System;
using System.Windows;
using System.Windows.Controls;
using Y0daiiIRC.Configuration;

namespace Y0daiiIRC
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            NotificationVolumeSlider.ValueChanged += (s, e) => UpdateVolumeText();
        }

        private void LoadSettings()
        {
            var settings = AppSettings.Load();
            
            // Load appearance settings
            ThemeComboBox.SelectedIndex = settings.Appearance.Theme == "Dark" ? 0 : 1;
            PrimaryColorComboBox.SelectedIndex = GetColorIndex(settings.Appearance.PrimaryColor);
            FontSizeComboBox.SelectedIndex = GetFontSizeIndex(settings.Appearance.FontSize);
            
            // Load connection settings
            AutoReconnectCheckBox.IsChecked = settings.Behavior.AutoReconnect;
            ReconnectDelayTextBox.Text = settings.Behavior.ReconnectDelay.ToString();
            ShowSystemMessagesCheckBox.IsChecked = settings.Behavior.ShowSystemMessages;
            LogMessagesCheckBox.IsChecked = settings.Behavior.LogMessages;
            LogPathTextBox.Text = settings.Behavior.LogPath;
            
            // Load notification settings
            EnableNotificationsCheckBox.IsChecked = settings.Notifications.EnableNotifications;
            NotifyOnMentionCheckBox.IsChecked = settings.Notifications.NotifyOnMention;
            NotifyOnPrivateMessageCheckBox.IsChecked = settings.Notifications.NotifyOnPrivateMessage;
            NotifyOnChannelMessageCheckBox.IsChecked = settings.Notifications.NotifyOnChannelMessage;
            NotificationVolumeSlider.Value = settings.Notifications.Volume;
            UpdateVolumeText();
            
            // Load update settings
            CheckForUpdatesOnStartupCheckBox.IsChecked = settings.Updates.CheckForUpdatesOnStartup;
            AutoDownloadUpdatesCheckBox.IsChecked = settings.Updates.AutoDownloadUpdates;
            AutoInstallUpdatesCheckBox.IsChecked = settings.Updates.AutoInstallUpdates;
            IncludePrereleasesCheckBox.IsChecked = settings.Updates.IncludePrereleases;
            NotifyOnUpdateAvailableCheckBox.IsChecked = settings.Updates.NotifyOnUpdateAvailable;
            UpdateCheckIntervalComboBox.SelectedIndex = GetUpdateIntervalIndex(settings.Updates.UpdateCheckIntervalDays);
        }

        private void SaveSettings()
        {
            var settings = AppSettings.Load();
            
            // Save appearance settings
            settings.Appearance.Theme = ThemeComboBox.SelectedIndex == 0 ? "Dark" : "Light";
            settings.Appearance.PrimaryColor = GetSelectedColor();
            settings.Appearance.FontSize = GetSelectedFontSize();
            
            // Save connection settings
            settings.Behavior.AutoReconnect = AutoReconnectCheckBox.IsChecked ?? false;
            if (int.TryParse(ReconnectDelayTextBox.Text, out int delay))
                settings.Behavior.ReconnectDelay = delay;
            settings.Behavior.ShowSystemMessages = ShowSystemMessagesCheckBox.IsChecked ?? false;
            settings.Behavior.LogMessages = LogMessagesCheckBox.IsChecked ?? false;
            settings.Behavior.LogPath = LogPathTextBox.Text;
            
            // Save notification settings
            settings.Notifications.EnableNotifications = EnableNotificationsCheckBox.IsChecked ?? false;
            settings.Notifications.NotifyOnMention = NotifyOnMentionCheckBox.IsChecked ?? false;
            settings.Notifications.NotifyOnPrivateMessage = NotifyOnPrivateMessageCheckBox.IsChecked ?? false;
            settings.Notifications.NotifyOnChannelMessage = NotifyOnChannelMessageCheckBox.IsChecked ?? false;
            settings.Notifications.Volume = (int)NotificationVolumeSlider.Value;
            
            // Save update settings
            settings.Updates.CheckForUpdatesOnStartup = CheckForUpdatesOnStartupCheckBox.IsChecked ?? false;
            settings.Updates.AutoDownloadUpdates = AutoDownloadUpdatesCheckBox.IsChecked ?? false;
            settings.Updates.AutoInstallUpdates = AutoInstallUpdatesCheckBox.IsChecked ?? false;
            settings.Updates.IncludePrereleases = IncludePrereleasesCheckBox.IsChecked ?? false;
            settings.Updates.NotifyOnUpdateAvailable = NotifyOnUpdateAvailableCheckBox.IsChecked ?? false;
            settings.Updates.UpdateCheckIntervalDays = GetSelectedUpdateInterval();
            
            settings.Save();
        }

        private void UpdateVolumeText()
        {
            VolumeText.Text = $"{(int)NotificationVolumeSlider.Value}%";
        }

        private int GetColorIndex(string color)
        {
            return color switch
            {
                "Blue" => 0,
                "Green" => 1,
                "Purple" => 2,
                "Orange" => 3,
                _ => 0
            };
        }

        private string GetSelectedColor()
        {
            return PrimaryColorComboBox.SelectedIndex switch
            {
                0 => "Blue",
                1 => "Green",
                2 => "Purple",
                3 => "Orange",
                _ => "Blue"
            };
        }

        private int GetFontSizeIndex(string fontSize)
        {
            return fontSize switch
            {
                "Small" => 0,
                "Medium" => 1,
                "Large" => 2,
                _ => 1
            };
        }

        private string GetSelectedFontSize()
        {
            return FontSizeComboBox.SelectedIndex switch
            {
                0 => "Small",
                1 => "Medium",
                2 => "Large",
                _ => "Medium"
            };
        }

        private int GetUpdateIntervalIndex(int days)
        {
            return days switch
            {
                1 => 0,
                7 => 1,
                30 => 2,
                0 => 3,
                _ => 1
            };
        }

        private int GetSelectedUpdateInterval()
        {
            if (UpdateCheckIntervalComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                return int.TryParse(tag, out int days) ? days : 7;
            }
            return 7;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettings();
                MessageBox.Show("Settings saved successfully!", "Settings", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}