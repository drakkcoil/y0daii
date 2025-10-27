using System.Windows;
using System.Windows.Controls;

namespace Y0daiiIRC
{
    public partial class JoinChannelDialog : Window
    {
        public string ChannelName => ChannelTextBox.Text.Trim();
        public string? Password => string.IsNullOrEmpty(PasswordTextBox.Text) ? null : PasswordTextBox.Text;

        public JoinChannelDialog()
        {
            InitializeComponent();
        }

        private void JoinButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ChannelName) || ChannelName == "#")
            {
                MessageBox.Show("Please enter a valid channel name.", "Validation Error", 
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

        private void ChannelSuggestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                ChannelTextBox.Text = button.Content.ToString();
            }
        }

        private void ChannelTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                JoinButton_Click(sender, e);
            }
        }

        private void PasswordTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                JoinButton_Click(sender, e);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
