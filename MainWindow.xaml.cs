using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualBasic;
using Y0daiiIRC.Configuration;
using Y0daiiIRC.IRC;
using Y0daiiIRC.Models;
using Y0daiiIRC.Services;
using Y0daiiIRC.Utils;

namespace Y0daiiIRC
{
    public partial class MainWindow : Window
    {
        private IRCClient _ircClient;
        private List<Channel> _channels;
        private List<User> _users;
        private Channel? _currentChannel;
        private Dictionary<string, List<ChatMessage>> _channelMessages;
        private List<string> _channelsToRejoin; // Track channels to auto-rejoin on reconnect
        private ServerListService _serverListService;
        private CommandProcessor _commandProcessor;
        private DCCService _dccService;
        private MessageGroupingService _messageGroupingService;
        // private TabItem? _consoleTab; // Removed - using Office 365-style navigation
        
        // Command history support
        private List<string> _commandHistory = new List<string>();
        private int _historyIndex = -1;

        public MainWindow()
        {
            InitializeComponent();
            _ircClient = new IRCClient();
            _channels = new List<Channel>();
            _users = new List<User>();
            _channelMessages = new Dictionary<string, List<ChatMessage>>();
            _channelsToRejoin = new List<string>();
            _serverListService = new ServerListService();
            _dccService = new DCCService();
            _commandProcessor = new CommandProcessor(_ircClient, _serverListService);
            _messageGroupingService = new MessageGroupingService();

            SetupEventHandlers();
            SetupUIContextMenus();
            SetupWindowDragging();
            InitializeConsole();
            LoadCompactViewSetting();
            UpdateUI();
        }

        private void SetupEventHandlers()
        {
            _ircClient.MessageReceived += OnIRCMessageReceived;
            _ircClient.ConnectionStatusChanged += OnConnectionStatusChanged;
            _ircClient.ErrorOccurred += OnErrorOccurred;
            _ircClient.DCCRequestReceived += OnDCCRequestReceived;
            _ircClient.CommandSent += OnCommandSent;
            _commandProcessor.CommandExecuted += OnCommandExecuted;
            _commandProcessor.CommandError += OnCommandError;
        }

        private void SetupUIContextMenus()
        {
            // Add context menu to Connect button
            var connectContextMenu = new ContextMenu();
            var connectItem = new MenuItem { Header = "🔌 Connect to Server" };
            connectItem.Click += (s, e) => ConnectButton_Click(s, e);
            connectContextMenu.Items.Add(connectItem);
            
            var disconnectItem = new MenuItem { Header = "🔌 Disconnect" };
            disconnectItem.Click += (s, e) => ConnectButton_Click(s, e);
            connectContextMenu.Items.Add(disconnectItem);
            
            ConnectButtonNav.ContextMenu = connectContextMenu;
        }

        private void SetupWindowDragging()
        {
            // Window dragging is now handled by the title bar event handler
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void ClearChannelsAndUsers()
        {
            // Clear channels (except console)
            var channelsToRemove = _channels.Where(c => c.Name != "console").ToList();
            foreach (var channel in channelsToRemove)
            {
                _channels.Remove(channel);
                
                // Remove channel button from UI
                var buttonToRemove = ChannelList.Children.OfType<Button>()
                    .FirstOrDefault(b => b.Tag == channel);
                if (buttonToRemove != null)
                {
                    ChannelList.Children.Remove(buttonToRemove);
                }
            }
            
            // Clear users
            _users.Clear();
            UserList.Children.Clear();
            
            // Switch to console if not already there
            var consoleChannel = _channels.FirstOrDefault(c => c.Name == "console");
            if (consoleChannel != null && _currentChannel != consoleChannel)
            {
                SwitchToChannel(consoleChannel);
            }
        }

        private void LoadCompactViewSetting()
        {
            try
            {
                var settings = Configuration.AppSettings.Load();
                ChatMessage.UseCompactView = settings.Appearance.UseCompactView;
                ChatMessage.EnableAnimations = settings.Appearance.EnableAnimations;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load appearance settings: {ex.Message}");
                ChatMessage.UseCompactView = false;
                ChatMessage.EnableAnimations = true;
            }
        }

        public void RefreshCompactViewSetting()
        {
            LoadCompactViewSetting();
            // Force UI refresh to apply the new compact view setting
            // Since static properties don't trigger data binding updates, we need to force a refresh
            // by clearing and re-adding all items
            var currentItems = MessageList.Items.Cast<object>().ToList();
            MessageList.Items.Clear();
            
            // Add a small delay to ensure the UI has time to process the clear
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var item in currentItems)
                {
                    MessageList.Items.Add(item);
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void InitializeConsole()
        {
            // Create console channel and add it to navigation
            var consoleChannel = new Channel { Name = "console", Type = ChannelType.Console };
            _channels.Add(consoleChannel);
            AddChannelButton(consoleChannel);
            SwitchToChannel(consoleChannel);
            
            AddSystemMessage("Welcome to y0daii IRC Client! Type /help for available commands.");
        }

        private void OnIRCMessageReceived(object? sender, IRCMessage message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [IRC] Received: {message.Command} {string.Join(" ", message.Parameters)}");
            
            Dispatcher.Invoke(() =>
            {
                // Only show important system messages, not protocol commands
                var displayMessage = message.Command switch
                {
                    "001" => $"✅ Welcome: {message.Content}",
                    "002" => $"ℹ️ Host: {message.Content}",
                    "003" => $"ℹ️ Created: {message.Content}",
                    "004" => $"ℹ️ Server: {message.Content}",
                    "005" => $"🔧 Features: {message.Content}",
                    "NOTICE" => $"📢 Notice: {message.Content}",
                    "ERROR" => $"❌ Error: {message.Content}",
                    _ => null // Don't show protocol commands like PING, PONG, etc.
                };
                
                // Only add system message if it's something important to show
                if (displayMessage != null)
                {
                    AddSystemMessage(displayMessage);
                }
                
                HandleIRCMessage(message);
            });
        }

        private void OnConnectionStatusChanged(object? sender, string status)
        {
            Dispatcher.Invoke(() =>
            {
                // Display "Connecting..." with ellipsis for connecting status
                StatusText.Text = status == "Connecting" ? "Connecting..." : status;
                ConnectButton.Content = status == "Connected" ? "Disconnect" : "Connect";
                
                // Log connection status changes to console
                if (status == "Connected")
                {
                    StatusIndicator.Fill = new SolidColorBrush(Colors.Green);
                    ConnectionInfo.Text = $"Connected to {_ircClient.Server}:{_ircClient.Port}";
                    AddSystemMessage($"✓ Connected to {_ircClient.Server}:{_ircClient.Port} {(status.Contains("SSL") ? "(SSL)" : "")}");
                    
                    // Auto-rejoin channels after successful connection
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000); // Wait 2 seconds for server to be ready
                        foreach (var channel in _channelsToRejoin.ToList())
                        {
                            try
                            {
                                await _ircClient.SendCommandAsync($"JOIN {channel}");
                                AddSystemMessage($"🔄 Auto-rejoining {channel}...");
                            }
                            catch (Exception ex)
                            {
                                AddSystemMessage($"❌ Failed to rejoin {channel}: {ex.Message}");
                            }
                        }
                    });
                }
                else if (status == "Disconnected")
                {
                    StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                    ConnectionInfo.Text = "";
                    AddSystemMessage("✗ Disconnected from server");
                    
                    // Clear channels and users on disconnect (but keep auto-rejoin list)
                    ClearChannelsAndUsers();
                }
                else if (status == "Connecting")
                {
                    StatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
                    AddSystemMessage($"🔄 Connecting to {_ircClient.Server}:{_ircClient.Port}...");
                }
                else
                {
                    StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                    ConnectionInfo.Text = "";
                    AddSystemMessage($"⚠ {status}");
                }
                
                UpdateUI();
            });
        }

