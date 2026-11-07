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
        private readonly IPermitNotifier _notifier;

        public MaterialPermitService(
            IUnitOfWork unitOfWork,
            IPermit permits,
            IWorkflowEngine workflow,
            IDepartmentService departments,
            IDbContextFactory<DataContext> dbFactory,
            IPermitNotifier notifier)
        {
            _unitOfWork = unitOfWork;
            _permits = permits;
            _workflow = workflow;
            _departments = departments;
            _dbFactory = dbFactory;
            _notifier = notifier;
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
            if (submit && request.ShiftApproval)
            {
                needsApproval = true;
            }
            else if (submit)
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
            await NotifySubmittedAsync(permit, submit, request.ShiftApproval, needsApproval);
            return permit.Id;
        }

        public async Task<Guid> SaveVehicleAsync(VehiclePermitRequest request, User user, bool submit)
        {
            if (user == null)
                throw new InvalidOperationException("المستخدم غير معروف.");
            ValidateVehicle(request, submit);

            var vehicles = NamedVehicles(request);
            var permitId = Guid.NewGuid();
            WorkflowDefinition? workflow = null;
            var needsApproval = false;
            if (submit && request.ShiftApproval)
            {
                needsApproval = true;
            }
            else if (submit)
            {
                workflow = await _workflow.EnsureActiveWorkflowAsync(PermitClassification.Cars);
                needsApproval = await _workflow.HasApplicableStepAsync(workflow, user);
            }

            var status = !submit
                ? UnifiedPermitStatus.Draft
                : needsApproval ? UnifiedPermitStatus.PendingApproval : UnifiedPermitStatus.Active;
            var primary = vehicles[0];

            var permit = new Permit
            {
                Id = permitId,
                createdBy = user.UserName,
                createdOn = DateTime.Now,
                no = request.PermitNumber,
                date = request.PermitDate,
                hourOfEntry = request.HourOfEntry,
                type = null,
                classification = nameof(PermitClassification.Cars),
                IsTemp = request.IsTemp,
                moveFrom = request.MoveFrom,
                moveTo = request.MoveTo,
                reqDepartment = request.RequestingDepartmentCode.Trim(),
                phoneNo = request.Phone,
                OrgDescription = request.ResponsibleParty,
                remarks = request.Notes,
                ValidFrom = request.PermitDate.Date,
                ValidTo = request.ValidTo.Date,
                ExpectedExecutionTime = string.IsNullOrWhiteSpace(request.ExpectedExecutionTime) ? null : request.ExpectedExecutionTime.Trim(),
                ShiftApproval = request.ShiftApproval,
                UnifiedStatus = status.ToString(),
                status = StatusMapper.ToLegacy(status),
                statusDescription = StatusMapper.UnifiedStatusDescription(status),
                WorkflowDefinitionId = workflow?.Id,
                VehicleDetail = VehicleDetail(permitId, request, primary, user.UserName),
                Cars = vehicles.Select(c => CarLine(permitId, c, request.ResponsibleParty, user.UserName)).ToList(),
                Procedures = new List<ProcedureMovment>
                {
                    Procedure(permitId, ProcedureType.Insert.ToString(), user, submit ? "إرسال للاعتماد" : "حفظ مسودة")
                }
            };

            await _permits.InsertPermitAsync(permit);
            await NotifySubmittedAsync(permit, submit, request.ShiftApproval, needsApproval);
            return permit.Id;
        }

        public async Task UpdateVehicleAsync(Guid permitId, VehiclePermitRequest request, User user)
        {
            if (user == null)
                throw new InvalidOperationException("المستخدم غير معروف.");
            ValidateVehicle(request, submit: false);

            await using var db = await _dbFactory.CreateDbContextAsync();
            var permit = await db.Permits
                .Include(p => p.Cars)
                .Include(p => p.VehicleDetail)
                .FirstOrDefaultAsync(p => p.Id == permitId)
                ?? throw new InvalidOperationException("التصريح غير موجود.");

            if (!StatusMapper.IsDraft(permit))
                throw new InvalidOperationException("التعديل متاح عندما تكون حالة التصريح مسودة.");

            var names = await GetSameCenterUserNamesAsync(user);
            if (!CanEdit(permit, user, names))
                throw new InvalidOperationException("التعديل متاح لمركز المسؤولية الذي أدخل التصريح.");

            var vehicles = NamedVehicles(request);
            var primary = vehicles[0];
            permit.date = request.PermitDate;
            permit.hourOfEntry = request.HourOfEntry;
            permit.type = null;
            permit.IsTemp = request.IsTemp;
            permit.moveFrom = request.MoveFrom;
            permit.moveTo = request.MoveTo;
            permit.reqDepartment = request.RequestingDepartmentCode.Trim();
            permit.phoneNo = request.Phone;
            permit.OrgDescription = request.ResponsibleParty;
            permit.remarks = request.Notes;
            permit.ValidFrom = request.PermitDate.Date;
            permit.ValidTo = request.ValidTo.Date;
            permit.ExpectedExecutionTime = string.IsNullOrWhiteSpace(request.ExpectedExecutionTime) ? null : request.ExpectedExecutionTime.Trim();
            permit.ShiftApproval = request.ShiftApproval;
            permit.updatedBy = user.UserName;
            permit.updatedOn = DateTime.Now;

            if (permit.VehicleDetail == null)
                permit.VehicleDetail = VehicleDetail(permit.Id, request, primary, user.UserName);
            else
            {
                permit.VehicleDetail.PlateNumber = primary.CarNo.Trim();
                permit.VehicleDetail.VehicleType = primary.CarType;
                permit.VehicleDetail.DriverName = primary.DriverName;
                permit.VehicleDetail.LicenseNo = primary.licenseNo;
                permit.VehicleDetail.remarks = request.Purpose?.Trim();
                permit.VehicleDetail.InternalUse = request.InternalUse;
                permit.VehicleDetail.TripCount = Math.Max(1, request.TripCount);
                permit.VehicleDetail.updatedBy = user.UserName;
                permit.VehicleDetail.updatedOn = DateTime.Now;
            }

            foreach (var car in permit.Cars.Where(c => !c.isDeleted).ToList())
            {
                car.isDeleted = true;
                car.deletedBy = user.UserName;
                car.deletedOn = DateTime.Now;
            }

            foreach (var vehicle in vehicles)
                db.CarsMovment.Add(CarLine(permit.Id, vehicle, request.ResponsibleParty, user.UserName));

            db.ProceduresMovment.Add(Procedure(permit.Id, "Update", user, "تعديل تصريح المركبات"));
            await db.SaveChangesAsync();
        }

        public async Task<Guid> SaveVisitorAsync(VisitorPermitRequest request, User user, bool submit)
        {
            if (user == null)
                throw new InvalidOperationException("المستخدم غير معروف.");
            ValidateVisitor(request, submit);

            var visitors = NamedVisitors(request);
            var permitId = Guid.NewGuid();
            WorkflowDefinition? workflow = null;
            var needsApproval = false;
            if (submit && request.ShiftApproval)
                needsApproval = true;
            else if (submit)
            {
                workflow = await _workflow.EnsureActiveWorkflowAsync(PermitClassification.Visitors);
                needsApproval = await _workflow.HasApplicableStepAsync(workflow, user);
            }

            var status = !submit
                ? UnifiedPermitStatus.Draft
                : needsApproval ? UnifiedPermitStatus.PendingApproval : UnifiedPermitStatus.Active;
            var primary = visitors[0];

            var permit = new Permit
            {
                Id = permitId,
                createdBy = user.UserName,
                createdOn = DateTime.Now,
                no = request.PermitNumber,
                date = request.PermitDate,
                hourOfEntry = request.HourOfEntry,
                type = null,
                classification = nameof(PermitClassification.Visitors),
                IsTemp = request.IsTemp,
                moveFrom = request.MoveFrom,
                moveTo = request.MoveTo,
                reqDepartment = request.RequestingDepartmentCode.Trim(),
                phoneNo = request.Phone,
                OrgDescription = request.VisitorOrganization,
                remarks = request.Notes,
                ValidFrom = request.PermitDate.Date,
                ValidTo = request.ValidTo.Date,
                ExpectedExecutionTime = string.IsNullOrWhiteSpace(request.ExpectedExecutionTime) ? null : request.ExpectedExecutionTime.Trim(),
                ShiftApproval = request.ShiftApproval,
                HostEmployeeId = request.HostEmployeeId,
                UnifiedStatus = status.ToString(),
                status = StatusMapper.ToLegacy(status),
                statusDescription = StatusMapper.UnifiedStatusDescription(status),
                WorkflowDefinitionId = workflow?.Id,
                VisitorDetail = VisitorDetail(permitId, request, primary, user.UserName),
                Visitors = visitors.Select(v => VisitorLine(permitId, v, user.UserName)).ToList(),
                Cars = NamedCars(request).Select(c => CarLine(permitId, c, request.VisitorOrganization, user.UserName)).ToList(),
                Procedures = new List<ProcedureMovment>
                {
                    Procedure(permitId, ProcedureType.Insert.ToString(), user, submit ? "إرسال للاعتماد" : "حفظ مسودة")
                }
            };

            await _permits.InsertPermitAsync(permit);
            await NotifySubmittedAsync(permit, submit, request.ShiftApproval, needsApproval);
            return permit.Id;
        }

        public async Task UpdateVisitorAsync(Guid permitId, VisitorPermitRequest request, User user)
        {
            if (user == null)
                throw new InvalidOperationException("المستخدم غير معروف.");
            ValidateVisitor(request, submit: false);

            await using var db = await _dbFactory.CreateDbContextAsync();
            var permit = await db.Permits
                .Include(p => p.Visitors)
                .Include(p => p.Cars)
                .Include(p => p.VisitorDetail)
                .FirstOrDefaultAsync(p => p.Id == permitId)
                ?? throw new InvalidOperationException("التصريح غير موجود.");

            if (!StatusMapper.IsDraft(permit))
                throw new InvalidOperationException("التعديل متاح عندما تكون حالة التصريح مسودة.");

            var names = await GetSameCenterUserNamesAsync(user);
            if (!CanEdit(permit, user, names))
                throw new InvalidOperationException("التعديل متاح لمركز المسؤولية الذي أدخل التصريح.");

            var visitors = NamedVisitors(request);
            var primary = visitors[0];
            permit.date = request.PermitDate;
            permit.hourOfEntry = request.HourOfEntry;
            permit.type = null;
            permit.IsTemp = request.IsTemp;
            permit.moveFrom = request.MoveFrom;
            permit.moveTo = request.MoveTo;
            permit.reqDepartment = request.RequestingDepartmentCode.Trim();
            permit.phoneNo = request.Phone;
            permit.OrgDescription = request.VisitorOrganization;
            permit.remarks = request.Notes;
            permit.ValidFrom = request.PermitDate.Date;
            permit.ValidTo = request.ValidTo.Date;
            permit.ExpectedExecutionTime = string.IsNullOrWhiteSpace(request.ExpectedExecutionTime) ? null : request.ExpectedExecutionTime.Trim();
            permit.ShiftApproval = request.ShiftApproval;
            permit.HostEmployeeId = request.HostEmployeeId;
            permit.updatedBy = user.UserName;
            permit.updatedOn = DateTime.Now;

            if (permit.VisitorDetail == null)
                permit.VisitorDetail = VisitorDetail(permit.Id, request, primary, user.UserName);
            else
            {
                var detail = permit.VisitorDetail;
                detail.VisitorName = primary.Name.Trim();
                detail.IdNumber = primary.IdNumber;
                detail.Nationality = primary.Nationality.ToString();
                detail.Purpose = request.Purpose?.Trim();
                detail.HostFileNumber = request.HostFileNumber?.Trim();
                detail.HostName = request.HostName?.Trim();
                detail.HostDepartment = request.HostDepartment?.Trim();
                detail.HostEmployeeId = request.HostEmployeeId;
                detail.ExpectedVisitTime = request.ExpectedVisitTime;
                detail.ExpectedDepartureTime = request.ExpectedDepartureTime;
                detail.Phone = request.Phone;
                detail.updatedBy = user.UserName;
                detail.updatedOn = DateTime.Now;
            }

            foreach (var visitor in permit.Visitors.Where(v => !v.isDeleted).ToList())
            {
                visitor.isDeleted = true;
                visitor.deletedBy = user.UserName;
                visitor.deletedOn = DateTime.Now;
            }

            foreach (var car in permit.Cars.Where(c => !c.isDeleted).ToList())
            {
                car.isDeleted = true;
                car.deletedBy = user.UserName;
                car.deletedOn = DateTime.Now;
            }

            foreach (var visitor in visitors)
                db.PermitVisitors.Add(VisitorLine(permit.Id, visitor, user.UserName));
            foreach (var vehicle in NamedCars(request))
                db.CarsMovment.Add(CarLine(permit.Id, vehicle, request.VisitorOrganization, user.UserName));

            db.ProceduresMovment.Add(Procedure(permit.Id, "Update", user, "تعديل تصريح الزوار"));
            await db.SaveChangesAsync();
        }

        public async Task<List<Permit>> GetInboxAsync(User user)
        {
            var candidates = await _unitOfWork.Permits.GetQueryable()
                .AsNoTracking()
                .Include(p => p.EquipmentDetail)
                .Include(p => p.VehicleDetail)
                .Where(p => !p.isDeleted
                    && p.status == 'I'
                    && p.UnifiedStatus == nameof(UnifiedPermitStatus.PendingApproval))
                .OrderBy(p => p.createdOn)
                .ToListAsync();

            var inbox = new List<Permit>();
            foreach (var permit in candidates)
            {
                if (permit.ShiftApproval)
                {
                    if (ShiftDuty.IsOfficer(user))
                        inbox.Add(permit);
                    continue;
                }

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

            if (permit.ShiftApproval)
            {
                await DecideShiftAsync(permit, user, decision, comment);
                return;
            }

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

        public async Task<List<Permit>> GetForResponsibilityCenterAsync(User user, Guid? gateId = null)
        {
            var centerCodes = await ResponsibilityCodesAsync(user);
            var creatorNames = await GetSameCenterUserNamesAsync(user);
            var relatedIds = await RelatedPermitIdsAsync(user, gateId);
            var codeList = centerCodes.ToList();
            var nameList = creatorNames.ToList();
            var hasCodes = codeList.Count > 0;
            var hasNames = nameList.Count > 0;
            var hasRelated = relatedIds.Count > 0;
            if (!hasCodes && !hasNames && !hasRelated)
                return new List<Permit>();

            return await _unitOfWork.Permits.GetQueryable()
                .AsNoTracking()
                .Include(p => p.EquipmentDetail)
                .Include(p => p.VehicleDetail)
                .Include(p => p.VisitorDetail)
                .Where(p => (hasCodes && codeList.Contains(p.reqDepartment!))
                    || (hasNames && nameList.Contains(p.createdBy!))
                    || (hasRelated && relatedIds.Contains(p.Id)))
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

        private async Task<List<Guid>> RelatedPermitIdsAsync(User user, Guid? gateId)
        {
            var ids = new HashSet<Guid>();
            if (!string.IsNullOrWhiteSpace(user.Id))
            {
                var decided = await _unitOfWork.PermitApprovals.GetQueryable()
                    .AsNoTracking()
                    .Where(a => a.ApproverUserId == user.Id)
                    .Select(a => a.PermitId)
                    .Distinct()
                    .ToListAsync();
                foreach (var id in decided)
                    ids.Add(id);
            }

            if (!string.IsNullOrWhiteSpace(user.UserName))
            {
                var name = user.UserName;
                var fromProcedures = await _unitOfWork.ProceduresMovment.GetQueryable()
                    .AsNoTracking()
                    .Where(p => p.createdBy == name)
                    .Select(p => p.permitId)
                    .Distinct()
                    .ToListAsync();
                foreach (var id in fromProcedures)
                    ids.Add(id);

                var fromMoves = await _unitOfWork.MovementLogs.GetQueryable()
                    .AsNoTracking()
                    .Where(m => m.PermitId != null && m.createdBy == name)
                    .Select(m => m.PermitId!.Value)
                    .Distinct()
                    .ToListAsync();
                foreach (var id in fromMoves)
                    ids.Add(id);
            }

            if (gateId is Guid gate && gate != Guid.Empty)
            {
                var assigned = await _unitOfWork.Permits.GetQueryable()
                    .AsNoTracking()
                    .Where(p => p.GateId == gate)
                    .Select(p => p.Id)
                    .ToListAsync();
                foreach (var id in assigned)
                    ids.Add(id);

                var movedAtGate = await _unitOfWork.MovementLogs.GetQueryable()
                    .AsNoTracking()
                    .Where(m => m.GateId == gate && m.PermitId != null)
                    .Select(m => m.PermitId!.Value)
                    .Distinct()
                    .ToListAsync();
                foreach (var id in movedAtGate)
                    ids.Add(id);
            }

            return ids.ToList();
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
            await _notifier.NotifyAsync(permit, "إلغاء تصريح", PermitNoticeText.Cancelled(permit.no), user.Id);
        }

        public async Task SubmitDraftAsync(Guid permitId, User user)
        {
            if (user == null)
                throw new InvalidOperationException("المستخدم غير معروف.");

            var permit = await _unitOfWork.Permits.GetQueryable()
                .Include(p => p.EquipmentsAndMatirials)
                .Include(p => p.Cars)
                .Include(p => p.EquipmentDetail)
                .Include(p => p.VehicleDetail)
                .Include(p => p.VisitorDetail)
                .Include(p => p.Visitors)
                .FirstOrDefaultAsync(p => p.Id == permitId)
                ?? throw new InvalidOperationException("التصريح غير موجود.");

            if (!StatusMapper.IsDraft(permit))
                throw new InvalidOperationException("الإحالة للاعتماد متاحة عندما تكون حالة التصريح مسودة.");

            if (!await SameCenterAsCreatorAsync(permit, user))
                throw new InvalidOperationException("الإحالة للاعتماد متاحة لمركز المسؤولية الذي أدخل التصريح.");

            var isVehicle = string.Equals(permit.classification, nameof(PermitClassification.Cars), StringComparison.OrdinalIgnoreCase);
            var isVisitor = string.Equals(permit.classification, nameof(PermitClassification.Visitors), StringComparison.OrdinalIgnoreCase);
            if (isVisitor)
            {
                if (!permit.Visitors.Any(v => !v.isDeleted && !string.IsNullOrWhiteSpace(v.Name)))
                    throw new InvalidOperationException("أضف زائراً واحداً على الأقل قبل الإحالة للاعتماد.");
                if (string.IsNullOrWhiteSpace(permit.VisitorDetail?.Purpose))
                    throw new InvalidOperationException("الغرض من الزيارة مطلوب عند الإحالة للاعتماد.");
            }
            else if (isVehicle)
            {
                if (!permit.Cars.Any(c => !c.isDeleted && !string.IsNullOrWhiteSpace(c.carNo)))
                    throw new InvalidOperationException("أضف مركبة واحدة على الأقل قبل الإحالة للاعتماد.");
                if (string.IsNullOrWhiteSpace(permit.VehicleDetail?.remarks))
                    throw new InvalidOperationException("الغرض من التصريح مطلوب عند الإحالة للاعتماد.");
            }
            else
            {
                if (!permit.EquipmentsAndMatirials.Any(e => !e.isDeleted && !string.IsNullOrWhiteSpace(e.description)))
                    throw new InvalidOperationException("أضف مادة أو معدة واحدة على الأقل قبل الإحالة للاعتماد.");
                if (string.IsNullOrWhiteSpace(permit.EquipmentDetail?.Description))
                    throw new InvalidOperationException("الغرض من التصريح مطلوب عند الإحالة للاعتماد.");
            }

            if (permit.ShiftApproval)
            {
                permit.WorkflowDefinitionId = null;
                SetStatus(permit, UnifiedPermitStatus.PendingApproval, 'I');
                permit.updatedBy = user.UserName;
                permit.updatedOn = DateTime.Now;
                _unitOfWork.ProceduresMovment.Insert(Procedure(permit.Id, "submit", user, "إحالة لاعتماد المناوب"));
                await _unitOfWork.SaveAsync();
                await NotifyShiftOfficersAsync(permit);
                return;
            }

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
            else
                await _notifier.NotifyAsync(permit, "إحالة تصريح", PermitNoticeText.Ready(permit.no));
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

        private async Task DecideShiftAsync(Permit permit, User user, ApprovalDecision decision, string? comment)
        {
            if (!ShiftDuty.IsPending(permit))
                throw new InvalidOperationException("هذا التصريح لا ينتظر اعتماد المناوب.");
            if (!ShiftDuty.IsOfficer(user))
                throw new InvalidOperationException("اعتماد المناوب متاح لمستخدمي مركز المسؤولية 43010.");
            if (decision == ApprovalDecision.Rejected && string.IsNullOrWhiteSpace(comment))
                throw new InvalidOperationException("ملاحظة الرفض إلزامية.");

            _unitOfWork.PermitApprovals.Insert(new PermitApproval
            {
                Id = Guid.NewGuid(),
                PermitId = permit.Id,
                ApproverUserId = user.Id,
                Decision = decision,
                Comment = string.IsNullOrWhiteSpace(comment) ? ShiftDuty.StepLabel : comment.Trim(),
                DecidedOn = DateTime.Now,
                createdBy = user.UserName,
                createdOn = DateTime.Now
            });

            _unitOfWork.ProceduresMovment.Insert(Procedure(
                permit.Id,
                decision switch
                {
                    ApprovalDecision.Approved => "approve",
                    ApprovalDecision.Rejected => "reject",
                    ApprovalDecision.Returned => "return",
                    _ => decision.ToString()
                },
                user,
                ShiftDuty.StepLabel));

            permit.WorkflowDefinitionId = null;
            switch (decision)
            {
                case ApprovalDecision.Approved:
                    SetStatus(permit, UnifiedPermitStatus.Active, 'A');
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
            await NotifyShiftResultAsync(permit, decision, user, comment);
        }

        private async Task NotifySubmittedAsync(Permit permit, bool submit, bool shiftApproval, bool needsApproval)
        {
            if (!submit)
                return;
            if (shiftApproval)
                await NotifyShiftOfficersAsync(permit);
            else if (needsApproval)
                await _workflow.NotifyCurrentApproversAsync(permit.Id);
            else
                await _notifier.NotifyAsync(permit, "إحالة تصريح", PermitNoticeText.Ready(permit.no));
        }

        private async Task NotifyShiftOfficersAsync(Permit permit)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var users = await db.Users.AsNoTracking()
                .Where(u => u.JobStatus == "AE" && u.JobCatId == ShiftDuty.JobCategoryId && u.DesignationId == ShiftDuty.DesignationId)
                .ToListAsync();

            var officers = users.Where(ShiftDuty.IsOfficer).Select(u => u.Id);
            await _notifier.NotifyAsync(
                permit,
                "إحالة تصريح",
                PermitNoticeText.Submitted(permit.no, ShiftDuty.StepLabel),
                alsoUserIds: officers);
        }

        private async Task NotifyShiftResultAsync(Permit permit, ApprovalDecision decision, User actor, string? comment)
        {
            var authority = "المناوب";
            var (title, body) = decision switch
            {
                ApprovalDecision.Rejected => ("رفض تصريح", PermitNoticeText.Rejected(permit.no, authority, comment)),
                ApprovalDecision.Returned => ("إرجاع تصريح", PermitNoticeText.Returned(permit.no, authority, comment)),
                _ => ("اعتماد تصريح", PermitNoticeText.Approved(permit.no, authority, ready: true, nextStep: null))
            };
            await _notifier.NotifyAsync(permit, title, body, actor.Id);
        }

        private static void SetStatus(Permit permit, UnifiedPermitStatus status, char legacy)
        {
            permit.UnifiedStatus = status.ToString();
            permit.status = legacy;
            permit.statusDescription = StatusMapper.UnifiedStatusDescription(status);
        }

        private static void ValidateVehicle(VehiclePermitRequest request, bool submit)
        {
            if (string.IsNullOrWhiteSpace(request.RequestingDepartmentCode))
                throw new InvalidOperationException("يرجى اختيار الجهة الطالبة.");
            if (request.ValidTo.Date < request.PermitDate.Date)
                throw new InvalidOperationException("نهاية السريان لا يجوز أن تسبق تاريخ التصريح.");
            if (request.TripCount < 1)
                throw new InvalidOperationException("عدد الرحلات يجب أن يكون رحلة واحدة على الأقل.");
            if (!NamedVehicles(request).Any())
                throw new InvalidOperationException("أضف مركبة واحدة على الأقل.");
            if (submit && string.IsNullOrWhiteSpace(request.Purpose))
                throw new InvalidOperationException("الغرض من التصريح مطلوب عند الإرسال للاعتماد.");
        }

        private static void ValidateVisitor(VisitorPermitRequest request, bool submit)
        {
            if (string.IsNullOrWhiteSpace(request.RequestingDepartmentCode))
                throw new InvalidOperationException("يرجى اختيار الجهة الطالبة.");
            if (request.ValidTo.Date < request.PermitDate.Date)
                throw new InvalidOperationException("نهاية السريان لا يجوز أن تسبق تاريخ التصريح.");
            if (!NamedVisitors(request).Any())
                throw new InvalidOperationException("أضف زائراً واحداً على الأقل.");
            if (!submit)
                return;
            if (string.IsNullOrWhiteSpace(request.Purpose))
                throw new InvalidOperationException("الغرض من الزيارة مطلوب عند الإرسال للاعتماد.");
            if (string.IsNullOrWhiteSpace(request.HostFileNumber) || string.IsNullOrWhiteSpace(request.HostName))
                throw new InvalidOperationException("أدخل رقم ملف الداعي واعرض بياناته قبل الإرسال.");
            if (string.IsNullOrWhiteSpace(request.VisitorOrganization))
                throw new InvalidOperationException("جهة الزائر مطلوبة عند الإرسال للاعتماد.");
            if (string.IsNullOrWhiteSpace(request.ExpectedVisitTime) || string.IsNullOrWhiteSpace(request.ExpectedDepartureTime))
                throw new InvalidOperationException("الساعة المتوقعة للزيارة والمغادرة مطلوبة عند الإرسال للاعتماد.");
            if (NamedVisitors(request).Any(v => string.IsNullOrWhiteSpace(v.IdNumber)))
                throw new InvalidOperationException("رقم هوية كل زائر مطلوب عند الإرسال للاعتماد.");
        }

        private static List<VisitorLineViewModel> NamedVisitors(VisitorPermitRequest request) =>
            request.Visitors
                .Where(v => !string.IsNullOrWhiteSpace(v.Name))
                .GroupBy(v => $"{v.Name.Trim()}|{v.IdNumber}|{v.Nationality}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

        private static List<CarMovmentViewModel> NamedCars(VisitorPermitRequest request) =>
            request.Vehicles
                .Where(c => !string.IsNullOrWhiteSpace(c.CarNo))
                .GroupBy(c => $"{c.CarNo.Trim()}|{c.CarType}|{c.DriverName}|{c.licenseNo}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

        private static VisitorPermitDetail VisitorDetail(Guid permitId, VisitorPermitRequest request, VisitorLineViewModel primary, string? userName) =>
            new()
            {
                Id = Guid.NewGuid(),
                PermitId = permitId,
                VisitorName = primary.Name.Trim(),
                IdNumber = primary.IdNumber,
                Nationality = primary.Nationality.ToString(),
                Purpose = request.Purpose?.Trim(),
                HostFileNumber = request.HostFileNumber?.Trim(),
                HostName = request.HostName?.Trim(),
                HostDepartment = request.HostDepartment?.Trim(),
                HostEmployeeId = request.HostEmployeeId,
                ExpectedVisitTime = request.ExpectedVisitTime,
                ExpectedDepartureTime = request.ExpectedDepartureTime,
                Phone = request.Phone,
                createdBy = userName,
                createdOn = DateTime.Now
            };

        private static PermitVisitor VisitorLine(Guid permitId, VisitorLineViewModel visitor, string? userName) =>
            new()
            {
                Id = Guid.NewGuid(),
                PermitId = permitId,
                Name = visitor.Name.Trim(),
                IdNumber = visitor.IdNumber?.Trim(),
                Nationality = visitor.Nationality.ToString(),
                createdBy = userName,
                createdOn = DateTime.Now
            };

        private static List<CarMovmentViewModel> NamedVehicles(VehiclePermitRequest request) =>
            request.Vehicles
                .Where(c => !string.IsNullOrWhiteSpace(c.CarNo))
                .GroupBy(c => $"{c.CarNo.Trim()}|{c.CarType}|{c.DriverName}|{c.licenseNo}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

        private static VehiclePermitDetail VehicleDetail(Guid permitId, VehiclePermitRequest request, CarMovmentViewModel primary, string? userName) =>
            new()
            {
                Id = Guid.NewGuid(),
                PermitId = permitId,
                PlateNumber = primary.CarNo.Trim(),
                VehicleType = primary.CarType,
                DriverName = primary.DriverName,
                LicenseNo = primary.licenseNo,
                remarks = request.Purpose?.Trim(),
                InternalUse = request.InternalUse,
                TripCount = Math.Max(1, request.TripCount),
                createdBy = userName,
                createdOn = DateTime.Now
            };

        private static CarMovment CarLine(Guid permitId, CarMovmentViewModel vehicle, string? party, string? userName) =>
            new()
            {
                Id = Guid.NewGuid(),
                permitId = permitId,
                carNo = vehicle.CarNo.Trim(),
                carType = vehicle.CarType ?? "",
                name = vehicle.DriverName ?? "",
                licenseNo = vehicle.licenseNo ?? "",
                orginaization = party ?? "",
                createdBy = userName,
                createdOn = DateTime.Now
            };

        private static ProcedureMovment Procedure(Guid permitId, string type, User user, string remarks) =>
            new()
            {
                Id = Guid.NewGuid(),
                permitId = permitId,
                procedureType = type,
                donedBy = user.FullName,
                doneAs = user.JobtypeName,
                shift = "A",
                doneOn = DateTime.Now,
                remarks = remarks,
                createdBy = user.UserName,
                createdOn = DateTime.Now
            };
    }
}
