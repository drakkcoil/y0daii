using System;
using System.Diagnostics;
using System.Windows;
using Y0daiiIRC.Utils;

namespace Y0daiiIRC
{
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            LoadVersionInfo();
        }

        private void LoadVersionInfo()
        {
            AppTitleText.Text = VersionInfo.ProductName;
            VersionText.Text = $"Version {VersionInfo.Version}";
            BuildText.Text = VersionInfo.BuildInfo;
            FrameworkText.Text = Environment.Version.ToString();
            BuildDateText.Text = VersionInfo.BuildDate.ToString("yyyy-MM-dd");
            CopyrightText.Text = VersionInfo.Copyright;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/drakkcoil/y0daii",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open GitHub repository: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
