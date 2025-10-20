using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Y0daiiIRC.Models;

namespace Y0daiiIRC.IRC
{
    public class IRCClient
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private SslStream? _sslStream;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isConnected = false;
        private bool _useSSL = false;

        public event EventHandler<IRCMessage>? MessageReceived;
        public event EventHandler<string>? ConnectionStatusChanged;
        public event EventHandler<Exception>? ErrorOccurred;
        public event EventHandler<CTCPRequest>? CTCPRequestReceived;
        public event EventHandler<DCCRequest>? DCCRequestReceived;
        public event EventHandler<string>? CommandSent;

        public bool IsConnected => _isConnected;

        public void SetConnected(bool connected)
        {
            _isConnected = connected;
            if (connected)
            {
                OnConnectionStatusChanged("Connected");
            }
        }
        public string? Server { get; private set; }
        public int Port { get; private set; }
        public string? Nickname { get; private set; }
        public string? Username { get; private set; }
        public string? RealName { get; private set; }

        public async Task<bool> ConnectAsync(string server, int port, string nickname, string username, string realName, bool useSSL = false, string? password = null, string? identServer = null, int identPort = 113)
        {
            try
            {
                Server = server;
                Port = port;
                Nickname = nickname;
                Username = username;
                RealName = realName;
                _useSSL = useSSL;

                // Create cancellation token source early
                _cancellationTokenSource = new CancellationTokenSource();
                Console.WriteLine($"Starting connection to {server}:{port}...");

                // Start ident server if specified
                if (!string.IsNullOrEmpty(identServer))
                {
                    Console.WriteLine($"Starting ident server on {identServer}:{identPort}...");
                    _ = Task.Run(() => StartIdentServer(identServer, identPort, username));
                }

                _tcpClient = new TcpClient();
                
                // Add connection timeout
                Console.WriteLine($"Attempting TCP connection to {server}:{port}...");
                var connectTask = _tcpClient.ConnectAsync(server, port);
                var timeoutTask = Task.Delay(10000, _cancellationTokenSource.Token); // 10 second timeout
                
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    Console.WriteLine($"Connection to {server}:{port} timed out after 10 seconds");
                    throw new TimeoutException($"Connection to {server}:{port} timed out after 10 seconds");
                }
                
                await connectTask; // Ensure any exceptions from connect are propagated
                Console.WriteLine($"TCP connection established to {server}:{port}");
                _stream = _tcpClient.GetStream();

                if (useSSL)
                {
                    Console.WriteLine($"Starting SSL handshake with {server}...");
                    _sslStream = new SslStream(_stream, false, ValidateServerCertificate);
                    await _sslStream.AuthenticateAsClientAsync(server);
                    Console.WriteLine($"SSL handshake completed with {server}");
                    _reader = new StreamReader(_sslStream, Encoding.UTF8);
                    _writer = new StreamWriter(_sslStream, Encoding.UTF8) { AutoFlush = true };
                }
                else
                {
                    _reader = new StreamReader(_stream, Encoding.UTF8);
                    _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
                }

                // Send initial IRC commands
                Console.WriteLine($"Sending initial IRC commands...");
                if (!string.IsNullOrEmpty(password))
                {
                    await SendCommandAsync($"PASS {password}");
                }
                await SendCommandAsync($"NICK {nickname}");
                await SendCommandAsync($"USER {username} 0 * :{realName}");
                Console.WriteLine($"Initial IRC commands sent, waiting for server response...");

                // Don't set connected yet - wait for welcome message (001)
                OnConnectionStatusChanged("Connecting");

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
            _sslStream?.Close();
            _stream?.Close();
            _tcpClient?.Close();

            _isConnected = false;
            OnConnectionStatusChanged("Disconnected");
        }

