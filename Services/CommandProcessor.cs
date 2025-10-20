using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Y0daiiIRC.IRC;
using Y0daiiIRC.Models;

namespace Y0daiiIRC.Services
{
    public class CommandProcessor
    {
        private readonly IRCClient _ircClient;
        private readonly ServerListService _serverListService;

        public event EventHandler<string>? CommandExecuted;
        public event EventHandler<string>? CommandError;

        public CommandProcessor(IRCClient ircClient, ServerListService serverListService)
        {
            _ircClient = ircClient;
            _serverListService = serverListService;
        }

        public async Task<bool> ProcessCommandAsync(string command, Channel? currentChannel)
        {
            if (string.IsNullOrEmpty(command) || !command.StartsWith("/"))
            {
                return false;
            }

            var parts = command.Substring(1).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            var cmd = parts[0].ToLower();
            var args = parts.Skip(1).ToArray();

            try
            {
                switch (cmd)
                {
                    // Connection commands
                    case "connect":
                    case "server":
                        await HandleConnectCommand(args);
                        break;
                    case "disconnect":
                    case "quit":
                        await HandleDisconnectCommand(args);
                        break;
                    case "reconnect":
                        await HandleReconnectCommand();
                        break;

                    // Channel commands
                    case "join":
                    case "j":
                        await HandleJoinCommand(args);
                        break;
                    case "part":
                    case "leave":
                        await HandlePartCommand(args, currentChannel);
                        break;
                    case "topic":
                        await HandleTopicCommand(args, currentChannel);
                        break;
                    case "names":
                    case "who":
                        await HandleNamesCommand(args, currentChannel);
                        break;
                    case "list":
                        await HandleListCommand(args);
                        break;

                    // User commands
                    case "nick":
                    case "nickname":
                        await HandleNickCommand(args);
                        break;
                    case "whois":
                        await HandleWhoisCommand(args);
                        break;
                    case "msg":
                    case "privmsg":
                    case "query":
                        await HandleMsgCommand(args);
                        break;
                    case "notice":
                        await HandleNoticeCommand(args);
                        break;
                    case "me":
                    case "action":
                        await HandleActionCommand(args, currentChannel);
                        break;

                    // Server commands
                    case "ping":
                        await HandlePingCommand(args);
                        break;
                    case "pong":
                        await HandlePongCommand(args);
                        break;
                    case "time":
                        await HandleTimeCommand(args);
                        break;
                    case "version":
                        await HandleVersionCommand(args);
                        break;
                    case "info":
                        await HandleInfoCommand(args);
                        break;
                    case "motd":
                        await HandleMotdCommand(args);
                        break;
                    case "lusers":
                        await HandleLusersCommand(args);
                        break;

                    // Mode commands
                    case "mode":
                        await HandleModeCommand(args, currentChannel);
                        break;
                    case "op":
                        await HandleOpCommand(args, currentChannel);
                        break;
                    case "deop":
                        await HandleDeopCommand(args, currentChannel);
                        break;
                    case "voice":
                    case "v":
                        await HandleVoiceCommand(args, currentChannel);
                        break;
                    case "devoice":
                    case "dv":
                        await HandleDevoiceCommand(args, currentChannel);
                        break;
                    case "ban":
                        await HandleBanCommand(args, currentChannel);
                        break;
                    case "unban":
                        await HandleUnbanCommand(args, currentChannel);
                        break;
                    case "kick":
                        await HandleKickCommand(args, currentChannel);
                        break;

                    // CTCP commands
                    case "ctcp":
                        await HandleCTCPCommand(args, currentChannel);
                        break;

                    // Utility commands
                    case "clear":
                        HandleClearCommand();
                        break;
                    case "help":
                        HandleHelpCommand(args);
                        break;
                    case "about":
                        HandleAboutCommand();
                        break;
                    case "raw":
                        await HandleRawCommand(args);
                        break;
                    case "quote":
                        await HandleQuoteCommand(args);
                        break;

                    // Server list commands
                    case "servers":
                        HandleServersCommand();
                        break;
                    case "addserver":
                        HandleAddServerCommand(args);
                        break;
                    case "removeserver":
                        HandleRemoveServerCommand(args);
                        break;

                    default:
                        CommandError?.Invoke(this, $"Unknown command: {cmd}");
                        return false;
                }

                CommandExecuted?.Invoke(this, command);
                return true;
            }
            catch (Exception ex)
            {
                CommandError?.Invoke(this, $"Error executing command: {ex.Message}");
                return false;
            }
        }