        private void OnErrorOccurred(object? sender, Exception ex)
        {
            Console.WriteLine($"MainWindow.OnErrorOccurred: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"MainWindow.OnErrorOccurred: Stack trace: {ex.StackTrace}");
            Dispatcher.Invoke(() =>
            {
                AddSystemMessage($"Error: {ex.Message}");
            });
        }

        private void OnCommandSent(object? sender, string command)
        {
            Console.WriteLine($"OnCommandSent called: {command}");
            Dispatcher.Invoke(() =>
            {
                // Only show important commands, not user messages or protocol commands
                if (command.StartsWith("PASS"))
                {
                    AddSystemMessage("→ PASS ***");
                }
                else if (command.StartsWith("JOIN"))
                {
                    AddSystemMessage($"→ {command}");
                }
                else if (command.StartsWith("PART"))
                {
                    AddSystemMessage($"→ {command}");
                }
                else if (command.StartsWith("NICK"))
                {
                    AddSystemMessage($"→ {command}");
                }
                else if (command.StartsWith("QUIT"))
                {
                    AddSystemMessage($"→ {command}");
                }
                // Don't show PRIVMSG, PING, PONG, etc. as they clutter the chat
            });
        }

        private async void OnDCCRequestReceived(object? sender, DCCRequest request)
        {
            if (request.Type == DCCRequestType.Send)
            {
                var result = MessageBox.Show(
                    $"User {request.Sender} wants to send you a file:\n\n" +
                    $"File: {request.FileName}\n" +
                    $"Size: {FormatFileSize(request.FileSize)}\n\n" +
                    $"Do you want to accept this file transfer?",
                    "DCC File Transfer Request",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _dccService.InitiateReceiveAsync(
                            request.Sender, 
                            request.FileName, 
                            request.FileSize, 
                            request.IPAddress, 
                            request.Port, 
                            request.Token);
                        
                        AddSystemMessage($"Accepting file transfer: {request.FileName}");
                    }
                    catch (Exception ex)
                    {
                        AddSystemMessage($"Failed to accept file transfer: {ex.Message}");
                    }
                }
                else
                {
                    AddSystemMessage($"Declined file transfer: {request.FileName}");
                }
            }
        }

        private void OnCommandExecuted(object? sender, string command)
        {
            Dispatcher.Invoke(() =>
            {
                AddSystemMessage($"✅ {command}");
            });
        }

        private void OnCommandError(object? sender, string error)
        {
            Dispatcher.Invoke(() =>
            {
                AddSystemMessage($"Error: {error}");
            });
        }

        private void HandleIRCMessage(IRCMessage message)
        {
            if (message.IsPing)
            {
                var pingData = message.Parameters.FirstOrDefault();
                AddSystemMessage($"🏓 Received PING from server, sending PONG: {pingData}");
                _ = Task.Run(async () => await _ircClient.SendCommandAsync($"PONG {pingData}"));
                return;
            }

            if (message.IsPrivateMessage)
            {
                var target = message.Target;
                var content = message.Content;
                var sender = message.Sender;

                if (message.IsCTCPAction)
                {
                    // CTCP ACTION (/me command)
                    var action = message.CTCPAction;
                    if (target?.StartsWith("#") == true)
                    {
                        // Channel action
                        AddChannelAction(target, sender, action);
                    }
                    else
                    {
                        // Private action
                        AddPrivateAction(sender, action);
                    }
                }
                else if (message.IsCTCPRequest && message.Content?.StartsWith("\x01") == true && message.Content.EndsWith("\x01") && message.Content.Length > 2)
                {
                    // Handle other CTCP requests (VERSION, TIME, PING, etc.)
                    _ = Task.Run(async () => await HandleCTCPRequest(message));
                }
                else
                {
                    if (target?.StartsWith("#") == true)
                    {
                        // Channel message
                        AddChannelMessage(target, sender, content);
                    }
                    else
                    {
                        // Private message
                        AddPrivateMessage(sender, content);
                    }
                }
            }
            else if (message.IsJoin)
            {
                var channel = message.Target;
                var user = message.Sender;
                if (user == _ircClient.Nickname)
                {
                    AddSystemMessage($"✅ Successfully joined {channel}");
                    
                    // Create the channel in the UI if it doesn't exist
                    if (!_channels.Any(c => c.Name == channel))
                    {
                        var newChannel = new Channel { Name = channel, Type = ChannelType.Channel };
                        _channels.Add(newChannel);
                        AddChannelButton(newChannel);
                    }
                    
                    // Add to auto-rejoin list (only for regular channels, not private messages)
                    if (channel.StartsWith("#") && !_channelsToRejoin.Contains(channel))
                    {
                        _channelsToRejoin.Add(channel);
                    }
                    
                    // Switch to the newly joined channel
                    var joinedChannel = _channels.FirstOrDefault(c => c.Name == channel);
                    if (joinedChannel != null)
                    {
                        SwitchToChannel(joinedChannel);
                    }
                }
                else
                {
                    AddSystemMessage($"👤 {user} joined {channel}");
                }
                if (channel == _currentChannel?.Name)
                {
                    AddUser(new User { Nickname = user });
                }
            }
            else if (message.IsPart)
            {
                var channel = message.Target;
                var user = message.Sender;
                AddSystemMessage($"{user} left {channel}");
                
                if (user == _ircClient.Nickname)
                {
                    // User left the channel - remove it from UI and auto-rejoin list
                    var channelToRemove = _channels.FirstOrDefault(c => c.Name == channel);
                    if (channelToRemove != null)
                    {
                        _channels.Remove(channelToRemove);
                        
                        // Remove from auto-rejoin list (user manually left)
                        _channelsToRejoin.Remove(channel);
                        
                        // Remove the channel button from UI
                        var buttonToRemove = ChannelList.Children.OfType<Button>()
                            .FirstOrDefault(b => b.Tag == channelToRemove);
                        if (buttonToRemove != null)
                        {
                            ChannelList.Children.Remove(buttonToRemove);
                        }
                        
                        // If this was the current channel, switch to console
                        if (channel == _currentChannel?.Name)
                        {
                            var consoleChannel = _channels.FirstOrDefault(c => c.Name == "console");
                            if (consoleChannel != null)
                            {
                                SwitchToChannel(consoleChannel);
                            }
                        }
                    }
                }
                else if (channel == _currentChannel?.Name)
                {
                    RemoveUser(user);
                }
            }
            else if (message.IsQuit)
            {
                var user = message.Sender;
                AddSystemMessage($"{user} quit IRC");
                RemoveUser(user);
            }
            else if (message.IsNick)
            {
                var oldNick = message.Sender;
                var newNick = message.Parameters.FirstOrDefault();
                AddSystemMessage($"{oldNick} is now known as {newNick}");
                UpdateUserNick(oldNick, newNick);
            }
            else if (message.IsMode)
            {
                HandleModeChange(message);
            }
            else if (message.IsNotice)
            {
                var content = message.Content;
                var sender = message.Sender;
                
                // Handle server notices (like ident responses)
                if (sender == "*" || string.IsNullOrEmpty(sender))
                {
                    if (content?.Contains("No Ident response") == true)
                    {
                        AddSystemMessage("ℹ️ No ident response - server will use ~username");
                    }
                    else if (content?.Contains("Checking Ident") == true)
                    {
                        AddSystemMessage("🔍 Server is checking ident...");
                    }
                    else if (content?.Contains("Looking up your hostname") == true)
                    {
                        AddSystemMessage("🌐 Server is looking up your hostname...");
                    }
                    else if (content?.Contains("Found your hostname") == true)
                    {
                        AddSystemMessage($"🌐 Hostname resolved: {content}");
                    }
                    else
                    {
                        AddSystemMessage($"📢 Server notice: {content}");
                    }
                }
                else
                {
                    // Regular notice from a user
                    AddSystemMessage($"📢 Notice from {sender}: {content}");
                }
            }
            else if (message.IsNumeric)
            {
                HandleNumericMessage(message);
            }
            else if (message.Command == "ERROR")
            {
                AddSystemMessage($"❌ Server Error: {message.Content}");
                
                // Handle specific error cases
                if (message.Content?.Contains("Connection timed out") == true)
                {
                    AddSystemMessage("💡 Connection timed out. This is often due to ident server issues.");
                    AddSystemMessage("💡 The app will automatically retry with different settings.");
                    _ircClient.SetConnected(false);
                }
                else if (message.Content?.Contains("No Ident response") == true)
                {
                    AddSystemMessage("💡 Server couldn't get ident response. This is usually not critical.");
                }
                else if (message.Content?.Contains("Ident required") == true || message.Content?.Contains("identd") == true)
                {
                    AddSystemMessage("💡 This server requires ident. Try enabling ident server in Settings → Connection.");
                    _ircClient.SetConnected(false);
                }
                else
                {
                    // For other errors, don't automatically disconnect - let the server handle it
                    Console.WriteLine($"Server sent ERROR message: {message.Content}");
                }
            }
        }

