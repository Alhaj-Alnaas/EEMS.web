using System;

namespace Core.Entities
{
    public class ReaderDeviceStats : Base
    {
        public Guid ReaderId { get; set; }
        public Reader? Reader { get; set; }
        public string DeviceSerial { get; set; } = "";
        public int UserCount { get; set; }
        public int FaceCount { get; set; }
        public int FingerprintCount { get; set; }
        public int CardCount { get; set; }
        public int AttLogCount { get; set; }
        public string? FirmwareVersion { get; set; }
        public DateTime FetchedAt { get; set; } = DateTime.Now;
    }
}