        private async Task HandleConnectCommand(string[] args)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /connect <server> [port] [password]");
                return;
            }

            var server = args[0];
            var port = args.Length > 1 && int.TryParse(args[1], out int p) ? p : 6667;
            var password = args.Length > 2 ? args[2] : null;

            // TODO: Implement connection logic
            CommandExecuted?.Invoke(this, $"Connecting to {server}:{port}...");
        }

        private async Task HandleDisconnectCommand(string[] args)
        {
            var reason = args.Length > 0 ? string.Join(" ", args) : "Y0daii IRC Client";
            await _ircClient.SendCommandAsync($"QUIT :{reason}");
        }

        private async Task HandleReconnectCommand()
        {
            if (_ircClient.IsConnected)
            {
                await _ircClient.DisconnectAsync();
                // TODO: Implement reconnection logic
            }
        }

        private async Task HandleJoinCommand(string[] args)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /join <channel> [password]");
                return;
            }

            var channel = args[0];
            var password = args.Length > 1 ? args[1] : null;

            if (password != null)
            {
                await _ircClient.SendCommandAsync($"JOIN {channel} {password}");
            }
            else
            {
                await _ircClient.JoinChannelAsync(channel);
            }
        }

        private async Task HandlePartCommand(string[] args, Channel? currentChannel)
        {
            var channel = args.Length > 0 ? args[0] : currentChannel?.Name;
            var reason = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "Leaving";

            if (channel != null)
            {
                await _ircClient.LeaveChannelAsync(channel);
            }
        }

        private async Task HandleTopicCommand(string[] args, Channel? currentChannel)
        {
            var channel = args.Length > 0 ? args[0] : currentChannel?.Name;
            var topic = args.Length > 1 ? string.Join(" ", args.Skip(1)) : null;

            if (channel != null)
            {
                if (topic != null)
                {
                    await _ircClient.SendCommandAsync($"TOPIC {channel} :{topic}");
                }
                else
                {
                    await _ircClient.SendCommandAsync($"TOPIC {channel}");
                }
            }
        }

        private async Task HandleNamesCommand(string[] args, Channel? currentChannel)
        {
            var channel = args.Length > 0 ? args[0] : currentChannel?.Name;
            if (channel != null)
            {
                await _ircClient.SendCommandAsync($"NAMES {channel}");
            }
        }

        private async Task HandleListCommand(string[] args)
        {
            var channels = args.Length > 0 ? string.Join(",", args) : "";
            await _ircClient.SendCommandAsync($"LIST {channels}");
        }

        private async Task HandleNickCommand(string[] args)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /nick <newnick>");
                return;
            }

            await _ircClient.SendCommandAsync($"NICK {args[0]}");
        }

        private async Task HandleWhoisCommand(string[] args)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /whois <nickname>");
                return;
            }

            await _ircClient.SendCommandAsync($"WHOIS {args[0]}");
        }

        private async Task HandleMsgCommand(string[] args)
        {
            if (args.Length < 2)
            {
                CommandError?.Invoke(this, "Usage: /msg <nickname> <message>");
                return;
            }

            var target = args[0];
            var message = string.Join(" ", args.Skip(1));
            await _ircClient.SendMessageAsync(target, message);
        }

        private async Task HandleNoticeCommand(string[] args)
        {
            if (args.Length < 2)
            {
                CommandError?.Invoke(this, "Usage: /notice <nickname> <message>");
                return;
            }

            var target = args[0];
            var message = string.Join(" ", args.Skip(1));
            await _ircClient.SendNoticeAsync(target, message);
        }

        private async Task HandleActionCommand(string[] args, Channel? currentChannel)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /me <action>");
                return;
            }

            var action = string.Join(" ", args);
            var target = currentChannel?.Name ?? "PRIVMSG";
            await _ircClient.SendCommandAsync($"PRIVMSG {target} :\x01ACTION {action}\x01");
        }

        private async Task HandlePingCommand(string[] args)
        {
            var target = args.Length > 0 ? args[0] : _ircClient.Server;
            await _ircClient.SendCommandAsync($"PING {target}");
        }

        private async Task HandlePongCommand(string[] args)
        {
            var target = args.Length > 0 ? args[0] : _ircClient.Server;
            await _ircClient.SendCommandAsync($"PONG {target}");
        }

        private async Task HandleTimeCommand(string[] args)
        {
            var target = args.Length > 0 ? args[0] : _ircClient.Server;
            await _ircClient.SendCommandAsync($"TIME {target}");
        }

        private async Task HandleVersionCommand(string[] args)
        {
            var target = args.Length > 0 ? args[0] : _ircClient.Server;
            await _ircClient.SendCommandAsync($"VERSION {target}");
        }

        private async Task HandleInfoCommand(string[] args)
        {
            var target = args.Length > 0 ? args[0] : _ircClient.Server;
            await _ircClient.SendCommandAsync($"INFO {target}");
        }

        private async Task HandleMotdCommand(string[] args)
        {
            var target = args.Length > 0 ? args[0] : _ircClient.Server;
            await _ircClient.SendCommandAsync($"MOTD {target}");
        }

        private async Task HandleLusersCommand(string[] args)
        {
            var target = args.Length > 0 ? args[0] : _ircClient.Server;
            await _ircClient.SendCommandAsync($"LUSERS {target}");
        }

        private async Task HandleModeCommand(string[] args, Channel? currentChannel)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /mode <target> [modes] [parameters]");
                return;
            }

            var target = args[0];
            var modes = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "";
            await _ircClient.SendCommandAsync($"MODE {target} {modes}");
        }

        private async Task HandleOpCommand(string[] args, Channel? currentChannel)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /op <nickname>");
                return;
            }

            var channel = currentChannel?.Name;
            if (channel != null)
            {
                await _ircClient.SendCommandAsync($"MODE {channel} +o {args[0]}");
            }
        }

        private async Task HandleDeopCommand(string[] args, Channel? currentChannel)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /deop <nickname>");
                return;
            }

            var channel = currentChannel?.Name;
            if (channel != null)
            {
                await _ircClient.SendCommandAsync($"MODE {channel} -o {args[0]}");
            }
        }

        private async Task HandleVoiceCommand(string[] args, Channel? currentChannel)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /voice <nickname>");
                return;
            }

            var channel = currentChannel?.Name;
            if (channel != null)
            {
                await _ircClient.SendCommandAsync($"MODE {channel} +v {args[0]}");
            }
        }

        private async Task HandleDevoiceCommand(string[] args, Channel? currentChannel)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /devoice <nickname>");
                return;
            }

            var channel = currentChannel?.Name;
            if (channel != null)
            {
                await _ircClient.SendCommandAsync($"MODE {channel} -v {args[0]}");
            }
        }

        private async Task HandleBanCommand(string[] args, Channel? currentChannel)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /ban <nickname>");
                return;
            }

            var channel = currentChannel?.Name;
            if (channel != null)
            {
                await _ircClient.SendCommandAsync($"MODE {channel} +b {args[0]}");
            }
        }

        private async Task HandleUnbanCommand(string[] args, Channel? currentChannel)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /unban <nickname>");
                return;
            }

            var channel = currentChannel?.Name;
            if (channel != null)
            {
                await _ircClient.SendCommandAsync($"MODE {channel} -b {args[0]}");
            }
        }

        private async Task HandleKickCommand(string[] args, Channel? currentChannel)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /kick <nickname> [reason]");
                return;
            }

            var channel = currentChannel?.Name;
            if (channel != null)
            {
                var reason = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "Kicked";
                await _ircClient.SendCommandAsync($"KICK {channel} {args[0]} :{reason}");
            }
        }

        private void HandleClearCommand()
        {
            // This will be handled by the UI
            CommandExecuted?.Invoke(this, "Clearing chat...");
        }

        private void HandleHelpCommand(string[] args)
        {
            var command = args.Length > 0 ? args[0].ToLower() : "";
            
            var helpText = command switch
            {
                "connect" => "Usage: /connect <server> [port] [password]\nConnects to an IRC server.",
                "join" => "Usage: /join <channel> [password]\nJoins a channel.",
                "part" => "Usage: /part [channel] [reason]\nLeaves a channel.",
                "msg" => "Usage: /msg <nickname> <message>\nSends a private message.",
                "nick" => "Usage: /nick <newnick>\nChanges your nickname.",
                "whois" => "Usage: /whois <nickname>\nGets information about a user.",
                "me" => "Usage: /me <action>\nSends an action message.",
                "mode" => "Usage: /mode <target> [modes] [parameters]\nChanges channel or user modes.",
                "op" => "Usage: /op <nickname>\nGives operator status to a user.",
                "voice" => "Usage: /voice <nickname>\nGives voice status to a user.",
                "kick" => "Usage: /kick <nickname> [reason]\nKicks a user from the channel.",
                "ban" => "Usage: /ban <nickname>\nBans a user from the channel.",
                "list" => "Usage: /list [channels]\nLists channels on the server.",
                "names" => "Usage: /names [channel]\nLists users in a channel.",
                "topic" => "Usage: /topic [channel] [new topic]\nGets or sets channel topic.",
                "quit" => "Usage: /quit [reason]\nDisconnects from the server.",
                "raw" => "Usage: /raw <command>\nSends a raw IRC command.",
                _ => "Available commands: connect, join, part, msg, nick, whois, me, mode, op, voice, kick, ban, list, names, topic, quit, raw, help"
            };

            CommandExecuted?.Invoke(this, helpText);
        }

        private void HandleAboutCommand()
        {
            CommandExecuted?.Invoke(this, "Y0daii IRC Client v1.0 - Modern IRC client with beautiful UX");
        }

        private async Task HandleRawCommand(string[] args)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /raw <command>");
                return;
            }

            var command = string.Join(" ", args);
            await _ircClient.SendCommandAsync(command);
        }

        private async Task HandleQuoteCommand(string[] args)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /quote <command>");
                return;
            }

            var command = string.Join(" ", args);
            await _ircClient.SendCommandAsync(command);
        }

        private void HandleServersCommand()
        {
            var servers = _serverListService.GetServers();
            var serverList = string.Join("\n", servers.Select(s => $"{s.Name} - {s.Host}:{s.Port}"));
            CommandExecuted?.Invoke(this, $"Saved servers:\n{serverList}");
        }

        private void HandleAddServerCommand(string[] args)
        {
            if (args.Length < 2)
            {
                CommandError?.Invoke(this, "Usage: /addserver <name> <host> [port]");
                return;
            }

            var name = args[0];
            var host = args[1];
            var port = args.Length > 2 && int.TryParse(args[2], out int p) ? p : 6667;

            var server = new ServerInfo
            {
                Name = name,
                Host = host,
                Port = port
            };

            _serverListService.AddServer(server);
            CommandExecuted?.Invoke(this, $"Added server: {name} ({host}:{port})");
        }

        private void HandleRemoveServerCommand(string[] args)
        {
            if (args.Length < 1)
            {
                CommandError?.Invoke(this, "Usage: /removeserver <name>");
                return;
            }

            var name = args[0];
            _serverListService.RemoveServer(name);
            CommandExecuted?.Invoke(this, $"Removed server: {name}");
        }

        // CTCP Command Handlers
        private async Task HandleCTCPCommand(string[] args, Channel? currentChannel)
        {
            if (args.Length < 2)
            {
                CommandError?.Invoke(this, "Usage: /ctcp <target> <command> [parameter]");
                return;
            }

            var target = args[0];
            var command = args[1].ToUpper();
            var parameter = args.Length > 2 ? string.Join(" ", args.Skip(2)) : null;

            await _ircClient.SendCTCPAsync(target, command, parameter);
            CommandExecuted?.Invoke(this, $"Sent CTCP {command} to {target}");
        }

    }
}
