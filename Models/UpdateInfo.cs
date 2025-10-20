using System;

namespace Y0daiiIRC.Models
{
    public class UpdateInfo
    {
        public string Version { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public long FileSize { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
        public bool IsPrerelease { get; set; }
        public bool IsDraft { get; set; }
        public string TagName { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
    }

    public class UpdateCheckResult
    {
        public bool HasUpdate { get; set; }
        public UpdateInfo? LatestVersion { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public bool IsError => !string.IsNullOrEmpty(ErrorMessage);
    }

    public enum UpdateStatus
    {
        Checking,
        Available,
        Downloading,
        Installing,
        Completed,
        Failed,
        NoUpdate
    }
}
