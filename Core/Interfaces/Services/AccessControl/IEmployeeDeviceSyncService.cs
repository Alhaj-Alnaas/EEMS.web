using Core.Entities;

namespace Core.Interfaces.Services.AccessControl
{
    /// <summary>
    /// App → device sync: enroll employees on selected readers and push status/name changes.
    /// Commands are queued in DeviceCommands; readers apply them on the next /iclock/getrequest poll.
    /// </summary>
    public interface IEmployeeDeviceSyncService
    {
        /// <summary>
        /// Push name + active/disable status to readers where this employee is already known
        /// (ZkUid present, or inferred from permissions/movements/templates/enrollment remarks).
        /// </summary>
        Task<int> SyncEmployeeToReadersAsync(Employee employee, CancellationToken ct = default);

        /// <summary>
        /// Explicit status sync (active ↔ disabled on device). Same pipeline as
        /// <see cref="SyncEmployeeToReadersAsync"/> — kept for clear call-sites.
        /// </summary>
        Task<int> SyncEmployeeStatusToReadersAsync(Employee employee, CancellationToken ct = default);

        /// <summary>
        /// Create/enroll the employee on the given readers (SpeedFace DATA UPDATE user with allocated uid).
        /// Persists ZkUid + enrolled serials on <paramref name="employee"/>.remarks.
        /// </summary>
        Task<int> EnrollEmployeeOnReadersAsync(
            Employee employee,
            IReadOnlyList<Guid> readerIds,
            CancellationToken ct = default);

        /// <summary>
        /// Device answered <c>Return=-1004</c> (PIN not on that reader).
        /// Queue one create for a stable uid. Repeating it updates that same row.
        /// </summary>
        Task BeginCreateAfterMissingUserAsync(Guid readerId, string? failedUpdatePayload, CancellationToken ct = default);

        /// <summary>
        /// Push stored face, palm, and finger templates to the employee's readers
        /// other than the reader that just uploaded them.
        /// </summary>
        Task PushTemplatesExceptSourceAsync(Guid employeeId, string sourceSerial, CancellationToken ct = default);

        /// <summary>
        /// After a user-table query/push from the device, assign the next Acc uid
        /// and queue <c>DATA UPDATE user</c> in the same format as device-created rows.
        /// </summary>
        Task<int> CompletePendingEnrollsFromDeviceUsersAsync(
            string deviceSerial,
            string body,
            CancellationToken ct = default);
    }
}
