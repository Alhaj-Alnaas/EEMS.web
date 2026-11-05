using Core.Common;
using Core.Entities;
using Core.Interfaces.Services.Workflow;
using Core.Interfaces.UnitOfWork;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using static Core.Enums.BaseEnums;

namespace Services.Workflow
{
    public class ApprovalPathService : IApprovalPathService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ApprovalPathService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<ApprovalPathRow>> GetAsync()
        {
            var workflows = await _unitOfWork.WorkflowDefinitions.GetQueryable()
                .Include(w => w.Steps)
                .Where(w => !w.isDeleted)
                .ToListAsync();

            return Enum.GetValues<PermitClassification>()
                .Select(classification => BuildRow(classification, workflows))
                .ToList();
        }

        public async Task SaveAsync(PermitClassification classification, IReadOnlyCollection<WorkflowStepType> enabledSteps, User user)
        {
            if (user == null)
                throw new InvalidOperationException("المستخدم غير معروف.");

            var selected = ApprovalPathCatalog.OrderedSteps
                .Where(enabledSteps.Contains)
                .ToList();

            if (selected.Count == 0)
                throw new InvalidOperationException("حدد خطوة واحدة على الأقل، أو اختر «لا يحتاج اعتماد».");

            if (selected.Contains(WorkflowStepType.NoApproval))
                selected = new List<WorkflowStepType> { WorkflowStepType.NoApproval };

            var existing = await _unitOfWork.WorkflowDefinitions.GetQueryable()
                .Include(w => w.Steps)
                .Where(w => w.PermitClassification == classification && !w.isDeleted)
                .ToListAsync();

            foreach (var previous in existing.Where(w => w.IsActive && ApprovalPathCatalog.IsConfiguration(w.Steps)))
            {
                previous.IsActive = false;
                previous.updatedBy = user.UserName;
                previous.updatedOn = DateTime.Now;
            }

            var workflowId = Guid.NewGuid();
            var version = existing.Count == 0 ? 1 : existing.Max(w => w.Version) + 1;
            var workflow = new WorkflowDefinition
            {
                Id = workflowId,
                Name = "مسار " + ApprovalPathCatalog.ClassificationTitle(classification),
                PermitClassification = classification,
                IsActive = true,
                Version = version,
                createdBy = user.UserName,
                createdOn = DateTime.Now,
                Steps = selected.Select((type, index) => new WorkflowStep
                {
                    Id = Guid.NewGuid(),
                    WorkflowDefinitionId = workflowId,
                    StepOrder = index + 1,
                    StepType = type,
                    RoleName = ApprovalPathCatalog.Label(type),
                    SlaHours = 24,
                    IsRequired = true,
                    createdBy = user.UserName,
                    createdOn = DateTime.Now
                }).ToList()
            };

            _unitOfWork.WorkflowDefinitions.Insert(workflow);
            await _unitOfWork.SaveAsync();
        }

        private static ApprovalPathRow BuildRow(PermitClassification classification, List<WorkflowDefinition> workflows)
        {
            var mine = workflows.Where(w => w.PermitClassification == classification).ToList();
            var configured = mine
                .Where(w => w.IsActive && ApprovalPathCatalog.IsConfiguration(w.Steps))
                .OrderByDescending(w => w.Version)
                .FirstOrDefault();
            var enabled = configured == null
                ? new HashSet<WorkflowStepType>()
                : configured.Steps.Where(s => !s.isDeleted).Select(s => s.StepType).ToHashSet();

            var runtime = mine
                .Where(w => w.IsActive && !ApprovalPathCatalog.IsConfiguration(w.Steps) && w.Steps.Any(s => !s.isDeleted))
                .OrderByDescending(w => w.Version)
                .FirstOrDefault();

            return new ApprovalPathRow
            {
                Classification = classification,
                Title = ApprovalPathCatalog.ClassificationTitle(classification),
                RuntimeSummary = runtime == null
                    ? "لا يوجد مسار مطبق على الإرسال حالياً"
                    : string.Join(" ← ", runtime.Steps.Where(s => !s.isDeleted).OrderBy(s => s.StepOrder).Select(s => ApprovalPathCatalog.Label(s.StepType))),
                Steps = ApprovalPathCatalog.OrderedSteps.Select(type => new ApprovalPathStepChoice
                {
                    Type = type,
                    Label = ApprovalPathCatalog.Label(type),
                    Hint = ApprovalPathCatalog.Hint(type),
                    Enabled = enabled.Contains(type)
                }).ToList()
            };
        }
    }
}
