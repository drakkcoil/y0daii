namespace Y0daiiIRC.Models
{
    public class User
    {
        public string Nickname { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? Host { get; set; }
        public UserMode Mode { get; set; }
        public bool IsOnline { get; set; } = true;
    }

    [Flags]
    public enum UserMode
    {
        None = 0,
        Voice = 1,
        HalfOp = 2,
        Op = 4,
        Admin = 8,
        Owner = 16
    }
}
