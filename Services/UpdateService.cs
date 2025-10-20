using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Y0daiiIRC.Models;
using Y0daiiIRC.Utils;

namespace Y0daiiIRC.Services
{
    public class UpdateService
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/drakkcoil/y0daii/releases/latest";
        private const string GitHubReleasesUrl = "https://github.com/drakkcoil/y0daii/releases";
        private readonly HttpClient _httpClient;
        private readonly string _tempDirectory;

        public event EventHandler<UpdateStatus>? UpdateStatusChanged;
        public event EventHandler<long>? DownloadProgressChanged;
        public event EventHandler<string>? UpdateErrorOccurred;

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Y0daiiIRC-UpdateService/1.0");
            _tempDirectory = Path.Combine(Path.GetTempPath(), "Y0daiiIRC_Updates");
            
            if (!Directory.Exists(_tempDirectory))
            {
                Directory.CreateDirectory(_tempDirectory);
            }
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            try
            {
                OnUpdateStatusChanged(UpdateStatus.Checking);
                
                var response = await _httpClient.GetStringAsync(GitHubApiUrl);
                var release = JsonSerializer.Deserialize<GitHubRelease>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (release == null)
                {
                    return new UpdateCheckResult
                    {
                        ErrorMessage = "Failed to parse release information"
                    };
                }

                var currentVersion = VersionInfo.Version;
                var latestVersion = release.TagName.TrimStart('v');
                
                var hasUpdate = IsNewerVersion(latestVersion, currentVersion);
                
                var updateInfo = new UpdateInfo
                {
                    Version = latestVersion,
                    DownloadUrl = GetDownloadUrl(release),
                    ReleaseNotes = release.Body ?? "No release notes available",
                    ReleaseDate = release.PublishedAt,
                    FileSize = GetFileSize(release),
                    FileName = GetFileName(release),
                    Checksum = GetChecksum(release),
                    IsPrerelease = release.Prerelease,
                    IsDraft = release.Draft,
                    TagName = release.TagName,
                    HtmlUrl = release.HtmlUrl
                };

                OnUpdateStatusChanged(hasUpdate ? UpdateStatus.Available : UpdateStatus.NoUpdate);

                return new UpdateCheckResult
                {
                    HasUpdate = hasUpdate,
                    LatestVersion = updateInfo,
                    CurrentVersion = currentVersion
                };
            }
            catch (Exception ex)
            {
                OnUpdateErrorOccurred($"Failed to check for updates: {ex.Message}");
                return new UpdateCheckResult
                {
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<long>? progress = null)
        {
            try
            {
                OnUpdateStatusChanged(UpdateStatus.Downloading);
                
                var filePath = Path.Combine(_tempDirectory, updateInfo.FileName);
                
                using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;
                
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                
                var buffer = new byte[8192];
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;
                    
                    progress?.Report(downloadedBytes);
                    OnDownloadProgressChanged(downloadedBytes);
                }
                
                // Verify checksum if available
                if (!string.IsNullOrEmpty(updateInfo.Checksum))
                {
                    var calculatedChecksum = await CalculateFileChecksumAsync(filePath);
                    if (!string.Equals(calculatedChecksum, updateInfo.Checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(filePath);
                        throw new Exception("File checksum verification failed");
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                OnUpdateErrorOccurred($"Failed to download update: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> InstallUpdateAsync(UpdateInfo updateInfo)
        {
            try
            {
                OnUpdateStatusChanged(UpdateStatus.Installing);
                
                var installerPath = Path.Combine(_tempDirectory, updateInfo.FileName);
                
                if (!File.Exists(installerPath))
                {
                    throw new FileNotFoundException("Update file not found");
                }
                
                // For MSI installers
                if (updateInfo.FileName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "msiexec.exe",
                        Arguments = $"/i \"{installerPath}\" /quiet /norestart",
                        UseShellExecute = true,
                        Verb = "runas" // Run as administrator
                    });
                    
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        OnUpdateStatusChanged(UpdateStatus.Completed);
                        return process.ExitCode == 0;
                    }
                }
                // For executable installers
                else if (updateInfo.FileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = installerPath,
                        Arguments = "/SILENT /NORESTART", // Common silent install arguments
                        UseShellExecute = true,
                        Verb = "runas" // Run as administrator
                    });
                    
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        OnUpdateStatusChanged(UpdateStatus.Completed);
                        return process.ExitCode == 0;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                OnUpdateErrorOccurred($"Failed to install update: {ex.Message}");
                OnUpdateStatusChanged(UpdateStatus.Failed);
                return false;
            }
        }

        public void OpenReleasesPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = GitHubReleasesUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                OnUpdateErrorOccurred($"Failed to open releases page: {ex.Message}");
            }
        }

        public void CleanupTempFiles()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch (Exception ex)
            {
                OnUpdateErrorOccurred($"Failed to cleanup temp files: {ex.Message}");
            }
        }

        private bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            try
            {
                var latest = new Version(latestVersion);
                var current = new Version(currentVersion);
                return latest > current;
            }
            catch
            {
                return false;
            }
        }

        private string GetDownloadUrl(GitHubRelease release)
        {
            // Look for Windows installer assets
            var asset = release.Assets?.FirstOrDefault(a => 
                a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            
            return asset?.BrowserDownloadUrl ?? string.Empty;
        }

        private long GetFileSize(GitHubRelease release)
        {
            var asset = release.Assets?.FirstOrDefault(a => 
                a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            
            return asset?.Size ?? 0;
        }

        private string GetFileName(GitHubRelease release)
        {
            var asset = release.Assets?.FirstOrDefault(a => 
                a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            
            return asset?.Name ?? "update.msi";
        }

        private string GetChecksum(GitHubRelease release)
        {
            // Look for checksum file
            var checksumAsset = release.Assets?.FirstOrDefault(a => 
                a.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) ||
                a.Name.EndsWith(".md5", StringComparison.OrdinalIgnoreCase));
            
            return checksumAsset?.Name ?? string.Empty;
        }

        private async Task<string> CalculateFileChecksumAsync(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await Task.Run(() => sha256.ComputeHash(stream));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        protected virtual void OnUpdateStatusChanged(UpdateStatus status)
        {
            UpdateStatusChanged?.Invoke(this, status);
        }

        protected virtual void OnDownloadProgressChanged(long bytesDownloaded)
        {
            DownloadProgressChanged?.Invoke(this, bytesDownloaded);
        }

        protected virtual void OnUpdateErrorOccurred(string error)
        {
            UpdateErrorOccurred?.Invoke(this, error);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // GitHub API response models
    public class GitHubRelease
    {
        public string TagName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public bool Prerelease { get; set; }
        public bool Draft { get; set; }
        public string HtmlUrl { get; set; } = string.Empty;
        public List<GitHubAsset>? Assets { get; set; }
    }

    public class GitHubAsset
    {
        public string Name { get; set; } = string.Empty;
        public string BrowserDownloadUrl { get; set; } = string.Empty;
        public long Size { get; set; }
    }
}
