using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Y0daiiIRC.IRC
{
    public class IRCClient
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isConnected = false;

        public event EventHandler<IRCMessage>? MessageReceived;
        public event EventHandler<string>? ConnectionStatusChanged;
        public event EventHandler<Exception>? ErrorOccurred;

        public bool IsConnected => _isConnected;
        public string? Server { get; private set; }
        public int Port { get; private set; }
        public string? Nickname { get; private set; }
        public string? Username { get; private set; }
        public string? RealName { get; private set; }

        public async Task<bool> ConnectAsync(string server, int port, string nickname, string username, string realName, string? identServer = null, int identPort = 113)
        {
            try
            {
                Server = server;
                Port = port;
                Nickname = nickname;
                Username = username;
                RealName = realName;

                // Start ident server if specified
                if (!string.IsNullOrEmpty(identServer))
                {
                    _ = Task.Run(() => StartIdentServer(identServer, identPort, username));
                }

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(server, port);
                _stream = _tcpClient.GetStream();
                _reader = new StreamReader(_stream, Encoding.UTF8);
                _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };

                _cancellationTokenSource = new CancellationTokenSource();

                // Send initial IRC commands
                await SendCommandAsync($"NICK {nickname}");
                await SendCommandAsync($"USER {username} 0 * :{realName}");

                _isConnected = true;
                OnConnectionStatusChanged("Connected");

                // Start listening for messages
                _ = Task.Run(ListenForMessagesAsync, _cancellationTokenSource.Token);

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_isConnected)
                {
                    await SendCommandAsync("QUIT :Y0daii IRC Client");
                }
            }
            catch { }

            _cancellationTokenSource?.Cancel();
            _reader?.Close();
            _writer?.Close();
            _stream?.Close();
            _tcpClient?.Close();

            _isConnected = false;
            OnConnectionStatusChanged("Disconnected");
        }

        public async Task SendCommandAsync(string command)
        {
            if (_writer != null && _isConnected)
            {
                await _writer.WriteLineAsync(command);
            }
        }

        public async Task JoinChannelAsync(string channel)
        {
            if (!channel.StartsWith("#"))
                channel = "#" + channel;
            
            await SendCommandAsync($"JOIN {channel}");
        }

        public async Task LeaveChannelAsync(string channel)
        {
            await SendCommandAsync($"PART {channel}");
        }

        public async Task SendMessageAsync(string target, string message)
        {
            await SendCommandAsync($"PRIVMSG {target} :{message}");
        }

        public async Task SendNoticeAsync(string target, string message)
        {
            await SendCommandAsync($"NOTICE {target} :{message}");
        }

        private async Task ListenForMessagesAsync()
        {
            try
            {
                while (_isConnected && !_cancellationTokenSource!.Token.IsCancellationRequested)
                {
                    var line = await _reader!.ReadLineAsync();
                    if (line == null) break;

                    var message = ParseIRCMessage(line);
                    if (message != null)
                    {
                        OnMessageReceived(message);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_cancellationTokenSource!.Token.IsCancellationRequested)
                {
                    OnErrorOccurred(ex);
                }
            }
        }

        private IRCMessage? ParseIRCMessage(string line)
        {
            try
            {
                var message = new IRCMessage();
                var parts = line.Split(' ');

                if (line.StartsWith(":"))
                {
                    var prefixEnd = line.IndexOf(' ');
                    if (prefixEnd > 0)
                    {
                        message.Prefix = line.Substring(1, prefixEnd - 1);
                        line = line.Substring(prefixEnd + 1);
                        parts = line.Split(' ');
                    }
                }

                if (parts.Length > 0)
                {
                    message.Command = parts[0];
                    message.Parameters = new List<string>();

                    var colonIndex = line.IndexOf(" :");
                    if (colonIndex > 0)
                    {
                        var beforeColon = line.Substring(0, colonIndex);
                        var afterColon = line.Substring(colonIndex + 2);
                        
                        var beforeParts = beforeColon.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 1; i < beforeParts.Length; i++)
                        {
                            message.Parameters.Add(beforeParts[i]);
                        }
                        message.Parameters.Add(afterColon);
                    }
                    else
                    {
                        for (int i = 1; i < parts.Length; i++)
                        {
                            message.Parameters.Add(parts[i]);
                        }
                    }
                }

                return message;
            }
            catch
            {
                return null;
            }
        }

        protected virtual void OnMessageReceived(IRCMessage message)
        {
            MessageReceived?.Invoke(this, message);
        }

        protected virtual void OnConnectionStatusChanged(string status)
        {
            ConnectionStatusChanged?.Invoke(this, status);
        }

        private async Task StartIdentServer(string identServer, int identPort, string username)
        {
            try
            {
                var listener = new TcpListener(System.Net.IPAddress.Parse(identServer), identPort);
                listener.Start();

                while (_isConnected)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleIdentRequest(client, username));
                }

                listener.Stop();
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
        }

        private async Task HandleIdentRequest(TcpClient client, string username)
        {
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream);
                using var writer = new StreamWriter(stream) { AutoFlush = true };

                var request = await reader.ReadLineAsync();
                if (request != null)
                {
                    var parts = request.Split(',');
                    if (parts.Length >= 2)
                    {
                        var response = $"{parts[0]}, {parts[1]} : USERID : UNIX : {username}";
                        await writer.WriteLineAsync(response);
                    }
                }
            }
            catch
            {
                // Ignore ident request errors
            }
            finally
            {
                client.Close();
            }
        }

        protected virtual void OnErrorOccurred(Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
    }
}
