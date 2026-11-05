using Core.Entities;
using static Core.Enums.BaseEnums;

namespace Core.Interfaces.Services.Workflow
{
    /// <summary>
    /// Phase 2: drives a permit through its configured <see cref="WorkflowDefinition"/>,
    /// recording <see cref="PermitApproval"/> decisions and advancing status.
    /// </summary>
    public interface IWorkflowEngine
    {
        Task<WorkflowDefinition?> GetActiveWorkflowAsync(PermitClassification classification);

        /// <summary>
        /// Returns the active workflow, creating the default materials path
        /// (department approver then security approver) when none exists.
        /// </summary>
        Task<WorkflowDefinition> EnsureActiveWorkflowAsync(PermitClassification classification);

        /// <summary>True when this entrant still has at least one applicable step on the path.</summary>
        Task<bool> HasApplicableStepAsync(WorkflowDefinition workflow, User entrant);

        Task<WorkflowStep?> GetCurrentStepAsync(Guid permitId);

        Task<bool> UserCanDecideAsync(Guid permitId, User user);

        Task<PermitApproval> SubmitDecisionAsync(Guid permitId, string approverUserId, ApprovalDecision decision, string? comment = null, Guid? executionGateId = null);

        Task<List<PermitApproval>> GetHistoryAsync(Guid permitId);

        Task NotifyCurrentApproversAsync(Guid permitId);
    }
}
