using System;

namespace Y0daiiIRC.Models
{
    public class CTCPRequest
    {
        public string Sender { get; set; } = "";
        public string Target { get; set; } = "";
        public string Command { get; set; } = "";
        public string? Parameter { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public CTCPRequest(string sender, string target, string command, string? parameter = null)
        {
            Sender = sender;
            Target = target;
            Command = command;
            Parameter = parameter;
        }
    }
}