        private void HandleWhoisResponse(IRCMessage message, string responseType)
        {
            // Extract the target user from the whois response
            var targetUser = message.Parameters.Count > 1 ? message.Parameters[1] : "Unknown";
            var groupId = $"whois_{targetUser}";
            var groupTitle = $"ℹ️ Whois: {targetUser}";

            // Create a message for this whois response
            var whoisMessage = new ChatMessage
            {
                Sender = "System",
                Content = $"{responseType}: {message.Content}",
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                SenderColor = Colors.Gray,
                Type = MessageType.System
            };

            // Add to grouping service
            _messageGroupingService.TryAddToGroup(groupId, groupTitle, whoisMessage, MessageType.Grouped);

            // Check if this completes the whois group
            if (message.Command == "318" || _messageGroupingService.IsGroupComplete(groupId))
            {
                var groupedMessage = _messageGroupingService.TryCompleteGroup(groupId);
                if (groupedMessage != null)
                {
                    AddSystemMessage(groupedMessage);
                }
            }
        }

        private async Task HandleCTCPRequest(IRCMessage message)
        {
            var sender = message.Sender;
            var ctcpType = message.CTCPType;
            var ctcpParameter = message.CTCPParameter;
            var content = message.Content;


            // Additional validation to ensure this is actually a CTCP request
            if (string.IsNullOrEmpty(sender) || string.IsNullOrEmpty(ctcpType) || 
                !content?.StartsWith("\x01") == true || !content?.EndsWith("\x01") == true ||
                content.Length <= 2)
                return;

            switch (ctcpType.ToUpperInvariant())
            {
                case "VERSION":
                    var version = Utils.VersionInfo.Version;
                    var versionResponse = $"y0daii IRC Client {version}";
                    await _ircClient.SendNoticeAsync(sender, $"\x01VERSION {versionResponse}\x01");
                    break;

                case "TIME":
                    var timeResponse = DateTime.Now.ToString("ddd MMM dd HH:mm:ss yyyy");
                    await _ircClient.SendNoticeAsync(sender, $"\x01TIME {timeResponse}\x01");
                    break;

                case "PING":
                    // Echo back the ping parameter or current timestamp
                    var pingResponse = !string.IsNullOrEmpty(ctcpParameter) ? ctcpParameter : DateTime.Now.Ticks.ToString();
                    await _ircClient.SendNoticeAsync(sender, $"\x01PING {pingResponse}\x01");
                    break;

                case "CLIENTINFO":
                    var clientInfo = "VERSION TIME PING CLIENTINFO";
                    await _ircClient.SendNoticeAsync(sender, $"\x01CLIENTINFO {clientInfo}\x01");
                    break;

                default:
                    // Unknown CTCP request - send ERRMSG
                    await _ircClient.SendNoticeAsync(sender, $"\x01ERRMSG {ctcpType} :Unknown CTCP command\x01");
                    break;
            }
        }

