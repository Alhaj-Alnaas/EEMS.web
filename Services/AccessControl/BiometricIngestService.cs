using Core.Entities;
using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static Core.Enums.BaseEnums;

namespace Services.AccessControl
{
    public class BiometricIngestService : IBiometricIngestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmployeeDeviceSyncService _deviceSync;
        private readonly ILogger<BiometricIngestService> _logger;

        public BiometricIngestService(
            IUnitOfWork unitOfWork,
            IEmployeeDeviceSyncService deviceSync,
            ILogger<BiometricIngestService> logger)
        {
            _unitOfWork = unitOfWork;
            _deviceSync = deviceSync;
            _logger = logger;
        }

        public async Task<BiometricIngestResult> ImportCombinedAsync(
            string deviceSerial, string? table, string body, CancellationToken ct = default)
        {
            // Firmware differs: users/templates may arrive under USERINFO, FINGERTMP,
            // BIODATA, or mixed inside an OPERLOG push. Run both parsers on every payload
            // and let them pick the lines they recognize.
            var users = await ImportUserInfoAsync(deviceSerial, table, body, ct);
            var templates = await ImportFingerTmpAsync(deviceSerial, table, body, ct);
            return new BiometricIngestResult(users, templates, 0);
        }

        public async Task<int> ImportUserInfoAsync(
            string deviceSerial, string? table, string body, CancellationToken ct = default)
        {
            var reader = await FindReaderAsync(deviceSerial, ct);
            if (reader == null) return 0;

            await RemoveBlankEmployeesAsync(ct);

            var entries = ZkTecoUserInfoParser.Parse(body, table);
            if (entries.Count == 0) return 0;

            var changes = 0;
            var pendingNamePush = new List<Employee>();
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(entry.Pin))
                    continue;

                var employee = await _unitOfWork.Employees.GetQueryable()
                    .FirstOrDefaultAsync(e => e.FileNumber == entry.Pin && !e.isDeleted, ct);

                if (employee == null)
                {
                    // Incomplete SpeedFace users (empty name) must not become PMS rows.
                    // Those appear after a DATA UPDATE without uid — they are inserts, not edits.
                    if (string.IsNullOrWhiteSpace(entry.DeviceName))
                    {
                        _logger.LogInformation(
                            "Skip stub device user SN={Serial} PIN={Pin} uid={Uid}",
                            deviceSerial, entry.Pin, entry.Uid ?? "-");
                        continue;
                    }

                    employee = new Employee
                    {
                        Id = Guid.NewGuid(),
                        FileNumber = entry.Pin,
                        FullName = entry.Name,
                        IsActive = true,
                        remarks = ZkTecoAccUserCommand.UpsertUid(
                            $"Imported from reader {reader.DeviceSerial}", entry.Uid ?? ""),
                        createdOn = DateTime.Now
                    };
                    if (string.IsNullOrWhiteSpace(entry.Uid))
                        employee.remarks = $"Imported from reader {reader.DeviceSerial}";
                    _unitOfWork.Employees.Insert(employee);
                    changes++;
                    continue;
                }

                var remarksChanged = false;
                if (!string.IsNullOrWhiteSpace(entry.Uid)
                    && ZkTecoAccUserCommand.TryGetUid(employee.remarks) != entry.Uid)
                {
                    employee.remarks = ZkTecoAccUserCommand.UpsertUid(employee.remarks, entry.Uid);
                    remarksChanged = true;
                }

                var deviceName = entry.DeviceName ?? "";
                var hasRealPmsName = !string.IsNullOrWhiteSpace(employee.FullName)
                    && !string.Equals(employee.FullName.Trim(), employee.FileNumber.Trim(), StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(entry.DeviceName)
                    && !string.Equals(employee.FullName, entry.DeviceName, StringComparison.Ordinal)
                    && !hasRealPmsName)
                {
                    employee.FullName = entry.DeviceName;
                    remarksChanged = true;
                }

                if (remarksChanged)
                {
                    employee.updatedOn = DateTime.Now;
                    _unitOfWork.Employees.Update(employee);
                    changes++;
                }

                if (hasRealPmsName
                    && !string.Equals(employee.FullName.Trim(), deviceName, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(ZkTecoAccUserCommand.TryGetUid(employee.remarks)))
                {
                    pendingNamePush.Add(employee);
                }
            }

