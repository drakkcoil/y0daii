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
    }

    public enum MessageType
    {
        Normal,
        System,
        Action,
        Notice,
        Error
    }
}
