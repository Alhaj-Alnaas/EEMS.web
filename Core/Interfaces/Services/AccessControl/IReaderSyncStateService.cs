using Core.Models;

namespace Core.Interfaces.Services.AccessControl
{
    /// <summary>
    /// Per-device ADMS sync watermarks and INFO stats (in-memory; resets on app restart).
    /// Stamp=0 in handshake forces the device to re-upload existing records.
    /// </summary>
    public interface IReaderSyncStateService
    {
        void ResetStamps(string deviceSerial);
        string GetStamp(string deviceSerial, string tableName);
        void UpdateStamp(string deviceSerial, string tableName, string? stamp);

        /// <summary>
        /// Updates snapshot from an INFO/options payload.
        /// Returns only fields that were present in this payload (not carried over from previous).
        /// </summary>
        ReaderDeviceSnapshot UpdateFromInfo(string deviceSerial, string? info);

        /// <summary>Overwrite specific counts from authoritative querydata / GET OPTION results.</summary>
        void SetCounts(string deviceSerial, int? userCount = null, int? faceCount = null,
            int? fingerprintCount = null, int? attLogCount = null, string? firmwareVersion = null);

        (string SessionId, string RegistryCode) GetOrCreateSession(string deviceSerial);
        ReaderDeviceSnapshot? GetSnapshot(string deviceSerial);
        IReadOnlyList<ReaderDeviceSnapshot> GetAllSnapshots();
    }
}
