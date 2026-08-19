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

        void UpdateFromInfo(string deviceSerial, string? info);
        (string SessionId, string RegistryCode) GetOrCreateSession(string deviceSerial);
        ReaderDeviceSnapshot? GetSnapshot(string deviceSerial);
        IReadOnlyList<ReaderDeviceSnapshot> GetAllSnapshots();
    }
}
