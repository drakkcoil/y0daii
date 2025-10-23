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
    public class IRCClient : IDisposable
    {
        private TcpClient? _tcpClient;
        private Stream? _networkStream;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private SslStream? _sslStream;
        private bool _isConnected;
        private bool _useSSL;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly SemaphoreSlim _streamLock = new(1, 1);
        private readonly SemaphoreSlim _connectionLock = new(1, 1);

        // Events
        public event EventHandler<IRCMessage>? MessageReceived;
        public event EventHandler<string>? ConnectionStatusChanged;
        public event EventHandler<Exception>? ErrorOccurred;
        public event EventHandler<CTCPRequest>? CTCPRequestReceived;
        public event EventHandler<DCCRequest>? DCCRequestReceived;
        public event EventHandler<string>? CommandSent;

        public bool IsConnected => _isConnected;

        // Iridium-inspired logging methods
        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [INFO] {message}");
        }

        private void LogDebug(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [DEBUG] {message}");
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [ERROR] {message}");
        }

        private void LogWarn(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [WARN] {message}");
        }

        // Properties
        public string? Server { get; private set; }
        public int Port { get; private set; }
        public string? Nickname { get; private set; }
        
        public void UpdateNickname(string newNickname)
        {
            Nickname = newNickname;
        }
        public string? Username { get; private set; }
        public string? RealName { get; private set; }

        // Iridium-inspired connection methods
        private async Task StartIdentServerBeforeConnection(string username)
        {
            LogInfo("🔧 Starting ident server before TCP connection (Iridium approach)");
            
            // Start ident server and wait for it to be ready
            try
            {
                // Try port 113 first (standard ident port), then 1130
                try
                {
                    LogInfo("🔧 Attempting to start ident server on port 113 (standard ident port)");
                    // Start ident server in background but wait for it to be ready
                    // Use IPAddress.Any (0.0.0.0) so EFnet can contact us from external IP
                    var identTask = Task.Run(() => StartIdentServer("0.0.0.0", 113, username, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
                    
                    // Give it time to start up
                    await Task.Delay(1000).ConfigureAwait(false);
                    LogInfo("✅ Ident server on port 113 should be ready");
                    
                    // Test ident server connectivity
                    await TestIdentServerConnectivityAsync("127.0.0.1", 113).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                    LogWarn($"Port 113 failed (likely requires admin privileges), trying 1130: {ex.Message}");
                    // Start ident server on port 1130
                    // Use IPAddress.Any (0.0.0.0) so EFnet can contact us from external IP
                    var identTask = Task.Run(() => StartIdentServer("0.0.0.0", 1130, username, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
                    
                    // Give it time to start up
                    await Task.Delay(1000).ConfigureAwait(false);
                    LogInfo("✅ Ident server on port 1130 should be ready");
                    
                    // Test ident server connectivity
                    await TestIdentServerConnectivityAsync("127.0.0.1", 1130).ConfigureAwait(false);
                        }
            }
            catch (Exception ex)
            {
                LogError($"Ident server failed on both ports: {ex.Message}");
                throw;
            }
        }

        private async Task TestIdentServerConnectivityAsync(string host, int port)
        {
            try
            {
                LogInfo($"🧪 Testing ident server connectivity on {host}:{port}...");
                
                using var client = new TcpClient();
                await client.ConnectAsync(host, port).ConfigureAwait(false);
                
                var stream = client.GetStream();
                using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.ASCII);
                
                // Send a test ident request
                await writer.WriteLineAsync("6667, 113").ConfigureAwait(false);
                
                // Read response with timeout
                var readTask = reader.ReadLineAsync();
                var completed = await Task.WhenAny(readTask, Task.Delay(2000)).ConfigureAwait(false);
                
                if (completed == readTask)
                {
                    var response = readTask.Result;
                    LogInfo($"✅ Ident server test successful: {response}");
                }
                else
                {
                    LogWarn("⚠️ Ident server test timeout - no response received");
                }
            }
            catch (Exception ex)
            {
                LogWarn($"⚠️ Ident server test failed: {ex.Message}");
            }
        }

        private async Task EstablishTcpConnection(string server, int port, bool useSSL, int connectionTimeout, int sslTimeout)
        {
            LogInfo("🌐 Creating TCP client...");
                _tcpClient = new TcpClient();

            // Resolve DNS with timeout
            LogInfo($"🔍 Resolving DNS for {server}...");
            var dnsTask = Dns.GetHostAddressesAsync(server);
            var dnsTimeout = Task.Delay(TimeSpan.FromSeconds(10));
            var dnsCompleted = await Task.WhenAny(dnsTask, dnsTimeout).ConfigureAwait(false);
            if (dnsCompleted != dnsTask)
            {
                throw new TimeoutException($"DNS resolution timed out for {server}");
            }
            var addresses = await dnsTask.ConfigureAwait(false);
            LogInfo($"✅ DNS resolved to {string.Join(", ", addresses.Select(a => a.ToString()))}");

            // Establish TCP connection with timeout
            LogInfo($"🔗 Connecting to {server}:{port}...");
            var connectTask = _tcpClient.ConnectAsync(server, port);
            var connectTimeout = Task.Delay(TimeSpan.FromSeconds(connectionTimeout));
            var connectCompleted = await Task.WhenAny(connectTask, connectTimeout).ConfigureAwait(false);
            if (connectCompleted != connectTask)
            {
                throw new TimeoutException($"TCP connection timed out after {connectionTimeout} seconds");
            }
                await connectTask.ConfigureAwait(false);
            LogInfo("✅ TCP connection established");

            // Enable TCP keep-alive
                _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            LogInfo("✅ TCP keep-alive enabled");

            // Get network stream
            _networkStream = _tcpClient.GetStream();
            LogInfo("✅ Network stream obtained");

            // Handle SSL if required
                if (useSSL)
                {
                LogInfo("🔒 Setting up SSL/TLS...");
                var sslStream = new SslStream(_networkStream, false, ValidateServerCertificate);
                var sslTimeoutTask = Task.Delay(TimeSpan.FromSeconds(sslTimeout));
                var sslTask = sslStream.AuthenticateAsClientAsync(server, null, System.Security.Authentication.SslProtocols.Tls12, false);
                var sslCompleted = await Task.WhenAny(sslTask, sslTimeoutTask).ConfigureAwait(false);
                if (sslCompleted != sslTask)
                {
                    throw new TimeoutException($"SSL handshake timed out after {sslTimeout} seconds");
                }
                await sslTask.ConfigureAwait(false);
                _networkStream = sslStream;
                LogInfo("✅ SSL/TLS handshake completed");
            }

            // Create stream reader and writer
            LogInfo("📝 Creating stream reader and writer...");
            _reader = new StreamReader(_networkStream, Encoding.UTF8, false, 4096, true);
            _writer = new StreamWriter(_networkStream, Encoding.UTF8, 4096, true) { NewLine = "\r\n", AutoFlush = true };
            LogInfo("✅ Stream reader and writer created");
            
            // Set connected state so we can send commands
            SetConnected(true);
        }

        private async Task SendRegistrationCommands(string nickname, string username, string realName, string? password)
        {
            // Send password if provided (must be sent before NICK/USER)
                if (!string.IsNullOrEmpty(password))
                {
                LogInfo("🔑 Sending password...");
                    await SendCommandAsync($"PASS {password}").ConfigureAwait(false);
                }

            // Start capability negotiation (modern IRC best practice)
            LogInfo("🔧 Starting capability negotiation...");
            await SendCommandAsync("CAP LS 302").ConfigureAwait(false);

            // Send NICK and USER commands immediately (server will queue them)
            LogInfo($"👤 Sending NICK command: {nickname}");
                await SendCommandAsync($"NICK {nickname}").ConfigureAwait(false);

            LogInfo($"👤 Sending USER command: {username}");
                await SendCommandAsync($"USER {username} 0 * :{realName}").ConfigureAwait(false);
            
            LogInfo("✅ Registration commands sent (modern IRC protocol)");
        }


        private async Task<bool> WaitForServerWelcome()
        {
            LogInfo("⏳ Waiting for server welcome message (001)...");
            
            var welcomeTimeout = Task.Delay(TimeSpan.FromSeconds(30), _cancellationTokenSource.Token);
            var welcomeReceived = new TaskCompletionSource<bool>();
            var errorReceived = new TaskCompletionSource<bool>();
            var capEndSent = false;
            
            // Subscribe to message received event temporarily to detect welcome or error
            EventHandler<IRCMessage>? connectionHandler = null;
            connectionHandler = (sender, message) =>
            {
                // Handle CAP negotiation
                if (message.Command == "CAP")
                {
                    var subCommand = message.Parameters?.Count > 1 ? message.Parameters[1] : "";
                    if (subCommand == "LS")
                    {
                        LogInfo("🔧 Server capabilities received, ending CAP negotiation");
                        if (!capEndSent)
                        {
                            capEndSent = true;
                            _ = Task.Run(async () =>
                            {
                                await SendCommandAsync("CAP END").ConfigureAwait(false);
                                LogInfo("🔧 CAP END sent");
                            });
                        }
                    }
                }
                // Handle welcome message (001) - the definitive registration signal
                else if (message.Command == "001") // RPL_WELCOME
                {
                    LogInfo($"✅ Welcome message received: {message.Content}");
                    welcomeReceived.SetResult(true);
                    MessageReceived -= connectionHandler;
                }
                // Handle additional welcome messages
                else if (message.Command == "002" || message.Command == "003" || message.Command == "004")
                {
                    LogInfo($"📋 Server info: {message.Content}");
                }
                // Handle errors
                else if (message.Command == "ERROR")
                {
                    LogError($"❌ Server sent ERROR: {message.Content}");
                    errorReceived.SetResult(true);
                    MessageReceived -= connectionHandler;
                }
                else if (message.Command == "433") // ERR_NICKNAMEINUSE
                {
                    LogWarn($"⚠️ Nickname in use: {message.Content}");
                    errorReceived.SetResult(true);
                    MessageReceived -= connectionHandler;
                }
                else if (message.Command == "432") // ERR_ERRONEUSNICKNAME
                {
                    LogWarn($"⚠️ Erroneous nickname: {message.Content}");
                    errorReceived.SetResult(true);
                    MessageReceived -= connectionHandler;
                }
                else if (message.Command == "431") // ERR_NONICKNAMEGIVEN
                {
                    LogWarn($"⚠️ No nickname given: {message.Content}");
                    errorReceived.SetResult(true);
                    MessageReceived -= connectionHandler;
                }
                else if (message.Command == "462") // ERR_ALREADYREGISTRED
                {
                    LogWarn($"⚠️ Already registered: {message.Content}");
                    errorReceived.SetResult(true);
                    MessageReceived -= connectionHandler;
                }
            };
            MessageReceived += connectionHandler;
            
            // Wait for either welcome, error, or timeout
            var completed = await Task.WhenAny(welcomeReceived.Task, errorReceived.Task, welcomeTimeout).ConfigureAwait(false);
            MessageReceived -= connectionHandler;
            
            if (completed == welcomeReceived.Task)
            {
                LogInfo("✅ IRC connection established successfully");
                return true;
            }
            else if (completed == errorReceived.Task)
            {
                LogError("❌ Connection failed due to server error");
                return false;
            }
            else // Timeout
            {
                LogError("⏰ Timeout waiting for server welcome message (001)");
                return false;
            }
        }

        // Main connection method with Iridium-inspired architecture
        public async Task<bool> ConnectAsync(string server, int port, string nickname, string username, string realName, bool useSSL = false, string? password = null, string? identServer = null, int identPort = 113, int? connectionTimeoutSeconds = null, int? sslTimeoutSeconds = null, bool? enableIdent = null)
        {
            // Prevent multiple simultaneous connections
            await _connectionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                LogInfo($"=== IRC Connection (Iridium-Inspired Architecture) ===");
                LogInfo($"Target: {server}:{port} (SSL: {useSSL})");
                LogInfo($"User: {nickname} ({username}) - {realName}");
                LogInfo($"Ident Server: {enableIdent ?? false}");
                
                try
            {
                Server = server;
                Port = port;
                Nickname = nickname;
                Username = username;
                RealName = realName;
                _useSSL = useSSL;

                // Cancel any existing connection
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                OnConnectionStatusChanged("Connecting");

                // Load settings
                var settings = Configuration.AppSettings.Load();
                var actualConnectionTimeout = connectionTimeoutSeconds ?? settings.Connection.ConnectionTimeoutSeconds;
                var actualSslTimeout = sslTimeoutSeconds ?? settings.Connection.SSLHandshakeTimeoutSeconds;
                var actualEnableIdent = enableIdent ?? settings.Connection.EnableIdentServer;

                // Iridium-inspired approach: Start ident server BEFORE TCP connection
                if (actualEnableIdent)
                {
                    LogInfo("🔧 Starting ident server (Iridium approach: before TCP connection)");
                    await StartIdentServerBeforeConnection(username).ConfigureAwait(false);
                }
                else
                {
                    LogInfo("🔧 Ident server disabled (modern approach)");
                }

                // Establish TCP connection
                LogInfo("🌐 Establishing TCP connection...");
                await EstablishTcpConnection(server, port, useSSL, actualConnectionTimeout, actualSslTimeout).ConfigureAwait(false);

                // Start background tasks first to receive server messages
                LogInfo("🔄 Starting background tasks...");
                _ = Task.Run(() => ListenForMessagesAsync(), _cancellationTokenSource.Token);
                _ = Task.Run(() => KeepAliveAsync(), _cancellationTokenSource.Token);

                // Send IRC registration commands immediately (proper IRC protocol)
                LogInfo("📝 Sending IRC registration commands (modern IRC flow)...");
                await SendRegistrationCommands(nickname, username, realName, password).ConfigureAwait(false);

                // Wait for server welcome (001) - the definitive registration signal
                LogInfo("⏳ Waiting for server welcome message (001)...");
                return await WaitForServerWelcome().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                LogError($"❌ Connection failed: {ex.Message}");
                        OnErrorOccurred(ex);
                await DisconnectAsync().ConfigureAwait(false);
                return false;
                    }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        // Fallback connection strategy
        public async Task<bool> ConnectWithFallbackAsync(string server, int port, string nickname, string username, string realName, bool useSSL = false, string? password = null, string? identServer = null, int identPort = 113, int? connectionTimeoutSeconds = null, int? sslTimeoutSeconds = null)
        {
            LogInfo($"=== IRC Connection Strategy (Respecting User Settings) ===");
            LogInfo($"Target: {server}:{port} (SSL: {useSSL})");
            LogInfo($"User: {nickname} ({username}) - {realName}");
            
            // Load settings to respect user preferences
            var settings = Configuration.AppSettings.Load();
            var userWantsIdent = settings.Connection.EnableIdentServer;
            
            LogInfo($"🔧 User ident preference: {(userWantsIdent ? "Enabled" : "Disabled")}");
            
            // Check if this is a traditional network that requires ident
            var isTraditionalNetwork = server.Contains("efnet") || server.Contains("undernet") || server.Contains("dalnet");
            if (isTraditionalNetwork && !userWantsIdent)
            {
                LogWarn("⚠️ This appears to be a traditional IRC network that may require ident server");
                LogWarn("💡 Consider enabling 'Enable Ident Server' in Settings > Connection tab");
            }
            
            // Add a small delay before initial connection attempt to avoid rate limiting
            if (isTraditionalNetwork)
            {
                LogInfo("⏳ Waiting 2 seconds before initial connection attempt (traditional network)");
                await Task.Delay(2000);
            }
            
            // Strategy 1: Try with user's preferred ident setting (respect user choice)
            LogInfo($"🔄 Strategy 1: Connection with ident server {(userWantsIdent ? "enabled" : "disabled")} (user preference)");
            var success = await ConnectAsync(server, port, nickname, username, realName, useSSL, password, identServer, identPort, connectionTimeoutSeconds, sslTimeoutSeconds, userWantsIdent);
            
            if (success)
            {
                LogInfo($"✅ Strategy 1 successful - connected with ident server {(userWantsIdent ? "enabled" : "disabled")}");
                return true;
            }
            
            // Strategy 2: Try with different nickname (in case of conflicts)
            LogWarn("❌ Strategy 1 failed, trying Strategy 2: Alternative nickname");
            await DisconnectAsync();
            
            // Longer delay for traditional networks like EFNet to avoid rate limiting
            var delay = isTraditionalNetwork ? 10000 : 1000; // 10 seconds for EFNet, 1 second for others
            LogInfo($"⏳ Waiting {delay}ms before retry (traditional network: {isTraditionalNetwork})");
            await Task.Delay(delay);
            
            var altNickname = $"{nickname}_";
            LogInfo($"🔄 Strategy 2: Trying with alternative nickname: {altNickname}");
            success = await ConnectAsync(server, port, altNickname, username, realName, useSSL, password, identServer, identPort, connectionTimeoutSeconds, sslTimeoutSeconds, userWantsIdent);
            
            if (success)
            {
                LogInfo($"✅ Strategy 2 successful - connected with nickname: {altNickname}");
                return true;
            }
            
            // Check if the failure was due to ident server requirement
            if (!userWantsIdent)
            {
                LogError("❌ Connection failed - this server requires ident server authentication");
                LogError("💡 EFnet and other traditional IRC networks require ident server");
                LogError("💡 Enable 'Enable Ident Server' in Settings > Connection tab and try again");
                LogError("💡 Or try connecting to modern networks like Libera.Chat or Freenode that don't require ident");
            }
            else
            {
                LogError("❌ All connection strategies failed");
            }
            return false;
        }

        // Ident server implementation
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
                    LogInfo($"Ident server started on {listenIp}:{identPort}");
                    LogInfo($"Ident server ready to respond to IRC server requests");
                }
                catch (Exception ex)
                {
                    LogError($"Failed to bind ident server on {listenIp}:{identPort}: {ex.Message}");
                    OnErrorOccurred(new Exception($"Ident server failed to bind on {listenIp}:{identPort}: {ex.Message}", ex));
                    return;
                }

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var acceptTask = listener.AcceptTcpClientAsync();
                        var completed = await Task.WhenAny(acceptTask, Task.Delay(100, token)).ConfigureAwait(false);
                        if (completed != acceptTask)
                            continue;

                        var client = acceptTask.Result;
                        LogInfo($"🔍 Ident request from {client.Client.RemoteEndPoint}");
                        // Handle ident request immediately with high priority
                        _ = Task.Run(async () => await HandleIdentRequest(client, username), token);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        LogError($"Error in ident accept loop: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Fatal ident server error: {ex.Message}");
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

                    // Read one line with timeout
                    var readTask = reader.ReadLineAsync();
                    var completed = await Task.WhenAny(readTask, Task.Delay(5000)).ConfigureAwait(false);
                    if (completed != readTask)
                    {
                        LogWarn("Ident request read timeout - no request received");
                        return;
        }

                    var request = readTask.Result;
                    if (string.IsNullOrWhiteSpace(request))
        {
                        LogWarn("Ident request was empty");
                        return;
                    }

                    LogInfo($"🔍 Processing ident request: '{request}'");

                    // Parse the ident request according to RFC 1413
                    var parts = request.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                    {
                        LogWarn($"Invalid ident request format: '{request}'");
                        await writer.WriteLineAsync($"{request} : ERROR : INVALID-REQUEST").ConfigureAwait(false);
                        return;
                    }

                    if (!int.TryParse(parts[0].Trim(), out int portA) || !int.TryParse(parts[1].Trim(), out int portB))
                    {
                        LogWarn($"Invalid port numbers in ident request: '{request}'");
                        await writer.WriteLineAsync($"{request} : ERROR : INVALID-PORT").ConfigureAwait(false);
                        return;
                    }

                    // Send USERID response according to RFC 1413
                    var response = $"{portA} , {portB} : USERID : UNIX : {username}";
                    LogInfo($"📤 Sending ident response: '{response}'");
                    await writer.WriteLineAsync(response).ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                    LogInfo("✅ Ident response sent successfully");
                    
                    // Keep connection open briefly to ensure response is received
                    await Task.Delay(50).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogError($"Ident request error: {ex.Message}");
                    OnErrorOccurred(ex);
                }
                finally
                {
                    try { client.Close(); } catch { }
                }
            }
        }

        // Message listening
        private async Task ListenForMessagesAsync()
        {
            LogInfo("Starting IRC message listening loop");
            if (_reader == null)
            {
                LogError("Reader is null, cannot start message listening");
                return;
            }

            var token = _cancellationTokenSource?.Token ?? CancellationToken.None;

            try
            {
                while (!token.IsCancellationRequested && _reader != null)
                {
                    var line = await _reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null)
                    {
                        LogWarn("Received null line, remote closed");
                        break;
                    }

                    LogDebug($"Received: {line}");

                    // Handle PING promptly
                    if (line.StartsWith("PING ", StringComparison.OrdinalIgnoreCase))
                    {
                        var pongPayload = line.Substring(5);
                        LogDebug($"🏓 PING received, sending PONG: {pongPayload}");
                        await SendPongDirectly(pongPayload).ConfigureAwait(false);
                        continue;
                    }

                    // Parse and raise message event
                    try
                    {
                        var message = ParseIRCMessage(line);
                        MessageReceived?.Invoke(this, message);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error parsing IRC message: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogInfo("Message listening cancelled");
            }
            catch (Exception ex)
            {
                // Don't show errors if we're disconnecting or the operation was cancelled
                if (!token.IsCancellationRequested && _isConnected)
                {
                    LogError($"Error in message listening: {ex.Message}");
                    OnErrorOccurred(ex);
                }
                else
                {
                    LogInfo("Message listening stopped (disconnecting or cancelled)");
                }
            }
        }

        // Keep-alive mechanism
        private async Task KeepAliveAsync()
        {
            LogInfo("Starting keep-alive mechanism");
            var token = _cancellationTokenSource?.Token ?? CancellationToken.None;

            try
            {
                while (!token.IsCancellationRequested && _isConnected)
                {
                    await Task.Delay(TimeSpan.FromSeconds(60), token).ConfigureAwait(false);
                    
                    if (_isConnected && _writer != null)
                    {
                        try
                        {
                            await SendCommandAsync("PING :keepalive").ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            LogError($"Keep-alive ping failed: {ex.Message}");
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogInfo("Keep-alive cancelled");
            }
            catch (Exception ex)
            {
                LogError($"Keep-alive error: {ex.Message}");
            }
        }

        // Command sending
        public async Task SendCommandAsync(string command)
        {
            if (_writer == null || !_isConnected)
            {
                throw new InvalidOperationException("Not connected to IRC server");
            }

            await _streamLock.WaitAsync().ConfigureAwait(false);
            try
            {
                LogDebug($"Sending command: {command}");
                await _writer.WriteLineAsync(command).ConfigureAwait(false);
                await _writer.FlushAsync().ConfigureAwait(false);
                CommandSent?.Invoke(this, command);
            }
            finally
            {
                _streamLock.Release();
            }
        }

        // IRC-specific command methods
        public async Task JoinChannelAsync(string channel)
        {
            await SendCommandAsync($"JOIN {channel}").ConfigureAwait(false);
        }

        public async Task LeaveChannelAsync(string channel, string? reason = null)
        {
            var command = string.IsNullOrEmpty(reason) ? $"PART {channel}" : $"PART {channel} :{reason}";
            await SendCommandAsync(command).ConfigureAwait(false);
        }

        public async Task SendMessageAsync(string target, string message)
        {
            await SendCommandAsync($"PRIVMSG {target} :{message}").ConfigureAwait(false);
        }

        public async Task SendNoticeAsync(string target, string message)
        {
            await SendCommandAsync($"NOTICE {target} :{message}").ConfigureAwait(false);
        }

        public async Task SendCTCPAsync(string target, string command, string? parameter = null)
        {
            var message = string.IsNullOrEmpty(parameter) ? $"\u0001{command}\u0001" : $"\u0001{command} {parameter}\u0001";
            await SendCommandAsync($"PRIVMSG {target} :{message}").ConfigureAwait(false);
        }

        private async Task SendPongDirectly(string payload)
        {
            if (_writer == null) return;

            try
            {
                await _writer.WriteLineAsync($"PONG {payload}").ConfigureAwait(false);
                await _writer.FlushAsync().ConfigureAwait(false);
            }
                    catch (Exception ex)
                    {
                LogError($"Failed to send PONG: {ex.Message}");
            }
        }

        // Message parsing
        private IRCMessage ParseIRCMessage(string line)
        {
            var message = new IRCMessage();
            
            if (string.IsNullOrWhiteSpace(line))
                return message;


            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return message;

            var index = 0;

            // Parse prefix (optional)
            if (parts[0].StartsWith(':'))
            {
                message.Prefix = parts[0].Substring(1);
                index++;
            }

            // Parse command
            if (index < parts.Length)
            {
                message.Command = parts[index++];
            }

            // Parse parameters
            var parameters = new List<string>();
            while (index < parts.Length)
            {
                if (parts[index].StartsWith(':'))
                {
                    // Last parameter (can contain spaces)
                    var lastParam = string.Join(" ", parts.Skip(index)).Substring(1);
                    parameters.Add(lastParam);
                    break;
                }
                else
                {
                    parameters.Add(parts[index]);
                    index++;
                }
            }

            message.Parameters = parameters;
            // Content is read-only, so we don't set it here


            return message;
        }

        // Disconnect
        public async Task DisconnectAsync()
        {
            LogInfo("Starting disconnect process");
            
            _cancellationTokenSource?.Cancel();
            await Task.Delay(100).ConfigureAwait(false);

            await _streamLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_isConnected && _writer != null)
                {
                    try
                    {
                        // Load settings to get the configured quit message
                        var settings = Configuration.AppSettings.Load();
                        var quitMessage = !string.IsNullOrEmpty(settings.User.QuitMessage) 
                            ? settings.User.QuitMessage 
                            : "y0daii IRC Client";
                        
                        await _writer.WriteLineAsync($"QUIT :{quitMessage}").ConfigureAwait(false);
                        await _writer.FlushAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error sending QUIT command: {ex.Message}");
                    }
                }

                // Close streams
                try { _reader?.Dispose(); } catch { }
                try { _writer?.Dispose(); } catch { }
                try { _sslStream?.Dispose(); } catch { }
                try { _networkStream?.Dispose(); } catch { }
                try { _tcpClient?.Close(); } catch { }

                _reader = null;
                _writer = null;
                _sslStream = null;
                _networkStream = null;
                _tcpClient = null;

                SetConnected(false);
                LogInfo("Disconnect completed");
            }
            finally
            {
                _streamLock.Release();
            }
        }

        // Helper methods
        public void SetConnected(bool connected)
        {
            _isConnected = connected;
            OnConnectionStatusChanged(connected ? "Connected" : "Disconnected");
        }

        private void OnConnectionStatusChanged(string status)
        {
            ConnectionStatusChanged?.Invoke(this, status);
        }

        private void OnErrorOccurred(Exception ex)
        {
            LogError($"Error occurred: {ex.Message}");
            ErrorOccurred?.Invoke(this, ex);
        }

        private bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            // For now, accept all certificates (like most IRC clients)
            return true;
        }

        // Test connectivity
        public static async Task<bool> TestConnectivityAsync(string server, int port, int timeoutSeconds = 10)
        {
            try
            {
                Console.WriteLine($"TestConnectivityAsync: Testing connection to {server}:{port}");
                
                var dnsTask = Dns.GetHostAddressesAsync(server);
                var dnsTimeout = Task.Delay(TimeSpan.FromSeconds(5));
                var dnsCompleted = await Task.WhenAny(dnsTask, dnsTimeout).ConfigureAwait(false);
                if (dnsCompleted != dnsTask)
                {
                    Console.WriteLine($"TestConnectivityAsync: DNS resolution timed out for {server}");
                    return false;
                }
                
                var addresses = await dnsTask.ConfigureAwait(false);
                Console.WriteLine($"TestConnectivityAsync: DNS resolved to {string.Join(", ", addresses.Select(a => a.ToString()))}");
                
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(server, port);
                var connectTimeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                var connectCompleted = await Task.WhenAny(connectTask, connectTimeout).ConfigureAwait(false);
                
                if (connectCompleted != connectTask)
                {
                    Console.WriteLine($"TestConnectivityAsync: Connection timed out to {server}:{port}");
                    return false;
                }
                
                await connectTask.ConfigureAwait(false);
                Console.WriteLine($"TestConnectivityAsync: Successfully connected to {server}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TestConnectivityAsync: Failed to connect to {server}:{port} - {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> TestIdentServerConnectivityAsync(int identPort = 1130)
        {
            try
            {
                Console.WriteLine($"TestIdentServerConnectivityAsync: Testing ident server on port {identPort}");
                
                var listener = new TcpListener(IPAddress.Any, identPort);
                try
                {
                    listener.Start();
                    Console.WriteLine($"TestIdentServerConnectivityAsync: Port {identPort} is available for ident server");
                    listener.Stop();
                    return true;
            }
            catch (Exception ex)
            {
                    Console.WriteLine($"TestIdentServerConnectivityAsync: Port {identPort} is not available: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TestIdentServerConnectivityAsync: Error testing ident server: {ex.Message}");
                return false;
            }
        }

        // Test if we can bind to port 113 (standard ident port)
        public static async Task<bool> TestStandardIdentPortAsync()
        {
            try
            {
                Console.WriteLine("TestStandardIdentPortAsync: Testing if we can bind to port 113 (standard ident port)");
                
                var listener = new TcpListener(IPAddress.Any, 113);
                try
                {
                    listener.Start();
                    Console.WriteLine("TestStandardIdentPortAsync: ✅ Port 113 is available - ident server can run on standard port");
                    listener.Stop();
                    return true;
            }
            catch (Exception ex)
            {
                    Console.WriteLine($"TestStandardIdentPortAsync: ❌ Port 113 is not available: {ex.Message}");
                    Console.WriteLine("TestStandardIdentPortAsync: 💡 This usually means you need to run as Administrator on Windows");
                    Console.WriteLine("TestStandardIdentPortAsync: 💡 IRC servers expect ident on port 113, but we'll fall back to port 1130");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TestStandardIdentPortAsync: Error testing port 113: {ex.Message}");
                return false;
            }
        }

        // Dispose
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _streamLock?.Dispose();
            _reader?.Dispose();
            _writer?.Dispose();
            _sslStream?.Dispose();
            _networkStream?.Dispose();
            _tcpClient?.Close();
        }
    }
}
