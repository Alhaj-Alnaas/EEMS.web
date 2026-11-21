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

        /// <summary>Approved employee-card requests waiting to be printed. No gate is involved.</summary>
        Task<List<PermitExecutionRow>> GetExecutableCardPermitsAsync();

        Task<(Permit Permit, PermitExecutionStep Step, List<PermitExecutionEventRow> History)> GetExecutionAsync(Guid permitId);

        /// <summary>
        /// Cards synced from the readers of <paramref name="gateId"/> whose device PIN equals the card number.
        /// Cards already handed to another open visitor permit are omitted.
        /// </summary>
        Task<List<VisitorCardOption>> GetVisitorCardsAsync(Guid gateId, Guid? permitId = null);

        Task ExecuteAsync(Guid permitId, Guid gateId, DateTime when, string? notes, User user, string? visitorCard = null);

        /// <summary>
        /// Prints an employee permit card: the new number replaces the previous one,
        /// the old card is cancelled, readers are updated, and the permit is closed.
        /// Returns how many reader commands were queued.
        /// </summary>
        Task<int> ExecuteCardAsync(Guid permitId, string cardNumber, User user);

        Task<List<OpenPermitAlert>> GetOverdueAsync();

        Task RunMaintenanceAsync();
    }
}
