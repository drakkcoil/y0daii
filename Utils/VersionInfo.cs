using System;
using System.Reflection;

namespace Y0daiiIRC.Utils
{
    public static class VersionInfo
    {
        private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();
        
        public static string Version => GetFileVersion() ?? _assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        public static string ProductName => GetAssemblyAttribute<AssemblyProductAttribute>()?.Product ?? "y0daii IRC Client";
        public static string Company => GetAssemblyAttribute<AssemblyCompanyAttribute>()?.Company ?? "Y0daii";
        public static string Copyright => GetAssemblyAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "Copyright © 2025 Y0daii. All rights reserved.";
        public static string Description => GetAssemblyAttribute<AssemblyDescriptionAttribute>()?.Description ?? "Modern IRC Client with Beautiful UX";
        public static string Title => GetAssemblyAttribute<AssemblyTitleAttribute>()?.Title ?? "y0daii IRC Client";
        
        public static DateTime BuildDate
        {
            get
            {
                var version = _assembly.GetName().Version;
                if (version != null)
                {
                    // .NET build date calculation from version
                    return new DateTime(2000, 1, 1).AddDays(version.Build).AddSeconds(version.Revision * 2);
                }
                return DateTime.Now;
            }
        }
        
        public static string BuildInfo => $"Build {Version} ({BuildDate:yyyy-MM-dd})";
        public static string FullVersionInfo => $"{ProductName} {Version}";
        
        private static string? GetFileVersion()
        {
            try
            {
                var fileVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(_assembly.Location);
                return fileVersionInfo.FileVersion;
            }
            catch
            {
                return null;
            }
        }
        
        private static T? GetAssemblyAttribute<T>() where T : Attribute
        {
            return _assembly.GetCustomAttribute<T>();
        }
        
        public static string GetSystemInfo()
        {
            return $"""
                Operating System: {Environment.OSVersion}
                .NET Version: {Environment.Version}
                Machine Name: {Environment.MachineName}
                User Name: {Environment.UserName}
                """;
        }
    }
}
