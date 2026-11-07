using Core.Common;
using Core.Entities;
using Core.Extensions;
using Core.Interfaces.Services.Workflow;
using Core.Interfaces.UnitOfWork;
using DataAccess;
using Services.Permits;
using Microsoft.EntityFrameworkCore;
using static Core.Enums.BaseEnums;

namespace Services.Workflow
{
    /// <summary>
    /// Routes a permit across WorkflowSteps, keeps the legacy status character
    /// in sync, and notifies the next approver or the requester.
    /// </summary>
    public class WorkflowEngine : IWorkflowEngine
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;
        private readonly IDbContextFactory<DataContext> _dbFactory;

        public WorkflowEngine(
            IUnitOfWork unitOfWork,
            INotificationService notifications,
            IDbContextFactory<DataContext> dbFactory)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
            _dbFactory = dbFactory;
        }

        public async Task<WorkflowDefinition?> GetActiveWorkflowAsync(PermitClassification classification)
        {
            var active = await _unitOfWork.WorkflowDefinitions.GetQueryable()
                .Include(w => w.Steps)
                .Where(w => w.PermitClassification == classification && w.IsActive && !w.isDeleted)
                .OrderByDescending(w => w.Version)
                .ToListAsync();

            return active.FirstOrDefault(w => ApprovalPathCatalog.IsConfiguration(w.Steps))
                ?? active.FirstOrDefault();
        }

        public async Task<bool> HasApplicableStepAsync(WorkflowDefinition workflow, User entrant)
        {
            var leaders = await LeaderCodesAsync();
            var code = entrant.ResponsibilityCode ?? "";
            var isLeader = entrant.JobCatId == 1;
            return workflow.Steps?.Any(s => !s.isDeleted && StepApplies(s, code, isLeader, leaders)) == true;
        }

        public async Task<WorkflowDefinition> EnsureActiveWorkflowAsync(PermitClassification classification)
        {
            var existing = await GetActiveWorkflowAsync(classification);
            if (existing != null)
                return existing;

            if (classification != PermitClassification.Materials)
                throw new InvalidOperationException("لا يوجد مسار اعتماد فعّال لهذا النوع من التصاريح.");

            var workflowId = Guid.NewGuid();
            var workflow = new WorkflowDefinition
            {
                Id = workflowId,
                Name = "اعتماد تصريح المواد",
                PermitClassification = PermitClassification.Materials,
                IsActive = true,
                Version = 1,
                createdOn = DateTime.Now,
                Steps = new List<WorkflowStep>
                {
                    NewStep(workflowId, 1, WorkflowStepType.DepartmentApproval, "معتمد القسم"),
                    NewStep(workflowId, 2, WorkflowStepType.SecurityApproval, "معتمد الأمن")
                }
            };

            _unitOfWork.WorkflowDefinitions.Insert(workflow);
            await _unitOfWork.SaveAsync();
            return workflow;
        }

        public async Task<WorkflowStep?> GetCurrentStepAsync(Guid permitId)
        {
            var permit = await _unitOfWork.Permits.GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == permitId);

            if (permit?.WorkflowDefinitionId == null)
                return null;

            if (permit.UnifiedStatus is nameof(UnifiedPermitStatus.Draft)
                or nameof(UnifiedPermitStatus.Active)
                or nameof(UnifiedPermitStatus.Approved)
                or nameof(UnifiedPermitStatus.Rejected)
                or nameof(UnifiedPermitStatus.Cancelled)
                or nameof(UnifiedPermitStatus.Expired))
                return null;

            var steps = await ApplicableStepsAsync(permit);
            var approvedStepIds = await ApprovedStepIdsAsync(permitId);
            return steps.FirstOrDefault(s => !approvedStepIds.Contains(s.Id));
        }

        public async Task<bool> UserCanDecideAsync(Guid permitId, User user)
        {
            if (user == null)
                return false;

            var step = await GetCurrentStepAsync(permitId);
            if (step == null)
                return false;

            var permit = await _unitOfWork.Permits.GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == permitId);

            if (permit == null)
                return false;

            var route = await RouteForAsync(permit);
            return UserMatchesStep(user, permit, step, route);
        }

        public async Task<PermitApproval> SubmitDecisionAsync(Guid permitId, string approverUserId, ApprovalDecision decision, string? comment = null, Guid? executionGateId = null)
        {
            if (decision == ApprovalDecision.Rejected && string.IsNullOrWhiteSpace(comment))
                throw new InvalidOperationException("ملاحظة الرفض إلزامية.");

            var permit = await _unitOfWork.Permits.GetQueryable()
                .FirstOrDefaultAsync(p => p.Id == permitId)
                ?? throw new InvalidOperationException("التصريح غير موجود.");

            if (permit.WorkflowDefinitionId == null)
                throw new InvalidOperationException("هذا التصريح غير مرتبط بمسار اعتماد.");

            var currentStep = await GetCurrentStepAsync(permitId)
                ?? throw new InvalidOperationException("لا توجد خطوة اعتماد حالية لهذا التصريح.");

            await using var db = await _dbFactory.CreateDbContextAsync();
            var approver = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == approverUserId);
            var route = await RouteForAsync(permit);
            if (approver == null || !UserMatchesStep(approver, permit, currentStep, route))
                throw new InvalidOperationException("ليست لديك صلاحية اعتماد هذه الخطوة.");

            if (decision == ApprovalDecision.Approved && currentStep.StepType == WorkflowStepType.GuardSection)
            {
                var gate = await db.Gates.AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == executionGateId && g.isActive && !g.isDeleted)
                    ?? throw new InvalidOperationException("حدد بوابة التنفيذ قبل اعتماد رئيس قسم الحراسة.");
                permit.GateId = gate.Id;
                var gateNote = "بوابة التنفيذ: " + PermitExecutionService.GateLabel(gate);
                comment = string.IsNullOrWhiteSpace(comment) ? gateNote : comment.Trim() + " | " + gateNote;
            }

            var approval = new PermitApproval
            {
                Id = Guid.NewGuid(),
                PermitId = permitId,
                WorkflowStepId = currentStep.Id,
                ApproverUserId = approverUserId,
                Decision = decision,
                Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
                DecidedOn = DateTime.Now,
                createdBy = approver.UserName,
                createdOn = DateTime.Now
            };
            _unitOfWork.PermitApprovals.Insert(approval);

            _unitOfWork.ProceduresMovment.Insert(new ProcedureMovment
            {
                Id = Guid.NewGuid(),
                permitId = permit.Id,
                procedureType = ProcedureCode(decision),
                donedBy = approver.FullName,
                doneAs = approver.JobtypeName,
                shift = "A",
                doneOn = DateTime.Now,
                remarks = approval.Comment,
                createdBy = approver.UserName,
                createdOn = DateTime.Now
            });

            await ApplyDecisionAsync(permit, currentStep, decision);
            permit.updatedBy = approver.UserName;
            permit.updatedOn = DateTime.Now;
            await _unitOfWork.SaveAsync();

            await NotifyAfterDecisionAsync(permit, decision, approverUserId);
            return approval;
        }

        public async Task<List<PermitApproval>> GetHistoryAsync(Guid permitId)
        {
            return await _unitOfWork.PermitApprovals.GetQueryable()
                .Include(a => a.WorkflowStep)
                .Where(a => a.PermitId == permitId)
                .OrderBy(a => a.DecidedOn)
                .ToListAsync();
        }

        public async Task NotifyCurrentApproversAsync(Guid permitId)
        {
            var permit = await _unitOfWork.Permits.GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == permitId);
            var step = await GetCurrentStepAsync(permitId);
            if (permit == null || step == null)
                return;

            var recipients = await FindApproverIdsAsync(permit, step);
            var stepName = step.StepType.GetDescription();
            var kind = KindLabel(permit.classification);
            foreach (var userId in recipients)
            {
                await _notifications.NotifyAsync(
                    userId,
                    $"تصريح {kind} بانتظار الاعتماد",
                    $"التصريح {permit.no} بانتظار خطوة: {stepName}.",
                    permit.Id);
            }
        }

        private async Task NotifyAfterDecisionAsync(Permit permit, ApprovalDecision decision, string actorUserId)
        {
            var creatorId = await FindUserIdByNameAsync(permit.createdBy);
            if (!string.IsNullOrEmpty(creatorId) && creatorId != actorUserId)
            {
                var kind = KindLabel(permit.classification);
                var (title, body) = decision switch
                {
                    ApprovalDecision.Rejected => ($"رُفض تصريح {kind}", $"رُفض التصريح {permit.no}."),
                    ApprovalDecision.Returned => ($"أُرجع تصريح {kind}", $"أُرجع التصريح {permit.no} لمقدم الطلب."),
                    _ when permit.UnifiedStatus == nameof(UnifiedPermitStatus.Active)
                        => ($"تصريح {kind} أصبح نشطاً", $"اكتمل اعتماد التصريح {permit.no} وأصبح نشطاً."),
                    _ => ($"تقدّم اعتماد تصريح {kind}", $"اعتُمدت خطوة على التصريح {permit.no}.")
                };

                await _notifications.NotifyAsync(creatorId, title, body, permit.Id);
            }

            if (decision == ApprovalDecision.Approved && permit.UnifiedStatus == nameof(UnifiedPermitStatus.PendingApproval))
                await NotifyCurrentApproversAsync(permit.Id);
        }

        private async Task ApplyDecisionAsync(Permit permit, WorkflowStep step, ApprovalDecision decision)
        {
            switch (decision)
            {
                case ApprovalDecision.Approved:
                    SetLegacyFlag(permit, step.StepType, true);
                    var approved = await ApprovedStepIdsAsync(permit.Id);
                    approved.Add(step.Id);
                    var steps = await ApplicableStepsAsync(permit);
                    var next = steps.FirstOrDefault(s => !approved.Contains(s.Id));
                    if (next == null)
                        SetStatus(permit, UnifiedPermitStatus.Active, 'A');
                    else
                        SetStatus(permit, UnifiedPermitStatus.PendingApproval, 'I');
                    break;

                case ApprovalDecision.Rejected:
                    SetStatus(permit, UnifiedPermitStatus.Rejected, 'J');
                    break;

                case ApprovalDecision.Returned:
                    SetLegacyFlag(permit, step.StepType, false);
                    permit.status = 'R';
                    permit.UnifiedStatus = nameof(UnifiedPermitStatus.PendingApproval);
                    permit.statusDescription = "مرجع";
                    break;
            }
        }

        private async Task<HashSet<Guid>> ApprovedStepIdsAsync(Guid permitId)
        {
            var approvals = await _unitOfWork.PermitApprovals.GetQueryable()
                .Where(a => a.PermitId == permitId && a.WorkflowStepId != null)
                .ToListAsync();

            return approvals
                .GroupBy(a => a.WorkflowStepId!.Value)
                .Select(g => g.OrderByDescending(a => a.DecidedOn).First())
                .Where(a => a.Decision == ApprovalDecision.Approved)
                .Select(a => a.WorkflowStepId!.Value)
                .ToHashSet();
        }

        private async Task<List<string>> FindApproverIdsAsync(Permit permit, WorkflowStep step)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var users = await db.Users.AsNoTracking()
                .Where(u => u.JobStatus == "AE" && u.JobCatId == 1)
                .ToListAsync();

            var route = await RouteForAsync(permit);
            return users
                .Where(u => UserMatchesStep(u, permit, step, route))
                .Select(u => u.Id)
                .Distinct()
                .ToList();
        }

        private async Task<string?> FindUserIdByNameAsync(string? userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                return null;

            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Users.AsNoTracking()
                .Where(u => u.UserName == userName)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();
        }

        private HashSet<string>? leaderCodes;

        private async Task<HashSet<string>> LeaderCodesAsync()
        {
            if (leaderCodes != null)
                return leaderCodes;

            await using var db = await _dbFactory.CreateDbContextAsync();
            var codes = await db.Users.AsNoTracking()
                .Where(u => u.JobStatus == "AE" && u.JobCatId == 1 && u.ResponsibilityCode != null)
                .Select(u => u.ResponsibilityCode!)
                .ToListAsync();

            leaderCodes = codes
                .Select(ApprovalRoute.Normalize)
                .Where(c => c.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return leaderCodes;
        }

        private async Task<RouteContext> RouteForAsync(Permit permit)
        {
            var entrant = await FindUserByNameAsync(permit.createdBy);
            var leaders = await LeaderCodesAsync();
            var code = !string.IsNullOrWhiteSpace(entrant?.ResponsibilityCode)
                ? entrant!.ResponsibilityCode
                : permit.reqDepartment ?? "";
            return new RouteContext(code, entrant?.JobCatId == 1, leaders);
        }

        private async Task<List<WorkflowStep>> ApplicableStepsAsync(Permit permit)
        {
            var steps = await _unitOfWork.WorkflowSteps.GetQueryable()
                .AsNoTracking()
                .Where(s => s.WorkflowDefinitionId == permit.WorkflowDefinitionId && !s.isDeleted)
                .OrderBy(s => s.StepOrder)
                .ToListAsync();

            var route = await RouteForAsync(permit);
            return steps.Where(s => StepApplies(s, route.EntrantCode, route.EntrantIsLeader, route.LeaderCodes)).ToList();
        }

        private static bool StepApplies(WorkflowStep step, string entrantCode, bool entrantIsLeader, IReadOnlySet<string> leaders)
        {
            if (!ApprovalPathCatalog.IsPathStep(step.StepType))
                return true;
            return ApprovalRoute.StepTarget(step.StepType, entrantCode, entrantIsLeader, leaders) != null;
        }

        private async Task<User?> FindUserByNameAsync(string? userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                return null;

            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == userName);
        }

        private static bool UserMatchesStep(User user, Permit permit, WorkflowStep step, RouteContext route)
        {
            if (!string.Equals(user.JobStatus, "AE", StringComparison.OrdinalIgnoreCase))
                return false;
            if (user.JobCatId != 1)
                return false;

            var code = user.ResponsibilityCode ?? "";
            if (ApprovalPathCatalog.IsPathStep(step.StepType))
            {
                var target = ApprovalRoute.StepTarget(step.StepType, route.EntrantCode, route.EntrantIsLeader, route.LeaderCodes);
                return target != null && ApprovalRoute.Same(code, target);
            }

            var department = permit.reqDepartment ?? "";
            return step.StepType switch
            {
                WorkflowStepType.DepartmentApproval => SameDepartmentPrefix(code, department),
                WorkflowStepType.SecurityApproval => code == "45012",
                WorkflowStepType.PermitsSectionApproval => code == "45011",
                _ => string.Equals(user.JobtypeName, step.RoleName, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(user.UserType, step.RoleName, StringComparison.OrdinalIgnoreCase)
            };
        }

        private sealed record RouteContext(string EntrantCode, bool EntrantIsLeader, HashSet<string> LeaderCodes);

        private static bool SameDepartmentPrefix(string responsibilityCode, string departmentCode)
        {
            if (responsibilityCode.Length < 3 || departmentCode.Length < 3)
                return false;

            return string.Equals(responsibilityCode[..3], departmentCode[..3], StringComparison.OrdinalIgnoreCase);
        }

        private static void SetLegacyFlag(Permit permit, WorkflowStepType stepType, bool value)
        {
            switch (stepType)
            {
                case WorkflowStepType.DepartmentApproval:
                    permit.reqDepApproval = value;
                    break;
                case WorkflowStepType.SecurityApproval:
                    permit.secuSectionApproval = value;
                    break;
                case WorkflowStepType.PermitsSectionApproval:
                    permit.PermintsSectionApproval = value;
                    break;
            }
        }

        private static string KindLabel(string? classification) =>
            Enum.TryParse<PermitClassification>(classification, out var parsed)
                ? ApprovalPathCatalog.ClassificationTitle(parsed)
                : "التصريح";

        private static void SetStatus(Permit permit, UnifiedPermitStatus status, char legacy)
        {
            permit.UnifiedStatus = status.ToString();
            permit.status = legacy;
            permit.statusDescription = StatusMapper.UnifiedStatusDescription(status);
        }

        private static string ProcedureCode(ApprovalDecision decision) => decision switch
        {
            ApprovalDecision.Approved => "approve",
            ApprovalDecision.Rejected => "reject",
            ApprovalDecision.Returned => "return",
            _ => decision.ToString()
        };

        private static WorkflowStep NewStep(Guid workflowId, int order, WorkflowStepType type, string roleName) => new()
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = workflowId,
            StepOrder = order,
            StepType = type,
            RoleName = roleName,
            SlaHours = 24,
            IsRequired = true,
            createdOn = DateTime.Now
        };
    }
}
