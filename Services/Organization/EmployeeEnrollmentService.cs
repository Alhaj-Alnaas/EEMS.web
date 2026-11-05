using Core.Entities;
using Core.Interfaces.Services.Organization;
using Core.Interfaces.UnitOfWork;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Services.Organization
{
    public class EmployeeEnrollmentService : IEmployeeEnrollmentService
    {
        private readonly IUnitOfWork _unitOfWork;

        public EmployeeEnrollmentService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<DeviceCredentialStatus> CheckDeviceCredentialAsync(
            string fileNumber, CancellationToken ct = default)
        {
            var pin = (fileNumber ?? "").Trim();
            if (string.IsNullOrWhiteSpace(pin))
            {
                return new DeviceCredentialStatus(
                    false, null, null, 0, false,
                    "أدخل الرقم الوظيفي أولاً (نفس PIN المستخدم على القارئة).");
            }

            var employee = await FindByPinAsync(pin, ct);
            if (employee == null)
            {
                return new DeviceCredentialStatus(
                    false, null, pin, 0, false,
                    $"لا يوجد موظف برقم «{pin}» في النظام. سجّل بصمته/كرته على القارئة ثم اضغط «مزامنة من القارئات».");
            }

            var (templates, hasCard) = await GetCredentialCountsAsync(employee.Id, employee.CardNumber, ct);
            if (templates == 0 && !hasCard)
            {
                return new DeviceCredentialStatus(
                    false, employee.Id, employee.FileNumber, 0, false,
                    $"الموظف «{pin}» وُجد من القارئة لكن بلا بصمة/كرت في النظام بعد. أعد المزامنة أو أكمل التسجيل على الجهاز ثم زامن.");
            }

            var parts = new List<string>();
            if (templates > 0) parts.Add($"{templates} قالب بصمة/وجه");
            if (hasCard) parts.Add("كرت");
            return new DeviceCredentialStatus(
                true, employee.Id, employee.FileNumber, templates, hasCard,
                $"تم التحقق: {string.Join(" + ", parts)}. يمكن إكمال التسجيل.");
        }

        public async Task<EmployeeEnrollmentInfo?> GetEnrollmentInfoAsync(
            Guid employeeId, CancellationToken ct = default)
        {
            var map = await GetEnrollmentMapAsync(new[] { employeeId }, ct);
            return map.TryGetValue(employeeId, out var info) ? info : null;
        }

        public async Task<Dictionary<Guid, EmployeeEnrollmentInfo>> GetEnrollmentMapAsync(
            IEnumerable<Guid> employeeIds, CancellationToken ct = default)
        {
            var ids = employeeIds.Distinct().ToList();
            var result = new Dictionary<Guid, EmployeeEnrollmentInfo>();
            if (ids.Count == 0) return result;

            // SQL Server 2012: avoid large Contains→OPENJSON — batch in memory after load.
            var employees = await _unitOfWork.Employees.GetQueryable()
                .AsNoTracking()
                .Where(e => !e.isDeleted)
                .Select(e => new
                {
                    e.Id,
                    e.FileNumber,
                    e.FullName,
                    e.CardNumber,
                    e.JobTitle,
                    e.DepartmentName,
                    e.Address,
                    e.Nationality,
                    e.BloodType
                })
                .ToListAsync(ct);

            var idSet = ids.ToHashSet();
            employees = employees.Where(e => idSet.Contains(e.Id)).ToList();
            if (employees.Count == 0) return result;

            var empIds = employees.Select(e => e.Id).ToList();
            var templateCounts = await _unitOfWork.BiometricTemplates.GetQueryable()
                .AsNoTracking()
                .Where(t => !t.isDeleted)
                .GroupBy(t => t.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var tmplMap = templateCounts
                .Where(x => empIds.Contains(x.EmployeeId))
                .ToDictionary(x => x.EmployeeId, x => x.Count);

            var gateCounts = await _unitOfWork.EmployeePermissions.GetQueryable()
                .AsNoTracking()
                .Where(p => p.IsActive && !p.isDeleted)
                .GroupBy(p => p.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var gateMap = gateCounts
                .Where(x => empIds.Contains(x.EmployeeId))
                .ToDictionary(x => x.EmployeeId, x => x.Count);

            foreach (var e in employees)
            {
                tmplMap.TryGetValue(e.Id, out var templates);
                gateMap.TryGetValue(e.Id, out var gates);
                var hasCard = !string.IsNullOrWhiteSpace(e.CardNumber);
                var basic = IEmployeeEnrollmentService.HasBasicRegistration(
                    e.FullName, e.JobTitle, e.DepartmentName, e.Address, e.Nationality, e.BloodType);

                var phase = ResolvePhase(templates, hasCard, basic, gates);
                result[e.Id] = new EmployeeEnrollmentInfo(
                    e.Id,
                    e.FileNumber ?? "",
                    e.FullName ?? "",
                    phase,
                    templates,
                    hasCard,
                    gates,
                    basic);
            }

            return result;
        }

        public async Task<List<EmployeeEnrollmentInfo>> GetQueueAsync(
            EmployeeEnrollmentPhase? phase = null, CancellationToken ct = default)
        {
            var ids = await _unitOfWork.Employees.GetQueryable()
                .AsNoTracking()
                .Where(e => !e.isDeleted && e.FileNumber != null && e.FileNumber != "")
                .Select(e => e.Id)
                .ToListAsync(ct);

            var map = await GetEnrollmentMapAsync(ids, ct);
            var list = map.Values.AsEnumerable();
            if (phase.HasValue)
                list = list.Where(x => x.Phase == phase.Value);
            else
                list = list.Where(x => x.Phase != EmployeeEnrollmentPhase.Complete);

            return list
                .OrderBy(x => x.Phase)
                .ThenBy(x => x.FileNumber)
                .ToList();
        }

        private static EmployeeEnrollmentPhase ResolvePhase(
            int templates, bool hasCard, bool basic, int gates)
        {
            // موظف بياناته الأساسية مكتملة مسبقاً: لا يُصنَّف «بانتظار بصمة/كرت»
            // حتى لو كان عمود CardNumber فارغاً (الكرت قد يظهر في الحركة فقط).
            if (basic)
            {
                if (gates > 0)
                    return EmployeeEnrollmentPhase.Complete;
                return EmployeeEnrollmentPhase.NeedsGates;
            }

            var hasCredential = templates > 0 || hasCard;
            if (!hasCredential)
                return EmployeeEnrollmentPhase.AwaitingCredential;

            return EmployeeEnrollmentPhase.ReadyToRegister;
        }

        private async Task<Employee?> FindByPinAsync(string pin, CancellationToken ct)
        {
            var emp = await _unitOfWork.Employees.GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => !e.isDeleted && e.FileNumber == pin, ct);
            if (emp != null) return emp;

            if (!long.TryParse(pin, out var num)) return null;
            var normalized = num.ToString();
            return await _unitOfWork.Employees.GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => !e.isDeleted && e.FileNumber == normalized, ct);
        }

        private async Task<(int Templates, bool HasCard)> GetCredentialCountsAsync(
            Guid employeeId, string? cardNumber, CancellationToken ct)
        {
            var templates = await _unitOfWork.BiometricTemplates.GetQueryable()
                .AsNoTracking()
                .CountAsync(t => t.EmployeeId == employeeId && !t.isDeleted, ct);
            var hasCard = !string.IsNullOrWhiteSpace(cardNumber);
            return (templates, hasCard);
        }
    }
}
