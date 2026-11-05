using Core.Entities;
using Core.Models;
using static Core.Enums.BaseEnums;

namespace Core.Interfaces.Services.Workflow
{
    public interface IApprovalPathService
    {
        Task<List<ApprovalPathRow>> GetAsync();

        Task SaveAsync(PermitClassification classification, IReadOnlyCollection<WorkflowStepType> enabledSteps, User user);
    }
}
