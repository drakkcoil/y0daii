# y0daii IRC Client

[![Version](https://img.shields.io/badge/version-1.0.7-blue.svg)](https://github.com/drakkcoil/y0daii)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)

A modern, full-featured IRC client built with C# and WPF, featuring a beautiful Material Design interface and comprehensive IRC functionality.

## ✨ Features

### 🚀 Core IRC Functionality
- ✅ **Multi-Server Support** - Connect to multiple IRC networks
- ✅ **SSL/TLS Encryption** - Secure connections with certificate validation
- ✅ **Channel Management** - Join, leave, and manage channels with visual indicators
- ✅ **Private Messages** - Full private messaging with dedicated tabs
- ✅ **User Management** - Real-time user lists with operator status indicators
- ✅ **Message History** - Persistent message storage across sessions
- ✅ **Command Support** - Complete IRC command set with slash commands
- ✅ **CTCP Support** - Client-to-Client Protocol for /me actions and more

### 🎨 Modern User Interface
- ✅ **Material Design** - Beautiful, modern interface with Material Design components
- ✅ **Dark Theme** - Elegant dark theme with customizable colors
- ✅ **Responsive Layout** - Resizable panels and adaptive interface
- ✅ **Channel Navigation** - Office 365-style navigation with channel tabs
- ✅ **User Lists** - Real-time user lists with mode indicators (@, +, etc.)
- ✅ **Message Bubbles** - Modern chat bubble interface with timestamps
- ✅ **Context Menus** - Right-click context menus for channels and users

### 🔧 Advanced Features
- ✅ **Auto-Reconnection** - Smart reconnection with exponential backoff
- ✅ **Ident Server** - Built-in ident server for traditional IRC networks
- ✅ **Server List** - Pre-configured server list with popular networks
- ✅ **Settings Management** - Comprehensive settings with JSON persistence
- ✅ **Update System** - Automatic update checking and installation
- ✅ **Message Grouping** - Smart message grouping for better readability
- ✅ **DCC Support** - Direct Client-to-Client file transfer capabilities

### 🛡️ Security & Reliability
- ✅ **SSL/TLS Support** - Encrypted connections for secure communication
- ✅ **Certificate Validation** - Proper SSL certificate verification
- ✅ **Error Handling** - Comprehensive error handling and recovery
- ✅ **Connection Strategies** - Multiple connection strategies for reliability
- ✅ **Rate Limiting** - Built-in rate limiting to prevent server bans

## 📸 Screenshots

The client features a clean, modern interface with:
- **Server Status Panel** - Shows current connection status and server info
- **Channel List** - Left panel with channel navigation and unread indicators
- **Main Chat Area** - Center panel with modern message bubbles and timestamps
- **User List** - Right panel with real-time user lists and operator status
- **Settings Window** - Comprehensive settings for customization
- **Update Dialog** - Built-in update management with progress tracking

## 🚀 Getting Started

### Prerequisites
- **.NET 8.0** or later
- **Windows 10/11** (Windows 11 recommended for best experience)
- **Visual Studio 2022** or **VS Code** (for development)

### Installation

#### Option 1: Download Release
1. Go to [Releases](https://github.com/drakkcoil/y0daii/releases)
2. Download the latest `Y0daiiIRC.msi` installer
3. Run the installer and follow the setup wizard
4. Launch y0daii IRC Client from your Start Menu

#### Option 2: Build from Source
```bash
# Clone the repository
git clone https://github.com/drakkcoil/y0daii.git
cd y0daii

# Restore dependencies
dotnet restore

# Build the application
dotnet build --configuration Release

# Run the application
dotnet run --project Y0daiiIRC.csproj
```

### Quick Start Guide

1. **Launch** the application
2. **Connect** to a server using the connection dialog
3. **Join channels** using `/join #channelname`
4. **Start chatting** with other users!

#### Popular IRC Networks
- **Libera.Chat** - `irc.libera.chat:6667` (Modern, no ident required)
- **EFNet** - `irc.efnet.org:6667` (Traditional, ident required)
- **Freenode** - `irc.freenode.net:6667` (Open source community)

## ⚙️ Configuration

### Settings Location
Settings are stored in `%APPDATA%\Y0daiiIRC\settings.json`

### Available Settings
- **Appearance** - Theme, colors, fonts, and layout preferences
- **Connection** - Default servers, SSL settings, and connection behavior
- **Updates** - Automatic update checking and installation preferences
- **Notifications** - Sound and visual notification settings
- **Advanced** - Ident server, logging, and debug options

### Update System
The client includes a comprehensive update system:
- **Automatic checking** on startup (configurable)
- **Progress tracking** with download speed and ETA
- **Checksum verification** for file integrity
- **Silent installation** with administrator privileges
- **Automatic restart** after successful installation

## 🎮 IRC Commands

### Basic Commands
- `/join #channel` - Join a channel
- `/part #channel` - Leave a channel
- `/nick newnick` - Change your nickname
- `/quit [message]` - Disconnect with optional message
- `/msg user message` - Send private message
- `/notice user message` - Send notice

### Advanced Commands
- `/me action` - Send action message (CTCP ACTION)
- `/whois user` - Get user information
- `/mode #channel +o user` - Give operator status
- `/topic #channel new topic` - Change channel topic
- `/kick #channel user reason` - Kick user from channel
- `/ban #channel user` - Ban user from channel

### Slash Commands
- `/help` - Show available commands
- `/clear` - Clear current channel
- `/reconnect` - Reconnect to current server
- `/servers` - Show server list
- `/settings` - Open settings window

## 🏗️ Architecture

### Core Components
- **`IRCClient`** - Handles IRC protocol communication with multiple connection strategies
- **`IRCMessage`** - Represents IRC messages with comprehensive parsing
- **`MainWindow`** - Main UI with modern chat interface and navigation
- **`UpdateService`** - Automatic update checking and installation
- **`CommandProcessor`** - Handles slash commands and IRC protocol commands
- **`DCCService`** - Direct Client-to-Client file transfer support
- **`MessageGroupingService`** - Smart message grouping for better readability

### Design Patterns
- **MVVM-inspired** architecture with data binding
- **Event-driven** communication between components
- **Async/await** for all network operations
- **Material Design** principles for modern UI
- **Service-oriented** architecture for modularity

### Key Technologies
- **.NET 8.0** - Latest .NET framework
- **WPF** - Windows Presentation Foundation for UI
- **Material Design** - Modern UI components
- **JSON** - Configuration and data persistence
- **HTTP Client** - Update system and web integration
- **SSL/TLS** - Secure network communication

## 🔄 Version Management

### Current Version: 1.0.7
- **Enhanced version management** with automatic detection
- **Build date tracking** with file creation time
- **Assembly metadata** integration
- **Easy version updates** with PowerShell scripts

### Version Update Process
```bash
# Auto-increment version
.\update-version.bat

# Specific version
.\update-version.bat 1.0.8

# Manual update
# Edit Y0daiiIRC.csproj and update version numbers
```

## 🤝 Contributing

We welcome contributions! Here's how to get started:

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Make** your changes
4. **Test** thoroughly
5. **Commit** your changes (`git commit -m 'Add amazing feature'`)
6. **Push** to your branch (`git push origin feature/amazing-feature`)
7. **Open** a Pull Request

### Development Setup
```bash
# Clone your fork
git clone https://github.com/yourusername/y0daii.git
cd y0daii

# Create development branch
git checkout -b development

# Install dependencies
dotnet restore

# Build and run
dotnet build
dotnet run --project Y0daiiIRC.csproj
```

## 📋 Roadmap

### High Priority
- [ ] **Multiple Server Connections** - Connect to multiple IRC networks simultaneously
- [ ] **Message Search** - Search through channel and private message history
- [ ] **Plugin System** - Extensible plugin architecture
- [ ] **Theme Customization** - User-created themes and customization
- [ ] **Mobile Companion** - iOS/Android companion app

### Medium Priority
- [ ] **Voice/Video Chat** - Integrated voice and video communication
- [ ] **File Sharing** - Enhanced file transfer with cloud integration
- [ ] **Advanced Scripting** - Lua scripting support
- [ ] **Cross-Platform** - Linux and macOS versions
- [ ] **Web Interface** - Browser-based access

### Long-term Vision
- [ ] **AI Integration** - Smart notifications and message summarization
- [ ] **Enterprise Features** - LDAP integration and audit logging
- [ ] **Cloud Sync** - Settings and history synchronization
- [ ] **Advanced Analytics** - Usage statistics and insights
- [ ] **Collaboration Tools** - Team channels and workspace integration

## 🐛 Bug Reports & Feature Requests

Found a bug or have a feature request? We'd love to hear from you!

- **🐛 Bug Reports** - [Open an Issue](https://github.com/drakkcoil/y0daii/issues/new?template=bug_report.md)
- **💡 Feature Requests** - [Open an Issue](https://github.com/drakkcoil/y0daii/issues/new?template=feature_request.md)
- **❓ Questions** - [Open a Discussion](https://github.com/drakkcoil/y0daii/discussions)

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **Material Design** - Google's Material Design for the beautiful UI components
- **.NET Community** - For the excellent .NET ecosystem and tools
- **IRC Community** - For keeping IRC alive and thriving
- **Contributors** - All the amazing people who contribute to this project

## 📞 Support

Need help? We're here for you!

- **📖 Documentation** - Check this README and the Help menu in the app
- **💬 Discussions** - [GitHub Discussions](https://github.com/drakkcoil/y0daii/discussions)
- **🐛 Issues** - [GitHub Issues](https://github.com/drakkcoil/y0daii/issues)
- **📧 Contact** - Open an issue for direct contact

---

**Made with ❤️ by the y0daii team**

*Bringing modern IRC to the 21st century*