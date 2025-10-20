using System;
using System.IO;
using Newtonsoft.Json;

namespace Y0daiiIRC.Configuration
{
    public class AppSettings
    {
        public AppearanceSettings Appearance { get; set; } = new();
        public ConnectionSettings Connection { get; set; } = new();
        public NotificationSettings Notifications { get; set; } = new();
        public BehaviorSettings Behavior { get; set; } = new();
        public UpdateSettings Updates { get; set; } = new();

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Y0daiiIRC", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }

    public class AppearanceSettings
    {
        public string Theme { get; set; } = "Light";
        public string PrimaryColor { get; set; } = "Blue";
        public string SecondaryColor { get; set; } = "Blue";
        public string FontSize { get; set; } = "Medium";
        public bool ShowTimestamps { get; set; } = true;
        public bool ShowJoinPart { get; set; } = true;
    }

    public class ConnectionSettings
    {
        public string DefaultServer { get; set; } = "irc.libera.chat";
        public int DefaultPort { get; set; } = 6667;
        public string DefaultNickname { get; set; } = "";
        public bool UseSSLByDefault { get; set; } = false;
        public int PingInterval { get; set; } = 60;
        public bool AutoConnect { get; set; } = false;
    }

    public class NotificationSettings
    {
        public bool EnableNotifications { get; set; } = true;
        public bool NotifyOnMention { get; set; } = true;
        public bool NotifyOnPrivateMessage { get; set; } = true;
        public bool NotifyOnChannelMessage { get; set; } = false;
        public int Volume { get; set; } = 50;
        public bool PlaySound { get; set; } = true;
    }

    public class BehaviorSettings
    {
        public bool AutoReconnect { get; set; } = true;
        public int ReconnectDelay { get; set; } = 5;
        public bool ShowSystemMessages { get; set; } = true;
        public bool LogMessages { get; set; } = false;
        public string LogPath { get; set; } = "";
    }

    public class UpdateSettings
    {
        public bool CheckForUpdatesOnStartup { get; set; } = true;
        public bool AutoDownloadUpdates { get; set; } = false;
        public bool AutoInstallUpdates { get; set; } = false;
        public bool IncludePrereleases { get; set; } = false;
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;
        public string LastCheckedVersion { get; set; } = "";
        public bool NotifyOnUpdateAvailable { get; set; } = true;
        public int UpdateCheckIntervalDays { get; set; } = 7;
    }
}
