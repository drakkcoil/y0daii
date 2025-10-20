using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
        private ServerListService _serverListService;
        private CommandProcessor _commandProcessor;
        private DCCService _dccService;
        // private TabItem? _consoleTab; // Removed - using Office 365-style navigation

        public MainWindow()
        {
            InitializeComponent();
            _ircClient = new IRCClient();
            _channels = new List<Channel>();
            _users = new List<User>();
            _channelMessages = new Dictionary<string, List<ChatMessage>>();
            _serverListService = new ServerListService();
            _dccService = new DCCService();
            _commandProcessor = new CommandProcessor(_ircClient, _serverListService);

            SetupEventHandlers();
            InitializeConsole();
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

        private void InitializeConsole()
        {
            // Console is now the default view, no need for tab management
            AddSystemMessage("Welcome to Y0daii IRC Client! Type /help for available commands.");
        }

        private void OnIRCMessageReceived(object? sender, IRCMessage message)
        {
            Console.WriteLine($"MainWindow.OnIRCMessageReceived: Received message: {message.Command}");
            Dispatcher.Invoke(() =>
            {
                // Log incoming IRC messages for debugging
                AddSystemMessage($"← {message.Command} {string.Join(" ", message.Parameters)}");
                
                HandleIRCMessage(message);
            });
        }

        private void OnConnectionStatusChanged(object? sender, string status)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;
                ConnectButton.Content = status == "Connected" ? "Disconnect" : "Connect";
                
                // Log connection status changes to console
                if (status == "Connected")
                {
                    StatusIndicator.Fill = new SolidColorBrush(Colors.Green);
                    ConnectionInfo.Text = $"Connected to {_ircClient.Server}:{_ircClient.Port}";
                    AddSystemMessage($"✓ Connected to {_ircClient.Server}:{_ircClient.Port} {(status.Contains("SSL") ? "(SSL)" : "")}");
                }
                else if (status == "Disconnected")
                {
                    StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                    ConnectionInfo.Text = "";
                    AddSystemMessage("✗ Disconnected from server");
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
                // Don't log sensitive commands like PASS
                if (!command.StartsWith("PASS"))
                {
                    AddSystemMessage($"→ {command}");
                }
                else
                {
                    AddSystemMessage("→ PASS ***");
                }
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
            else if (message.IsJoin)
            {
                var channel = message.Target;
                var user = message.Sender;
                if (user == _ircClient.Nickname)
                {
                    AddSystemMessage($"✅ Successfully joined {channel}");
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
                if (channel == _currentChannel?.Name)
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
                // Don't automatically disconnect on ERROR - let the server handle it
                // Some servers send ERROR messages that don't require disconnection
                Console.WriteLine($"Server sent ERROR message: {message.Content}");
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
                            var cleanUser = user.TrimStart('@', '+', '%', '&', '~');
                            AddUser(new User { Nickname = cleanUser });
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

            var message = new ChatMessage
            {
                Sender = sender,
                Content = displayContent,
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                SenderColor = GetUserColor(sender)
            };

            _channelMessages[channel].Add(message);

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

        private void AddSystemMessage(string content)
        {
            var message = new ChatMessage
            {
                Sender = "System",
                Content = content,
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                SenderColor = Colors.Gray
            };

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
                AddUserButton(user);
            }
        }

        private void RemoveUser(string nickname)
        {
            var user = _users.FirstOrDefault(u => u.Nickname == nickname);
            if (user != null)
            {
                _users.Remove(user);
                RemoveUserButton(user);
            }
        }

        private void UpdateUserNick(string oldNick, string newNick)
        {
            var user = _users.FirstOrDefault(u => u.Nickname == oldNick);
            if (user != null)
            {
                user.Nickname = newNick;
                UpdateUserButton(user);
            }
        }

        private void AddChannelButton(Channel channel)
        {
            var button = new Button
            {
                Content = $"{GetChannelIcon(channel)} {channel.Name}",
                Style = (Style)FindResource("NavigationItemStyle"),
                Tag = channel
            };
            button.Click += (s, e) => SwitchToChannel(channel);
            ChannelList.Children.Add(button);
        }

        private void AddChannelTab(Channel channel)
        {
            var tab = new TabItem
            {
                Header = $"{GetChannelIcon(channel)} {channel.Name}",
                Tag = channel.Name
            };

            // Tab management removed - using Office 365-style navigation instead
        }

        private void AddUserButton(User user)
        {
            var button = new Button
            {
                Content = user.Nickname,
                Style = (Style)FindResource("NavigationItemStyle"),
                Tag = user
            };
            button.Click += (s, e) => StartPrivateMessage(user);
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
                button.Content = user.Nickname;
            }
        }

        private string GetChannelIcon(Channel channel)
        {
            return channel.Type switch
            {
                ChannelType.Channel => "#",
                ChannelType.Private => "💬",
                _ => "#"
            };
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
            
            // Update chat header
            ChatTitle.Text = channel.Name;
            ChatIcon.Kind = channel.Type == ChannelType.Channel ? MaterialDesignThemes.Wpf.PackIconKind.Pound : MaterialDesignThemes.Wpf.PackIconKind.Message;
            
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
                    
                    var success = await _ircClient.ConnectAsync(
                        server.Host, server.Port, server.Nickname ?? "Y0daiiUser", 
                        server.Username ?? "y0daii", server.RealName ?? "Y0daii IRC User",
                        server.UseSSL, null, server.IdentServer, server.IdentPort);
                    
                    if (!success)
                    {
                        AddSystemMessage("❌ Failed to connect to IRC server.");
                        MessageBox.Show("Failed to connect to IRC server.", "Connection Error", 
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
        }

        private async Task SendMessage()
        {
            var message = MessageTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            MessageTextBox.Clear();

            if (message.StartsWith("/"))
            {
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
                    await _ircClient.SendMessageAsync(_currentChannel.Name, message);
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
