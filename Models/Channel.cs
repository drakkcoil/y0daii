namespace Y0daiiIRC.Models
{
    public class Channel
    {
        public string Name { get; set; } = string.Empty;
        public ChannelType Type { get; set; }
        public int UserCount { get; set; }
        public string? Topic { get; set; }
        public string? TopicSetBy { get; set; }
        public DateTime? TopicSetDate { get; set; }
        public int UnreadCount { get; set; } = 0;
        public bool HasUnreadMessages => UnreadCount > 0;
    }

    public enum ChannelType
    {
        Channel,
        Private
    }
}
