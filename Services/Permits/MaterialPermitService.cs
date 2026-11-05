using Core.Common;
using Core.Entities;
using Core.Interfaces.Services;
using Core.Interfaces.Services.Permits;
using Core.Interfaces.Services.Workflow;
using Core.Interfaces.UnitOfWork;
using Core.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using static Core.Enums.BaseEnums;

namespace Services.Permits
{
    public class MaterialPermitService : IMaterialPermitService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPermit _permits;
        private readonly IWorkflowEngine _workflow;
        private readonly IDepartmentService _departments;
        private readonly IDbContextFactory<DataContext> _dbFactory;

        public MaterialPermitService(
            IUnitOfWork unitOfWork,
            IPermit permits,
            IWorkflowEngine workflow,
            IDepartmentService departments,
            IDbContextFactory<DataContext> dbFactory)
        {
            _unitOfWork = unitOfWork;
            _permits = permits;
            _workflow = workflow;
            _departments = departments;
            _dbFactory = dbFactory;
        }

        public async Task<Guid> SaveAsync(MaterialPermitRequest request, User user, bool submit)
        {
            if (user == null)
                throw new InvalidOperationException("المستخدم غير معروف.");
            if (string.IsNullOrWhiteSpace(request.RequestingDepartmentCode))
                throw new InvalidOperationException("يرجى اختيار الجهة الطالبة.");

            var items = request.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.ItemName))
                .ToList();
            if (items.Count == 0)
                throw new InvalidOperationException("أضف مادة أو معدة واحدة على الأقل.");
            if (submit && string.IsNullOrWhiteSpace(request.Purpose))
                throw new InvalidOperationException("الغرض من التصريح مطلوب عند الإرسال للاعتماد.");
            if (request.ValidTo.Date < request.PermitDate.Date)
                throw new InvalidOperationException("نهاية السريان لا يجوز أن تسبق تاريخ التصريح.");

            var permitId = Guid.NewGuid();
            WorkflowDefinition? workflow = null;
            var needsApproval = false;
            if (submit)
            {
                workflow = await _workflow.EnsureActiveWorkflowAsync(PermitClassification.Materials);
                needsApproval = await _workflow.HasApplicableStepAsync(workflow, user);
            }

            var status = !submit
                ? UnifiedPermitStatus.Draft
                : needsApproval ? UnifiedPermitStatus.PendingApproval : UnifiedPermitStatus.Active;

            var permit = new Permit
            {
                Id = permitId,
                createdBy = user.UserName,
                createdOn = DateTime.Now,
                no = request.PermitNumber,
                date = request.PermitDate,
                hourOfEntry = request.HourOfEntry,
                type = request.Type.ToString(),
                classification = "Materials",
                IsTemp = request.IsTemp,
                moveFrom = request.MoveFrom,
                moveTo = request.MoveTo,
                reqDepartment = request.RequestingDepartmentCode,
                phoneNo = request.Phone,
                OrgDescription = request.AuthorizedParty,
                remarks = request.Notes,
                ValidFrom = request.PermitDate.Date,
                ValidTo = request.ValidTo.Date,
                ExpectedExecutionTime = string.IsNullOrWhiteSpace(request.ExpectedExecutionTime) ? null : request.ExpectedExecutionTime.Trim(),
                ShiftApproval = request.ShiftApproval,
                UnifiedStatus = status.ToString(),
                status = StatusMapper.ToLegacy(status),
                statusDescription = StatusMapper.UnifiedStatusDescription(status),
                WorkflowDefinitionId = workflow?.Id,
                EquipmentDetail = new EquipmentPermitDetail
                {
                    Id = Guid.NewGuid(),
                    PermitId = permitId,
                    Description = request.Purpose?.Trim() ?? "",
                    Qty = items.Sum(i => i.Quantity),
                    Unit = items.FirstOrDefault()?.Unit.ToString() ?? "",
                    createdBy = user.UserName,
                    createdOn = DateTime.Now
                }
            };

            permit.EquipmentsAndMatirials = items.Select(e => new EquipMatiMovment
            {
                Id = Guid.NewGuid(),
                permitId = permitId,
                description = e.ItemName.Trim(),
                qty = e.Quantity,
                unit = e.Unit.ToString(),
                createdBy = user.UserName,
                createdOn = DateTime.Now
            }).ToList();

            permit.Cars = request.Cars
                .Where(c => !string.IsNullOrWhiteSpace(c.CarNo) || !string.IsNullOrWhiteSpace(c.DriverName))
                .Select(c => new CarMovment
                {
                    Id = Guid.NewGuid(),
                    permitId = permitId,
                    carType = c.CarType ?? "",
                    carNo = c.CarNo ?? "",
                    licenseNo = c.licenseNo ?? "",
                    name = c.DriverName ?? "",
                    nattiunality = c.driverNationality.ToString(),
                    orginaization = request.AuthorizedParty ?? "",
                    createdBy = user.UserName,
                    createdOn = DateTime.Now
                }).ToList();

            permit.Procedures = new List<ProcedureMovment>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    permitId = permitId,
                    procedureType = ProcedureType.Insert.ToString(),
                    donedBy = user.FullName,
                    doneAs = user.JobtypeName,
                    shift = "A",
                    doneOn = DateTime.Now,
                    remarks = submit ? "إرسال للاعتماد" : "حفظ مسودة",
                    createdBy = user.UserName,
                    createdOn = DateTime.Now
                }
            };

            await _permits.InsertPermitAsync(permit);

            if (submit && needsApproval)
                await _workflow.NotifyCurrentApproversAsync(permit.Id);

            return permit.Id;
        }

        public async Task<List<Permit>> GetInboxAsync(User user)
        {
            var candidates = await _unitOfWork.Permits.GetQueryable()
                .AsNoTracking()
                .Include(p => p.EquipmentDetail)
                .Where(p => !p.isDeleted
                    && p.status == 'I'
                    && p.UnifiedStatus == nameof(UnifiedPermitStatus.PendingApproval))
                .OrderBy(p => p.createdOn)
                .ToListAsync();

            var inbox = new List<Permit>();
            foreach (var permit in candidates)
            {
                var canDecide = permit.WorkflowDefinitionId != null
                    ? await _workflow.UserCanDecideAsync(permit.Id, user)
                    : _permits.GetVisibility(user, permit).CanApprove;

                if (canDecide)
                    inbox.Add(permit);
            }

            return inbox;
        }

        public async Task DecideAsync(Guid permitId, User user, ApprovalDecision decision, string? comment, Guid? executionGateId = null)
        {
            var permit = await _unitOfWork.Permits.GetQueryable()
                .FirstOrDefaultAsync(p => p.Id == permitId)
                ?? throw new InvalidOperationException("التصريح غير موجود.");

            if (permit.WorkflowDefinitionId != null)
            {
                await _workflow.SubmitDecisionAsync(permitId, user.Id, decision, comment, executionGateId);
                return;
            }

            if (!_permits.GetVisibility(user, permit).CanApprove)
                throw new InvalidOperationException("ليست لديك صلاحية اعتماد هذا التصريح.");
            if (decision == ApprovalDecision.Rejected && string.IsNullOrWhiteSpace(comment))
                throw new InvalidOperationException("ملاحظة الرفض إلزامية.");

            if (decision == ApprovalDecision.Approved && ApprovalRoute.Same(user.ResponsibilityCode, "45011"))
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                var gate = await db.Gates.AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == executionGateId && g.isActive && !g.isDeleted)
                    ?? throw new InvalidOperationException("حدد بوابة التنفيذ قبل اعتماد رئيس قسم الحراسة.");
                permit.GateId = gate.Id;
                var gateNote = "بوابة التنفيذ: " + PermitExecutionService.GateLabel(gate);
                comment = string.IsNullOrWhiteSpace(comment) ? gateNote : comment.Trim() + " | " + gateNote;
            }

            _unitOfWork.ProceduresMovment.Insert(new ProcedureMovment
            {
                Id = Guid.NewGuid(),
                permitId = permit.Id,
                procedureType = decision switch
                {
                    ApprovalDecision.Approved => "approve",
                    ApprovalDecision.Rejected => "reject",
                    ApprovalDecision.Returned => "return",
                    _ => decision.ToString()
                },
                donedBy = user.FullName,
                doneAs = user.JobtypeName,
                shift = "A",
                doneOn = DateTime.Now,
                remarks = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
                createdBy = user.UserName,
                createdOn = DateTime.Now
            });

            switch (decision)
            {
                case ApprovalDecision.Approved:
                    if (user.ResponsibilityCode == "45011")
                        permit.PermintsSectionApproval = true;
                    else if (user.ResponsibilityCode == "45012")
                        permit.secuSectionApproval = true;
                    else
                        permit.reqDepApproval = true;

                    if (permit.reqDepApproval && permit.secuSectionApproval)
                        SetStatus(permit, UnifiedPermitStatus.Active, 'A');
                    else
                        SetStatus(permit, UnifiedPermitStatus.PendingApproval, 'I');
                    break;
                case ApprovalDecision.Rejected:
                    SetStatus(permit, UnifiedPermitStatus.Rejected, 'J');
                    break;
                case ApprovalDecision.Returned:
                    permit.status = 'R';
                    permit.UnifiedStatus = nameof(UnifiedPermitStatus.PendingApproval);
                    permit.statusDescription = "مرجع";
                    break;
            }

            permit.updatedBy = user.UserName;
            permit.updatedOn = DateTime.Now;
            await _unitOfWork.SaveAsync();
        }

        public async Task<List<Permit>> GetForResponsibilityCenterAsync(User user)
        {
            var centerCodes = await ResponsibilityCodesAsync(user);
            var creatorNames = await GetSameCenterUserNamesAsync(user);
            var approvedIds = await ApprovedPermitIdsAsync(user);
            if (centerCodes.Count == 0 && creatorNames.Count == 0 && approvedIds.Count == 0)
                return new List<Permit>();

            var codeList = centerCodes.ToList();
            var nameList = creatorNames.ToList();

            return await _unitOfWork.Permits.GetQueryable()
                .AsNoTracking()
                .Include(p => p.EquipmentDetail)
                .Where(p => codeList.Contains(p.reqDepartment!)
                    || nameList.Contains(p.createdBy!)
                    || approvedIds.Contains(p.Id))
                .OrderByDescending(p => p.date)
                .ThenByDescending(p => p.createdOn)
                .ToListAsync();
        }

        public async Task<bool> WasApprovedByAsync(Guid permitId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            return await _unitOfWork.PermitApprovals.GetQueryable()
                .AsNoTracking()
                .AnyAsync(a => a.PermitId == permitId
                    && a.ApproverUserId == userId
                    && a.Decision == ApprovalDecision.Approved);
        }

        private async Task<List<Guid>> ApprovedPermitIdsAsync(User user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Id))
                return new List<Guid>();

            return await _unitOfWork.PermitApprovals.GetQueryable()
                .AsNoTracking()
                .Where(a => a.ApproverUserId == user.Id && a.Decision == ApprovalDecision.Approved)
                .Select(a => a.PermitId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<HashSet<string>> GetSameCenterUserNamesAsync(User user)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(user.UserName))
                names.Add(user.UserName);

            if (string.IsNullOrWhiteSpace(user.ResponsibilityCode))
                return names;

            await using var db = await _dbFactory.CreateDbContextAsync();
            var sameCenter = await db.Users.AsNoTracking()
                .Where(u => u.ResponsibilityCode == user.ResponsibilityCode && u.UserName != null)
                .Select(u => u.UserName!)
                .ToListAsync();

            foreach (var name in sameCenter)
                names.Add(name);

            return names;
        }

        public async Task CancelAsync(Guid permitId, User user)
        {
            var permit = await _unitOfWork.Permits.GetQueryable()
                .FirstOrDefaultAsync(p => p.Id == permitId)
                ?? throw new InvalidOperationException("التصريح غير موجود.");

            if (StatusMapper.IsApprovedOrExecuted(permit))
                throw new InvalidOperationException("لا يمكن إلغاء تصريح معتمد أو منفّذ.");

            if (!await SameCenterAsCreatorAsync(permit, user))
                throw new InvalidOperationException("الإلغاء متاح لمركز المسؤولية الذي أدخل التصريح.");

            SetStatus(permit, UnifiedPermitStatus.Cancelled, 'D');
            permit.statusDescription = "ملغي";
            permit.isDeleted = false;
            permit.updatedBy = user.UserName;
            permit.updatedOn = DateTime.Now;

            _unitOfWork.ProceduresMovment.Insert(new ProcedureMovment
            {
                Id = Guid.NewGuid(),
                permitId = permit.Id,
                procedureType = "delete",
                donedBy = user.FullName,
                doneAs = user.JobtypeName,
                shift = "A",
                doneOn = DateTime.Now,
                remarks = "إلغاء التصريح",
                createdBy = user.UserName,
                createdOn = DateTime.Now
            });

            await _unitOfWork.SaveAsync();
        }

        public async Task SubmitDraftAsync(Guid permitId, User user)
        {
            if (user == null)
                throw new InvalidOperationException("المستخدم غير معروف.");

            var permit = await _unitOfWork.Permits.GetQueryable()
                .Include(p => p.EquipmentsAndMatirials)
                .Include(p => p.EquipmentDetail)
                .FirstOrDefaultAsync(p => p.Id == permitId)
                ?? throw new InvalidOperationException("التصريح غير موجود.");

            if (!StatusMapper.IsDraft(permit))
                throw new InvalidOperationException("الإحالة للاعتماد متاحة عندما تكون حالة التصريح مسودة.");

            if (!await SameCenterAsCreatorAsync(permit, user))
                throw new InvalidOperationException("الإحالة للاعتماد متاحة لمركز المسؤولية الذي أدخل التصريح.");

            if (!permit.EquipmentsAndMatirials.Any(e => !e.isDeleted && !string.IsNullOrWhiteSpace(e.description)))
                throw new InvalidOperationException("أضف مادة أو معدة واحدة على الأقل قبل الإحالة للاعتماد.");

            if (string.IsNullOrWhiteSpace(permit.EquipmentDetail?.Description))
                throw new InvalidOperationException("الغرض من التصريح مطلوب عند الإحالة للاعتماد.");

            var classification = Enum.TryParse<PermitClassification>(permit.classification, out var parsed)
                ? parsed
                : PermitClassification.Materials;
            var workflow = await _workflow.EnsureActiveWorkflowAsync(classification);

            await using var db = await _dbFactory.CreateDbContextAsync();
            var creator = string.IsNullOrWhiteSpace(permit.createdBy)
                ? null
                : await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == permit.createdBy);
            var needsApproval = await _workflow.HasApplicableStepAsync(workflow, creator ?? user);
            var status = needsApproval ? UnifiedPermitStatus.PendingApproval : UnifiedPermitStatus.Active;

            permit.WorkflowDefinitionId = workflow.Id;
            SetStatus(permit, status, StatusMapper.ToLegacy(status));
            permit.updatedBy = user.UserName;
            permit.updatedOn = DateTime.Now;

            _unitOfWork.ProceduresMovment.Insert(new ProcedureMovment
            {
                Id = Guid.NewGuid(),
                permitId = permit.Id,
                procedureType = "submit",
                donedBy = user.FullName,
                doneAs = user.JobtypeName,
                shift = "A",
                doneOn = DateTime.Now,
                remarks = "إحالة للاعتماد",
                createdBy = user.UserName,
                createdOn = DateTime.Now
            });

            await _unitOfWork.SaveAsync();

            if (needsApproval)
                await _workflow.NotifyCurrentApproversAsync(permit.Id);
        }

        public static bool CanEdit(Permit permit, User user, IReadOnlySet<string> sameCenterUserNames) =>
            StatusMapper.IsDraft(permit)
            && !string.IsNullOrWhiteSpace(permit.createdBy)
            && sameCenterUserNames.Contains(permit.createdBy);

        private async Task<bool> SameCenterAsCreatorAsync(Permit permit, User user)
        {
            var names = await GetSameCenterUserNamesAsync(user);
            return !string.IsNullOrWhiteSpace(permit.createdBy) && names.Contains(permit.createdBy);
        }

        private async Task<HashSet<string>> ResponsibilityCodesAsync(User user)
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(user.ResponsibilityCode))
                codes.Add(user.ResponsibilityCode.Trim());

            if (string.IsNullOrWhiteSpace(user.ResponsibilityCode))
                return codes;

            var departments = await _departments.GetDepartmentsByResponsibilityAsync(user.ResponsibilityCode);
            foreach (var department in departments)
            {
                if (!string.IsNullOrWhiteSpace(department.RespCode))
                    codes.Add(department.RespCode.Trim());
            }

            return codes;
        }

        private static void SetStatus(Permit permit, UnifiedPermitStatus status, char legacy)
        {
            permit.UnifiedStatus = status.ToString();
            permit.status = legacy;
            permit.statusDescription = StatusMapper.UnifiedStatusDescription(status);
        }
    }
}
