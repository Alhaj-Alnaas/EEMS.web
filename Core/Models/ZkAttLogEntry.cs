namespace Core.Models
{
    /// <summary>Parsed row from ZKTeco ATTLOG push payload.</summary>
    public class ZkAttLogEntry
    {
        public string Pin { get; set; } = "";
        public DateTime EventTime { get; set; }
        public int Status { get; set; }
        public int VerifyMode { get; set; }
        /// <summary>Acc transaction <c>index</c> — unique per reader for deduplication.</summary>
        public int? DeviceIndex { get; set; }
        /// <summary>Card number from device event when present (<c>cardno</c>).</summary>
        public string? CardNo { get; set; }
        public string RawLine { get; set; } = "";
    }
}
