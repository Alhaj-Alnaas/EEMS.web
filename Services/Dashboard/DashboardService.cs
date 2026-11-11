using Core.Common;
using Core.Entities;
using Core.Interfaces.Services;
using Core.Interfaces.Services.Permits;
using Core.Models;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using static Core.Enums.BaseEnums;

namespace Services.Dashboard
{
    public class DashboardService : IDashboardService
    {
        private static readonly TimeSpan LateAfter = new(9, 0, 0);
        private static readonly TimeSpan ThreeOClock = new(15, 0, 0);

        private readonly IDbContextFactory<DataContext> _dbFactory;
        private readonly IMaterialPermitService _permits;

        public DashboardService(IDbContextFactory<DataContext> dbFactory, IMaterialPermitService permits)
        {
            _dbFactory = dbFactory;
            _permits = permits;
        }

        public async Task<LeaderDashboard> BuildAsync(User user)
        {
            var board = new LeaderDashboard
            {
                IsLeader = user.JobCatId == 1,
                LeaderName = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? "" : user.FullName.Trim(),
                UnitCode = ApprovalRoute.Normalize(user.ResponsibilityCode),
                UnitName = string.IsNullOrWhiteSpace(user.Department) ? "" : user.Department.Trim(),
                Day = DateTime.Today
            };

            if (!board.IsLeader)
                return board;

            board.IsSecurity = ApprovalRoute.BelongsTo(board.UnitCode, ApprovalRoute.SecurityAffairsCode);
            if (board.IsSecurity && string.IsNullOrWhiteSpace(board.UnitName))
                board.UnitName = SecurityTitle(board.UnitCode);

            if (board.IsSecurity)
                board.Security = await BuildSecurityAsync();
            else
                board.Division = await BuildDivisionAsync(user, board.UnitCode);

            return board;
        }

        private async Task<DivisionBoard> BuildDivisionAsync(User user, string unit)
        {
            var people = await PeopleAsync();
            var mine = people.Where(p => InUnit(p.DeptCode, unit) || InUnit(p.RespCode, unit)).ToList();
            var attendance = Attendance(mine.Select(p => p.Id), await TodayMovesAsync());

            var permits = await PermitRowsAsync();
            var own = permits.Where(p => InUnit(p.Req, unit)).ToList();

            var awaiting = 0;
            try
            {
                awaiting = (await _permits.GetInboxAsync(user)).Count;
            }
            catch
            {
                awaiting = 0;
            }

            return new DivisionBoard
            {
                RegisteredEmployees = mine.Count,
                AbsentToday = attendance.Absent,
                LateAfterNine = attendance.Late,
                LeftBeforeThree = attendance.LeftBeforeThree,
                InExecution = own.Count(InExecution),
                AwaitingLeader = awaiting,
                Executed = own.Count(Executed)
            };
        }

        private async Task<SecurityBoard> BuildSecurityAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var people = await PeopleAsync();
            var moves = await TodayMovesAsync();
            var attendance = Attendance(people.Select(p => p.Id), moves);

            var permits = await PermitRowsAsync();
            var alive = permits.Where(p => !Cancelled(p)).ToList();
            var openIds = await db.MovementLogs.AsNoTracking()
                .Where(m => !m.isDeleted && m.PermitId != null)
                .Select(m => m.PermitId!.Value)
                .Distinct()
                .ToListAsync();
            var openSet = openIds.ToHashSet();

            var readers = await db.Readers.AsNoTracking()
                .Where(r => !r.isDeleted && r.IsActive)
                .Select(r => r.Status)
                .ToListAsync();

            var gates = await db.Gates.AsNoTracking()
                .Where(g => !g.isDeleted)
                .Select(g => new { g.Id, g.isActive })
                .ToListAsync();
            var usedGates = moves.Select(m => m.GateId).Distinct().Count();

            return new SecurityBoard
            {
                Employees = people.Count,
                PresentToday = attendance.Present,
                AbsentToday = attendance.Absent,
                LateAfterNine = attendance.Late,
                LeftBeforeThree = attendance.LeftBeforeThree,
                LeftAfterThree = attendance.LeftAfterThree,
                PermitsTotal = alive.Count,
                Materials = alive.Count(p => Kind(p) == PermitClassification.Materials),
                Visitors = alive.Count(p => Kind(p) == PermitClassification.Visitors),
                Vehicles = alive.Count(p => Kind(p) == PermitClassification.Cars),
                EmployeeAccess = alive.Count(p => Kind(p) == PermitClassification.EmployeeAccess),
                ActivePermits = alive.Count(InExecution),
                OpenPermits = alive.Count(p => InExecution(p) && openSet.Contains(p.Id)),
                PendingPermits = alive.Count(Pending),
                ExecutedPermits = permits.Count(Executed),
                Readers = readers.Count,
                ReadersOnline = readers.Count(s => s == ReaderStatus.Online),
                ReadersOffline = readers.Count(s => s == ReaderStatus.Offline),
                ReadersFaulty = readers.Count(s => s == ReaderStatus.Faulty),
                Gates = gates.Count,
                ActiveGates = gates.Count(g => g.isActive),
                GatesUsedToday = usedGates,
                MovementsToday = moves.Count
            };
        }

