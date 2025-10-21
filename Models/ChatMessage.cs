using System.Collections.Generic;
using System.Windows.Media;

namespace Y0daiiIRC.Models
{
    public class ChatMessage
    {
        public string Sender { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public Color SenderColor { get; set; } = Colors.Black;
        public MessageType Type { get; set; } = MessageType.Normal;
        public List<ChatMessage> SubMessages { get; set; } = new List<ChatMessage>();
        public string GroupTitle { get; set; } = string.Empty;
    }

    public enum MessageType
    {
        Normal,
        System,
        Action,
        Notice,
        Error,
        Grouped
    }
}
