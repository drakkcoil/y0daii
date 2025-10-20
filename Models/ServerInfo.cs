using System;

namespace Y0daiiIRC.Models
{
    public class ServerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 6667;
        public bool UseSSL { get; set; } = false;
        public string? Password { get; set; }
        public string? Nickname { get; set; }
        public string? Username { get; set; }
        public string? RealName { get; set; }
        public string? IdentServer { get; set; }
        public int IdentPort { get; set; } = 113;
        public bool AutoConnect { get; set; } = false;
        public string? AutoJoinChannels { get; set; }
        public DateTime LastConnected { get; set; }
        public int ConnectionCount { get; set; }
        public bool IsFavorite { get; set; } = false;
    }
}
