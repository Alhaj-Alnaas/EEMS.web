namespace Core.Models
{
    /// <summary>One raw data push received from a device (diagnostics only, in-memory).</summary>
    public record DevicePushEvent(
        DateTime ReceivedAt,
        string DeviceSerial,
        string Table,
        int ByteCount,
        int LineCount,
        string Preview,
        string Outcome);
}
