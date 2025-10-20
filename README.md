# Y0daii IRC Client

[![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)](https://github.com/drakkcoil/y0daii)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

A modern, full-featured IRC client built with C# and WPF, featuring a beautiful Office 365-style user interface.

## Features

### Core IRC Functionality
- ✅ Connect to IRC servers with SSL/TLS support
- ✅ Join and leave channels
- ✅ Send and receive private messages
- ✅ User list display
- ✅ Real-time message display with timestamps
- ✅ Command support (/join, /part, /nick, /quit, etc.)

### User Interface
- ✅ Modern Material Design interface
- ✅ Dark theme with customizable colors
- ✅ Responsive layout with resizable panels
- ✅ Channel and user lists with easy navigation
- ✅ Rich message formatting with user colors

### Advanced Features
- ✅ Connection management with status indicators
- ✅ Settings and configuration management
- ✅ Notification system
- ✅ Auto-reconnection support
- ✅ Message logging capabilities

## Screenshots

The client features a clean, modern interface with:
- Server connection dialog
- Channel list on the left
- Main chat area in the center
- User list on the right
- Settings window for customization

## Getting Started

### Prerequisites
- .NET 8.0 or later
- Windows 10/11

### Installation
1. Clone the repository
2. Open the solution in Visual Studio or your preferred IDE
3. Restore NuGet packages
4. Build and run the application

### Quick Start
1. **Download**: Clone or download the repository
2. **Build**: Run `dotnet build` in the project directory
3. **Run**: Execute `dotnet run` or run the built executable
4. **Connect**: Click "Connect" and select a server from the list
5. **Chat**: Join channels and start chatting!

### Usage
1. Click "Connect" to open the connection dialog
2. Enter server details (default: irc.libera.chat:6667)
3. Set your nickname and other details
4. Click "Connect" to join the IRC network
5. Use "Join Channel" to enter channels
6. Start chatting!

## Configuration

The application stores settings in `%APPDATA%\Y0daiiIRC\settings.json` including:
- Appearance settings (theme, colors)
- Connection defaults
- Notification preferences
- Behavior settings

## IRC Commands

The client supports standard IRC commands:
- `/join #channel` - Join a channel
- `/part #channel` - Leave a channel
- `/nick newnick` - Change nickname
- `/quit [message]` - Disconnect with optional message
- `/msg user message` - Send private message
- `/notice user message` - Send notice

## Architecture

### Core Components
- **IRCClient**: Handles IRC protocol communication
- **IRCMessage**: Represents IRC messages with parsing
- **MainWindow**: Main UI with chat interface
- **Models**: Data structures for channels, users, and messages
- **Configuration**: Settings management system

### Design Patterns
- MVVM-inspired architecture
- Event-driven communication
- Async/await for network operations
- Material Design principles for UI

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## Version History

### Version 1.0.0 (Current)
- Initial release
- Modern Office 365-style interface
- Full IRC protocol support
- mIRC-style slash commands
- ANSI color support
- Server list management
- Ident server support
- Tabbed interface with console
- Comprehensive help system

## License

This project is open source and available under the MIT License.

## Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

## Support

For issues, feature requests, or questions:
- Open an issue on [GitHub](https://github.com/drakkcoil/y0daii/issues)
- Check the Help menu in the application
- Review the documentation in this README

## Roadmap

Future enhancements may include:
- [ ] Multiple server connections
- [ ] Message history and search
- [ ] File transfer support
- [ ] Plugin system
- [ ] Cross-platform support
- [ ] Voice/video chat integration
- [ ] Advanced scripting support

## Support

For issues, feature requests, or questions, please open an issue on GitHub.