        private void HandleNumericMessage(IRCMessage message)
        {
            switch (message.Command)
            {
                case "001": // RPL_WELCOME
                    _ircClient.SetConnected(true);
                    AddSystemMessage($"Connected to {_ircClient.Server}");
                    break;
                case "002": // RPL_YOURHOST
                    AddSystemMessage($"Your host is {message.Content}");
                    break;
                case "003": // RPL_CREATED
                    AddSystemMessage($"This server was created {message.Content}");
                    break;
                case "004": // RPL_MYINFO
                    AddSystemMessage($"Server info: {message.Content}");
                    break;
                case "005": // RPL_ISUPPORT
                    // Server capabilities - could be used for feature detection
                    AddSystemMessage($"🔧 Server capabilities: {message.Content}");
                    break;
                case "250": // RPL_STATSCONN
                    AddSystemMessage($"Highest connection count: {message.Content}");
                    break;
                case "251": // RPL_LUSERCLIENT
                    AddSystemMessage($"User info: {message.Content}");
                    break;
                case "252": // RPL_LUSEROP
                    AddSystemMessage($"Operator info: {message.Content}");
                    break;
                case "253": // RPL_LUSERUNKNOWN
                    AddSystemMessage($"Unknown connections: {message.Content}");
                    break;
                case "254": // RPL_LUSERCHANNELS
                    AddSystemMessage($"Channels formed: {message.Content}");
                    break;
                case "255": // RPL_LUSERME
                    AddSystemMessage($"Local users: {message.Content}");
                    break;
                case "265": // RPL_LOCALUSERS
                    AddSystemMessage($"Local users: {message.Content}");
                    break;
                case "266": // RPL_GLOBALUSERS
                    AddSystemMessage($"Global users: {message.Content}");
                    break;
                case "353": // RPL_NAMREPLY - User list
                    if (message.Parameters.Count >= 4)
                    {
                        var channel = message.Parameters[2];
                        var users = message.Parameters[3].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var user in users)
                        {
                            var (cleanUser, mode) = ParseUserWithMode(user);
                            AddUser(new User { Nickname = cleanUser, Mode = mode });
                        }
                    }
                    break;
                case "366": // RPL_ENDOFNAMES
                    // End of user list
                    break;
                case "372": // RPL_MOTD
                    AddSystemMessage($"MOTD: {message.Content}");
                    break;
                case "375": // RPL_MOTDSTART
                    AddSystemMessage("--- Message of the Day ---");
                    break;
                case "376": // RPL_ENDOFMOTD
                    AddSystemMessage("--- End of Message of the Day ---");
                    break;
                case "311": // RPL_WHOISUSER
                    HandleWhoisResponse(message, "User Info");
                    break;
                case "312": // RPL_WHOISSERVER
                    HandleWhoisResponse(message, "Server Info");
                    break;
                case "313": // RPL_WHOISOPERATOR
                    HandleWhoisResponse(message, "Operator Info");
                    break;
                case "317": // RPL_WHOISIDLE
                    HandleWhoisResponse(message, "Idle Time");
                    break;
                case "318": // RPL_ENDOFWHOIS
                    HandleWhoisResponse(message, "End of /WHOIS list");
                    break;
                case "319": // RPL_WHOISCHANNELS
                    HandleWhoisResponse(message, "Channels");
                    break;
                case "320": // RPL_WHOISSPECIAL
                    HandleWhoisResponse(message, "Special Info");
                    break;
                case "332": // RPL_TOPIC
                    HandleTopicMessage(message);
                    break;
                case "333": // RPL_TOPICWHOTIME
                    HandleTopicTimeMessage(message);
                    break;
                case "433": // ERR_NICKNAMEINUSE
                    AddSystemMessage($"❌ Nickname {message.Parameters[1]} is already in use");
                    _ircClient.SetConnected(false);
                    break;
                case "436": // ERR_NICKCOLLISION
                    AddSystemMessage($"❌ Nickname collision: {message.Content}");
                    _ircClient.SetConnected(false);
                    break;
                case "451": // ERR_NOTREGISTERED
                    AddSystemMessage("❌ You have not registered");
                    _ircClient.SetConnected(false);
                    break;
                case "464": // ERR_PASSWDMISMATCH
                    AddSystemMessage("❌ Password incorrect");
                    _ircClient.SetConnected(false);
                    break;
                case "465": // ERR_YOUREBANNEDCREEP
                    AddSystemMessage("❌ You are banned from this server");
                    _ircClient.SetConnected(false);
                    break;
                default:
                    // Log unknown numeric responses for debugging
                    if (message.Content != null)
                    {
                        AddSystemMessage($"[{message.Command}] {message.Content}");
                    }
                    break;
            }
        }

        private void AddChannelMessage(string channel, string sender, string content)
        {
            if (!_channelMessages.ContainsKey(channel))
            {
                _channelMessages[channel] = new List<ChatMessage>();
            }

            // Parse ANSI colors
            var formattedContent = ANSIColorParser.ParseANSIText(content);
            var displayContent = string.Join("", formattedContent.Select(f => f.Text));

            // Determine message type and styling
            var messageType = MessageType.Normal;
            var senderColor = GetUserColor(sender);
            
            // Special styling for different types of messages
            if (sender == _ircClient.Nickname)
            {
                // User's own messages - make them stand out
                messageType = MessageType.Normal;
                senderColor = Colors.DarkBlue; // Make user's own messages blue
            }
            else if (sender == "System")
            {
                messageType = MessageType.System;
                senderColor = Colors.Gray;
            }

            // iMessage-style message properties
            var isUserMessage = sender == _ircClient.Nickname;
            var isSystemMessage = sender == "System";
            var isOtherMessage = !isUserMessage && !isSystemMessage;

            var message = new ChatMessage
            {
                Sender = sender,
                Content = displayContent,
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                SenderColor = senderColor,
                Type = messageType,
                IsUserMessage = isUserMessage,
                IsOtherMessage = isOtherMessage,
                IsSystemMessage = isSystemMessage,
                CurrentUserNickname = _ircClient.Nickname
            };

            _channelMessages[channel].Add(message);

            // Increment unread count if this is not the current channel
            if (_currentChannel?.Name != channel)
            {
                var channelObj = _channels.FirstOrDefault(c => c.Name == channel);
                if (channelObj != null)
                {
                    channelObj.UnreadCount++;
                    UpdateUnreadIndicator(channelObj);
                }
            }

            if (channel == _currentChannel?.Name)
            {
                MessageList.Items.Add(message);
                ScrollToBottom();
            }
        }

        private void AddPrivateMessage(string sender, string content)
        {
            // Create or switch to private message channel
            var pmChannel = $"PM:{sender}";
            if (!_channels.Any(c => c.Name == pmChannel))
            {
                var channel = new Channel { Name = pmChannel, Type = ChannelType.Private };
                _channels.Add(channel);
                AddChannelButton(channel);
            }

            AddChannelMessage(pmChannel, sender, content);
        }

        private void AddChannelAction(string channel, string sender, string action)
        {
            if (!_channelMessages.ContainsKey(channel))
            {
                _channelMessages[channel] = new List<ChatMessage>();
            }

            // Parse ANSI colors
            var formattedAction = ANSIColorParser.ParseANSIText(action);
            var displayAction = string.Join("", formattedAction.Select(f => f.Text));

            // Create action message with special formatting
            var message = new ChatMessage
            {
                Sender = sender,
                Content = $"* {sender} {displayAction}",
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                SenderColor = GetUserColor(sender),
                Type = MessageType.Action // We'll need to add this to the enum
            };

            _channelMessages[channel].Add(message);

            if (channel == _currentChannel?.Name)
            {
                MessageList.Items.Add(message);
                ScrollToBottom();
            }
        }

        private void AddPrivateAction(string sender, string action)
        {
            // Create or switch to private message channel
            var pmChannel = $"PM:{sender}";
            if (!_channels.Any(c => c.Name == pmChannel))
            {
                var channel = new Channel { Name = pmChannel, Type = ChannelType.Private };
                _channels.Add(channel);
                AddChannelButton(channel);
            }

            AddChannelAction(pmChannel, sender, action);
        }

        private void ClosePrivateMessage(Channel channel)
        {
            // Remove the private message channel
            _channels.Remove(channel);
            
            // Remove the channel button from UI (now it's in a StackPanel)
            var containerToRemove = PrivateMessageList.Children.OfType<StackPanel>()
                .FirstOrDefault(sp => sp.Children.OfType<Button>()
                    .Any(b => b.Tag is { } tagObj && 
                             tagObj.GetType().GetProperty("Channel")?.GetValue(tagObj) == channel));
            if (containerToRemove != null)
            {
                PrivateMessageList.Children.Remove(containerToRemove);
            }
            
            // If this was the current channel, switch to console
            if (channel == _currentChannel)
            {
                var consoleChannel = _channels.FirstOrDefault(c => c.Name == "console");
                if (consoleChannel != null)
                {
                    SwitchToChannel(consoleChannel);
                }
            }
        }

        private void HandleTopicMessage(IRCMessage message)
        {
            // RPL_TOPIC (332): Channel topic
            if (message.Parameters.Count >= 3)
            {
                var channelName = message.Parameters[1];
                var topic = message.Content ?? "";
                
                // Find the channel and update its topic
                var channel = _channels.FirstOrDefault(c => c.Name == channelName);
                if (channel != null)
                {
                    channel.Topic = topic;
                    UpdateChannelHeader();
                }
            }
        }

        private void HandleTopicTimeMessage(IRCMessage message)
        {
            // RPL_TOPICWHOTIME (333): Who set the topic and when
            if (message.Parameters.Count >= 4)
            {
                var channelName = message.Parameters[1];
                var topicSetBy = message.Parameters[2];
                var topicSetTime = message.Parameters[3];
                
                // Find the channel and update topic metadata
                var channel = _channels.FirstOrDefault(c => c.Name == channelName);
                if (channel != null)
                {
                    channel.TopicSetBy = topicSetBy;
                    
                    // Parse Unix timestamp
                    if (long.TryParse(topicSetTime, out long unixTimestamp))
                    {
                        channel.TopicSetDate = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
                    }
                    
                    UpdateChannelHeader();
                }
            }
        }

        private void UpdateChannelHeader()
        {
            if (_currentChannel != null)
            {
                // Update the chat header with topic information
                var headerText = GetDisplayChannelName(_currentChannel);
                
                if (!string.IsNullOrEmpty(_currentChannel.Topic))
                {
                    headerText += $" - {_currentChannel.Topic}";
                }
                
                ChatTitle.Text = headerText;
                
                // Update tooltip with detailed information
                var tooltipText = $"Channel: {_currentChannel.Name}";
                if (!string.IsNullOrEmpty(_currentChannel.Topic))
                {
                    tooltipText += $"\nTopic: {_currentChannel.Topic}";
                }
                if (_currentChannel.TopicSetBy != null && _currentChannel.TopicSetDate != null)
                {
                    var formattedDate = _currentChannel.TopicSetDate.Value.ToString("MMM dd, yyyy 'at' HH:mm");
                    tooltipText += $"\nSet by: {_currentChannel.TopicSetBy} on {formattedDate}";
                }
                
                ChatTitle.ToolTip = tooltipText;
            }
        }

        private void AddSystemMessage(string content)
        {
            var message = new ChatMessage
            {
                Sender = "System",
                Content = content,
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                SenderColor = Colors.Gray,
                Type = MessageType.System,
                IsUserMessage = false,
                IsOtherMessage = false,
                IsSystemMessage = true,
                CurrentUserNickname = _ircClient.Nickname
            };

            AddMessageToChannels(message);
        }

        private void AddSystemMessage(ChatMessage message)
        {
            AddMessageToChannels(message);
        }

        private void AddMessageToChannels(ChatMessage message)
        {
            // Always add to console
            if (!_channelMessages.ContainsKey("console"))
            {
                _channelMessages["console"] = new List<ChatMessage>();
            }
            _channelMessages["console"].Add(message);

            // Add to current channel if it exists
            if (_currentChannel != null)
            {
                if (!_channelMessages.ContainsKey(_currentChannel.Name))
                {
                    _channelMessages[_currentChannel.Name] = new List<ChatMessage>();
                }
                _channelMessages[_currentChannel.Name].Add(message);
            }

            // Show in current view
            MessageList.Items.Add(message);
            ScrollToBottom();
        }

        private void AddUser(User user)
        {
            if (!_users.Any(u => u.Nickname == user.Nickname))
            {
                _users.Add(user);
                SortAndRefreshUserList();
            }
        }

        private void RemoveUser(string nickname)
        {
            var user = _users.FirstOrDefault(u => u.Nickname == nickname);
            if (user != null)
            {
                _users.Remove(user);
                SortAndRefreshUserList();
            }
        }

        private void UpdateUserNick(string oldNick, string newNick)
        {
            var user = _users.FirstOrDefault(u => u.Nickname == oldNick);
            if (user != null)
            {
                user.Nickname = newNick;
                SortAndRefreshUserList();
            }
        }

        private void SortAndRefreshUserList()
        {
            // Clear the current user list UI
            UserList.Children.Clear();
            
            // Sort users: mode users first (alphabetically), then regular users (alphabetically)
            var sortedUsers = _users.OrderBy(u => GetUserSortPriority(u.Mode))
                                   .ThenBy(u => u.Nickname, StringComparer.OrdinalIgnoreCase)
                                   .ToList();
            
            // Add sorted users to the UI
            foreach (var user in sortedUsers)
            {
                AddUserButton(user);
            }
        }

        private int GetUserSortPriority(UserMode mode)
        {
            // Lower numbers = higher priority (appear first)
            if (mode.HasFlag(UserMode.Owner)) return 1;
            if (mode.HasFlag(UserMode.Admin)) return 2;
            if (mode.HasFlag(UserMode.Op)) return 3;
            if (mode.HasFlag(UserMode.HalfOp)) return 4;
            if (mode.HasFlag(UserMode.Voice)) return 5;
            return 6; // Regular users (no mode)
        }

        private void AddChannelButton(Channel channel)
        {
            // Create a container for the button and unread indicator
            var container = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 2, 8, 2)
            };

            var button = new Button
            {
                Content = $"{GetChannelIcon(channel)} {GetDisplayChannelName(channel)}",
                Style = (Style)FindResource("macOSmacOSNavigationItemStyle"),
                Tag = channel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            button.Click += (s, e) => SwitchToChannel(channel);
            
            // Add context menu for channel
            var contextMenu = new ContextMenu();
            
            if (channel.Type == ChannelType.Channel)
            {
                var leaveItem = new MenuItem { Header = "🚪 Leave Channel" };
                leaveItem.Click += (s, e) => _ = _ircClient.SendCommandAsync($"PART {channel.Name}");
                contextMenu.Items.Add(leaveItem);
                
                var whoItem = new MenuItem { Header = "👥 Who's Here" };
                whoItem.Click += (s, e) => _ = _ircClient.SendCommandAsync($"NAMES {channel.Name}");
                contextMenu.Items.Add(whoItem);
                
                var topicItem = new MenuItem { Header = "📋 View Topic" };
                topicItem.Click += (s, e) => _ = _ircClient.SendCommandAsync($"TOPIC {channel.Name}");
                contextMenu.Items.Add(topicItem);
            }
            else if (channel.Type == ChannelType.Private)
            {
                var closeItem = new MenuItem { Header = "❌ Close Chat" };
                closeItem.Click += (s, e) => ClosePrivateMessage(channel);
                contextMenu.Items.Add(closeItem);
            }
            
            button.ContextMenu = contextMenu;
            container.Children.Add(button);

            // Add unread message indicator (red dot)
            var unreadIndicator = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(Colors.Red),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = channel.HasUnreadMessages ? Visibility.Visible : Visibility.Collapsed
            };
            container.Children.Add(unreadIndicator);

            // Store references for updating unread count
            button.Tag = new { Channel = channel, UnreadIndicator = unreadIndicator };

            // Add to appropriate list based on channel type
            if (channel.Type == ChannelType.Channel)
            {
                ChannelList.Children.Add(container);
            }
            else if (channel.Type == ChannelType.Private)
            {
                PrivateMessageList.Children.Add(container);
            }
            else if (channel.Type == ChannelType.Console)
            {
                // Console goes at the top of the channels list
                ChannelList.Children.Insert(0, container);
            }
        }

        private void AddChannelTab(Channel channel)
        {
            var tab = new TabItem
            {
                Header = $"{GetChannelIcon(channel)} {GetDisplayChannelName(channel)}",
                Tag = channel.Name
            };

            // Tab management removed - using Office 365-style navigation instead
        }

        private void AddUserButton(User user)
        {
            var modePrefix = GetModePrefix(user.Mode);
            var displayName = $"{modePrefix}{user.Nickname}";
            
            var button = new Button
            {
                Content = displayName,
                Style = (Style)FindResource("macOSmacOSNavigationItemStyle"),
                Tag = user
            };
            button.MouseDoubleClick += (s, e) => StartPrivateMessage(user);
            button.Click += (s, e) => 
            {
                _selectedUser = user.Nickname;
            };
            
            // Use the enhanced context menu from XAML
            button.ContextMenu = (ContextMenu)FindResource("UserContextMenu");
            UserList.Children.Add(button);
        }

        private void RemoveUserButton(User user)
        {
            var button = UserList.Children.OfType<Button>().FirstOrDefault(b => b.Tag == user);
            if (button != null)
            {
                UserList.Children.Remove(button);
            }
        }

        private void UpdateUserButton(User user)
        {
            var button = UserList.Children.OfType<Button>().FirstOrDefault(b => b.Tag == user);
            if (button != null)
            {
                var modePrefix = GetModePrefix(user.Mode);
                var displayName = $"{modePrefix}{user.Nickname}";
                button.Content = displayName;
            }
        }

        private string GetChannelIcon(Channel channel)
        {
            return channel.Type switch
            {
                ChannelType.Channel => "💬", // Use a nice chat icon instead of #
                ChannelType.Private => "👤", // Use a person icon for private messages
                _ => "💬"
            };
        }

        private string GetDisplayChannelName(Channel channel)
        {
            // Remove prefixes for display since we're using icons and proper organization
            var name = channel.Name;
            if (name.StartsWith("#"))
            {
                name = name.Substring(1);
            }
            else if (name.StartsWith("PM:"))
            {
                name = name.Substring(3); // Remove "PM:" prefix
            }
            return name;
        }

        private Color GetUserColor(string nickname)
        {
            var hash = nickname.GetHashCode();
            var colors = new[]
            {
                Colors.Red, Colors.Blue, Colors.Green, Colors.Orange, Colors.Purple,
                Colors.Teal, Colors.Brown, Colors.Pink, Colors.Indigo, Colors.Cyan
            };
            return colors[Math.Abs(hash) % colors.Length];
        }

        private void SwitchToChannel(Channel channel)
        {
            _currentChannel = channel;
            
            // Clear unread count when switching to a channel
            channel.UnreadCount = 0;
            UpdateUnreadIndicator(channel);
            
            // Update chat header
            ChatIcon.Text = GetChannelIcon(channel);
            UpdateChannelHeader();
            
            // Update button highlighting
            UpdateChannelButtonHighlighting();
            
            MessageList.Items.Clear();
            if (_channelMessages.ContainsKey(channel.Name))
            {
                foreach (var message in _channelMessages[channel.Name])
                {
                    MessageList.Items.Add(message);
                }
            }
            ScrollToBottom();
        }

        private void UpdateUnreadIndicator(Channel channel)
        {
            // Find the button for this channel and update its unread indicator
            var allButtons = ChannelList.Children.OfType<StackPanel>()
                .Concat(PrivateMessageList.Children.OfType<StackPanel>())
                .SelectMany(sp => sp.Children.OfType<Button>())
                .Where(b => b.Tag is { } tagObj && 
                           tagObj.GetType().GetProperty("Channel")?.GetValue(tagObj) == channel);

            foreach (var button in allButtons)
            {
                if (button.Tag is { } tagObj)
                {
                    var unreadIndicator = tagObj.GetType().GetProperty("UnreadIndicator")?.GetValue(tagObj) as Ellipse;
                    if (unreadIndicator != null)
                    {
                        unreadIndicator.Visibility = channel.HasUnreadMessages ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }

        private void UpdateChannelButtonHighlighting()
        {
            // Reset all channel buttons to normal style
            var allChannelButtons = ChannelList.Children.OfType<StackPanel>()
                .Concat(PrivateMessageList.Children.OfType<StackPanel>())
                .SelectMany(sp => sp.Children.OfType<Button>());
            
            foreach (Button button in allChannelButtons)
            {
                button.Style = (Style)FindResource("macOSmacOSNavigationItemStyle");
            }
            
            // Reset all user buttons to normal style
            foreach (Button button in UserList.Children.OfType<Button>())
            {
                button.Style = (Style)FindResource("macOSmacOSNavigationItemStyle");
            }
            
            // Highlight the current channel button
            if (_currentChannel != null)
            {
                var currentButton = allChannelButtons
                    .FirstOrDefault(b => b.Tag is { } tagObj && 
                                       tagObj.GetType().GetProperty("Channel")?.GetValue(tagObj) == _currentChannel);
                if (currentButton != null)
                {
                    // Create a highlighted style
                    var highlightedStyle = new Style(typeof(Button), (Style)FindResource("macOSNavigationItemStyle"));
                    var backgroundSetter = new Setter(Button.BackgroundProperty, new SolidColorBrush(Colors.LightBlue));
                    var foregroundSetter = new Setter(Button.ForegroundProperty, new SolidColorBrush(Colors.DarkBlue));
                    highlightedStyle.Setters.Add(backgroundSetter);
                    highlightedStyle.Setters.Add(foregroundSetter);
                    currentButton.Style = highlightedStyle;
                }
                
                // If it's a private message, also highlight the corresponding user button
                if (_currentChannel.Type == ChannelType.Private && _currentChannel.Name.StartsWith("PM:"))
                {
                    var userNickname = _currentChannel.Name.Substring(3); // Remove "PM:" prefix
                    var userButton = UserList.Children.OfType<Button>()
                        .FirstOrDefault(b => b.Tag is User user && user.Nickname == userNickname);
                    if (userButton != null)
                    {
                        var highlightedStyle = new Style(typeof(Button), (Style)FindResource("macOSNavigationItemStyle"));
                        var backgroundSetter = new Setter(Button.BackgroundProperty, new SolidColorBrush(Colors.LightGreen));
                        var foregroundSetter = new Setter(Button.ForegroundProperty, new SolidColorBrush(Colors.DarkGreen));
                        highlightedStyle.Setters.Add(backgroundSetter);
                        highlightedStyle.Setters.Add(foregroundSetter);
                        userButton.Style = highlightedStyle;
                    }
                }
            }
        }

        private (string nickname, UserMode mode) ParseUserWithMode(string userWithMode)
        {
            var mode = UserMode.None;
            var nickname = userWithMode;
            
            // Parse IRC user modes from the beginning of the nickname
            while (nickname.Length > 0 && IsModePrefix(nickname[0]))
            {
                var prefix = nickname[0];
                mode |= GetUserModeFromPrefix(prefix);
                nickname = nickname.Substring(1);
            }
            
            return (nickname, mode);
        }

        private bool IsModePrefix(char c)
        {
            return c == '@' || c == '+' || c == '%' || c == '&' || c == '~';
        }

        private UserMode GetUserModeFromPrefix(char prefix)
        {
            return prefix switch
            {
                '+' => UserMode.Voice,
                '%' => UserMode.HalfOp,
                '@' => UserMode.Op,
                '&' => UserMode.Admin,
                '~' => UserMode.Owner,
                _ => UserMode.None
            };
        }

        private string GetModePrefix(UserMode mode)
        {
            // Return the highest priority mode prefix
            if (mode.HasFlag(UserMode.Owner)) return "~";
            if (mode.HasFlag(UserMode.Admin)) return "&";
            if (mode.HasFlag(UserMode.Op)) return "@";
            if (mode.HasFlag(UserMode.HalfOp)) return "%";
            if (mode.HasFlag(UserMode.Voice)) return "+";
            return "";
        }

        private void HandleModeChange(IRCMessage message)
        {
            if (message.Parameters.Count < 3) return;
            
            var channel = message.Parameters[0];
            var modeString = message.Parameters[1];
            var target = message.Parameters[2];
            
            // Only handle user mode changes in the current channel
            if (channel != _currentChannel?.Name) return;
            
            var user = _users.FirstOrDefault(u => u.Nickname == target);
            if (user == null) return;
            
            var isAdding = modeString.StartsWith('+');
            var modeChar = modeString.Length > 1 ? modeString[1] : '\0';
            
            var modeChange = GetUserModeFromPrefix(modeChar);
            if (modeChange == UserMode.None) return;
            
            if (isAdding)
            {
                user.Mode |= modeChange;
                AddSystemMessage($"🔧 {target} was given {GetModeDescription(modeChange)} by {message.Sender}");
            }
            else
            {
                user.Mode &= ~modeChange;
                AddSystemMessage($"🔧 {target} lost {GetModeDescription(modeChange)} by {message.Sender}");
            }
            
            // Refresh the user list to maintain proper sorting
            SortAndRefreshUserList();
        }

        private string GetModeDescription(UserMode mode)
        {
            return mode switch
            {
                UserMode.Voice => "voice (+)",
                UserMode.HalfOp => "half-op (%)",
                UserMode.Op => "operator (@)",
                UserMode.Admin => "admin (&)",
                UserMode.Owner => "owner (~)",
                _ => "unknown mode"
            };
        }

        private void StartPrivateMessage(User user)
        {
            var pmChannel = $"PM:{user.Nickname}";
            var channel = _channels.FirstOrDefault(c => c.Name == pmChannel);
            if (channel == null)
            {
                channel = new Channel { Name = pmChannel, Type = ChannelType.Private };
                _channels.Add(channel);
                AddChannelButton(channel);
            }
            SwitchToChannel(channel);
        }

        private void ScrollToBottom()
        {
            MessageScrollViewer.ScrollToEnd();
        }

        private void UpdateUI()
        {
            JoinChannelButton.IsEnabled = _ircClient.IsConnected;
            MessageTextBox.IsEnabled = _ircClient.IsConnected;
            SendButton.IsEnabled = _ircClient.IsConnected;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ircClient.IsConnected)
            {
                AddSystemMessage("🔄 Disconnecting from server...");
                await _ircClient.DisconnectAsync();
            }
            else
            {
                var dialog = new ServerListDialog(_serverListService);
                if (dialog.ShowDialog() == true && dialog.SelectedServer != null)
                {
                    var server = dialog.SelectedServer;
                    AddSystemMessage($"🔄 Attempting to connect to {server.Host}:{server.Port} {(server.UseSSL ? "(SSL)" : "")}...");
                    AddSystemMessage($"📝 Using nickname: {server.Nickname ?? "Y0daiiUser"}");
                    
                    // Test connectivity first
                    AddSystemMessage("🔍 Testing connectivity...");
            var canConnect = await IRCClient.TestConnectivityAsync(server.Host, server.Port, 10);
            if (!canConnect)
            {
                AddSystemMessage("❌ Cannot reach server. Please check your internet connection and server details.");
                MessageBox.Show($"Cannot reach server {server.Host}:{server.Port}. Please check your internet connection and server details.", "Connection Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            AddSystemMessage("✅ Server is reachable, establishing IRC connection...");
            
            // Test ident server connectivity if ident is enabled
            var settings = AppSettings.Load();
            if (settings.Connection.EnableIdentServer)
            {
                // First test if we can use the standard ident port 113
                AddSystemMessage("🔍 Testing standard ident port 113...");
                var standardIdentAvailable = await IRCClient.TestStandardIdentPortAsync();
                
                if (!standardIdentAvailable)
                {
                    AddSystemMessage("⚠️ Standard ident port 113 not available, testing port 1130...");
                    var identPortAvailable = await IRCClient.TestIdentServerConnectivityAsync(1130);
                    if (!identPortAvailable)
                    {
                        AddSystemMessage("❌ No ident server ports available. Consider disabling ident server in Settings.");
                        AddSystemMessage("💡 Tip: Run as Administrator to use standard ident port 113");
                    }
                    else
                    {
                        AddSystemMessage("✅ Ident server port 1130 is available (non-standard)");
                    }
                }
                else
                {
                    AddSystemMessage("✅ Standard ident port 113 is available");
                }
            }
            else
            {
                AddSystemMessage("🔧 Ident server disabled in Settings (modern approach)");
            }
                    var success = await _ircClient.ConnectWithFallbackAsync(
                        server.Host, server.Port, server.Nickname ?? "Y0daiiUser", 
                        server.Username ?? "y0daii", server.RealName ?? "y0daii IRC User",
                        server.UseSSL, null, server.IdentServer, server.IdentPort);
                    
                    if (!success)
                    {
                        AddSystemMessage("❌ Failed to establish IRC connection. Check server settings and try again.");
                        MessageBox.Show("Failed to establish IRC connection. This could be due to:\n\n• Incorrect server address or port\n• Server is down or unreachable\n• Network firewall blocking the connection\n• SSL/TLS configuration issues\n\nPlease check your server settings and try again.", "Connection Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow();
            settings.ShowDialog();
        }

        private async void JoinChannelButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new JoinChannelDialog();
            if (dialog.ShowDialog() == true)
            {
                AddSystemMessage($"🔄 Joining channel {dialog.ChannelName}...");
                await _ircClient.JoinChannelAsync(dialog.ChannelName);
                
                var channel = new Channel { Name = dialog.ChannelName, Type = ChannelType.Channel };
                _channels.Add(channel);
                AddChannelButton(channel);
                SwitchToChannel(channel);
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                await SendMessage();
            }
            else if (e.Key == Key.Up)
            {
                e.Handled = true;
                NavigateHistory(-1);
            }
            else if (e.Key == Key.Down)
            {
                e.Handled = true;
                NavigateHistory(1);
            }
        }

        private void NavigateHistory(int direction)
        {
            if (_commandHistory.Count == 0) return;

            if (direction == -1) // Up arrow
            {
                if (_historyIndex < _commandHistory.Count - 1)
                {
                    _historyIndex++;
                    MessageTextBox.Text = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
                    MessageTextBox.CaretIndex = MessageTextBox.Text.Length;
                }
            }
            else if (direction == 1) // Down arrow
            {
                if (_historyIndex > 0)
                {
                    _historyIndex--;
                    MessageTextBox.Text = _commandHistory[_commandHistory.Count - 1 - _historyIndex];
                    MessageTextBox.CaretIndex = MessageTextBox.Text.Length;
                }
                else if (_historyIndex == 0)
                {
                    _historyIndex = -1;
                    MessageTextBox.Clear();
                }
            }
        }

        private async Task SendMessage()
        {
            var message = MessageTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            // Add to command history
            if (!_commandHistory.Contains(message))
            {
                _commandHistory.Add(message);
                // Keep only last 100 commands
                if (_commandHistory.Count > 100)
                {
                    _commandHistory.RemoveAt(0);
                }
            }
            _historyIndex = -1; // Reset history index

            MessageTextBox.Clear();

            if (message.StartsWith("/"))
            {
                // Special handling for /me command to show immediately
                if (message.StartsWith("/me "))
                {
                    var action = message.Substring(4); // Remove "/me "
                    if (_currentChannel != null)
                    {
                        var userNick = _ircClient.Nickname ?? "You";
                        AddChannelAction(_currentChannel.Name, userNick, action);
                    }
                }
                
                var handled = await _commandProcessor.ProcessCommandAsync(message, _currentChannel);
                if (!handled)
                {
                    AddSystemMessage($"Unknown command: {message}");
                }
            }
            else
            {
                if (_currentChannel != null)
                {
                    // Immediately show the message as coming from the user
                    var userNick = _ircClient.Nickname ?? "You";
                    AddChannelMessage(_currentChannel.Name, userNick, message);
                    
                    // Then send it to the server
                    var target = _currentChannel.Name;
                    if (_currentChannel.Type == ChannelType.Private && target.StartsWith("PM:"))
                    {
                        // For private messages, extract the actual nickname
                        target = target.Substring(3); // Remove "PM:" prefix
                    }
                    await _ircClient.SendMessageAsync(target, message);
                }
                else
                {
                    AddSystemMessage("No channel selected. Join a channel first.");
                }
            }
        }

        private void ChatTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Tab selection handling removed - using Office 365-style navigation instead
            // This method is kept for compatibility but functionality moved to navigation pane
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var aboutDialog = new AboutDialog();
            aboutDialog.Owner = this;
            aboutDialog.ShowDialog();
        }

        private void ShortcutsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var shortcuts = """
                Keyboard Shortcuts:
                
                General:
                • Ctrl+N - New connection
                • Ctrl+J - Join channel
                • Ctrl+Q - Quit application
                • F1 - Show help
                
                Chat:
                • Enter - Send message
                • Shift+Enter - New line in message
                • Ctrl+A - Select all text
                • Ctrl+C - Copy selected text
                • Ctrl+V - Paste text
                
                Navigation:
                • Ctrl+Tab - Switch between channels
                • Ctrl+1-9 - Switch to channel by number
                • Alt+Left/Right - Navigate message history
                
                IRC Commands:
                • /help - Show command help
                • /join #channel - Join a channel
                • /part - Leave current channel
                • /msg user message - Send private message
                • /nick newname - Change nickname
                • /quit - Disconnect from server
                """;
            
            MessageBox.Show(shortcuts, "Keyboard Shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CommandsHelpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var commands = """
                IRC Commands Help:
                
                Connection Commands:
                • /connect <server> [port] - Connect to IRC server
                • /disconnect - Disconnect from server
                • /reconnect - Reconnect to current server
                
                Channel Commands:
                • /join <channel> [password] - Join a channel
                • /part [channel] [reason] - Leave a channel
                • /topic [new topic] - Get or set channel topic
                • /list [channels] - List channels on server
                • /names [channel] - List users in channel
                
                User Commands:
                • /nick <newnick> - Change your nickname
                • /whois <nickname> - Get user information
                • /msg <nickname> <message> - Send private message
                • /notice <nickname> <message> - Send notice
                • /me <action> - Send action message
                
                Channel Management:
                • /op <nickname> - Give operator status
                • /deop <nickname> - Remove operator status
                • /voice <nickname> - Give voice status
                • /kick <nickname> [reason] - Kick user
                • /ban <nickname> - Ban user
                • /unban <nickname> - Unban user
                
                Server Commands:
                • /ping - Ping server
                • /time - Get server time
                • /version - Get server version
                • /motd - Get message of the day
                • /lusers - Get user statistics
                
                Utility Commands:
                • /clear - Clear current chat
                • /help [command] - Show help for command
                • /raw <command> - Send raw IRC command
                • /servers - Show saved servers
                • /addserver <name> <host> [port] - Add server
                """;
            
            MessageBox.Show(commands, "IRC Commands Help", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void CheckForUpdatesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var updateDialog = new UpdateDialog();
            updateDialog.Owner = this;
            updateDialog.ShowDialog();
        }

        private void GitHubMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/drakkcoil/y0daii",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open GitHub repository: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DCCTransferButton_Click(object sender, RoutedEventArgs e)
        {
            var dccDialog = new DCCTransferDialog(_dccService);
            dccDialog.Owner = this;
            dccDialog.ShowDialog();
        }

        // Context Menu Handlers
        private string? _selectedUser;
        private string? _selectedChannel;

        private void UserContextMenu_PrivateMessage_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedUser))
            {
                var channel = new Channel { Name = _selectedUser, Type = ChannelType.Private };
                SwitchToChannel(channel);
            }
        }

        private async void UserContextMenu_Whois_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedUser) && _ircClient.IsConnected)
            {
                await _ircClient.SendCommandAsync($"WHOIS {_selectedUser}");
            }
        }

        private async void UserContextMenu_Version_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedUser) && _ircClient.IsConnected)
            {
                await _ircClient.SendCTCPAsync(_selectedUser, "VERSION");
            }
        }

        private async void UserContextMenu_Time_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedUser) && _ircClient.IsConnected)
            {
                await _ircClient.SendCTCPAsync(_selectedUser, "TIME");
            }
        }

        private async void UserContextMenu_Ping_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedUser) && _ircClient.IsConnected)
            {
                await _ircClient.SendCTCPAsync(_selectedUser, "PING", DateTime.Now.Ticks.ToString());
            }
        }

        private void UserContextMenu_SendFile_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedUser))
            {
                // Open file dialog to select file to send
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = $"Send File to {_selectedUser}",
                    Filter = "All Files (*.*)|*.*",
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var filePath = openFileDialog.FileName;
                    var fileName = System.IO.Path.GetFileName(filePath);
                    var fileSize = new System.IO.FileInfo(filePath).Length;
                    
                    // Open DCC transfer dialog for file sending
                    var dccDialog = new DCCTransferDialog(_dccService);
                    dccDialog.Owner = this;
                    dccDialog.SetRecipient(_selectedUser);
                    dccDialog.SetFileToSend(filePath, fileName, fileSize);
                    dccDialog.ShowDialog();
                }
            }
        }

        private void UserContextMenu_CopyNickname_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedUser))
            {
                try
                {
                    System.Windows.Clipboard.SetText(_selectedUser);
                    AddSystemMessage($"Copied nickname: {_selectedUser}");
                }
                catch (Exception ex)
                {
                    AddSystemMessage($"Failed to copy nickname: {ex.Message}");
                }
            }
        }

        private void UserContextMenu_Ignore_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedUser))
            {
                // TODO: Implement ignore functionality
                AddSystemMessage($"Ignore functionality for {_selectedUser} not yet implemented");
            }
        }

        private async void UserContextMenu_Kick_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedUser) && _ircClient.IsConnected && _currentChannel != null)
            {
                var reason = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Enter kick reason for {_selectedUser}:", 
                    "Kick User", 
                    "Kicked by user");
                
                if (!string.IsNullOrEmpty(reason))
                {
                    await _ircClient.SendCommandAsync($"KICK {_currentChannel.Name} {_selectedUser} :{reason}");
                }
            }
        }

        private async void UserContextMenu_Ban_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedUser) && _ircClient.IsConnected && _currentChannel != null)
            {
                var reason = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Enter ban reason for {_selectedUser}:", 
                    "Ban User", 
                    "Banned by user");
                
                if (!string.IsNullOrEmpty(reason))
                {
                    // Ban the user and then kick them
                    await _ircClient.SendCommandAsync($"MODE {_currentChannel.Name} +b {_selectedUser}!*@*");
                    await _ircClient.SendCommandAsync($"KICK {_currentChannel.Name} {_selectedUser} :{reason}");
                }
            }
        }

        private void ChannelContextMenu_Join_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedChannel))
            {
                var joinDialog = new JoinChannelDialog();
                joinDialog.Owner = this;
                if (joinDialog.ShowDialog() == true)
                {
                    _ = Task.Run(async () => await _ircClient.JoinChannelAsync(joinDialog.ChannelName));
                }
            }
        }

        private async void ChannelContextMenu_Leave_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedChannel) && _ircClient.IsConnected)
            {
                await _ircClient.LeaveChannelAsync(_selectedChannel);
            }
        }

        private void ChannelContextMenu_Settings_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement channel settings dialog
            MessageBox.Show("Channel settings not yet implemented.", "Information", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MessageContextMenu_Copy_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement message copying
            MessageBox.Show("Message copying not yet implemented.", "Information", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MessageContextMenu_CopyTimestamp_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement timestamp copying
            MessageBox.Show("Timestamp copying not yet implemented.", "Information", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MessageContextMenu_PrivateMessage_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement private message from message context
            MessageBox.Show("Private message from message context not yet implemented.", "Information", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void MessageContextMenu_Whois_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement whois from message context
            MessageBox.Show("Whois from message context not yet implemented.", "Information", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
