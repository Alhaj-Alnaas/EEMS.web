namespace Core.Interfaces.Services.AccessControl
{
    public interface IMovementLogIngestService
    {
        /// <summary>Parse ATTLOG body from a reader Push upload and persist new movement rows.</summary>
        Task<int> ImportAttLogAsync(string deviceSerial, string body, CancellationToken ct = default);

        /// <summary>
        /// Fix historical rows (PIN, EmployeeId, EventTime from time_second, DeviceTxnIndex).
        /// </summary>
        Task<int> RepairUnlinkedAsync(CancellationToken ct = default);

        /// <summary>Max Acc transaction index already stored for a reader (incremental QUERY).</summary>
        Task<int?> GetMaxDeviceTxnIndexAsync(Guid readerId, CancellationToken ct = default);
    }
}
