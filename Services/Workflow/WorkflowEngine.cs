using Core.Common;
using Core.Entities;
using Core.Interfaces.Services;
using Core.Interfaces.Services.Permits;
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
        private readonly IPermitNotifier _notifier;
        private readonly IDbContextFactory<DataContext> _dbFactory;
        private readonly IAuditLogService _audit;

        public WorkflowEngine(
            IUnitOfWork unitOfWork,
            IPermitNotifier notifier,
            IDbContextFactory<DataContext> dbFactory,
            IAuditLogService audit)
        {
            _unitOfWork = unitOfWork;
            _notifier = notifier;
            _dbFactory = dbFactory;
            _audit = audit;
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
            var directory = await DirectoryAsync();
            var code = entrant.ResponsibilityCode ?? "";
            var isLeader = entrant.JobCatId == 1;
            return ShapePath(workflow.Steps ?? Enumerable.Empty<WorkflowStep>(), workflow.Id, code, isLeader, directory).Count > 0;
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
            if (decision is ApprovalDecision.Rejected or ApprovalDecision.Returned && string.IsNullOrWhiteSpace(comment))
                throw new InvalidOperationException(decision == ApprovalDecision.Rejected
                    ? "ملاحظة القرار إلزامية عند الرفض."
                    : "ملاحظة القرار إلزامية عند الترجيع.");

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

            if (decision == ApprovalDecision.Approved
                && currentStep.StepType == WorkflowStepType.GuardSection
                && !EmployeeCardPermit.Is(permit))
            {
                var gate = await db.Gates.AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == executionGateId && g.isActive && !g.isDeleted)
                    ?? throw new InvalidOperationException("حدد بوابة التنفيذ قبل اعتماد رئيس قسم الحراسة.");
                permit.GateId = gate.Id;
                var gateNote = "بوابة التنفيذ: " + PermitExecutionService.GateLabel(gate);
                comment = string.IsNullOrWhiteSpace(comment) ? gateNote : comment.Trim() + " | " + gateNote;
            }

            await EnsureStepPersistedAsync(currentStep);

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

            var action = decision switch
            {
                ApprovalDecision.Rejected => AuditAction.Reject,
                ApprovalDecision.Returned => AuditAction.Return,
                _ => AuditAction.Approve
            };
            await _audit.WriteAsync(new AuditLogEntry
            {
                SourceKey = "permit-decision:" + approval.Id,
                Area = AuditArea.Permits,
                Action = action,
                Summary = AuditAction.Label(action) + " التصريح " + permit.no,
                ActorFileNumber = approver.UserName,
                ActorName = approver.FullName,
                OccurredAt = approval.DecidedOn
            });

            await NotifyAfterDecisionAsync(permit, decision, approver, currentStep, comment);
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
            var waiting = ApprovalPathCatalog.Label(step.StepType);
            await _notifier.NotifyAsync(
                permit,
                "إحالة تصريح",
                PermitNoticeText.Submitted(permit.no, waiting),
                alsoUserIds: recipients);
        }

        private async Task NotifyAfterDecisionAsync(
            Permit permit,
            ApprovalDecision decision,
            User approver,
            WorkflowStep step,
            string? comment)
        {
            var authority = PermitNoticeText.Authority(step.StepType);
            string title;
            string body;
            IEnumerable<string>? nextApprovers = null;

            if (decision == ApprovalDecision.Rejected)
            {
                title = "رفض تصريح";
                body = PermitNoticeText.Rejected(permit.no, authority, comment);
            }
            else if (decision == ApprovalDecision.Returned)
            {
                title = "إرجاع تصريح";
                body = PermitNoticeText.Returned(permit.no, authority, comment);
            }
            else
            {
                var ready = permit.UnifiedStatus is nameof(UnifiedPermitStatus.Active) or nameof(UnifiedPermitStatus.Approved);
                string? nextLabel = null;
                if (!ready)
                {
                    var next = await GetCurrentStepAsync(permit.Id);
                    nextLabel = next == null ? "جهة الاعتماد التالية" : ApprovalPathCatalog.Label(next.StepType);
                    if (next != null)
                        nextApprovers = await FindApproverIdsAsync(permit, next);
                }

                title = "اعتماد تصريح";
                body = PermitNoticeText.Approved(permit.no, authority, ready, nextLabel);
            }

            await _notifier.NotifyAsync(permit, title, body, approver.Id, nextApprovers);
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

        private RouteDirectory? directory;

        private async Task<RouteDirectory> DirectoryAsync()
        {
            if (directory != null)
                return directory;

            await using var db = await _dbFactory.CreateDbContextAsync();
            var rows = await db.Users.AsNoTracking()
                .Where(u => u.JobStatus == "AE" && u.ResponsibilityCode != null && u.ResponsibilityCode != "")
                .Select(u => new { u.ResponsibilityCode, u.JobCatId })
                .ToListAsync();

            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var leaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var code = ApprovalRoute.Normalize(row.ResponsibilityCode);
                if (code.Length == 0)
                    continue;
                known.Add(code);
                if (row.JobCatId == 1)
                    leaders.Add(code);
            }

            directory = new RouteDirectory(leaders, known);
            return directory;
        }

        private async Task<RouteContext> RouteForAsync(Permit permit)
        {
            var entrant = await FindUserByNameAsync(permit.createdBy);
            var units = await DirectoryAsync();
            var code = !string.IsNullOrWhiteSpace(entrant?.ResponsibilityCode)
                ? entrant!.ResponsibilityCode
                : permit.reqDepartment ?? "";
            return new RouteContext(code, entrant?.JobCatId == 1, units);
        }

        private async Task<List<WorkflowStep>> ApplicableStepsAsync(Permit permit)
        {
            var steps = await _unitOfWork.WorkflowSteps.GetQueryable()
                .AsNoTracking()
                .Where(s => s.WorkflowDefinitionId == permit.WorkflowDefinitionId && !s.isDeleted)
                .OrderBy(s => s.StepOrder)
                .ToListAsync();

            var route = await RouteForAsync(permit);
            return ShapePath(steps, permit.WorkflowDefinitionId ?? Guid.Empty, route.EntrantCode, route.EntrantIsLeader, route.Units);
        }

        private static List<WorkflowStep> ShapePath(
            IEnumerable<WorkflowStep> steps,
            Guid workflowId,
            string entrantCode,
            bool entrantIsLeader,
            RouteDirectory units)
        {
            var ordered = steps.Where(s => !s.isDeleted).OrderBy(s => s.StepOrder).ToList();
            var direct = entrantIsLeader
                ? null
                : ApprovalRoute.DirectSupervisor(entrantCode, false, units.Leaders);

            WorkflowStep? gate = null;
            if (!string.IsNullOrEmpty(direct))
            {
                gate = ordered.FirstOrDefault(step =>
                    ApprovalRoute.Same(TargetOf(step, entrantCode, entrantIsLeader, units), direct));
                if (gate == null && workflowId != Guid.Empty)
                    gate = DirectSupervisorGate(workflowId);
            }

            var path = new List<WorkflowStep>();
            if (gate != null)
                path.Add(gate);

            foreach (var step in ordered)
            {
                if (gate != null && step.Id == gate.Id)
                    continue;
                if (!StepApplies(step, entrantCode, entrantIsLeader, units))
                    continue;
                if (!string.IsNullOrEmpty(direct))
                {
                    if (ApprovalRoute.Same(TargetOf(step, entrantCode, entrantIsLeader, units), direct))
                        continue;
                    if (ApprovalPathCatalog.IsPathStep(step.StepType)
                        && !ApprovalRoute.ComesAfterDirectSupervisor(step.StepType, direct, units.KnownUnits))
                        continue;
                }

                path.Add(step);
            }

            return path;
        }

        private static WorkflowStep DirectSupervisorGate(Guid workflowId) => new()
        {
            Id = ApprovalRoute.DirectSupervisorStepId(workflowId),
            WorkflowDefinitionId = workflowId,
            StepOrder = 0,
            StepType = WorkflowStepType.DirectSupervisor,
            RoleName = ApprovalPathCatalog.Label(WorkflowStepType.DirectSupervisor),
            SlaHours = 24,
            IsRequired = true,
            createdOn = DateTime.Now,
            remarks = "اعتماد الرئيس المباشر"
        };

        private async Task EnsureStepPersistedAsync(WorkflowStep step)
        {
            var exists = await _unitOfWork.WorkflowSteps.GetQueryable().AnyAsync(s => s.Id == step.Id);
            if (exists || step.WorkflowDefinitionId == Guid.Empty)
                return;

            _unitOfWork.WorkflowSteps.Insert(new WorkflowStep
            {
                Id = step.Id,
                WorkflowDefinitionId = step.WorkflowDefinitionId,
                StepOrder = step.StepOrder,
                StepType = step.StepType,
                RoleName = step.RoleName,
                SlaHours = step.SlaHours,
                IsRequired = step.IsRequired,
                createdOn = DateTime.Now,
                remarks = string.IsNullOrWhiteSpace(step.remarks) ? "اعتماد الرئيس المباشر" : step.remarks
            });
        }

        private static string? TargetOf(WorkflowStep step, string entrantCode, bool entrantIsLeader, RouteDirectory units) =>
            ApprovalRoute.StepTarget(step.StepType, entrantCode, entrantIsLeader, units.Leaders, units.KnownUnits);

        private static bool StepApplies(WorkflowStep step, string entrantCode, bool entrantIsLeader, RouteDirectory units)
        {
            if (!ApprovalPathCatalog.IsPathStep(step.StepType))
                return true;
            return ApprovalRoute.StepTarget(step.StepType, entrantCode, entrantIsLeader, units.Leaders, units.KnownUnits) != null;
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
                var target = ApprovalRoute.StepTarget(
                    step.StepType, route.EntrantCode, route.EntrantIsLeader, route.Units.Leaders, route.Units.KnownUnits);
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

        private sealed record RouteDirectory(HashSet<string> Leaders, HashSet<string> KnownUnits);

        private sealed record RouteContext(string EntrantCode, bool EntrantIsLeader, RouteDirectory Units);

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
