using Core.Common;
using Core.Entities;
using Core.Interfaces.Services;
using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.Services.Permits;
using Core.Interfaces.Services.Workflow;
using Core.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Services.AccessControl;
using static Core.Enums.BaseEnums;

namespace Services.Permits
{
    public class PermitExecutionService : IPermitExecutionService
    {
        private readonly IDbContextFactory<DataContext> _dbFactory;
        private readonly INotificationService _notifications;
        private readonly IPermitNotifier _notifier;
        private readonly IAuditLogService _audit;
        private readonly IEmployeeDeviceSyncService _deviceSync;

        public PermitExecutionService(
            IDbContextFactory<DataContext> dbFactory,
            INotificationService notifications,
            IPermitNotifier notifier,
            IAuditLogService audit,
            IEmployeeDeviceSyncService deviceSync)
        {
            _dbFactory = dbFactory;
            _notifications = notifications;
            _notifier = notifier;
            _audit = audit;
            _deviceSync = deviceSync;
        }

        public async Task<List<Gate>> GetActiveGatesAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Gates.AsNoTracking()
                .Where(g => g.isActive && !g.isDeleted)
                .OrderBy(g => g.no)
                .ThenBy(g => g.description)
                .ToListAsync();
        }

        public async Task<Dictionary<PermitClassification, List<Guid>>> GetLinksAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var links = await db.PermitClassificationGates.AsNoTracking().ToListAsync();
            var result = new Dictionary<PermitClassification, List<Guid>>();
            foreach (var link in links)
            {
                if (!Enum.TryParse<PermitClassification>(link.Classification, out var classification))
                    continue;
                if (!result.TryGetValue(classification, out var ids))
                {
                    ids = new List<Guid>();
                    result[classification] = ids;
                }
                ids.Add(link.GateId);
            }
            return result;
        }

        public async Task SaveLinksAsync(PermitClassification classification, IReadOnlyCollection<Guid> gateIds)
        {
            var key = classification.ToString();
            var requested = gateIds.Distinct().ToList();

            await using var db = await _dbFactory.CreateDbContextAsync();
            var activeIds = await db.Gates.AsNoTracking()
                .Where(g => g.isActive && !g.isDeleted && requested.Contains(g.Id))
                .Select(g => g.Id)
                .ToListAsync();

            var existing = await db.PermitClassificationGates
                .Where(x => x.Classification == key)
                .ToListAsync();
            db.PermitClassificationGates.RemoveRange(existing);

            foreach (var gateId in activeIds)
            {
                db.PermitClassificationGates.Add(new PermitClassificationGate
                {
                    Id = Guid.NewGuid(),
                    Classification = key,
                    GateId = gateId
                });
            }

            await db.SaveChangesAsync();
            await _audit.WriteAsync(new AuditLogEntry
            {
                SourceKey = "gates:" + key + ":" + DateTime.Now.Ticks,
                Area = AuditArea.Administration,
                Action = AuditAction.Settings,
                Summary = "تعديل بوابات تنفيذ " + ApprovalPathCatalog.ClassificationTitle(classification),
                OccurredAt = DateTime.Now
            });
        }

        public async Task<List<PermitExecutionRow>> GetActivePermitsAsync()
        {
            var permits = await LoadActiveAsync();
            return await DescribeAsync(permits);
        }

        public async Task<Dictionary<Guid, string>> ExecutionGateLabelsAsync(IReadOnlyList<Permit> permits)
        {
            if (permits.Count == 0)
                return new Dictionary<Guid, string>();

            var rows = await DescribeAsync(permits.ToList());
            return rows.ToDictionary(
                row => row.Permit.Id,
                row => string.IsNullOrWhiteSpace(row.ExecutionScope) ? "—" : row.ExecutionScope);
        }

        public async Task<List<PermitExecutionRow>> GetExecutableAsync(Guid gateId)
        {
            var permits = await LoadActiveAsync();
            var rows = await DescribeAsync(permits);
            await using var db = await _dbFactory.CreateDbContextAsync();
            var linked = await db.PermitClassificationGates.AsNoTracking()
                .Where(x => x.GateId == gateId)
                .Select(x => x.Classification)
                .ToListAsync();
            var linkedSet = linked.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var ids = rows.Select(r => r.Permit.Id).ToList();
            var moves = ids.Count == 0
                ? new List<MovementLog>()
                : await db.MovementLogs.AsNoTracking()
                    .Where(m => m.PermitId != null && ids.Contains(m.PermitId.Value) && m.Source == MovementSource.GateOfficer)
                    .ToListAsync();
            var byPermit = moves.GroupBy(m => m.PermitId!.Value).ToDictionary(g => g.Key, g => (IReadOnlyList<MovementLog>)g.ToList());

            var executable = rows.Where(row => !EmployeeCardPermit.Is(row.Permit)).Where(row =>
            {
                var list = byPermit.TryGetValue(row.Permit.Id, out var found) ? found : Array.Empty<MovementLog>();
                var openGate = PermitExecutionPlan.OpenMovementGate(row.Permit, list);
                if (openGate != null)
                    return openGate == gateId;
                return row.PassedGuard
                    ? row.Permit.GateId == gateId
                    : linkedSet.Contains(row.Permit.classification ?? "");
            }).ToList();

            if (executable.Count == 0)
                return executable;

            foreach (var row in executable)
            {
                var step = PermitExecutionPlan.Next(row.Permit, byPermit.TryGetValue(row.Permit.Id, out var list) ? list : Array.Empty<MovementLog>());
                row.NextStep = step.Completed ? "تم التنفيذ" : step.Progress;
            }

            return executable;
        }

        public async Task<List<PermitExecutionRow>> GetExecutableCardPermitsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var permits = await db.Permits.AsNoTracking()
                .Include(p => p.RequesterEmployee)
                .Where(p => !p.isDeleted
                    && !p.isClosed
                    && p.status != 'C'
                    && p.status != 'D'
                    && p.status != 'J'
                    && (p.status == 'A'
                        || p.UnifiedStatus == nameof(UnifiedPermitStatus.Active)
                        || p.UnifiedStatus == nameof(UnifiedPermitStatus.Approved))
                    && (p.classification == nameof(PermitClassification.EmployeeAccess)
                        || p.classification == "دخول موظفين"
                        || p.classification == EmployeeCardPermit.Title))
                .OrderBy(p => p.date)
                .ThenBy(p => p.createdOn)
                .ToListAsync();

            return permits.Select(permit => new PermitExecutionRow
            {
                Permit = permit,
                PassedGuard = true,
                NextStep = "طباعة البطاقة"
            }).ToList();
        }

        private async Task<List<Permit>> LoadActiveAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Permits.AsNoTracking()
                .Include(p => p.EquipmentDetail)
                .Include(p => p.VehicleDetail)
                .Include(p => p.VisitorDetail)
                .Include(p => p.Visitors)
                .Include(p => p.Gate)
                .Where(p => !p.isDeleted
                    && !p.isClosed
                    && p.status != 'C'
                    && p.status != 'D'
                    && p.status != 'J'
                    && (p.status == 'A'
                        || p.UnifiedStatus == nameof(UnifiedPermitStatus.Active)
                        || p.UnifiedStatus == nameof(UnifiedPermitStatus.Approved)))
                .OrderByDescending(p => p.date)
                .ThenByDescending(p => p.createdOn)
                .ToListAsync();
        }

        private async Task<List<PermitExecutionRow>> DescribeAsync(List<Permit> permits)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var definitionIds = permits
                .Where(p => p.WorkflowDefinitionId != null)
                .Select(p => p.WorkflowDefinitionId!.Value)
                .Distinct()
                .ToList();

            var guardDefinitions = definitionIds.Count == 0
                ? new List<Guid>()
                : await db.WorkflowSteps.AsNoTracking()
                    .Where(s => definitionIds.Contains(s.WorkflowDefinitionId)
                        && !s.isDeleted
                        && s.StepType == WorkflowStepType.GuardSection)
                    .Select(s => s.WorkflowDefinitionId)
                    .Distinct()
                    .ToListAsync();
            var guardSet = guardDefinitions.ToHashSet();

            var links = await db.PermitClassificationGates.AsNoTracking()
                .Include(x => x.Gate)
                .ToListAsync();
            var namesByClass = links
                .GroupBy(x => x.Classification, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => string.Join("، ", g.Select(x => GateLabel(x.Gate)).Where(name => name.Length > 0)),
                    StringComparer.OrdinalIgnoreCase);

            return permits.Select(permit =>
            {
                var passedGuard = permit.WorkflowDefinitionId != null
                    && guardSet.Contains(permit.WorkflowDefinitionId.Value);
                string scope;
                if (passedGuard)
                    scope = permit.Gate == null ? "لم تُحدد بوابة التنفيذ" : GateLabel(permit.Gate);
                else if (namesByClass.TryGetValue(permit.classification ?? "", out var names) && !string.IsNullOrWhiteSpace(names))
                    scope = names;
                else
                    scope = "لا توجد بوابات مرتبطة بهذا النوع";

                return new PermitExecutionRow
                {
                    Permit = permit,
                    PassedGuard = passedGuard,
                    ExecutionScope = scope
                };
            }).ToList();
        }

        public async Task<(Permit Permit, PermitExecutionStep Step, List<PermitExecutionEventRow> History)> GetExecutionAsync(Guid permitId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var permit = await db.Permits.AsNoTracking()
                .Include(p => p.EquipmentDetail)
                .Include(p => p.EquipmentsAndMatirials)
                .Include(p => p.Cars)
                .Include(p => p.VehicleDetail)
                .Include(p => p.VisitorDetail)
                .Include(p => p.Visitors)
                .Include(p => p.Gate)
                .Include(p => p.RequesterEmployee)
                .Include(p => p.Approvals)
                    .ThenInclude(a => a.WorkflowStep)
                .FirstOrDefaultAsync(p => p.Id == permitId)
                ?? throw new InvalidOperationException("التصريح غير موجود.");

            var moves = await ExecutionMovesAsync(db, permitId);
            return (permit, PermitExecutionPlan.Next(permit, moves), History(moves));
        }

        public async Task<List<VisitorCardOption>> GetVisitorCardsAsync(Guid gateId, Guid? permitId = null)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var serials = await db.Readers.AsNoTracking()
                .Where(r => r.GateId == gateId && r.IsActive && !r.isDeleted && r.DeviceSerial != "")
                .Select(r => r.DeviceSerial)
                .ToListAsync();
            var serialSet = serials
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (serialSet.Count == 0)
                return new List<VisitorCardOption>();

            var people = await db.Employees.AsNoTracking()
                .Where(e => !e.isDeleted && e.CardNumber != null && e.CardNumber != "" && e.FileNumber != "")
                .Select(e => new { e.Id, e.FileNumber, e.CardNumber, e.FullName, e.remarks })
                .ToListAsync();

            var onGate = people
                .Where(e => SamePinAndCard(e.FileNumber, e.CardNumber))
                .Where(e => ZkTecoAccUserCommand.TryGetEnrolledSerials(e.remarks)
                    .Any(sn => serialSet.Contains(sn.Trim())))
                .GroupBy(e => e.CardNumber!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            if (onGate.Count == 0)
                return new List<VisitorCardOption>();

            var held = await HeldVisitorCardsAsync(db, permitId);
            return onGate
                .Where(e => !held.Contains(e.CardNumber!.Trim()))
                .Select(e => new VisitorCardOption
                {
                    EmployeeId = e.Id,
                    CardNumber = e.CardNumber!.Trim(),
                    Pin = e.FileNumber.Trim(),
                    Name = string.IsNullOrWhiteSpace(e.FullName) ? null : e.FullName.Trim()
                })
                .OrderBy(c => c.CardNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task ExecuteAsync(Guid permitId, Guid gateId, DateTime when, string? notes, User user, string? visitorCard = null)
        {
            if (user == null)
                throw new InvalidOperationException("المستخدم غير معروف.");
            if (when > DateTime.Now.AddMinutes(5))
                throw new InvalidOperationException("لا يمكن تسجيل وقت تنفيذ في المستقبل.");

            var allowed = await GetExecutableAsync(gateId);
            if (allowed.All(row => row.Permit.Id != permitId))
                throw new InvalidOperationException("هذا التصريح غير متاح للتنفيذ على هذه البوابة.");

            await using var db = await _dbFactory.CreateDbContextAsync();
            var permit = await db.Permits
                .Include(p => p.VehicleDetail)
                .Include(p => p.VisitorDetail)
                .Include(p => p.Visitors)
                .FirstOrDefaultAsync(p => p.Id == permitId)
                ?? throw new InvalidOperationException("التصريح غير موجود.");

            if (permit.isClosed || permit.status is 'C' or 'D')
                throw new InvalidOperationException("هذا التصريح مغلق.");

            var gate = await db.Gates.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == gateId && g.isActive && !g.isDeleted)
                ?? throw new InvalidOperationException("البوابة غير متاحة.");

            var moves = await ExecutionMovesAsync(db, permitId);
            var step = PermitExecutionPlan.Next(permit, moves);
            if (step.Completed)
                throw new InvalidOperationException("اكتمل تنفيذ هذا التصريح.");

            string? card = null;
            if (IsVisitors(permit))
            {
                card = visitorCard?.Trim();
                if (string.IsNullOrWhiteSpace(card))
                    throw new InvalidOperationException("اختر كرت الزائر قبل تسجيل الحركة.");

                var available = await GetVisitorCardsAsync(gateId, permitId);
                if (available.All(c => !string.Equals(c.CardNumber, card, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("الكرت المختار غير متاح على قارئات هذه البوابة.");
            }

            var movement = Record(db, permit, gate, step.Direction, when, notes, user.FullName, user.UserName, user.JobtypeName, step.ClosesPermit, card);
            await db.SaveChangesAsync();
            var direction = step.Direction == MovementDirection.In ? "دخول" : "خروج";
            await _audit.WriteAsync(new AuditLogEntry
            {
                SourceKey = "movement:" + movement.Id,
                Area = AuditArea.Permits,
                Action = AuditAction.Manual,
                Summary = "تسجيل حركة " + direction + " للتصريح " + permit.no,
                ActorFileNumber = user.UserName,
                ActorName = user.FullName,
                OccurredAt = when,
                GateName = GateLabel(gate)
            });
            await _notifier.NotifyAsync(
                permit,
                "تنفيذ تصريح",
                PermitNoticeText.Executed(permit.no, step.Direction == MovementDirection.In, when, step.ClosesPermit),
                user.Id);
        }

        public async Task<int> ExecuteCardAsync(Guid permitId, string cardNumber, User user)
        {
            if (user == null)
                throw new InvalidOperationException("المستخدم غير معروف.");

            var card = new string((cardNumber ?? "").Where(char.IsDigit).ToArray());
            if (card.Length == 0)
                throw new InvalidOperationException("رقم الكرت إلزامي.");
            if (card.Length > 20)
                throw new InvalidOperationException("رقم الكرت أطول من المسموح.");

            await using var db = await _dbFactory.CreateDbContextAsync();
            var permit = await db.Permits
                .Include(p => p.RequesterEmployee)
                .FirstOrDefaultAsync(p => p.Id == permitId)
                ?? throw new InvalidOperationException("التصريح غير موجود.");

            if (!EmployeeCardPermit.Is(permit))
                throw new InvalidOperationException("هذا التصريح ليس طلب إصدار بطاقة.");
            if (permit.isClosed || permit.status is 'C' or 'D' or 'J')
                throw new InvalidOperationException("هذا التصريح مغلق.");
            if (permit.status != 'A'
                && permit.UnifiedStatus != nameof(UnifiedPermitStatus.Active)
                && permit.UnifiedStatus != nameof(UnifiedPermitStatus.Approved))
                throw new InvalidOperationException("الطلب لم يُعتمد بعد.");

            var employee = permit.RequesterEmployee;
            if (employee == null && permit.RequesterEmployeeId != null)
            {
                employee = await db.Employees.FirstOrDefaultAsync(e =>
                    e.Id == permit.RequesterEmployeeId && !e.isDeleted);
            }

            var blocked = EmployeeCardPermit.PrintBlockMessage(employee);
            if (blocked != null)
                throw new InvalidOperationException(blocked);

            var taken = await db.Employees.AnyAsync(e =>
                !e.isDeleted
                && e.Id != employee!.Id
                && e.CardNumber != null
                && e.CardNumber == card);
            if (taken)
                throw new InvalidOperationException("رقم الكرت مستخدم لموظف آخر.");

            var previous = await db.EmployeeCards
                .Where(c => c.EmployeeId == employee!.Id && !c.isDeleted && c.Status == "Active")
                .ToListAsync();
            var now = DateTime.Now;
            foreach (var old in previous)
            {
                old.Status = "Cancelled";
                old.updatedBy = user.UserName;
                old.updatedOn = now;
            }

            employee!.CardNumber = card;
            employee.updatedBy = user.UserName;
            employee.updatedOn = now;
            db.EmployeeCards.Add(new EmployeeCard
            {
                Id = Guid.NewGuid(),
                EmployeeId = employee.Id,
                CardNumber = card,
                PhotoPath = employee.PhotoPath,
                PrintedOn = now,
                PrintedBy = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName,
                Status = "Active",
                createdBy = user.UserName,
                createdOn = now
            });
            db.ProceduresMovment.Add(new ProcedureMovment
            {
                Id = Guid.NewGuid(),
                permitId = permit.Id,
                procedureType = "print",
                donedBy = user.FullName,
                doneAs = user.JobtypeName,
                shift = "A",
                doneOn = now,
                remarks = "كرت: " + card,
                createdBy = user.UserName,
                createdOn = now
            });

            permit.isClosed = true;
            permit.status = 'C';
            permit.statusDescription = "منفذ";
            permit.closeOn = now;
            permit.updatedBy = user.UserName;
            permit.updatedOn = now;
            await db.SaveChangesAsync();

            var queued = await _deviceSync.SyncEmployeeToReadersAsync(employee);
            await _audit.WriteAsync(new AuditLogEntry
            {
                SourceKey = "permit-card:" + permit.Id,
                Area = AuditArea.Permits,
                Action = AuditAction.Manual,
                Summary = "تنفيذ طلب إصدار بطاقة " + permit.no + " برقم الكرت " + card,
                ActorFileNumber = user.UserName,
                ActorName = user.FullName,
                OccurredAt = now
            });
            await _notifier.NotifyAsync(
                permit,
                "تنفيذ تصريح",
                PermitNoticeText.CardPrinted(permit.no, card),
                user.Id);
            return queued;
        }

        public async Task<List<OpenPermitAlert>> GetOverdueAsync()
        {
            var now = DateTime.Now;
            var permits = await OpenPermitsAsync();
            var result = new List<OpenPermitAlert>();
            foreach (var permit in permits)
            {
                var moves = MovesOf(permit);
                var pastValidity = permit.ValidTo != null && permit.ValidTo.Value.Date < now.Date;
                var overdue = PermitExecutionPlan.IsOverdue(permit, moves, now, out var since, out var waiting);
                var suspended = pastValidity && PermitExecutionPlan.ShouldKeepOpenAfterValidity(permit, moves);
                if (!overdue && !suspended)
                    continue;
                if (!overdue)
                {
                    var openMove = moves.OrderByDescending(m => m.EventTime).FirstOrDefault();
                    since = openMove?.EventTime ?? permit.ValidTo ?? now;
                    waiting = PermitExecutionPlan.HasOpenMovement(permit, moves) && IsVisitors(permit)
                        ? "تسجيل ساعة الخروج"
                        : "تسجيل ساعة الدخول";
                }

                result.Add(new OpenPermitAlert
                {
                    PermitId = permit.Id,
                    Number = permit.no,
                    Kind = KindLabel(permit),
                    Waiting = waiting,
                    Since = since,
                    HoursOpen = (int)(now - since).TotalHours
                });
            }

            return result.OrderByDescending(r => r.HoursOpen).ToList();
        }

        public async Task RunMaintenanceAsync()
        {
            var now = DateTime.Now;
            await using var db = await _dbFactory.CreateDbContextAsync();
            var permits = await db.Permits
                .Include(p => p.VehicleDetail)
                .Include(p => p.MovementLogs)
                .Where(p => !p.isDeleted && !p.isClosed && p.status != 'C' && p.status != 'D' && p.status == 'A')
                .ToListAsync();

            var expiry = await CloseExpiredAsync(db, now);
            await db.SaveChangesAsync();
            foreach (var permit in expiry.Closed)
                await _notifier.NotifyAsync(permit, "إغلاق تصريح", PermitNoticeText.Expired(permit.no));
            foreach (var permit in expiry.MaterialsClosed)
                await _notifier.NotifyAsync(permit, "إغلاق تصريح", PermitNoticeText.MaterialsLeftOpen(permit.no));
            await NotifySuspendedAsync(expiry.Suspended);
            await NotifyOverdueAsync(permits, now);
        }

        private async Task NotifySuspendedAsync(List<Permit> permits)
        {
            const string title = "تصريح معلق بعد انتهاء السريان";
            foreach (var permit in permits)
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                var already = await db.Notifications.AsNoTracking().AnyAsync(n =>
                    n.RelatedPermitId == permit.Id && n.Title == title && !n.isDeleted);
                if (already)
                    continue;

                var body = IsVisitors(permit)
                    ? PermitNoticeText.VisitorStillInside(permit.no)
                    : PermitNoticeText.VehicleStillOut(permit.no);
                var officials = await OfficialIdsAsync(db, permit);
                await _notifier.NotifyAsync(permit, title, body, alsoUserIds: officials);
            }
        }

        private sealed class ExpiryResult
        {
            public List<Permit> Closed { get; } = new();
            public List<Permit> MaterialsClosed { get; } = new();
            public List<Permit> Suspended { get; } = new();
        }

        private static async Task<ExpiryResult> CloseExpiredAsync(DataContext db, DateTime now)
        {
            var today = now.Date;
            var expired = await db.Permits
                .Include(p => p.VehicleDetail)
                .Include(p => p.MovementLogs)
                .Where(p => !p.isDeleted
                    && !p.isClosed
                    && p.status != 'C'
                    && p.status != 'D'
                    && p.status != 'J'
                    && p.ValidTo != null
                    && p.ValidTo.Value.Date < today)
                .ToListAsync();

            var result = new ExpiryResult();
            foreach (var permit in expired)
            {
                var moves = MovesOf(permit);
                var active = permit.status == 'A'
                    || permit.UnifiedStatus == nameof(UnifiedPermitStatus.Active)
                    || permit.UnifiedStatus == nameof(UnifiedPermitStatus.Approved);
                if (active && PermitExecutionPlan.ShouldKeepOpenAfterValidity(permit, moves))
                {
                    result.Suspended.Add(permit);
                    continue;
                }

                var materialsPartial = PermitExecutionPlan.MaterialsEntryWithoutExit(permit, moves);
                var note = materialsPartial
                    ? "أغلق آليا بعد تسجيل الدخول دون الخروج لتجاوز تاريخ السريان، وعلى الجهة الطالبة إعداد تصريح استخراج"
                    : "أغلق آليا لتجاوز التاريخ المسموح به للتنفيذ";
                permit.isClosed = true;
                permit.status = 'C';
                permit.UnifiedStatus = nameof(UnifiedPermitStatus.Expired);
                permit.statusDescription = "مغلق";
                permit.closeOn = now;
                permit.updatedBy = "system";
                permit.updatedOn = now;
                db.ProceduresMovment.Add(new ProcedureMovment
                {
                    Id = Guid.NewGuid(),
                    permitId = permit.Id,
                    procedureType = "expire",
                    donedBy = "النظام",
                    doneAs = "النظام",
                    shift = "A",
                    doneOn = now,
                    remarks = note,
                    createdBy = "system",
                    createdOn = now
                });
                if (materialsPartial)
                    result.MaterialsClosed.Add(permit);
                else
                    result.Closed.Add(permit);
            }

            return result;
        }

        private async Task NotifyOverdueAsync(List<Permit> permits, DateTime now)
        {
            foreach (var permit in permits)
            {
                if (permit.isClosed || permit.status == 'C')
                    continue;
                var moves = MovesOf(permit);
                if (permit.ValidTo != null
                    && permit.ValidTo.Value.Date < now.Date
                    && PermitExecutionPlan.ShouldKeepOpenAfterValidity(permit, moves))
                    continue;
                if (!PermitExecutionPlan.IsOverdue(permit, moves, now, out var since, out var waiting))
                    continue;

                var title = "تصريح مفتوح تجاوز 24 ساعة";
                await using var db = await _dbFactory.CreateDbContextAsync();
                var already = await db.Notifications.AsNoTracking().AnyAsync(n =>
                    n.RelatedPermitId == permit.Id
                    && n.Title == title
                    && !n.isDeleted
                    && n.createdOn >= now.AddHours(-24));
                if (already)
                    continue;

                var body = $"التصريح {permit.no} بانتظار {waiting} منذ {since:yyyy/MM/dd HH:mm}.";
                foreach (var userId in await OfficialIdsAsync(db, permit))
                    await _notifications.NotifyAsync(userId, title, body, permit.Id);
            }
        }

        private static async Task<List<string>> OfficialIdsAsync(DataContext db, Permit permit)
        {
            var code = (permit.reqDepartment ?? "").Trim();
            var users = await db.Users.AsNoTracking()
                .Where(u => u.JobStatus == "AE" && (
                    (u.JobCatId == 1 && u.ResponsibilityCode == code)
                    || u.ResponsibilityCode == "45011"
                    || u.UserName == permit.createdBy))
                .Select(u => u.Id)
                .ToListAsync();
            return users.Distinct().ToList();
        }

        private async Task<List<Permit>> OpenPermitsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Permits.AsNoTracking()
                .Include(p => p.VehicleDetail)
                .Include(p => p.MovementLogs)
                .Where(p => !p.isDeleted && !p.isClosed && p.status != 'C' && p.status != 'D' && p.status == 'A')
                .ToListAsync();
        }

        private static async Task<List<MovementLog>> ExecutionMovesAsync(DataContext db, Guid permitId)
        {
            return await db.MovementLogs.AsNoTracking()
                .Include(m => m.Gate)
                .Where(m => m.PermitId == permitId && m.Source == MovementSource.GateOfficer)
                .OrderBy(m => m.EventTime)
                .ToListAsync();
        }

        private static List<MovementLog> MovesOf(Permit permit) =>
            permit.MovementLogs
                .Where(m => m.Source == MovementSource.GateOfficer)
                .OrderBy(m => m.EventTime)
                .ToList();

        private static List<PermitExecutionEventRow> History(IEnumerable<MovementLog> moves) =>
            moves.Select(m => new PermitExecutionEventRow
            {
                When = m.EventTime,
                Action = m.Direction == MovementDirection.In ? "دخول" : "خروج",
                Notes = m.ManualReason ?? "",
                Gate = GateLabel(m.Gate),
                Card = m.DevicePin?.Trim() ?? ""
            }).ToList();

        private static MovementLog Record(
            DataContext db,
            Permit permit,
            Gate gate,
            MovementDirection direction,
            DateTime when,
            string? notes,
            string? fullName,
            string? userName,
            string? job,
            bool closes,
            string? visitorCard = null)
        {
            var note = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            var card = string.IsNullOrWhiteSpace(visitorCard) ? null : visitorCard.Trim();
            if (card != null)
            {
                var cardNote = "كرت: " + card;
                note = string.IsNullOrWhiteSpace(note) ? cardNote : note + " — " + cardNote;
            }
            var movement = new MovementLog
            {
                Id = Guid.NewGuid(),
                PermitId = permit.Id,
                GateId = gate.Id,
                Direction = direction,
                EventTime = when,
                Source = MovementSource.GateOfficer,
                DevicePin = card,
                ManualReason = note,
                RecordedBy = fullName,
                createdBy = userName,
                createdOn = DateTime.Now
            };
            db.MovementLogs.Add(movement);

            db.ProceduresMovment.Add(new ProcedureMovment
            {
                Id = Guid.NewGuid(),
                permitId = permit.Id,
                procedureType = direction == MovementDirection.In ? "entry" : "exit",
                donedBy = fullName,
                doneAs = job,
                shift = "A",
                doneOn = when,
                remarks = note,
                createdBy = userName,
                createdOn = DateTime.Now
            });

            if (!closes)
                return movement;

            permit.isClosed = true;
            permit.status = 'C';
            permit.statusDescription = "منفذ";
            permit.closeOn = when;
            permit.updatedBy = userName;
            permit.updatedOn = DateTime.Now;
            return movement;
        }

        private static async Task<HashSet<string>> HeldVisitorCardsAsync(DataContext db, Guid? permitId)
        {
            var moves = await (
                from m in db.MovementLogs.AsNoTracking()
                join p in db.Permits.AsNoTracking() on m.PermitId equals p.Id
                where m.Source == MovementSource.GateOfficer
                    && m.DevicePin != null
                    && m.DevicePin != ""
                    && !p.isDeleted
                    && !p.isClosed
                    && p.status != 'C'
                    && (p.classification == nameof(PermitClassification.Visitors)
                        || p.classification == "زوار"
                        || p.classification == "تصريح زوار")
                select new { m.PermitId, m.DevicePin, m.Direction }
            ).ToListAsync();

            return moves
                .Where(m => permitId == null || m.PermitId != permitId)
                .GroupBy(m => m.DevicePin!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count(x => x.Direction == MovementDirection.In)
                    > g.Count(x => x.Direction == MovementDirection.Out))
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static bool SamePinAndCard(string? pin, string? card)
        {
            var left = pin?.Trim() ?? "";
            var right = card?.Trim() ?? "";
            return left.Length > 0
                && right.Length > 0
                && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVisitors(Permit permit)
        {
            var classification = permit.classification?.Trim() ?? "";
            return classification.Equals(nameof(PermitClassification.Visitors), StringComparison.OrdinalIgnoreCase)
                || classification is "زوار" or "تصريح زوار";
        }

        private static string KindLabel(Permit permit)
        {
            var classification = permit.classification?.Trim() ?? "";
            if (classification.Equals(nameof(PermitClassification.Visitors), StringComparison.OrdinalIgnoreCase) || classification is "زوار" or "تصريح زوار")
                return "زوار";
            if (classification.Equals(nameof(PermitClassification.Cars), StringComparison.OrdinalIgnoreCase) || classification is "سيارات" or "مركبات" or "تصريح سيارات")
                return "مركبات";
            return "مواد";
        }

        internal static string GateLabel(Gate? gate)
        {
            if (gate == null)
                return "";
            var number = gate.no?.Trim() ?? "";
            var description = gate.description?.Trim() ?? "";
            if (number.Length == 0)
                return description;
            if (description.Length == 0)
                return number;
            return number + " - " + description;
        }
    }
}
