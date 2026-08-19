namespace Core.Models
{
    /// <summary>Parsed row from ZKTeco ATTLOG push payload.</summary>
    public class ZkAttLogEntry
    {
        public string Pin { get; set; } = "";
        public DateTime EventTime { get; set; }
        public int Status { get; set; }
        public int VerifyMode { get; set; }
        public string RawLine { get; set; } = "";
    }
}
