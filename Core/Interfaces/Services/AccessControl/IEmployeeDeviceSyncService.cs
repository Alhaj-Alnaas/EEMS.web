using Core.Entities;

namespace Core.Interfaces.Services.AccessControl
{
    /// <summary>
    /// Pushes employee name/status changes to ZKTeco readers where the employee is enrolled.
    /// </summary>
    public interface IEmployeeDeviceSyncService
    {
        /// <summary>
        /// Queues DATA UPDATE USERINFO (and acc user table) commands for each matching reader.
        /// The device applies them on the next /iclock/getrequest poll.
        /// </summary>
        Task<int> SyncEmployeeToReadersAsync(Employee employee, CancellationToken ct = default);
    }
}