        public async Task SendCommandAsync(string command)
        {
            if (_writer != null)
            {
                await _writer.WriteLineAsync(command);
                Console.WriteLine($"Sending IRC command: {command}");
                CommandSent?.Invoke(this, command);
            }
            else
            {
                Console.WriteLine($"Cannot send command - writer is null: {command}");
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
                while (!_cancellationTokenSource!.Token.IsCancellationRequested)
                {
                    var line = await _reader!.ReadLineAsync();
                    if (line == null) break;

                    // Debug: Log raw IRC messages to console
                    Console.WriteLine($"IRC Raw: {line}");
                    
                    var message = ParseIRCMessage(line);
                    if (message != null)
                    {
                        // Debug: Log parsed messages
                        Console.WriteLine($"IRC Parsed: {message.Command} | {string.Join(" ", message.Parameters)}");
                        // Check for CTCP messages
                        if (message.Command == "PRIVMSG" && message.Parameters.Count >= 2)
                        {
                            var target = message.Parameters[0];
                            var msgText = message.Parameters[1];
                            
                            // Check if this is a CTCP message
                            if (msgText.StartsWith("\u0001") && msgText.EndsWith("\u0001"))
                            {
                                var sender = ExtractNicknameFromPrefix(message.Prefix);
                                HandleCTCPMessage(sender, target, msgText);
                            }
                            // Check if this is a DCC message
                            else if (msgText.StartsWith("DCC "))
                            {
                                var sender = ExtractNicknameFromPrefix(message.Prefix);
                                HandleDCCMessage(sender, target, msgText);
                            }
                        }
                        else if (message.Command == "NOTICE" && message.Parameters.Count >= 2)
                        {
                            var target = message.Parameters[0];
                            var msgText = message.Parameters[1];
                            
                            // Check if this is a CTCP response
                            if (msgText.StartsWith("\u0001") && msgText.EndsWith("\u0001"))
                            {
                                var sender = ExtractNicknameFromPrefix(message.Prefix);
                                HandleCTCPResponse(sender, target, msgText);
                            }
                        }
                        
                        OnMessageReceived(message);
                    }
                    else
                    {
                        // Debug: Log failed parsing
                        Console.WriteLine($"IRC Parse Failed: {line}");
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

                while (!_cancellationTokenSource!.Token.IsCancellationRequested)
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

        // Helper Methods
        private string ExtractNicknameFromPrefix(string? prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return "";
            
            var exclamationIndex = prefix.IndexOf('!');
            return exclamationIndex > 0 ? prefix.Substring(0, exclamationIndex) : prefix;
        }

        private void HandleCTCPResponse(string sender, string target, string message)
        {
            if (!message.StartsWith("\u0001") || !message.EndsWith("\u0001"))
                return;

            var ctcpContent = message.Substring(1, message.Length - 2);
            var parts = ctcpContent.Split(' ', 2);
            var command = parts[0].ToUpper();
            var response = parts.Length > 1 ? parts[1] : "";

            // Handle CTCP response - no need to create fake messages
            // CTCP responses are handled by the main message processing
        }

        private void HandleDCCMessage(string sender, string target, string message)
        {
            if (!message.StartsWith("DCC "))
                return;

            var dccContent = message.Substring(4); // Remove "DCC "
            var parts = dccContent.Split(' ');
            
            if (parts.Length < 4)
                return;

            var type = parts[0].ToUpper();
            var fileName = parts[1];
            
            if (!long.TryParse(parts[2], out long fileSize))
                return;
                
            if (!int.TryParse(parts[3], out int port))
                return;

            // Parse IP address (can be in different formats)
            string ipAddress = "";
            if (parts.Length > 4)
            {
                // IP address might be in dotted decimal or as a single number
                if (int.TryParse(parts[4], out int ipNumber))
                {
                    // Convert integer IP to dotted decimal
                    ipAddress = $"{ipNumber >> 24 & 0xFF}.{ipNumber >> 16 & 0xFF}.{ipNumber >> 8 & 0xFF}.{ipNumber & 0xFF}";
                }
                else
                {
                    ipAddress = parts[4];
                }
            }

            var dccType = type switch
            {
                "SEND" => DCCRequestType.Send,
                "CHAT" => DCCRequestType.Chat,
                "RESUME" => DCCRequestType.Resume,
                _ => DCCRequestType.Send
            };

            var dccRequest = new DCCRequest(sender, target, dccType, fileName, fileSize, ipAddress, port);
            DCCRequestReceived?.Invoke(this, dccRequest);
        }

        // CTCP Methods
        public async Task SendCTCPAsync(string target, string command, string? parameter = null)
        {
            if (!_isConnected || _writer == null) return;

            var message = parameter != null ? $"{command} {parameter}" : command;
            var ctcpMessage = $"\u0001{message}\u0001";
            await _writer.WriteLineAsync($"PRIVMSG {target} :{ctcpMessage}");
            await _writer.FlushAsync();
        }

        public async Task SendCTCPResponseAsync(string target, string command, string response)
        {
            if (!_isConnected || _writer == null) return;

            var ctcpResponse = $"\u0001{command} {response}\u0001";
            await _writer.WriteLineAsync($"NOTICE {target} :{ctcpResponse}");
            await _writer.FlushAsync();
        }

        public async Task SendDCCOfferAsync(string target, string fileName, long fileSize, string ipAddress, int port)
        {
            if (!_isConnected || _writer == null) return;

            // Convert IP address to integer format for DCC
            var ipParts = ipAddress.Split('.');
            if (ipParts.Length == 4 && 
                int.TryParse(ipParts[0], out int a) && 
                int.TryParse(ipParts[1], out int b) && 
                int.TryParse(ipParts[2], out int c) && 
                int.TryParse(ipParts[3], out int d))
            {
                var ipNumber = (a << 24) | (b << 16) | (c << 8) | d;
                var dccMessage = $"DCC SEND {fileName} {fileSize} {port} {ipNumber}";
                await _writer.WriteLineAsync($"PRIVMSG {target} :{dccMessage}");
                await _writer.FlushAsync();
            }
        }

        private void HandleCTCPMessage(string sender, string target, string message)
        {
            if (!message.StartsWith("\u0001") || !message.EndsWith("\u0001"))
                return;

            var ctcpContent = message.Substring(1, message.Length - 2);
            var parts = ctcpContent.Split(' ', 2);
            var command = parts[0].ToUpper();
            var parameter = parts.Length > 1 ? parts[1] : null;

            var ctcpRequest = new CTCPRequest(sender, target, command, parameter);
            CTCPRequestReceived?.Invoke(this, ctcpRequest);

            // Handle automatic responses
            if (target.Equals(Nickname, StringComparison.OrdinalIgnoreCase))
            {
                HandleAutomaticCTCPResponse(sender, command, parameter);
            }
        }

        private async void HandleAutomaticCTCPResponse(string sender, string command, string? parameter)
        {
            switch (command)
            {
                case "VERSION":
                    var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                    var versionString = version != null ? version.ToString() : "1.0.0";
                    var response = $"y0daii - Soon you will be with him - {versionString}";
                    await SendCTCPResponseAsync(sender, "VERSION", response);
                    break;

                case "PING":
                    if (!string.IsNullOrEmpty(parameter))
                    {
                        await SendCTCPResponseAsync(sender, "PING", parameter);
                    }
                    break;

                case "TIME":
                    var time = DateTime.Now.ToString("ddd MMM dd HH:mm:ss yyyy");
                    await SendCTCPResponseAsync(sender, "TIME", time);
                    break;

                case "FINGER":
                    var fingerInfo = $"{RealName} ({Username}@{Server})";
                    await SendCTCPResponseAsync(sender, "FINGER", fingerInfo);
                    break;
            }
        }

        protected virtual void OnErrorOccurred(Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }

        private bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            // For IRC, we'll accept all certificates for now
            // In a production environment, you might want to implement proper certificate validation
            return true;
        }
    }
}