        private async Task<List<Person>> PeopleAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var employees = await db.Employees.AsNoTracking()
                .Where(e => !e.isDeleted && e.IsActive)
                .Select(e => new { e.Id, e.FileNumber, Dept = e.Department != null ? e.Department.Code : "" })
                .ToListAsync();

            var users = await db.Users.AsNoTracking()
                .Where(u => u.FileNumber != null && u.FileNumber != "")
                .Select(u => new { u.FileNumber, u.ResponsibilityCode })
                .ToListAsync();
            var byFile = users
                .GroupBy(u => u.FileNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().ResponsibilityCode, StringComparer.OrdinalIgnoreCase);

            return employees.Select(e =>
            {
                var file = (e.FileNumber ?? "").Trim();
                byFile.TryGetValue(file, out var resp);
                return new Person(e.Id, e.Dept, resp);
            }).ToList();
        }

        private async Task<List<Move>> TodayMovesAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var day = DateTime.Today;
            var next = day.AddDays(1);
            var rows = await db.MovementLogs.AsNoTracking()
                .Where(m => !m.isDeleted && m.EmployeeId != null && m.EventTime >= day && m.EventTime < next)
                .Select(m => new { Id = m.EmployeeId!.Value, m.Direction, m.EventTime, m.GateId })
                .ToListAsync();
            return rows.Select(m => new Move(m.Id, m.Direction, m.EventTime, m.GateId)).ToList();
        }

        private async Task<List<PermitRow>> PermitRowsAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var rows = await db.Permits.AsNoTracking()
                .Where(p => !p.isDeleted)
                .Select(p => new
                {
                    p.Id,
                    p.reqDepartment,
                    p.status,
                    p.isClosed,
                    p.UnifiedStatus,
                    p.statusDescription,
                    p.classification
                })
                .ToListAsync();
            return rows.Select(p => new PermitRow(
                p.Id, p.reqDepartment, p.status, p.isClosed, p.UnifiedStatus, p.statusDescription, p.classification)).ToList();
        }

        private static AttendanceCount Attendance(IEnumerable<Guid> ids, List<Move> moves)
        {
            var scope = ids.ToHashSet();
            var byPerson = moves.Where(m => scope.Contains(m.EmployeeId)).GroupBy(m => m.EmployeeId);
            var present = 0;
            var late = 0;
            var before = 0;
            var after = 0;
            foreach (var group in byPerson)
            {
                var ins = group.Where(m => m.Direction == MovementDirection.In).Select(m => m.Time).ToList();
                var outs = group.Where(m => m.Direction == MovementDirection.Out).Select(m => m.Time).ToList();
                if (ins.Count == 0)
                    continue;

                present++;
                if (ins.Min().TimeOfDay > LateAfter)
                    late++;
                if (outs.Count > 0 && outs.Max().TimeOfDay < ThreeOClock)
                    before++;
                if (outs.Count > 0 && outs.Max().TimeOfDay > ThreeOClock)
                    after++;
            }

            return new AttendanceCount(scope.Count - present, present, late, before, after);
        }

        private static bool InUnit(string? code, string unit) =>
            !string.IsNullOrWhiteSpace(unit) && ApprovalRoute.BelongsTo(code, unit);

        private static bool Cancelled(PermitRow p) =>
            p.Status == 'D' || p.Unified == nameof(UnifiedPermitStatus.Cancelled);

        private static bool Draft(PermitRow p) =>
            p.Unified == nameof(UnifiedPermitStatus.Draft);

        private static bool Pending(PermitRow p) =>
            !Cancelled(p) && !Draft(p) && p.Status == 'I'
            && p.Unified == nameof(UnifiedPermitStatus.PendingApproval);

        private static bool InExecution(PermitRow p) =>
            !p.Closed && !Cancelled(p) && !Draft(p) && p.Status != 'J' && p.Status != 'C'
            && (p.Status == 'A' || p.Unified is nameof(UnifiedPermitStatus.Approved) or nameof(UnifiedPermitStatus.Active));

        private static bool Executed(PermitRow p) =>
            (p.Closed || p.Status == 'C')
            && p.Unified != nameof(UnifiedPermitStatus.Expired)
            && p.Description != "مغلق"
            && p.Status != 'D';

        private static PermitClassification? Kind(PermitRow p) =>
            Enum.TryParse<PermitClassification>(p.Classification, out var kind) ? kind : null;

        private static string SecurityTitle(string code) => code switch
        {
            "45011" => "قسم الحراسة",
            "45012" => "قسم المراقبة الإلكترونية",
            _ => "إدارة الشؤون الأمنية"
        };

        private sealed record Person(Guid Id, string? DeptCode, string? RespCode);
        private sealed record Move(Guid EmployeeId, MovementDirection Direction, DateTime Time, Guid GateId);
        private sealed record PermitRow(Guid Id, string? Req, char Status, bool Closed, string? Unified, string? Description, string? Classification);
        private sealed record AttendanceCount(int Absent, int Present, int Late, int LeftBeforeThree, int LeftAfterThree);
    }
}
