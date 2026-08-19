using Core.Entities;
using static Core.Enums.BaseEnums;

namespace Core.Interfaces.Services.AccessControl
{
    /// <summary>Phase 1: queue and track commands sent to biometric readers.</summary>
    public interface IDeviceCommandService
    {
        Task<List<DeviceCommand>> GetPendingAsync(int take = 50);
        Task<List<DeviceCommand>> GetPendingByReaderAsync(Guid readerId, int take = 50);
        Task<List<DeviceCommand>> GetByReaderAsync(Guid readerId);
        Task<DeviceCommand?> GetByProtocolIdAsync(Guid readerId, int protocolCommandId);
        Task<DeviceCommand> EnqueueAsync(Guid readerId, DeviceCommandType type, string? payload = null, DateTime? scheduledOn = null);
        Task UpdateStatusAsync(Guid id, DeviceCommandStatus status, string? error = null);
        Task MarkSentAsync(Guid id, int protocolCommandId);
        Task<List<DeviceCommand>> GetTimedOutSentAsync(TimeSpan timeout);
        Task ScheduleRetryAsync(Guid id, TimeSpan delay, string? error = null);
        /// <summary>Cancel all Pending/Sent commands older than the given age.</summary>
        Task<int> CancelStaleAsync(TimeSpan maxAge);
    }
}
