namespace Core.Models
{
    /// <summary>One incoming Push/ADMS HTTP hit (for live connection diagnostics).</summary>
    public record ReaderConnectionEvent(
        DateTime ReceivedAt,
        string DeviceSerial,
        string RemoteIp,
        string Endpoint,
        string HttpMethod,
        bool IsRegistered,
        string? Note);
}
