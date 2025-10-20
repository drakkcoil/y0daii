using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
            // ...existing code...
            _isConnected = connected;
            OnConnectionStatusChanged(connected ? "Connected" : "Disconnected");
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

                // Create the cancellation token source early so background tasks can observe it.
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                OnConnectionStatusChanged("Connecting");

                // Start ident server (if requested) but make it tolerant to bind failures.
                if (!string.IsNullOrEmpty(identServer))
                {
                    // Fire-and-forget but swallow exceptions inside StartIdentServer.
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await StartIdentServer(identServer, identPort, username, _cancellationTokenSource.Token).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // Log/wrap but don't let ident failure kill connect flow.
                            OnErrorOccurred(ex);
                        }
                    }, _cancellationTokenSource.Token);
                }

                // Create TCP client and attempt connection with timeout.
                _tcpClient = new TcpClient();

                var connectCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
                var connectTimeout = TimeSpan.FromSeconds(30);
                connectCts.CancelAfter(connectTimeout);

                Task connectTask = _tcpClient.ConnectAsync(server, port);

                var completed = await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, connectCts.Token)).ConfigureAwait(false);
                if (completed != connectTask)
                {
                    throw new TimeoutException($"Connection to {server}:{port} timed out after {connectTimeout.TotalSeconds} seconds.");
                }

                // Ensure any exception from ConnectAsync is observed
                await connectTask.ConfigureAwait(false);

                _stream = _tcpClient.GetStream();

                if (useSSL)
                {
                    _sslStream = new SslStream(_stream, false, ValidateServerCertificate);
                    // authenticate with a timeout
                    var authTask = _sslStream.AuthenticateAsClientAsync(server);
                    var authTimeout = Task.Delay(TimeSpan.FromSeconds(30), _cancellationTokenSource.Token);
                    var authCompleted = await Task.WhenAny(authTask, authTimeout).ConfigureAwait(false);
                    if (authCompleted != authTask)
                    {
                        throw new TimeoutException("SSL authentication timed out.");
                    }
                    await authTask.ConfigureAwait(false);

                    _reader = new StreamReader(_sslStream, Encoding.UTF8);
                    _writer = new StreamWriter(_sslStream, Encoding.UTF8) { AutoFlush = true };
                }
                else
                {
                    _reader = new StreamReader(_stream, Encoding.UTF8);
                    _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
                }

                // Send initial commands
                if (!string.IsNullOrEmpty(password))
                {
                    await SendCommandAsync($"PASS {password}").ConfigureAwait(false);
                }

                await SendCommandAsync($"NICK {nickname}").ConfigureAwait(false);
                await SendCommandAsync($"USER {username} 0 * :{realName}").ConfigureAwait(false);

                // Start listening for server messages using the cancellation token
                _ = Task.Run(() => ListenForMessagesAsync(), _cancellationTokenSource.Token);

                // mark connected; you might want to wait for RPL_WELCOME (001) instead
                SetConnected(true);

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                // Clean up partial resources on failure
                try { await DisconnectAsync().ConfigureAwait(false); } catch { }
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                // Cancel background operations
                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();
                }

                // Send QUIT command if connected
                if (_isConnected && _writer != null)
                {
                    try
                    {
                        await SendCommandAsync("QUIT :Y0daii IRC Client").ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred(ex);
                    }
                }

                // Close reader/writer/streams
                try { _reader?.Dispose(); } catch { }
                try { _writer?.Dispose(); } catch { }
                try { _sslStream?.Dispose(); } catch { }
                try { _stream?.Dispose(); } catch { }
                try { _tcpClient?.Close(); } catch { }

                _reader = null;
                _writer = null;
                _sslStream = null;
                _stream = null;
                _tcpClient = null;

                SetConnected(false);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
            finally
            {
                try { _cancellationTokenSource?.Dispose(); } catch { }
                _cancellationTokenSource = null;
            }
        }

        public async Task SendCommandAsync(string command)
        {
            if (_writer == null || !_isConnected)
            {
                OnErrorOccurred(new InvalidOperationException("Not connected or writer not initialized when sending command."));
                return;
            }

            try
            {
                // Ensure CRLF terminated as per IRC spec
                await _writer.WriteLineAsync(command).ConfigureAwait(false);
                // Raise command sent event (non-blocking)
                try { CommandSent?.Invoke(this, command); } catch { }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
        }

        public async Task JoinChannelAsync(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
                return;

            if (!channel.StartsWith("#") && !channel.StartsWith("&"))
            {
                // Normalize: prefix with #
                channel = "#" + channel;
            }

            await SendCommandAsync($"JOIN {channel}").ConfigureAwait(false);
        }

        public async Task LeaveChannelAsync(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
                return;

            await SendCommandAsync($"PART {channel}"). ConfigureAwait(false);
        }

        public async Task SendMessageAsync(string target, string message)
        {
            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(message))
                return;

            await SendCommandAsync($"PRIVMSG {target} :{message}").ConfigureAwait(false);
        }

        public async Task SendNoticeAsync(string target, string message)
        {
            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(message))
                return;

            await SendCommandAsync($"NOTICE {target} :{message}").ConfigureAwait(false);
        }

        private async Task ListenForMessagesAsync()
        {
            if (_reader == null)
                return;

            var token = _cancellationTokenSource?.Token ?? CancellationToken.None;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    Task<string?> readTask = _reader.ReadLineAsync();
                    var completed = await Task.WhenAny(readTask, Task.Delay(1000, token)).ConfigureAwait(false);
                    if (completed != readTask)
                    {
                        // check cancellation and loop
                        continue;
                    }

                    string? line = await readTask.ConfigureAwait(false);
                    if (line == null)
                    {
                        // remote closed
                        break;
                    }

                    // Handle PING promptly
                    if (line.StartsWith("PING ", StringComparison.OrdinalIgnoreCase))
                    {
                        var pongPayload = line.Substring(5);
                        await SendCommandAsync($"PONG {pongPayload}").ConfigureAwait(false);
                        continue;
                    }

                    // Parse and raise message event where possible
                    try
                    {
                        var message = ParseIRCMessage(line);
                        if (message != null)
                        {
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
                            
                            try { MessageReceived?.Invoke(this, message); } catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        // ensure parsing errors don't kill the loop
                        OnErrorOccurred(ex);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException ex)
            {
                // network errors: notify and disconnect
                OnErrorOccurred(ex);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
            finally
            {
                // ensure disconnected state
                SetConnected(false);
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
            // ...existing code...
            try { MessageReceived?.Invoke(this, message); } catch { }
        }

        protected virtual void OnConnectionStatusChanged(string status)
        {
            try { ConnectionStatusChanged?.Invoke(this, status); } catch { }
        }

        protected virtual void OnErrorOccurred(Exception ex)
        {
            try { ErrorOccurred?.Invoke(this, ex); } catch { }
        }

        private async Task StartIdentServer(string identListenAddress, int identPort, string username, CancellationToken token)
        {
            TcpListener? listener = null;
            try
            {
                IPAddress listenIp;
                if (string.IsNullOrWhiteSpace(identListenAddress) || identListenAddress.Equals("any", StringComparison.OrdinalIgnoreCase))
                {
                    listenIp = IPAddress.Any;
                }
                else if (!IPAddress.TryParse(identListenAddress, out IPAddress? parsedIp) || parsedIp == null)
                {
                    // If a hostname was supplied, fallback to Any (binding to a hostname is non-trivial).
                    listenIp = IPAddress.Any;
                }
                else
                {
                    listenIp = parsedIp;
                }

                listener = new TcpListener(listenIp, identPort);

                try
                {
                    listener.Start();
                }
                catch (Exception ex)
                {
                    // Binding failed (permission / port in use) — log and return; ident is non-fatal.
                    OnErrorOccurred(new Exception($"Ident server failed to bind on {listenIp}:{identPort}: {ex.Message}", ex));
                    return;
                }

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var acceptTask = listener.AcceptTcpClientAsync();
                        var completed = await Task.WhenAny(acceptTask, Task.Delay(1000, token)).ConfigureAwait(false);
                        if (completed != acceptTask)
                            continue;

                        var client = acceptTask.Result;
                        // Fire-and-forget to handle ident request
                        _ = Task.Run(() => HandleIdentRequest(client, username), token);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        // Non-fatal loop error — log and continue
                        OnErrorOccurred(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
            finally
            {
                try { listener?.Stop(); } catch { }
            }
        }

        private async Task HandleIdentRequest(TcpClient client, string username)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    stream.ReadTimeout = 5000;
                    stream.WriteTimeout = 5000;

                    using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                    using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, true) { NewLine = "\r\n", AutoFlush = true };

                    // Read one line (like "portA , portB")
                    var readTask = reader.ReadLineAsync();
                    var completed = await Task.WhenAny(readTask, Task.Delay(5000)).ConfigureAwait(false);
                    if (completed != readTask)
                        return;

                    var request = readTask.Result;
                    if (string.IsNullOrWhiteSpace(request))
                        return;

                    var parts = request.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                    {
                        await writer.WriteLineAsync($"{request} : ERROR : INVALID-REQUEST").ConfigureAwait(false);
                        return;
                    }

                    if (!int.TryParse(parts[0], out int portA) || !int.TryParse(parts[1], out int portB))
                    {
                        await writer.WriteLineAsync($"{request} : ERROR : INVALID-PORT").ConfigureAwait(false);
                        return;
                    }

                    // Send USERID response. Use UNIX as OS token to be accepted by most servers.
                    var response = $"{portA} , {portB} : USERID : UNIX : {username}";
                    await writer.WriteLineAsync(response).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    OnErrorOccurred(ex);
                }
                finally
                {
                    try { client.Close(); } catch { }
                }
            }
        }

        // Helper Methods
        private string ExtractNicknameFromPrefix(string? prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return string.Empty;

            var exIdx = prefix.IndexOf('!');
            if (exIdx > 0)
                return prefix.Substring(0, exIdx);
            return prefix;
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
            if (!_isConnected || _writer == null)
            {
                OnErrorOccurred(new InvalidOperationException("Cannot send CTCP: not connected."));
                return;
            }

            var payload = string.IsNullOrEmpty(parameter) ? $"\u0001{command}\u0001" : $"\u0001{command} {parameter}\u0001";
            await SendMessageAsync(target, payload).ConfigureAwait(false);
        }

        public async Task SendCTCPResponseAsync(string target, string command, string response)
        {
            if (!_isConnected || _writer == null) return;

            var ctcpResponse = $"\u0001{command} {response}\u0001";
            await SendCommandAsync($"NOTICE {target} :{ctcpResponse}").ConfigureAwait(false);
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
                await SendCommandAsync($"PRIVMSG {target} :{dccMessage}").ConfigureAwait(false);
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

        // Minimal SSL certificate validator - returns true only if there are no SSL policy errors.
        private bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            return sslPolicyErrors == SslPolicyErrors.None;
        }
    }
}
