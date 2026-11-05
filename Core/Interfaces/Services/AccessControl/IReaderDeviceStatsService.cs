using Core.Entities;
using static Core.Enums.BaseEnums;

namespace Core.Interfaces.Services.AccessControl
{
    public interface IReaderDeviceStatsService
    {
        Task<ReaderDeviceStats?> GetByReaderAsync(Guid readerId);
        Task UpsertAsync(Guid readerId, string deviceSerial,
            int? userCount = null, int? faceCount = null, int? fingerprintCount = null,
            int? cardCount = null, int? attLogCount = null, string? firmwareVersion = null);
        Task EnqueueFullSyncAsync(Guid readerId);
        Task EnqueueFullSyncAllAsync();

        /// <summary>Enqueue specific command types for one reader.</summary>
        Task EnqueueCommandsAsync(Guid readerId, bool force = false, params DeviceCommandType[] types);

        /// <summary>Enqueue specific command types for all readers.</summary>
        Task EnqueueCommandsAllAsync(bool force = false, params DeviceCommandType[] types);
    }
}
