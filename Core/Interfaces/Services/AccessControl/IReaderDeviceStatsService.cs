using Core.Entities;
using static Core.Enums.BaseEnums;

namespace Core.Interfaces.Services.AccessControl
{
    public interface IReaderDeviceStatsService
    {
        Task<ReaderDeviceStats?> GetByReaderAsync(Guid readerId);
        Task UpsertAsync(Guid readerId, string deviceSerial,
            int userCount, int faceCount, int fingerprintCount,
            int cardCount, int attLogCount, string? firmwareVersion);
        Task EnqueueFullSyncAsync(Guid readerId);
        Task EnqueueFullSyncAllAsync();

        /// <summary>Enqueue specific command types for one reader.</summary>
        Task EnqueueCommandsAsync(Guid readerId, params DeviceCommandType[] types);

        /// <summary>Enqueue specific command types for all active readers.</summary>
        Task EnqueueCommandsAllAsync(params DeviceCommandType[] types);
    }
}
