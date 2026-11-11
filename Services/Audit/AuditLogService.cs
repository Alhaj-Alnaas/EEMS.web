using Core.Common;
using Core.Entities;
using Core.Interfaces.Services;
using Core.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using static Core.Enums.BaseEnums;

namespace Services.Audit
{
    public class AuditLogService : IAuditLogService
    {
        private const int Take = 400;
        private readonly IDbContextFactory<DataContext> _dbFactory;

        public AuditLogService(IDbContextFactory<DataContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task WriteAsync(AuditLogEntry entry)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                if (!string.IsNullOrWhiteSpace(entry.SourceKey)
                    && await db.AuditLogEntries.AnyAsync(x => x.SourceKey == entry.SourceKey))
                    return;

                if (entry.Id == Guid.Empty)
                    entry.Id = Guid.NewGuid();
                entry.OccurredAt = entry.OccurredAt == default ? DateTime.Now : entry.OccurredAt;
                entry.createdOn = DateTime.Now;
                entry.Summary = Trim(entry.Summary, 500);
                db.AuditLogEntries.Add(entry);
                await db.SaveChangesAsync();
            }
            catch
            {
                // The business change already succeeded. Losing one audit row must not undo it.
            }
        }

        public async Task<List<AuditRow>> SearchAsync(AuditQuery query)
        {
            var from = query.From?.Date;
            var to = query.To?.Date.AddDays(1);
            await using var db = await _dbFactory.CreateDbContextAsync();

            var storedQuery = db.AuditLogEntries.AsNoTracking().Where(x => !x.isDeleted);
            if (from != null)
                storedQuery = storedQuery.Where(x => x.OccurredAt >= from);
            if (to != null)
                storedQuery = storedQuery.Where(x => x.OccurredAt < to);
            if (!string.IsNullOrWhiteSpace(query.Area))
                storedQuery = storedQuery.Where(x => x.Area == query.Area);
            if (!string.IsNullOrWhiteSpace(query.Action))
                storedQuery = storedQuery.Where(x => x.Action == query.Action);

            var stored = await storedQuery
                .OrderByDescending(x => x.OccurredAt)
                .Take(Take)
                .ToListAsync();

            var known = stored
                .Select(x => x.SourceKey)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var people = await db.Users.AsNoTracking()
                .Select(u => new Person(u.Id, u.UserName, u.FileNumber, u.FullName))
                .ToListAsync();
            var byId = people.Where(p => !string.IsNullOrWhiteSpace(p.Id))
                .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var byName = people.Where(p => !string.IsNullOrWhiteSpace(p.UserName))
                .GroupBy(p => p.UserName!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var rows = stored.Select(entry => ToRow(entry, byName)).ToList();
            if (Wants(query, AuditArea.Permits, AuditAction.Cancel))
                rows.AddRange(await CancelledPermitsAsync(db, from, to, known, byName));
            if (WantsDecision(query))
                rows.AddRange(await DecisionsAsync(db, from, to, known, byId, byName, query.Action));
            if (Wants(query, AuditArea.Permits, AuditAction.Manual))
                rows.AddRange(await MovementsAsync(db, from, to, known));
            if (Wants(query, AuditArea.Administration, AuditAction.Delete))
            {
                rows.AddRange(await DeletedEmployeesAsync(db, from, to, known));
                rows.AddRange(await DeletedReadersAsync(db, from, to, known));
                rows.AddRange(await DeletedLevelsAsync(db, from, to, known));
            }

            IEnumerable<AuditRow> filtered = rows;
            if (!string.IsNullOrWhiteSpace(query.FileNumber))
            {
                var file = query.FileNumber.Trim();
                filtered = filtered.Where(row =>
                    row.FileNumber.Contains(file, StringComparison.OrdinalIgnoreCase)
                    || row.ActorName.Contains(file, StringComparison.OrdinalIgnoreCase));
            }

            return filtered
                .OrderByDescending(row => row.OccurredAt)
                .Take(Take)
                .ToList();
        }

        private static bool Wants(AuditQuery query, string area, string action) =>
            (string.IsNullOrWhiteSpace(query.Area) || query.Area == area)
            && (string.IsNullOrWhiteSpace(query.Action) || query.Action == action);

        private static bool WantsDecision(AuditQuery query)
        {
            if (!string.IsNullOrWhiteSpace(query.Area) && query.Area != AuditArea.Permits)
                return false;
            return string.IsNullOrWhiteSpace(query.Action)
                || query.Action is AuditAction.Approve or AuditAction.Reject or AuditAction.Return;
        }

        private async Task<List<AuditRow>> CancelledPermitsAsync(
            DataContext db, DateTime? from, DateTime? to, HashSet<string> known, Dictionary<string, Person> byName)
        {
            var query = db.Permits.AsNoTracking()
                .Where(p => p.status == 'D' || p.UnifiedStatus == nameof(UnifiedPermitStatus.Cancelled));
            if (from != null)
                query = query.Where(p => (p.updatedOn ?? p.createdOn) >= from);
            if (to != null)
                query = query.Where(p => (p.updatedOn ?? p.createdOn) < to);

            var permits = await query.OrderByDescending(p => p.updatedOn).Take(Take).ToListAsync();
            var rows = new List<AuditRow>();
            foreach (var permit in permits)
            {
                var key = "permit-cancel:" + permit.Id;
                if (!known.Add(key))
                    continue;
                var person = PersonOf(byName, permit.updatedBy);
                rows.Add(Row(
                    permit.updatedOn ?? permit.createdOn,
                    AuditArea.Permits,
                    AuditAction.Cancel,
                    "إلغاء التصريح " + permit.no,
                    person?.File ?? permit.updatedBy,
                    person?.Name,
                    null,
                    null,
                    null));
            }
            return rows;
        }

        private async Task<List<AuditRow>> DecisionsAsync(
            DataContext db, DateTime? from, DateTime? to, HashSet<string> known,
            Dictionary<string, Person> byId, Dictionary<string, Person> byName, string? action)
        {
            var query = db.PermitApprovals.AsNoTracking().Where(a => !a.isDeleted);
            if (from != null)
                query = query.Where(a => a.DecidedOn >= from);
            if (to != null)
                query = query.Where(a => a.DecidedOn < to);
            if (action == AuditAction.Approve)
                query = query.Where(a => a.Decision == ApprovalDecision.Approved);
            else if (action == AuditAction.Reject)
                query = query.Where(a => a.Decision == ApprovalDecision.Rejected);
            else if (action == AuditAction.Return)
                query = query.Where(a => a.Decision == ApprovalDecision.Returned);

            var approvals = await query.OrderByDescending(a => a.DecidedOn).Take(Take).ToListAsync();
            var permitIds = approvals.Select(a => a.PermitId).Distinct().ToList();
            var numbers = new Dictionary<Guid, string>();
            foreach (var permitId in permitIds)
            {
                var number = await db.Permits.AsNoTracking()
                    .Where(p => p.Id == permitId)
                    .Select(p => p.no)
                    .FirstOrDefaultAsync();
                if (!string.IsNullOrWhiteSpace(number))
                    numbers[permitId] = number;
            }

            var rows = new List<AuditRow>();
            foreach (var approval in approvals)
            {
                var key = "permit-decision:" + approval.Id;
                if (!known.Add(key))
                    continue;
                var code = approval.Decision switch
                {
                    ApprovalDecision.Rejected => AuditAction.Reject,
                    ApprovalDecision.Returned => AuditAction.Return,
                    _ => AuditAction.Approve
                };
                var verb = AuditAction.Label(code);
                numbers.TryGetValue(approval.PermitId, out var number);
                byId.TryGetValue(approval.ApproverUserId ?? "", out var byApprover);
                var person = byApprover ?? PersonOf(byName, approval.createdBy);
                rows.Add(Row(
                    approval.DecidedOn,
                    AuditArea.Permits,
                    code,
                    verb + " التصريح " + (string.IsNullOrWhiteSpace(number) ? "" : number),
                    person?.File ?? approval.createdBy,
                    person?.Name,
                    null,
                    null,
                    null));
            }
            return rows;
        }

        private async Task<List<AuditRow>> MovementsAsync(
            DataContext db, DateTime? from, DateTime? to, HashSet<string> known)
        {
            var query = db.MovementLogs.AsNoTracking()
                .Include(m => m.Gate)
                .Include(m => m.Reader)
                .Include(m => m.Permit)
                .Where(m => !m.isDeleted && (m.Source == MovementSource.GateOfficer || m.Source == MovementSource.Manual));
            if (from != null)
                query = query.Where(m => m.EventTime >= from);
            if (to != null)
                query = query.Where(m => m.EventTime < to);

            var moves = await query.OrderByDescending(m => m.EventTime).Take(Take).ToListAsync();
            var rows = new List<AuditRow>();
            foreach (var move in moves)
            {
                var key = "movement:" + move.Id;
                if (!known.Add(key))
                    continue;
                var direction = move.Direction == MovementDirection.In ? "دخول" : "خروج";
                var number = move.Permit?.no;
                var summary = string.IsNullOrWhiteSpace(number)
                    ? "تسجيل حركة " + direction
                    : "تسجيل حركة " + direction + " للتصريح " + number;
                var device = ReaderLabel(move.Reader);
                rows.Add(Row(
                    move.EventTime,
                    AuditArea.Permits,
                    AuditAction.Manual,
                    summary,
                    move.createdBy,
                    move.RecordedBy,
                    GateLabel(move.Gate),
                    device,
                    move.Reader?.IpAddress));
            }
            return rows;
        }

        private async Task<List<AuditRow>> DeletedEmployeesAsync(
            DataContext db, DateTime? from, DateTime? to, HashSet<string> known)
        {
            var query = db.Employees.AsNoTracking().Where(e => e.isDeleted && e.deletedOn != null);
            if (from != null)
                query = query.Where(e => e.deletedOn >= from);
            if (to != null)
                query = query.Where(e => e.deletedOn < to);
            var employees = await query.OrderByDescending(e => e.deletedOn).Take(200).ToListAsync();
            var rows = new List<AuditRow>();
            foreach (var employee in employees)
            {
                var key = "employee-delete:" + employee.Id;
                if (!known.Add(key))
                    continue;
                rows.Add(Row(
                    employee.deletedOn ?? employee.updatedOn ?? employee.createdOn,
                    AuditArea.Administration,
                    AuditAction.Delete,
                    "حذف الموظف " + employee.FileNumber + " " + employee.FullName,
                    employee.deletedBy,
                    null,
                    null,
                    null,
                    null));
            }
            return rows;
        }

        private async Task<List<AuditRow>> DeletedReadersAsync(
            DataContext db, DateTime? from, DateTime? to, HashSet<string> known)
        {
            var query = db.Readers.AsNoTracking().Include(r => r.Gate).Where(r => r.isDeleted);
            if (from != null)
                query = query.Where(r => (r.deletedOn ?? r.updatedOn ?? r.createdOn) >= from);
            if (to != null)
                query = query.Where(r => (r.deletedOn ?? r.updatedOn ?? r.createdOn) < to);
            var readers = await query.OrderByDescending(r => r.deletedOn).Take(200).ToListAsync();
            var rows = new List<AuditRow>();
            foreach (var reader in readers)
            {
                var key = "reader-delete:" + reader.Id;
                if (!known.Add(key))
                    continue;
                rows.Add(Row(
                    reader.deletedOn ?? reader.updatedOn ?? reader.createdOn,
                    AuditArea.Administration,
                    AuditAction.Delete,
                    "حذف قارئة البصمة " + reader.Name,
                    reader.deletedBy,
                    null,
                    GateLabel(reader.Gate),
                    ReaderLabel(reader),
                    reader.IpAddress));
            }
            return rows;
        }

        private async Task<List<AuditRow>> DeletedLevelsAsync(
            DataContext db, DateTime? from, DateTime? to, HashSet<string> known)
        {
            var query = db.AccessLevels.AsNoTracking().Where(a => a.isDeleted);
            if (from != null)
                query = query.Where(a => (a.deletedOn ?? a.updatedOn ?? a.createdOn) >= from);
            if (to != null)
                query = query.Where(a => (a.deletedOn ?? a.updatedOn ?? a.createdOn) < to);
            var levels = await query.OrderByDescending(a => a.deletedOn).Take(100).ToListAsync();
            var rows = new List<AuditRow>();
            foreach (var level in levels)
            {
                var key = "level-delete:" + level.Id;
                if (!known.Add(key))
                    continue;
                rows.Add(Row(
                    level.deletedOn ?? level.updatedOn ?? level.createdOn,
                    AuditArea.Administration,
                    AuditAction.Delete,
                    "حذف مستوى الدخول " + level.Name,
                    level.deletedBy,
                    null,
                    null,
                    null,
                    null));
            }
            return rows;
        }

        private static AuditRow ToRow(AuditLogEntry entry, Dictionary<string, Person> byName)
        {
            var person = PersonOf(byName, entry.ActorFileNumber);
            return Row(
                entry.OccurredAt,
                entry.Area,
                entry.Action,
                entry.Summary,
                person?.File ?? entry.ActorFileNumber,
                string.IsNullOrWhiteSpace(entry.ActorName) ? person?.Name : entry.ActorName,
                entry.GateName,
                entry.DeviceName,
                entry.NetworkAddress,
                entry.MacAddress);
        }

        private static AuditRow Row(
            DateTime when, string area, string action, string summary,
            string? file, string? name, string? gate, string? device, string? address, string? mac = null) =>
            new()
            {
                OccurredAt = when,
                Area = area,
                AreaLabel = AuditArea.Label(area),
                Action = action,
                ActionLabel = AuditAction.Label(action),
                Summary = string.IsNullOrWhiteSpace(summary) ? "—" : summary.Trim(),
                FileNumber = string.IsNullOrWhiteSpace(file) ? "—" : file.Trim(),
                ActorName = string.IsNullOrWhiteSpace(name) ? "—" : name.Trim(),
                GateName = string.IsNullOrWhiteSpace(gate) ? "—" : gate.Trim(),
                DeviceName = string.IsNullOrWhiteSpace(device) ? "—" : device.Trim(),
                NetworkAddress = string.IsNullOrWhiteSpace(address) ? "—" : address.Trim(),
                MacAddress = string.IsNullOrWhiteSpace(mac) ? "—" : mac.Trim()
            };

        private static Person? PersonOf(Dictionary<string, Person> byName, string? userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                return null;
            return byName.TryGetValue(userName.Trim(), out var person) ? person : null;
        }

        private static string GateLabel(Gate? gate)
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

        private static string ReaderLabel(Reader? reader)
        {
            if (reader == null)
                return "";
            var name = reader.Name?.Trim() ?? "";
            var serial = reader.DeviceSerial?.Trim() ?? "";
            if (name.Length == 0)
                return serial;
            if (serial.Length == 0)
                return name;
            return name + " (" + serial + ")";
        }

        private static string Trim(string? value, int max)
        {
            var text = (value ?? "").Trim();
            return text.Length <= max ? text : text[..max];
        }

        private sealed record Person(string Id, string? UserName, string? FileNumber, string? FullName)
        {
            public string? File => string.IsNullOrWhiteSpace(FileNumber) ? UserName : FileNumber;
            public string? Name => FullName;
        }
    }
}
