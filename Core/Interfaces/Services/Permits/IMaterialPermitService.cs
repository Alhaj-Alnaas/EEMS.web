using Core.Entities;
using Core.Models;
using static Core.Enums.BaseEnums;

namespace Core.Interfaces.Services.Permits
{
    /// <summary>
    /// Materials/equipment permits: keeps the previous line-item permit and
    /// routes it through the configurable approval workflow.
    /// </summary>
    public interface IMaterialPermitService
    {
        Task<Guid> SaveAsync(MaterialPermitRequest request, User user, bool submit);

        /// <summary>Materials permits waiting for a decision from this user.</summary>
        Task<List<Permit>> GetInboxAsync(User user);

        Task DecideAsync(Guid permitId, User user, ApprovalDecision decision, string? comment, Guid? executionGateId = null);

        /// <summary>Permits of this responsibility center, plus permits this user has approved and may keep following.</summary>
        Task<List<Permit>> GetForResponsibilityCenterAsync(User user);

        /// <summary>True when this user recorded an approval on the permit.</summary>
        Task<bool> WasApprovedByAsync(Guid permitId, string userId);

        /// <summary>User names that share the given user's responsibility center.</summary>
        Task<HashSet<string>> GetSameCenterUserNamesAsync(User user);

        Task CancelAsync(Guid permitId, User user);

        /// <summary>Moves a draft to the approval path configured for its type.</summary>
        Task SubmitDraftAsync(Guid permitId, User user);
    }
}
