using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Y0daiiIRC.Utils
{
    public static class VersionInfo
    {
        private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();
        private static readonly Lazy<FileVersionInfo?> _fileVersionInfo = new(() => GetFileVersionInfo());
        
        public static string Version => GetFileVersion() ?? _assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        public static string ProductName => GetAssemblyAttribute<AssemblyProductAttribute>()?.Product ?? "y0daii IRC Client";
        public static string Company => GetAssemblyAttribute<AssemblyCompanyAttribute>()?.Company ?? "Y0daii";
        public static string Copyright => GetAssemblyAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "Copyright © 2025 Y0daii. All rights reserved.";
        public static string Description => GetAssemblyAttribute<AssemblyDescriptionAttribute>()?.Description ?? "Modern IRC Client with Beautiful UX";
        public static string Title => GetAssemblyAttribute<AssemblyTitleAttribute>()?.Title ?? "y0daii IRC Client";
        
        /// <summary>
        /// Gets the build date from file creation time or assembly version
        /// </summary>
        public static DateTime BuildDate
        {
            get
            {
                try
                {
                    // Try to get build date from file creation time (most accurate)
                    var fileInfo = new FileInfo(_assembly.Location);
                    if (fileInfo.Exists)
                    {
                        return fileInfo.CreationTimeUtc.ToLocalTime();
                    }
                }
                catch
                {
                    // Fall back to assembly version calculation
                }
                
                // Fallback to .NET build date calculation from version
                var version = _assembly.GetName().Version;
                if (version != null && version.Build > 0)
                {
                    return new DateTime(2000, 1, 1).AddDays(version.Build).AddSeconds(version.Revision * 2);
                }
                
                return DateTime.Now;
            }
        }
        
        /// <summary>
        /// Gets the full version string with build date
        /// </summary>
        public static string BuildInfo => $"Build {Version} ({BuildDate:yyyy-MM-dd HH:mm})";
        
        /// <summary>
        /// Gets the short version string
        /// </summary>
        public static string ShortVersion => Version;
        
        /// <summary>
        /// Gets the full product information
        /// </summary>
        public static string FullVersionInfo => $"{ProductName} {Version}";
        
        /// <summary>
        /// Gets the detailed version information for about dialog
        /// </summary>
        public static string DetailedVersionInfo => $"{ProductName} v{Version}\nBuild {BuildDate:yyyy-MM-dd HH:mm}";
        
        /// <summary>
        /// Gets the file version from assembly metadata
        /// </summary>
        private static string? GetFileVersion()
        {
            try
            {
                return _fileVersionInfo.Value?.FileVersion;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Gets the file version info safely
        /// </summary>
        private static FileVersionInfo? GetFileVersionInfo()
        {
            try
            {
                return FileVersionInfo.GetVersionInfo(_assembly.Location);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Gets assembly attribute safely
        /// </summary>
        private static T? GetAssemblyAttribute<T>() where T : Attribute
        {
            try
            {
                return _assembly.GetCustomAttribute<T>();
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Gets system information for debugging
        /// </summary>
        public static string GetSystemInfo()
        {
            return $"""
                Operating System: {Environment.OSVersion}
                .NET Version: {Environment.Version}
                Machine Name: {Environment.MachineName}
                User Name: {Environment.UserName}
                Assembly Location: {_assembly.Location}
                """;
        }
        
        /// <summary>
        /// Gets all version information as a formatted string
        /// </summary>
        public static string GetAllVersionInfo()
        {
            return $"""
                {ProductName}
                Version: {Version}
                Build Date: {BuildDate:yyyy-MM-dd HH:mm:ss}
                Company: {Company}
                Copyright: {Copyright}
                Description: {Description}
                """;
        }
    }
}
