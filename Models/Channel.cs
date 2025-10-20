namespace Y0daiiIRC.Models
{
    public class Channel
    {
        public string Name { get; set; } = string.Empty;
        public ChannelType Type { get; set; }
        public int UserCount { get; set; }
        public string? Topic { get; set; }
    }

    public enum ChannelType
    {
        Channel,
        Private
    }
}