            if (changes > 0)
                await _unitOfWork.SaveAsync();

            foreach (var employee in pendingNamePush)
            {
                try
                {
                    await _deviceSync.SyncEmployeeToReadersAsync(employee, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not queue name push after user import PIN={Pin}", employee.FileNumber);
                }
            }

            _logger.LogInformation(
                "USERINFO import SN={Serial} parsed={Parsed} changed={Changed} namePush={Push}",
                deviceSerial, entries.Count, changes, pendingNamePush.Count);

            return entries.Count;
        }

        public async Task<int> ImportFingerTmpAsync(
            string deviceSerial, string? table, string body, CancellationToken ct = default)
        {
            var reader = await FindReaderAsync(deviceSerial, ct);
            if (reader == null) return 0;

            var entries = ZkTecoFingerTmpParser.Parse(body, table);
            if (entries.Count == 0) return 0;

            var saved = 0;
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                if (!entry.Valid || string.IsNullOrWhiteSpace(entry.TemplateBase64))
                    continue;
                if (string.IsNullOrWhiteSpace(entry.Pin))
                    continue;

                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(entry.TemplateBase64);
                }
                catch (FormatException)
                {
                    // Some firmware sends hex; store UTF8 of raw TMP as fallback.
                    bytes = System.Text.Encoding.UTF8.GetBytes(entry.TemplateBase64);
                }

                var employee = await _unitOfWork.Employees.GetQueryable()
                    .FirstOrDefaultAsync(e => e.FileNumber == entry.Pin && !e.isDeleted, ct);

                if (employee == null)
                    continue;

                // Replace existing template for same finger index when possible (tracked via remarks).
                var marker = $"FID={entry.FingerIndex}";
                var existing = await _unitOfWork.BiometricTemplates.GetQueryable()
                    .FirstOrDefaultAsync(t =>
                        t.EmployeeId == employee.Id
                        && !t.isDeleted
                        && t.TemplateType == BiometricTemplateType.Fingerprint
                        && t.remarks != null
                        && t.remarks.Contains(marker), ct);

                if (existing != null)
                {
                    existing.TemplateData = bytes;
                    existing.updatedOn = DateTime.Now;
                    existing.remarks = $"{marker}; from {reader.DeviceSerial}";
                    _unitOfWork.BiometricTemplates.Update(existing);
                }
                else
                {
                    _unitOfWork.BiometricTemplates.Insert(new BiometricTemplate
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = employee.Id,
                        TemplateType = BiometricTemplateType.Fingerprint,
                        TemplateData = bytes,
                        IsApproved = true,
                        ApprovedBy = $"ZKTeco/{reader.DeviceSerial}",
                        ApprovedOn = DateTime.Now,
                        remarks = $"{marker}; from {reader.DeviceSerial}",
                        createdOn = DateTime.Now
                    });
                }

                saved++;
            }

            if (saved > 0)
            {
                await _unitOfWork.SaveAsync();
                _logger.LogInformation(
                    "FINGERTMP import SN={Serial} templates={Count}", deviceSerial, saved);
            }

            return saved;
        }

        private async Task RemoveBlankEmployeesAsync(CancellationToken ct)
        {
            var stubs = await _unitOfWork.Employees.GetQueryable()
                .Where(e => !e.isDeleted && (e.FileNumber == null || e.FileNumber.Trim() == ""))
                .ToListAsync(ct);
            if (stubs.Count == 0) return;

            foreach (var stub in stubs)
            {
                stub.isDeleted = true;
                stub.deletedOn = DateTime.Now;
                stub.remarks = string.IsNullOrWhiteSpace(stub.remarks)
                    ? "Removed blank FileNumber stub"
                    : stub.remarks + "; removed blank FileNumber stub";
                _unitOfWork.Employees.Update(stub);
            }

            await _unitOfWork.SaveAsync();
            _logger.LogWarning("Removed {Count} employee rows with empty file number", stubs.Count);
        }

        private async Task<Reader?> FindReaderAsync(string deviceSerial, CancellationToken ct)
        {
            var reader = await _unitOfWork.Readers.GetQueryable()
                .FirstOrDefaultAsync(r => r.DeviceSerial == deviceSerial.Trim() && !r.isDeleted, ct);
            if (reader == null)
                _logger.LogWarning("Biometric import ignored — unknown reader SN={Serial}", deviceSerial);
            return reader;
        }
    }
}
