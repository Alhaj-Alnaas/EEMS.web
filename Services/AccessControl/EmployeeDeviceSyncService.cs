using Core.Entities;
using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using static Core.Enums.BaseEnums;

namespace Services.AccessControl
{
    public class EmployeeDeviceSyncService : IEmployeeDeviceSyncService
    {
        private static readonly Regex FromSerial = new(@"from\s+([A-Za-z0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IUnitOfWork _unitOfWork;
        private readonly IDeviceCommandService _commands;
        private readonly ILogger<EmployeeDeviceSyncService> _logger;

        public EmployeeDeviceSyncService(
            IUnitOfWork unitOfWork,
            IDeviceCommandService commands,
            ILogger<EmployeeDeviceSyncService> logger)
        {
            _unitOfWork = unitOfWork;
            _commands = commands;
            _logger = logger;
        }

        public Task<int> SyncEmployeeStatusToReadersAsync(Employee employee, CancellationToken ct = default)
            => SyncEmployeeToReadersAsync(employee, ct);

        public async Task<int> SyncEmployeeToReadersAsync(Employee employee, CancellationToken ct = default)
        {
            var pin = employee.FileNumber?.Trim();
            if (string.IsNullOrWhiteSpace(pin))
            {
                _logger.LogWarning("Skip device sync — employee {Id} has no file number/PIN", employee.Id);
                return 0;
            }

            var readers = await ResolveReadersAsync(employee, pin, ct);
            if (readers.Count == 0)
            {
                _logger.LogInformation("No readers to sync for PIN={Pin} ({Name})", pin, employee.FullName);
                return 0;
            }

            var disable = (!employee.IsActive || employee.isDeleted) ? "1" : "0";
            var queued = 0;
            foreach (var reader in readers)
            {
                ct.ThrowIfCancellationRequested();
                await SupersedePendingUpdatesAsync(reader.Id, pin, ct);
                queued += await QueueAccAccessAsync(reader, pin, employee.FullName, disable, enrollUser: false, ct);
            }

            return queued;
        }

        public async Task<int> EnrollEmployeeOnReadersAsync(
            Employee employee,
            IReadOnlyList<Guid> readerIds,
            CancellationToken ct = default)
        {
            var pin = employee.FileNumber?.Trim();
            if (string.IsNullOrWhiteSpace(pin))
            {
                _logger.LogWarning("Skip enroll — employee {Id} has no file number/PIN", employee.Id);
                return 0;
            }

            if (readerIds == null || readerIds.Count == 0)
            {
                _logger.LogInformation("Skip enroll PIN={Pin} — no readers selected", pin);
                return 0;
            }

            // Avoid EF Contains→OPENJSON on SQL Server 2012: load then filter in memory.
            var idSet = readerIds.ToHashSet();
            var readers = (await _unitOfWork.Readers.GetQueryable()
                .Where(r => !r.isDeleted)
                .ToListAsync(ct))
                .Where(r => idSet.Contains(r.Id))
                .ToList();

            if (readers.Count == 0)
            {
                _logger.LogWarning(
                    "Skip enroll PIN={Pin} — selected reader ids not found: {Ids}",
                    pin, string.Join(',', readerIds));
                return 0;
            }

            var inactive = readers.Where(r => !r.IsActive).Select(r => r.DeviceSerial).ToList();
            if (inactive.Count > 0)
                _logger.LogWarning("Enroll PIN={Pin}: some selected readers are inactive: {Serials}",
                    pin, string.Join(',', inactive));

            readers = readers.Where(r => r.IsActive).ToList();
            if (readers.Count == 0)
            {
                _logger.LogWarning("Skip enroll PIN={Pin} — selected readers exist but IsActive=false", pin);
                return 0;
            }

            // Do NOT invent Acc uid — that creates blank rows on SpeedFace M4.
            // Update remarks on the tracked instance when possible to avoid EF tracking conflicts.
            var trackedEmp = await _unitOfWork.Employees.GetQueryable()
                .FirstOrDefaultAsync(e => e.Id == employee.Id, ct);

            if (trackedEmp != null)
            {
                trackedEmp.remarks = ZkTecoAccUserCommand.UpsertEnrolledSerials(
                    trackedEmp.remarks,
                    readers.Select(r => r.DeviceSerial));
                trackedEmp.updatedOn = DateTime.Now;
                employee.remarks = trackedEmp.remarks;
            }
            else
            {
                employee.remarks = ZkTecoAccUserCommand.UpsertEnrolledSerials(
                    employee.remarks,
                    readers.Select(r => r.DeviceSerial));
                _unitOfWork.Employees.Update(employee);
            }
            await _unitOfWork.SaveAsync();

            var disable = (!employee.IsActive || employee.isDeleted) ? "1" : "0";
            var name = trackedEmp?.FullName ?? employee.FullName;
            var queued = 0;
            foreach (var reader in readers)
            {
                ct.ThrowIfCancellationRequested();
                await SupersedePendingUpdatesAsync(reader.Id, pin, ct);
                queued += await QueueAccAccessAsync(reader, pin, name, disable, enrollUser: true, ct);
            }

            return queued;
        }

        private async Task<int> QueueAccAccessAsync(
            Reader reader, string pin, string? name, string disable, bool enrollUser, CancellationToken ct)
        {
            var employee = await _unitOfWork.Employees.GetQueryable()
                .FirstOrDefaultAsync(e => e.FileNumber == pin && !e.isDeleted, ct);
            var card = await ResolveCardNumberAsync(employee, ct);
            var userLine = ZkTecoAccUserCommand.UserInfoUpsertLine(pin, name ?? employee?.FullName, card);
            await _commands.EnqueueAsync(reader.Id, DeviceCommandType.UpdateUserInfo, userLine);
            var templates = await QueueTemplatesAsync(reader.Id, employee, pin, ct);
            await _commands.EnqueueAsync(reader.Id, DeviceCommandType.QueryBioData, pin);
            _logger.LogWarning(
                "Queued sync PIN={Pin} card={Card} templates={Templates} SN={Serial} enrollUser={Enroll}",
                pin, string.IsNullOrWhiteSpace(card) ? "-" : card, templates, reader.DeviceSerial, enrollUser);
            return 2 + templates;
        }

        private async Task<string?> ResolveCardNumberAsync(Employee? employee, CancellationToken ct)
        {
            if (employee == null) return null;
            if (!string.IsNullOrWhiteSpace(employee.CardNumber))
                return employee.CardNumber.Trim();

            var card = await _unitOfWork.EmployeeCards.GetQueryable()
                .Where(c => c.EmployeeId == employee.Id && !c.isDeleted && c.CardNumber != "")
                .Select(c => c.CardNumber)
                .FirstOrDefaultAsync(ct);
            return string.IsNullOrWhiteSpace(card) ? null : card.Trim();
        }

        private async Task<int> QueueTemplatesAsync(Guid readerId, Employee? employee, string pin, CancellationToken ct)
        {
            if (employee == null) return 0;

            var pending = await _unitOfWork.DeviceCommands.GetQueryable()
                .Where(c =>
                    c.ReaderId == readerId
                    && !c.isDeleted
                    && c.CommandType == nameof(DeviceCommandType.AddTemplate)
                    && c.Payload != null
                    && (c.Status == DeviceCommandStatus.Pending || c.Status == DeviceCommandStatus.Sent))
                .ToListAsync(ct);
            foreach (var cmd in pending.Where(c => PayloadTargetsPin(c.Payload, pin)))
            {
                cmd.Status = DeviceCommandStatus.Failed;
                cmd.LastError = "Superseded by a new template sync";
                cmd.CompletedOn = DateTime.Now;
                cmd.updatedOn = DateTime.Now;
                _unitOfWork.DeviceCommands.Update(cmd);
            }
            if (pending.Count > 0)
                await _unitOfWork.SaveAsync();

            var templates = await _unitOfWork.BiometricTemplates.GetQueryable()
                .Where(t => t.EmployeeId == employee.Id && !t.isDeleted && t.TemplateData != null)
                .ToListAsync(ct);

            var queued = 0;
            foreach (var template in templates)
            {
                if (template.TemplateData == null || template.TemplateData.Length == 0)
                    continue;
                if (!TryParseTemplateMarker(template.remarks, out var marker))
                {
                    marker = new TemplateMarker(
                        0,
                        template.TemplateType == BiometricTemplateType.Face ? 9 : 1,
                        null,
                        null,
                        null);
                }

                var line = ZkTecoAccUserCommand.BiodataUpdateLine(
                    pin,
                    marker.Fid,
                    marker.BioType,
                    Convert.ToBase64String(template.TemplateData),
                    marker.Major,
                    marker.Minor,
                    marker.Format);
                await _commands.EnqueueAsync(readerId, DeviceCommandType.AddTemplate, line);
                queued++;
            }

            return queued;
        }

        private readonly record struct TemplateMarker(int Fid, int BioType, int? Major, int? Minor, int? Format);

        private static bool TryParseTemplateMarker(string? remarks, out TemplateMarker marker)
        {
            marker = default;
            if (string.IsNullOrWhiteSpace(remarks)) return false;
            var typeMatch = Regex.Match(remarks, @"BioType=(\d+)", RegexOptions.IgnoreCase);
            if (!typeMatch.Success || !int.TryParse(typeMatch.Groups[1].Value, out var bioType))
                return false;
            var fid = 0;
            var fidMatch = Regex.Match(remarks, @"FID=(\d+)", RegexOptions.IgnoreCase);
            if (fidMatch.Success)
                int.TryParse(fidMatch.Groups[1].Value, out fid);
            marker = new TemplateMarker(fid, bioType, ReadInt(remarks, "MajorVer"), ReadInt(remarks, "MinorVer"), ReadInt(remarks, "Format"));
            return true;
        }

        private static int? ReadInt(string remarks, string key)
        {
            var match = Regex.Match(remarks, key + @"=(\d+)", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : null;
        }

        public async Task PushTemplatesExceptSourceAsync(Guid employeeId, string sourceSerial, CancellationToken ct = default)
        {
            var employee = await _unitOfWork.Employees.GetQueryable()
                .FirstOrDefaultAsync(e => e.Id == employeeId && !e.isDeleted, ct);
            var pin = employee?.FileNumber?.Trim();
            if (employee == null || string.IsNullOrWhiteSpace(pin))
                return;

            var readers = await ResolveReadersAsync(employee, pin, ct);
            var source = sourceSerial.Trim();
            foreach (var reader in readers)
            {
                if (string.Equals(reader.DeviceSerial, source, StringComparison.OrdinalIgnoreCase))
                    continue;
                ct.ThrowIfCancellationRequested();
                var queued = await QueueTemplatesAsync(reader.Id, employee, pin, ct);
                if (queued > 0)
                {
                    _logger.LogWarning(
                        "Pushed {Count} templates PIN={Pin} from SN={Source} to SN={Target}",
                        queued, pin, source, reader.DeviceSerial);
                }
            }
        }

        public async Task BeginCreateAfterMissingUserAsync(
            Guid readerId, string? failedUpdatePayload, CancellationToken ct = default)
        {
            if (!TryParsePlainUserUpdate(failedUpdatePayload, out var pin, out var disable))
            {
                _logger.LogWarning(
                    "Skip create-after-missing reader={ReaderId} — payload is not a Pin update",
                    readerId);
                return;
            }

            var pending = await _unitOfWork.DeviceCommands.GetQueryable()
                .Where(c =>
                    c.ReaderId == readerId
                    && !c.isDeleted
                    && c.CommandType == nameof(DeviceCommandType.UpdateUserInfo)
                    && c.Payload != null
                    && (c.Status == DeviceCommandStatus.Pending || c.Status == DeviceCommandStatus.Sent))
                .ToListAsync(ct);

            if (pending.Any(c =>
                    PayloadTargetsPin(c.Payload, pin)
                    && ZkTecoAccUserCommand.IsCreateUserLine(c.Payload)))
            {
                _logger.LogInformation(
                    "Create already queued for PIN={Pin} reader={ReaderId}", pin, readerId);
                return;
            }

            foreach (var wait in pending.Where(c => ZkTecoAccUserCommand.IsWaitForUid(c.Payload) && PayloadTargetsPin(c.Payload, pin)))
            {
                wait.Status = DeviceCommandStatus.Failed;
                wait.LastError = "Replaced by direct create";
                wait.CompletedOn = DateTime.Now;
                wait.updatedOn = DateTime.Now;
                _unitOfWork.DeviceCommands.Update(wait);
            }

            var reader = await _unitOfWork.Readers.GetQueryable()
                .FirstOrDefaultAsync(r => r.Id == readerId && !r.isDeleted, ct);
            if (reader == null) return;

            var employee = await _unitOfWork.Employees.GetQueryable()
                .FirstOrDefaultAsync(e => e.FileNumber == pin && !e.isDeleted, ct);

            var card = await ResolveCardNumberAsync(employee, ct);
            var line = ZkTecoAccUserCommand.UserInfoUpsertLine(pin, employee?.FullName, card);
            await _commands.EnqueueAsync(readerId, DeviceCommandType.UpdateUserInfo, line);
            var templates = await QueueTemplatesAsync(readerId, employee, pin, ct);
            await _commands.EnqueueAsync(readerId, DeviceCommandType.QueryBioData, pin);
            _logger.LogWarning(
                "Queued USERINFO upsert after -1004 PIN={Pin} templates={Templates} SN={Serial}",
                pin, templates, reader.DeviceSerial);
        }

        private async Task<int> AllocateReaderSlotAsync(Reader reader, Employee? employee, CancellationToken ct)
        {
            const int firstServerUid = 1000;
            var serial = reader.DeviceSerial.Trim();
            var existing = ZkTecoAccUserCommand.TryGetReaderSlot(employee?.remarks, serial);
            if (existing is > 0)
                return existing.Value;

            var next = ZkTecoAccUserCommand.TryGetNextUid(reader.remarks) ?? firstServerUid;
            if (next < firstServerUid)
                next = firstServerUid;

            reader.remarks = ZkTecoAccUserCommand.UpsertNextUid(reader.remarks, next + 1);
            reader.updatedOn = DateTime.Now;
            _unitOfWork.Readers.Update(reader);

            if (employee != null)
            {
                employee.remarks = ZkTecoAccUserCommand.UpsertReaderSlot(employee.remarks, serial, next);
                employee.updatedOn = DateTime.Now;
                _unitOfWork.Employees.Update(employee);
            }

            await _unitOfWork.SaveAsync();
            return next;
        }

        public async Task<int> CompletePendingEnrollsFromDeviceUsersAsync(
            string deviceSerial, string body, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(deviceSerial)) return 0;

            var reader = await _unitOfWork.Readers.GetQueryable()
                .FirstOrDefaultAsync(r =>
                    r.DeviceSerial == deviceSerial.Trim() && !r.isDeleted, ct);
            if (reader == null) return 0;

            var waits = await _unitOfWork.DeviceCommands.GetQueryable()
                .Where(c =>
                    c.ReaderId == reader.Id
                    && !c.isDeleted
                    && c.CommandType == nameof(DeviceCommandType.UpdateUserInfo)
                    && c.Status == DeviceCommandStatus.Pending
                    && c.Payload != null
                    && c.Payload.StartsWith(ZkTecoAccUserCommand.WaitUidPrefix))
                .OrderBy(c => c.createdOn)
                .ToListAsync(ct);

            if (waits.Count == 0) return 0;

            var slots = ZkTecoUserInfoParser.ParseAccSlots(body);
            if (slots.Count == 0)
            {
                _logger.LogWarning(
                    "Pending Acc enroll not completed SN={Serial} — no uid slots to allocate a new user",
                    deviceSerial);
                return 0;
            }
            var pinToUid = slots
                .Where(s => s.Pin.Length > 0)
                .GroupBy(s => s.Pin, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Uid.ToString(), StringComparer.OrdinalIgnoreCase);

            var promoted = 0;

            foreach (var wait in waits)
            {
                ct.ThrowIfCancellationRequested();
                if (!TryParseWait(wait.Payload, out var pin, out var disable, out var employeeId))
                    continue;

                var employee = await _unitOfWork.Employees.GetByIdAsync(employeeId)
                    ?? await _unitOfWork.Employees.GetQueryable()
                        .FirstOrDefaultAsync(e => e.FileNumber == pin, ct);

                var capturedUid = pinToUid.TryGetValue(pin, out var existingUid) ? existingUid : null;
                if (capturedUid != null)
                {
                    // PIN is already on this reader: send the same update the device accepts.
                    wait.Payload = ZkTecoAccUserCommand.AccUserLine(pin, employee?.FullName, disable);
                    wait.LastError = null;
                    wait.ScheduledOn = null;
                    wait.updatedOn = DateTime.Now;
                    _unitOfWork.DeviceCommands.Update(wait);
                    promoted++;
                    if (employee != null && string.IsNullOrWhiteSpace(ZkTecoAccUserCommand.TryGetUid(employee.remarks)))
                    {
                        employee.remarks = ZkTecoAccUserCommand.UpsertUid(employee.remarks, capturedUid);
                        employee.updatedOn = DateTime.Now;
                        _unitOfWork.Employees.Update(employee);
                    }
                }
                else
                {
                    var createUid = await AllocateReaderSlotAsync(reader, employee, ct);
                    wait.Payload = ZkTecoAccUserCommand.AccUserLine(pin, employee?.FullName, disable, createUid);
                    wait.LastError = null;
                    wait.ScheduledOn = null;
                    wait.updatedOn = DateTime.Now;
                    _unitOfWork.DeviceCommands.Update(wait);
                    promoted++;
                    _logger.LogWarning(
                        "Queued Acc create PIN={Pin} uid={Uid} SN={Serial}",
                        pin, createUid, deviceSerial);
                }
            }

            await _unitOfWork.SaveAsync();
            return promoted;
        }

        private static bool TryParseWait(string? payload, out string pin, out string disable, out Guid employeeId)
        {
            pin = "";
            disable = "0";
            employeeId = Guid.Empty;
            if (string.IsNullOrWhiteSpace(payload)) return false;

            var pinMatch = Regex.Match(payload, @"pin=([^\t]+)", RegexOptions.IgnoreCase);
            if (!pinMatch.Success) return false;
            pin = pinMatch.Groups[1].Value.Trim();

            var disMatch = Regex.Match(payload, @"disable=([01])", RegexOptions.IgnoreCase);
            if (disMatch.Success) disable = disMatch.Groups[1].Value;

            var idMatch = Regex.Match(payload, @"employeeId=([0-9a-fA-F-]{36})", RegexOptions.IgnoreCase);
            if (idMatch.Success)
                Guid.TryParse(idMatch.Groups[1].Value, out employeeId);

            return pin.Length > 0;
        }

        private static bool TryParsePlainUserUpdate(string? payload, out string pin, out string disable)
        {
            pin = "";
            disable = "0";
            if (string.IsNullOrWhiteSpace(payload) || ZkTecoAccUserCommand.IsCreateUserLine(payload))
                return false;
            if (!payload.Contains("UPDATE user", StringComparison.OrdinalIgnoreCase))
                return false;

            var pinMatch = Regex.Match(payload, @"\bPin=(\S+)", RegexOptions.IgnoreCase);
            if (!pinMatch.Success) return false;
            pin = pinMatch.Groups[1].Value.Trim();

            var groupMatch = Regex.Match(payload, @"\bGroup=(\d+)", RegexOptions.IgnoreCase);
            if (groupMatch.Success && groupMatch.Groups[1].Value == "99")
                disable = "1";

            return pin.Length > 0;
        }

        private async Task<List<Reader>> ResolveReadersAsync(Employee employee, string pin, CancellationToken ct)
        {
            var ids = new HashSet<Guid>();

            var movementIds = await _unitOfWork.MovementLogs.GetQueryable()
                .Where(m => !m.isDeleted
                    && m.ReaderId != null
                    && (m.EmployeeId == employee.Id || m.DevicePin == pin))
                .Select(m => m.ReaderId!.Value)
                .Distinct()
                .ToListAsync(ct);
            foreach (var id in movementIds)
                ids.Add(id);

            var gateIds = await _unitOfWork.EmployeePermissions.GetQueryable()
                .Where(p => p.EmployeeId == employee.Id && p.IsActive && !p.isDeleted)
                .Select(p => p.GateId)
                .Distinct()
                .ToListAsync(ct);
            if (gateIds.Count > 0)
            {
                var gated = await _unitOfWork.Readers.GetQueryable()
                    .Where(r => gateIds.Contains(r.GateId) && r.IsActive && !r.isDeleted)
                    .Select(r => r.Id)
                    .ToListAsync(ct);
                foreach (var id in gated)
                    ids.Add(id);
            }

            var remarks = await _unitOfWork.BiometricTemplates.GetQueryable()
                .Where(t => t.EmployeeId == employee.Id && !t.isDeleted && t.remarks != null)
                .Select(t => t.remarks!)
                .ToListAsync(ct);
            var serials = remarks
                .Select(r => FromSerial.Match(r))
                .Where(m => m.Success)
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var sn in ZkTecoAccUserCommand.TryGetEnrolledSerials(employee.remarks))
                serials.Add(sn);

            serials = serials.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (serials.Count > 0)
            {
                var fromSerials = await _unitOfWork.Readers.GetQueryable()
                    .Where(r => serials.Contains(r.DeviceSerial) && !r.isDeleted)
                    .Select(r => r.Id)
                    .ToListAsync(ct);
                foreach (var id in fromSerials)
                    ids.Add(id);
            }

            var query = _unitOfWork.Readers.GetQueryable().Where(r => r.IsActive && !r.isDeleted);
            if (ids.Count > 0)
                query = query.Where(r => ids.Contains(r.Id));
            else if (string.IsNullOrWhiteSpace(ZkTecoAccUserCommand.TryGetUid(employee.remarks)))
                return new List<Reader>(); // no targets and never enrolled — do not broadcast

            return await query.ToListAsync();
        }

