using System.Collections.Generic;

namespace Y0daiiIRC.IRC
{
    public class IRCMessage
    {
        public string? Prefix { get; set; }
        public string Command { get; set; } = string.Empty;
        public List<string> Parameters { get; set; } = new List<string>();

        public string? Sender
        {
            get
            {
                if (string.IsNullOrEmpty(Prefix)) return null;
                var exclamationIndex = Prefix.IndexOf('!');
                return exclamationIndex > 0 ? Prefix.Substring(0, exclamationIndex) : Prefix;
            }
        }

        public string? Host
        {
            get
            {
                if (string.IsNullOrEmpty(Prefix)) return null;
                var atIndex = Prefix.IndexOf('@');
                return atIndex > 0 ? Prefix.Substring(atIndex + 1) : null;
            }
        }

        public string? Target => Parameters.Count > 0 ? Parameters[0] : null;
        public string? Content => Parameters.Count > 1 ? Parameters[Parameters.Count - 1] : null;

        public bool IsPrivateMessage => Command == "PRIVMSG";
        public bool IsNotice => Command == "NOTICE";
        public bool IsJoin => Command == "JOIN";
        public bool IsPart => Command == "PART";
        public bool IsQuit => Command == "QUIT";
        public bool IsNick => Command == "NICK";
        public bool IsMode => Command == "MODE";
        public bool IsPing => Command == "PING";
        public bool IsPong => Command == "PONG";
        public bool IsNumeric => int.TryParse(Command, out _);
    }
}
