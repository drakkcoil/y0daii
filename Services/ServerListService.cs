using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Y0daiiIRC.Models;

namespace Y0daiiIRC.Services
{
    public class ServerListService
    {
        private static readonly string ServerListPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Y0daiiIRC", "servers.json");

        private List<ServerInfo> _servers = new();

        public event EventHandler<ServerInfo>? ServerAdded;
        public event EventHandler<ServerInfo>? ServerRemoved;
        public event EventHandler<ServerInfo>? ServerUpdated;

        public ServerListService()
        {
            LoadServers();
        }

        public List<ServerInfo> GetServers()
        {
            return _servers.ToList();
        }

        public List<ServerInfo> GetFavoriteServers()
        {
            return _servers.Where(s => s.IsFavorite).ToList();
        }

        public ServerInfo? GetServer(string name)
        {
            return _servers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public void AddServer(ServerInfo server)
        {
            if (string.IsNullOrEmpty(server.Name))
            {
                server.Name = $"{server.Host}:{server.Port}";
            }

            var existing = _servers.FirstOrDefault(s => s.Name.Equals(server.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                UpdateServer(server);
                return;
            }

            _servers.Add(server);
            SaveServers();
            ServerAdded?.Invoke(this, server);
        }

        public void UpdateServer(ServerInfo server)
        {
            var existing = _servers.FirstOrDefault(s => s.Name.Equals(server.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                var index = _servers.IndexOf(existing);
                _servers[index] = server;
                SaveServers();
                ServerUpdated?.Invoke(this, server);
            }
        }

        public void RemoveServer(string name)
        {
            var server = _servers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (server != null)
            {
                _servers.Remove(server);
                SaveServers();
                ServerRemoved?.Invoke(this, server);
            }
        }

        public void UpdateConnectionStats(string name)
        {
            var server = _servers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (server != null)
            {
                server.LastConnected = DateTime.Now;
                server.ConnectionCount++;
                SaveServers();
            }
        }

        public void SetFavorite(string name, bool isFavorite)
        {
            var server = _servers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (server != null)
            {
                server.IsFavorite = isFavorite;
                SaveServers();
            }
        }

        private void LoadServers()
        {
            try
            {
                if (File.Exists(ServerListPath))
                {
                    var json = File.ReadAllText(ServerListPath);
                    _servers = JsonConvert.DeserializeObject<List<ServerInfo>>(json) ?? new List<ServerInfo>();
                }
                else
                {
                    // Add some default servers
                    _servers = GetDefaultServers();
                    SaveServers();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load server list: {ex.Message}");
                _servers = GetDefaultServers();
            }
        }

        private void SaveServers()
        {
            try
            {
                var directory = Path.GetDirectoryName(ServerListPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(_servers, Formatting.Indented);
                File.WriteAllText(ServerListPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save server list: {ex.Message}");
            }
        }

        private List<ServerInfo> GetDefaultServers()
        {
            return new List<ServerInfo>
            {
                new ServerInfo
                {
                    Name = "Libera Chat",
                    Host = "irc.libera.chat",
                    Port = 6667,
                    UseSSL = false,
                    Nickname = "Y0daiiUser",
                    Username = "y0daii",
                    RealName = "y0daii IRC User",
                    IsFavorite = true
                },
                new ServerInfo
                {
                    Name = "Libera Chat (SSL)",
                    Host = "irc.libera.chat",
                    Port = 6697,
                    UseSSL = true,
                    Nickname = "Y0daiiUser",
                    Username = "y0daii",
                    RealName = "y0daii IRC User",
                    IsFavorite = true
                },
                new ServerInfo
                {
                    Name = "IRCNet (US)",
                    Host = "irc1.us.open-ircnet.net",
                    Port = 6667,
                    UseSSL = false,
                    Nickname = "Y0daiiUser",
                    Username = "y0daii",
                    RealName = "y0daii IRC User",
                    IsFavorite = true
                },
                new ServerInfo
                {
                    Name = "IRCNet (EU)",
                    Host = "ircnet.choopa.net",
                    Port = 6667,
                    UseSSL = false,
                    Nickname = "Y0daiiUser",
                    Username = "y0daii",
                    RealName = "Y0daii IRC User"
                },
                new ServerInfo
                {
                    Name = "QuakeNet",
                    Host = "irc.quakenet.org",
                    Port = 6667,
                    UseSSL = false,
                    Nickname = "Y0daiiUser",
                    Username = "y0daii",
                    RealName = "Y0daii IRC User"
                },
                new ServerInfo
                {
                    Name = "Undernet",
                    Host = "us.undernet.org",
                    Port = 6667,
                    UseSSL = false,
                    Nickname = "Y0daiiUser",
                    Username = "y0daii",
                    RealName = "Y0daii IRC User"
                }
            };
        }
    }
}
