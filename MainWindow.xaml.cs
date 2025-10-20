using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        private TabItem? _consoleTab;

        public MainWindow()
        {
            InitializeComponent();
            _ircClient = new IRCClient();
            _channels = new List<Channel>();
            _users = new List<User>();
            _channelMessages = new Dictionary<string, List<ChatMessage>>();
            _serverListService = new ServerListService();
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
            _commandProcessor.CommandExecuted += OnCommandExecuted;
            _commandProcessor.CommandError += OnCommandError;
        }

        private void InitializeConsole()
        {
            _consoleTab = ChatTabs.Items.Cast<TabItem>().FirstOrDefault(t => t.Tag?.ToString() == "console");
            if (_consoleTab != null)
            {
                AddSystemMessage("Welcome to Y0daii IRC Client! Type /help for available commands.");
            }
        }

        private void OnIRCMessageReceived(object? sender, IRCMessage message)
        {
            Dispatcher.Invoke(() =>
            {
                HandleIRCMessage(message);
            });
        }

        private void OnConnectionStatusChanged(object? sender, string status)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;
                ConnectButton.Content = status == "Connected" ? "Disconnect" : "Connect";
                
                // Update status indicator
                if (status == "Connected")
                {
                    StatusIndicator.Fill = new SolidColorBrush(Colors.Green);
                    ConnectionInfo.Text = $"Connected to {_ircClient.Server}:{_ircClient.Port}";
                }
                else
                {
                    StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                    ConnectionInfo.Text = "";
                }
                
                UpdateUI();
            });
        }

        private void OnErrorOccurred(object? sender, Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                AddSystemMessage($"Error: {ex.Message}");
            });
        }

        private void OnCommandExecuted(object? sender, string command)
        {
            Dispatcher.Invoke(() =>
            {
                AddSystemMessage(command);
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
                _ = Task.Run(async () => await _ircClient.SendCommandAsync($"PONG {message.Parameters.FirstOrDefault()}"));
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
                AddSystemMessage($"{user} joined {channel}");
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
            else if (message.IsNumeric)
            {
                HandleNumericMessage(message);
            }
        }

        private void HandleNumericMessage(IRCMessage message)
        {
            switch (message.Command)
            {
                case "001": // Welcome message
                    AddSystemMessage($"Connected to {_ircClient.Server}");
                    break;
                case "353": // RPL_NAMREPLY - User list
                    var channel = message.Parameters[2];
                    var users = message.Parameters[3].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var user in users)
                    {
                        var cleanUser = user.TrimStart('@', '+', '%', '&', '~');
                        AddUser(new User { Nickname = cleanUser });
                    }
                    break;
                case "366": // RPL_ENDOFNAMES
                    // End of user list
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

            ChatTabs.Items.Add(tab);
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
            ChatIcon.Kind = channel.Type == ChannelType.Channel ? MaterialDesignThemes.Wpf.PackIconKind.Hash : MaterialDesignThemes.Wpf.PackIconKind.Message;
            
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
                await _ircClient.DisconnectAsync();
            }
            else
            {
                var dialog = new ServerListDialog(_serverListService);
                if (dialog.ShowDialog() == true && dialog.SelectedServer != null)
                {
                    var server = dialog.SelectedServer;
                    var success = await _ircClient.ConnectAsync(
                        server.Host, server.Port, server.Nickname ?? "Y0daiiUser", 
                        server.Username ?? "y0daii", server.RealName ?? "Y0daii IRC User",
                        server.IdentServer, server.IdentPort);
                    
                    if (!success)
                    {
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
            if (ChatTabs.SelectedItem is TabItem selectedTab)
            {
                var tag = selectedTab.Tag?.ToString();
                if (tag == "console")
                {
                    _currentChannel = null;
                    MessageList.Items.Clear();
                    // Show console messages
                    if (_channelMessages.ContainsKey("console"))
                    {
                        foreach (var message in _channelMessages["console"])
                        {
                            MessageList.Items.Add(message);
                        }
                    }
                }
                else if (tag != null)
                {
                    var channel = _channels.FirstOrDefault(c => c.Name == tag);
                    if (channel != null)
                    {
                        SwitchToChannel(channel);
                    }
                }
            }
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
    }
}
