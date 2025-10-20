using System;

namespace Y0daiiIRC.Models
{
    public class DCCTransfer
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Sender { get; set; } = "";
        public string Receiver { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public long BytesTransferred { get; set; }
        public DCCTransferType Type { get; set; }
        public DCCTransferStatus Status { get; set; } = DCCTransferStatus.Pending;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? ErrorMessage { get; set; }
        public int Port { get; set; }
        public string? IPAddress { get; set; }
        public string? Token { get; set; }

        public double ProgressPercentage => FileSize > 0 ? (double)BytesTransferred / FileSize * 100 : 0;
        public TimeSpan? Duration => EndTime?.Subtract(StartTime) ?? DateTime.Now.Subtract(StartTime);
        public long TransferRate => Duration?.TotalSeconds > 0 ? (long)(BytesTransferred / Duration.Value.TotalSeconds) : 0;
        public long RemainingBytes => FileSize - BytesTransferred;
        public TimeSpan? EstimatedTimeRemaining => TransferRate > 0 ? TimeSpan.FromSeconds(RemainingBytes / TransferRate) : null;
    }

    public enum DCCTransferType
    {
        Send,
        Receive
    }

    public enum DCCTransferStatus
    {
        Pending,
        Connecting,
        InProgress,
        Completed,
        Failed,
        Cancelled
    }
}
