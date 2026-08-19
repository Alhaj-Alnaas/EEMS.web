using Core.Entities;
using static Core.Enums.BaseEnums;

namespace Core.Interfaces.Services.Workflow
{
    /// <summary>
    /// Phase 2: drives a permit through its configured <see cref="WorkflowDefinition"/>,
    /// recording <see cref="PermitApproval"/> decisions and advancing/settling status.
    /// </summary>
    public interface IWorkflowEngine
    {
        Task<WorkflowDefinition?> GetActiveWorkflowAsync(PermitClassification classification);
        Task<WorkflowStep?> GetCurrentStepAsync(Guid permitId);
        Task<PermitApproval> SubmitDecisionAsync(Guid permitId, string approverUserId, ApprovalDecision decision, string? comment = null);
        Task<List<PermitApproval>> GetHistoryAsync(Guid permitId);
    }
}
