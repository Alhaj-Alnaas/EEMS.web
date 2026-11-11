using Core.Entities;
using Core.Models;
using static Core.Enums.BaseEnums;

namespace Core.Interfaces.Services.Permits
{
    /// <summary>
    /// Links a permit classification to the gates that may execute it,
    /// and records gate execution according to the permit type.
    /// </summary>
    public interface IPermitExecutionService
    {
        Task<List<Gate>> GetActiveGatesAsync();

        Task<Dictionary<PermitClassification, List<Guid>>> GetLinksAsync();

        Task SaveLinksAsync(PermitClassification classification, IReadOnlyCollection<Guid> gateIds);

        Task<List<PermitExecutionRow>> GetActivePermitsAsync();

        Task<Dictionary<Guid, string>> ExecutionGateLabelsAsync(IReadOnlyList<Permit> permits);

        Task<List<PermitExecutionRow>> GetExecutableAsync(Guid gateId);

        Task<(Permit Permit, PermitExecutionStep Step, List<PermitExecutionEventRow> History)> GetExecutionAsync(Guid permitId);

        Task ExecuteAsync(Guid permitId, Guid gateId, DateTime when, string? notes, User user);

        Task<List<OpenPermitAlert>> GetOverdueAsync();

        Task RunMaintenanceAsync();
    }
}
