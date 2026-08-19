using Core.Entities;
using static Core.Enums.BaseEnums;

namespace Core.Interfaces.Services.AccessControl
{
    public interface IReaderDowntimeService
    {
        /// <summary>
        /// Reacts to a reader status change: opens/closes downtime windows and notifies admins.
        /// </summary>
        Task OnStatusChangedAsync(
            Reader reader,
            ReaderStatus previousStatus,
            ReaderStatus newStatus,
            string? reason = null);

        Task<ReaderDowntime?> GetOpenAsync(Guid readerId);
        Task<List<ReaderDowntime>> GetRecentAsync(int take = 50);
        Task<List<ReaderDowntime>> GetByReaderAsync(Guid readerId, int take = 50);
    }
}
