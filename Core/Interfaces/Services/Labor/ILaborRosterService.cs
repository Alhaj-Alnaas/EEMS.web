using Core.Models;

namespace Core.Interfaces.Services.Labor
{
    public interface ILaborRosterService
    {
        Task<List<LaborCompanyRow>> ListCompaniesAsync(CancellationToken ct = default);
        Task<LaborCompanyRow?> GetCompanyAsync(Guid id, CancellationToken ct = default);
        Task<Guid> SaveCompanyAsync(LaborCompanyInput input, LaborActor actor, CancellationToken ct = default);
        Task DeleteCompanyAsync(Guid id, LaborActor actor, CancellationToken ct = default);
        Task<ExternalCompanyLookup> ListExternalAsync(CancellationToken ct = default);
        Task<LaborExternalImportResult> ImportExternalAsync(IReadOnlyList<ExternalLaborCompany> rows, LaborActor actor, CancellationToken ct = default);

        Task<List<LaborWorkerRow>> ListWorkersAsync(Guid companyId, CancellationToken ct = default);
        Task<Guid> SaveWorkerAsync(LaborWorkerInput input, LaborActor actor, CancellationToken ct = default);

        Task<Guid> StageAsync(Guid companyId, string mode, string? fileName, IReadOnlyList<LaborSheetRow> rows, LaborActor actor, CancellationToken ct = default);
        Task<LaborBatchDetail?> GetBatchAsync(Guid id, CancellationToken ct = default);
        Task<Guid?> LatestBatchIdAsync(Guid companyId, CancellationToken ct = default);
        Task RenumberAsync(Guid batchId, LaborActor actor, CancellationToken ct = default);
        Task ApplyAsync(Guid batchId, LaborActor actor, CancellationToken ct = default);
        Task CancelAsync(Guid batchId, LaborActor actor, CancellationToken ct = default);

        Task<List<LaborFollowRow>> SearchAsync(Guid? companyId, string? term, string? status, CancellationToken ct = default);
        Task MarkPrintedAsync(IReadOnlyList<Guid> workerIds, LaborActor actor, CancellationToken ct = default);
    }
}
