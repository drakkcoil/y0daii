using System;

namespace Y0daiiIRC.Models
{
    public class DCCRequest
    {
        public string Sender { get; set; } = "";
        public string Target { get; set; } = "";
        public DCCRequestType Type { get; set; }
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
        public string IPAddress { get; set; } = "";
        public int Port { get; set; }
        public string? Token { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public DCCRequest(string sender, string target, DCCRequestType type, string fileName, long fileSize, string ipAddress, int port, string? token = null)
        {
            Sender = sender;
            Target = target;
            Type = type;
            FileName = fileName;
            FileSize = fileSize;
            IPAddress = ipAddress;
            Port = port;
            Token = token;
        }
    }

    public enum DCCRequestType
    {
        Send,
        Receive,
        Chat,
        Resume
    }
}
