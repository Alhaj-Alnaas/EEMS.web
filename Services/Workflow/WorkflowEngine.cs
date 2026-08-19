using Core.Entities;
using Core.Interfaces.Services.Workflow;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using static Core.Enums.BaseEnums;

namespace Services.Workflow
{
    /// <summary>
    /// Thin first-cut workflow engine: enough to record approval decisions and
    /// look up the active workflow/current step for a permit classification.
    /// The full step-advancement/SLA logic is left as a follow-up (Phase 2 backlog).
    /// </summary>
    public class WorkflowEngine : IWorkflowEngine
    {
        private readonly IUnitOfWork _unitOfWork;

        public WorkflowEngine(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<WorkflowDefinition?> GetActiveWorkflowAsync(PermitClassification classification)
        {
            return await _unitOfWork.WorkflowDefinitions.GetQueryable()
                .Include(w => w.Steps)
                .Where(w => w.PermitClassification == classification && w.IsActive)
                .OrderByDescending(w => w.Version)
                .FirstOrDefaultAsync();
        }

        public async Task<WorkflowStep?> GetCurrentStepAsync(Guid permitId)
        {
            var permit = await _unitOfWork.Permits.GetByIdAsync(permitId);
            if (permit?.WorkflowDefinitionId == null) return null;

            var decidedStepIds = await _unitOfWork.PermitApprovals.GetQueryable()
                .Where(a => a.PermitId == permitId && a.WorkflowStepId != null)
                .Select(a => a.WorkflowStepId!.Value)
                .ToListAsync();

            return await _unitOfWork.WorkflowSteps.GetQueryable()
                .Where(s => s.WorkflowDefinitionId == permit.WorkflowDefinitionId && !decidedStepIds.Contains(s.Id))
                .OrderBy(s => s.StepOrder)
                .FirstOrDefaultAsync();
        }

        public async Task<PermitApproval> SubmitDecisionAsync(Guid permitId, string approverUserId, ApprovalDecision decision, string? comment = null)
        {
            var currentStep = await GetCurrentStepAsync(permitId);

            var approval = new PermitApproval
            {
                Id = Guid.NewGuid(),
                PermitId = permitId,
                WorkflowStepId = currentStep?.Id,
                ApproverUserId = approverUserId,
                Decision = decision,
                Comment = comment,
                DecidedOn = DateTime.Now,
                createdBy = approverUserId,
                createdOn = DateTime.Now
            };

            _unitOfWork.PermitApprovals.Insert(approval);
            await _unitOfWork.SaveAsync();
            return approval;
        }

        public async Task<List<PermitApproval>> GetHistoryAsync(Guid permitId)
        {
            return await _unitOfWork.PermitApprovals.GetQueryable()
                .Where(a => a.PermitId == permitId)
                .OrderBy(a => a.DecidedOn)
                .ToListAsync();
        }
    }
}
