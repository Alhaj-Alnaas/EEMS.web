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

            var name = SanitizeName(employee.FullName, pin);
            var disable = (!employee.IsActive || employee.isDeleted) ? "1" : "0";
            var uid = ZkTecoAccUserCommand.TryGetUid(employee.remarks);

            var queued = 0;
            foreach (var reader in readers)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(uid))
                {
                    if (await HasPendingQueryUsersAsync(reader.Id, ct))
                        continue;

                    await _commands.EnqueueAsync(
                        reader.Id,
                        DeviceCommandType.QueryUserInfo,
                        ZkTecoAccUserCommand.QueryAllUsers());
                    queued++;
                    _logger.LogInformation(
                        "Queued user-table query for PIN={Pin} reader={Serial} (device uid unknown)",
                        pin, reader.DeviceSerial);
                    continue;
                }

                if (await HasPendingOrRecentUpdateAsync(reader.Id, pin, ct))
                    continue;

                var accUser = ZkTecoAccUserCommand.Update(pin, name, disable, uid);
                await _commands.EnqueueAsync(reader.Id, DeviceCommandType.UpdateUserInfo, accUser);
                queued++;
                _logger.LogInformation(
                    "Queued employee sync PIN={Pin} uid={Uid} name={Name} reader={Serial}",
                    pin, uid, name, reader.DeviceSerial);
            }

            return queued;
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
            if (serials.Count > 0)
            {
                var fromTemplates = await _unitOfWork.Readers.GetQueryable()
                    .Where(r => serials.Contains(r.DeviceSerial) && !r.isDeleted)
                    .Select(r => r.Id)
                    .ToListAsync(ct);
                foreach (var id in fromTemplates)
                    ids.Add(id);
            }

            var query = _unitOfWork.Readers.GetQueryable().Where(r => r.IsActive && !r.isDeleted);
            if (ids.Count > 0)
                query = query.Where(r => ids.Contains(r.Id));

            return await query.ToListAsync();
        }

        private async Task<bool> HasPendingQueryUsersAsync(Guid readerId, CancellationToken ct)
        {
            var cutoff = DateTime.Now.AddMinutes(-3);
            return await _unitOfWork.DeviceCommands.GetQueryable().AnyAsync(c =>
                c.ReaderId == readerId
                && !c.isDeleted
                && c.CommandType == nameof(DeviceCommandType.QueryUserInfo)
                && (c.Status == DeviceCommandStatus.Pending
                    || (c.Status == DeviceCommandStatus.Sent && c.SentOn != null && c.SentOn > cutoff)), ct);
        }

        private async Task<bool> HasPendingOrRecentUpdateAsync(Guid readerId, string pin, CancellationToken ct)
        {
            var pinToken = $"pin={pin}";
            var sentCutoff = DateTime.Now.AddMinutes(-3);
            return await _unitOfWork.DeviceCommands.GetQueryable().AnyAsync(c =>
                c.ReaderId == readerId
                && !c.isDeleted
                && c.CommandType == nameof(DeviceCommandType.UpdateUserInfo)
                && c.Payload != null
                && c.Payload.ToLower().Contains(pinToken.ToLower())
                && (c.Status == DeviceCommandStatus.Pending
                    || (c.Status == DeviceCommandStatus.Sent && c.SentOn != null && c.SentOn > sentCutoff)), ct);
        }

        private static string SanitizeName(string? fullName, string pin)
        {
            var name = (fullName ?? pin).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (name.Length == 0) name = pin;
            return TruncateUtf8(name, 36);
        }

        private static string TruncateUtf8(string value, int maxBytes)
        {
            var enc = System.Text.Encoding.UTF8;
            if (enc.GetByteCount(value) <= maxBytes) return value;
            var chars = value.ToCharArray();
            for (var i = chars.Length; i > 0; i--)
            {
                var slice = new string(chars, 0, i);
                if (enc.GetByteCount(slice) <= maxBytes)
                    return slice;
            }
            return value;
        }
    }
}
