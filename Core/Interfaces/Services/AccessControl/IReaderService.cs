using Core.Entities;
using static Core.Enums.BaseEnums;

namespace Core.Interfaces.Services.AccessControl
{
    /// <summary>Phase 1: manage biometric reader devices and their online status.</summary>
    public interface IReaderService
    {
        Task<List<Reader>> GetAllAsync();
        Task<List<Reader>> GetByGateAsync(Guid gateId);
        Task<Reader?> GetByIdAsync(Guid id);
        Task<Reader?> GetBySerialAsync(string deviceSerial);
        Task InsertAsync(Reader reader);
        Task UpdateAsync(Reader reader);
        Task DeleteAsync(Guid id);
        Task ReportHeartbeatAsync(Guid readerId, ReaderStatus status = ReaderStatus.Online);
        Task ReportHeartbeatBySerialAsync(string deviceSerial, ReaderStatus status = ReaderStatus.Online);
        Task SetStatusAsync(Guid readerId, ReaderStatus status, string? error = null);
        Task MarkSyncAsync(Guid readerId);
    }
}
