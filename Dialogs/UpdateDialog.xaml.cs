using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Y0daiiIRC.Models;
using Y0daiiIRC.Services;
using Y0daiiIRC.Utils;

namespace Y0daiiIRC
{
    public partial class UpdateDialog : Window
    {
        private readonly UpdateService _updateService;
        private UpdateInfo? _availableUpdate;
        private DispatcherTimer? _loadingTimer;
        private DateTime _downloadStartTime;

        public UpdateDialog()
        {
            InitializeComponent();
            _updateService = new UpdateService();
            
            SetupEventHandlers();
            LoadCurrentVersionInfo();
            StartLoadingAnimation();
        }

        private void SetupEventHandlers()
        {
            _updateService.UpdateStatusChanged += OnUpdateStatusChanged;
            _updateService.DownloadProgressChanged += OnDownloadProgressChanged;
            _updateService.UpdateErrorOccurred += OnUpdateErrorOccurred;
        }

        private void LoadCurrentVersionInfo()
        {
            CurrentVersionText.Text = VersionInfo.Version;
            BuildDateText.Text = VersionInfo.BuildDate.ToString("yyyy-MM-dd");
        }

        private void StartLoadingAnimation()
        {
            _loadingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _loadingTimer.Tick += (s, e) =>
            {
                LoadingRotation.Angle += 10;
                if (LoadingRotation.Angle >= 360)
                    LoadingRotation.Angle = 0;
            };
        }

        private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                ShowStatusPanel(CheckingPanel);
                _loadingTimer?.Start();
                
                CheckForUpdatesButton.IsEnabled = false;
                
                var result = await _updateService.CheckForUpdatesAsync();
                
                _loadingTimer?.Stop();
                
                if (result.IsError)
                {
                    ShowError(result.ErrorMessage);
                    return;
                }
                
                if (result.HasUpdate && result.LatestVersion != null)
                {
                    _availableUpdate = result.LatestVersion;
                    ShowUpdateAvailable(result.LatestVersion);
                }
                else
                {
                    ShowNoUpdate();
                }
            }
            catch (Exception ex)
            {
                _loadingTimer?.Stop();
                ShowError($"Failed to check for updates: {ex.Message}");
            }
            finally
            {
                CheckForUpdatesButton.IsEnabled = true;
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_availableUpdate == null) return;
            