        private async Task SupersedePendingUpdatesAsync(Guid readerId, string pin, CancellationToken ct)
        {
            var pending = await LoadMatchingUpdatesAsync(readerId, pin, ct);
            foreach (var cmd in pending.Where(c =>
                         c.Status == DeviceCommandStatus.Pending
                         || c.Status == DeviceCommandStatus.Sent))
            {
                cmd.Status = DeviceCommandStatus.Failed;
                cmd.LastError = "Superseded by a new enroll/update";
                cmd.CompletedOn = DateTime.Now;
                cmd.updatedOn = DateTime.Now;
                _unitOfWork.DeviceCommands.Update(cmd);
            }
            if (pending.Count > 0)
                await _unitOfWork.SaveAsync();
        }

        private async Task<bool> HasPendingOrRecentUpdateAsync(Guid readerId, string pin, CancellationToken ct)
        {
            var sentCutoff = DateTime.Now.AddMinutes(-3);
            var matching = await LoadMatchingUpdatesAsync(readerId, pin, ct);
            return matching.Any(c =>
                c.Status == DeviceCommandStatus.Pending
                || (c.Status == DeviceCommandStatus.Sent && c.SentOn != null && c.SentOn > sentCutoff));
        }

        /// <summary>
        /// Load in memory so PIN matching is not translated by EF
        /// (<c>Contains(..., StringComparison)</c> throws and was swallowed as queued=0).
        /// </summary>
        private async Task<List<DeviceCommand>> LoadMatchingUpdatesAsync(
            Guid readerId, string pin, CancellationToken ct)
        {
            var rows = await _unitOfWork.DeviceCommands.GetQueryable()
                .Where(c =>
                    c.ReaderId == readerId
                    && !c.isDeleted
                    && c.CommandType == nameof(DeviceCommandType.UpdateUserInfo)
                    && c.Payload != null
                    && (c.Status == DeviceCommandStatus.Pending || c.Status == DeviceCommandStatus.Sent))
                .ToListAsync(ct);

            return rows.Where(c => PayloadTargetsPin(c.Payload, pin)).ToList();
        }

        private static bool PayloadTargetsPin(string? payload, string pin)
        {
            if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(pin))
                return false;
            // Avoid pin=1 matching pin=12.
            return Regex.IsMatch(
                payload,
                $@"(?:^|[\s;,])(?:pin|PIN)={Regex.Escape(pin)}(?:$|[\s;,])",
                RegexOptions.IgnoreCase);
        }
    }
}
