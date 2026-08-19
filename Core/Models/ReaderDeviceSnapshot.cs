namespace Core.Models
{
    /// <summary>Live stats reported by a reader (INFO line / GET OPTION).</summary>
    public record ReaderDeviceSnapshot(
        DateTime UpdatedAt,
        string DeviceSerial,
        int? UserCount,
        int? FingerprintCount,
        int? FaceCount,
        int? AttLogCount,
        string? FirmwareVersion,
        string? RawInfo);
}