            try
            {
                DownloadButton.IsEnabled = false;
                ShowStatusPanel(DownloadProgressPanel);
                
                _downloadStartTime = DateTime.Now;
                
                var progress = new Progress<long>(bytesDownloaded =>
                {
                    var progressPercent = (double)bytesDownloaded / _availableUpdate.FileSize * 100;
                    DownloadProgressBar.Value = progressPercent;
                    
                    var elapsed = DateTime.Now - _downloadStartTime;
                    var speed = bytesDownloaded / elapsed.TotalSeconds;
                    var speedText = FormatBytes(speed) + "/s";
                    
                    var downloadedText = FormatBytes(bytesDownloaded);
                    var totalText = FormatBytes(_availableUpdate.FileSize);
                    
                    DownloadStatusText.Text = $"{downloadedText} of {totalText} ({progressPercent:F1}%)";
                    DownloadSpeedText.Text = speedText;
                });
                
                var downloadSuccess = await _updateService.DownloadUpdateAsync(_availableUpdate, progress);
                
                if (downloadSuccess)
                {
                    ShowStatusPanel(InstallProgressPanel);
                    
                    var installSuccess = await _updateService.InstallUpdateAsync(_availableUpdate);
                    
                    if (installSuccess)
                    {
                        MessageBox.Show("Update installed successfully! The application will now restart.", 
                            "Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // Restart the application
                        System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                        Application.Current.Shutdown();
                    }
                    else
                    {
                        ShowError("Failed to install the update. Please try again or install manually.");
                    }
                }
                else
                {
                    ShowError("Failed to download the update. Please check your internet connection and try again.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Update failed: {ex.Message}");
            }
            finally
            {
                DownloadButton.IsEnabled = true;
            }
        }

        private void ViewReleasesButton_Click(object sender, RoutedEventArgs e)
        {
            _updateService.OpenReleasesPage();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void ShowUpdateAvailable(UpdateInfo updateInfo)
        {
            ShowStatusPanel(UpdateAvailablePanel);
            
            LatestVersionText.Text = updateInfo.Version;
            ReleaseDateText.Text = updateInfo.ReleaseDate.ToString("yyyy-MM-dd");
            FileSizeText.Text = FormatBytes(updateInfo.FileSize);
            ReleaseNotesText.Text = updateInfo.ReleaseNotes;
            
            DownloadButton.Visibility = Visibility.Visible;
        }

        private void ShowNoUpdate()
        {
            ShowStatusPanel(NoUpdatePanel);
            DownloadButton.Visibility = Visibility.Collapsed;
        }

        private void ShowError(string errorMessage)
        {
            ShowStatusPanel(ErrorPanel);
            ErrorText.Text = errorMessage;
            DownloadButton.Visibility = Visibility.Collapsed;
        }

        private void ShowStatusPanel(FrameworkElement panelToShow)
        {
            // Hide all panels
            CheckingPanel.Visibility = Visibility.Collapsed;
            NoUpdatePanel.Visibility = Visibility.Collapsed;
            UpdateAvailablePanel.Visibility = Visibility.Collapsed;
            DownloadProgressPanel.Visibility = Visibility.Collapsed;
            InstallProgressPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Collapsed;
            
            // Show the requested panel
            panelToShow.Visibility = Visibility.Visible;
        }

        private void OnUpdateStatusChanged(object? sender, UpdateStatus status)
        {
            Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    case UpdateStatus.Checking:
                        ShowStatusPanel(CheckingPanel);
                        _loadingTimer?.Start();
                        break;
                    case UpdateStatus.Available:
                        _loadingTimer?.Stop();
                        break;
                    case UpdateStatus.Downloading:
                        ShowStatusPanel(DownloadProgressPanel);
                        break;
                    case UpdateStatus.Installing:
                        ShowStatusPanel(InstallProgressPanel);
                        break;
                    case UpdateStatus.Completed:
                        break;
                    case UpdateStatus.Failed:
                        _loadingTimer?.Stop();
                        break;
                    case UpdateStatus.NoUpdate:
                        ShowNoUpdate();
                        _loadingTimer?.Stop();
                        break;
                }
            });
        }

        private void OnDownloadProgressChanged(object? sender, long bytesDownloaded)
        {
            Dispatcher.Invoke(() =>
            {
                if (_availableUpdate != null)
                {
                    var progressPercent = (double)bytesDownloaded / _availableUpdate.FileSize * 100;
                    DownloadProgressBar.Value = progressPercent;
                    
                    var elapsed = DateTime.Now - _downloadStartTime;
                    var speed = bytesDownloaded / elapsed.TotalSeconds;
                    var speedText = FormatBytes(speed) + "/s";
                    
                    var downloadedText = FormatBytes(bytesDownloaded);
                    var totalText = FormatBytes(_availableUpdate.FileSize);
                    
                    DownloadStatusText.Text = $"{downloadedText} of {totalText} ({progressPercent:F1}%)";
                    DownloadSpeedText.Text = speedText;
                }
            });
        }

        private void OnUpdateErrorOccurred(object? sender, string error)
        {
            Dispatcher.Invoke(() =>
            {
                ShowError(error);
            });
        }

        private static string FormatBytes(double bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            while (bytes >= 1024 && counter < suffixes.Length - 1)
            {
                bytes /= 1024;
                counter++;
            }
            return $"{bytes:F1} {suffixes[counter]}";
        }

        protected override void OnClosed(EventArgs e)
        {
            _loadingTimer?.Stop();
            _updateService?.Dispose();
            base.OnClosed(e);
        }
    }
}
