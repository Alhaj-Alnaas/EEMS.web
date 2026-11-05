using Core.Models;

namespace Core.Interfaces.Services.AccessControl
{
    public interface IReaderSyncDiagnosticService
    {
        /// <summary>Diagnose employee sync pipeline for all readers (step-by-step).</summary>
        Task<IReadOnlyList<ReaderSyncDiagnosticStep>> DiagnoseEmployeeSyncAsync(CancellationToken ct = default);
    }
}
