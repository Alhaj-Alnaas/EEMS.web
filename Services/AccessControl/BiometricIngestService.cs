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
            var parsed = ZkTecoUserInfoParser.Parse(body, table).Count;
            return new BiometricIngestResult(users, templates, 0, parsed);
        }

        public async Task<int> ImportUserInfoAsync(
            string deviceSerial, string? table, string body, CancellationToken ct = default)
        {
            var reader = await FindReaderAsync(deviceSerial, ct);
            if (reader == null) return 0;

            await RemoveBlankEmployeesAsync(ct);

            IReadOnlyList<ZkTecoUserInfoEntry> entries = ZkTecoUserInfoParser.Parse(body, table);
            // SpeedFace sometimes posts user rows under type=tabledata / empty table hint.
            if (entries.Count == 0 && body.Contains("pin=", StringComparison.OrdinalIgnoreCase))
                entries = ZkTecoUserInfoParser.Parse(body, "user");

            if (entries.Count == 0)
            {
                _logger.LogWarning(
                    "USERINFO parse=0 SN={Serial} table={Table} bodyLen={Len} preview={Preview}",
                    deviceSerial, table ?? "-", body.Length, PreviewBody(body, 200));
                await TryCompletePendingEnrollsAsync(deviceSerial, body, ct);
                return 0;
            }

            _logger.LogWarning(
                "USERINFO parse={Parsed} SN={Serial} table={Table} pins=[{Pins}]",
                entries.Count, deviceSerial, table ?? "-",
                string.Join(",", entries.Select(e => e.Pin)));

            var changes = 0;
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(entry.Pin))
                    continue;

                // Include soft-deleted — unique FileNumber index is not filtered.
                var employee = await _unitOfWork.Employees.GetQueryable()
                    .FirstOrDefaultAsync(e => e.FileNumber == entry.Pin, ct);

                if (employee == null)
                {
                    // The name is entered once at registration. Never copy it from the reader.
                    employee = new Employee
                    {
                        Id = Guid.NewGuid(),
                        FileNumber = entry.Pin,
                        FullName = entry.Pin,
                        CardNumber = string.IsNullOrWhiteSpace(entry.Card) ? null : entry.Card.Trim(),
                        IsActive = true,
                        remarks = ZkTecoAccUserCommand.UpsertEnrolledSerials(
                            !string.IsNullOrWhiteSpace(entry.Uid)
                                ? ZkTecoAccUserCommand.UpsertUid(
                                    $"Imported from reader {reader.DeviceSerial}", entry.Uid)
                                : $"Imported from reader {reader.DeviceSerial}",
                            new[] { reader.DeviceSerial }),
                        createdOn = DateTime.Now
                    };
                    _unitOfWork.Employees.Insert(employee);
                    changes++;
                    _logger.LogWarning(
                        "Created employee from device SN={Serial} PIN={Pin} uid={Uid} name={Name} card={Card}",
                        deviceSerial, entry.Pin, entry.Uid ?? "-", entry.Pin, entry.Card ?? "-");
                    continue;
                }

                if (employee.isDeleted)
                {
                    employee.isDeleted = false;
                    employee.deletedOn = null;
                    employee.deletedBy = null;
                    employee.IsActive = true;
                    employee.updatedOn = DateTime.Now;
                    if (string.IsNullOrWhiteSpace(employee.FullName))
                        employee.FullName = entry.Pin;
                    if (!string.IsNullOrWhiteSpace(entry.Uid))
                        employee.remarks = ZkTecoAccUserCommand.UpsertUid(employee.remarks, entry.Uid);
                    if (!string.IsNullOrWhiteSpace(entry.Card) && string.IsNullOrWhiteSpace(employee.CardNumber))
                        employee.CardNumber = entry.Card.Trim();
                    employee.remarks = ZkTecoAccUserCommand.UpsertEnrolledSerials(
                        employee.remarks, new[] { reader.DeviceSerial });
                    _unitOfWork.Employees.Update(employee);
                    changes++;
                    _logger.LogWarning(
                        "Reactivated soft-deleted employee PIN={Pin} from SN={Serial}",
                        entry.Pin, deviceSerial);
                    continue;
                }

                var remarksChanged = false;
                if (!string.IsNullOrWhiteSpace(entry.Uid)
                    && ZkTecoAccUserCommand.TryGetUid(employee.remarks) != entry.Uid)
                {
                    employee.remarks = ZkTecoAccUserCommand.UpsertUid(employee.remarks, entry.Uid);
                    remarksChanged = true;
                }

                var remarksBeforeSerials = employee.remarks;
                employee.remarks = ZkTecoAccUserCommand.UpsertEnrolledSerials(
                    employee.remarks, new[] { reader.DeviceSerial });
                if (!string.Equals(remarksBeforeSerials ?? "", employee.remarks ?? "", StringComparison.Ordinal))
                    remarksChanged = true;

                if (!string.IsNullOrWhiteSpace(entry.Card)
                    && !string.Equals(employee.CardNumber?.Trim(), entry.Card.Trim(), StringComparison.Ordinal))
                {
                    // لا نكتب فوق كرت PMS إن وُجد واختلف — نملأ فقط إن كان فارغاً
                    if (string.IsNullOrWhiteSpace(employee.CardNumber))
                    {
                        employee.CardNumber = entry.Card.Trim();
                        remarksChanged = true;
                    }
                }

                if (remarksChanged)
                {
                    employee.updatedOn = DateTime.Now;
                    _unitOfWork.Employees.Update(employee);
                    changes++;
                }
            }

            if (changes > 0)
            {
                try
                {
                    await _unitOfWork.SaveAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "USERINFO SaveAsync failed SN={Serial} parsed={Parsed} changes={Changes}",
                        deviceSerial, entries.Count, changes);
                    throw;
                }
            }

            _logger.LogInformation(
                "USERINFO import SN={Serial} parsed={Parsed} changed={Changed}",
                deviceSerial, entries.Count, changes);

            try
            {
                await TryCompletePendingEnrollsAsync(deviceSerial, body, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Complete pending Acc enroll failed SN={Serial}", deviceSerial);
            }

            return changes;
        }

        public async Task<int> ImportFingerTmpAsync(
            string deviceSerial, string? table, string body, CancellationToken ct = default)
        {
            var reader = await FindReaderAsync(deviceSerial, ct);
            if (reader == null) return 0;

            var entries = ZkTecoFingerTmpParser.Parse(body, table);
            if (entries.Count == 0) return 0;

            var saved = 0;
            var handCount = 0;
            var faceCount = 0;
            var changedEmployees = new HashSet<Guid>();
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                if (!entry.Valid || string.IsNullOrWhiteSpace(entry.TemplateBase64))
                    continue;
                if (string.IsNullOrWhiteSpace(entry.Pin))
                    continue;

                var templateType = ZkTecoFingerTmpParser.IsFaceBioType(entry.BioType)
                    ? BiometricTemplateType.Face
                    : BiometricTemplateType.Fingerprint;

                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(entry.TemplateBase64);
                }
                catch (FormatException)
                {
                    _logger.LogWarning(
                        "Skip bio template SN={Serial} PIN={Pin} type={BioType} — TMP is not base64",
                        deviceSerial, entry.Pin, entry.BioType);
                    continue;
                }

                var employee = await _unitOfWork.Employees.GetQueryable()
                    .FirstOrDefaultAsync(e => e.FileNumber == entry.Pin && !e.isDeleted, ct);

                if (employee == null)
                {
                    _logger.LogInformation(
                        "Skip bio template SN={Serial} PIN={Pin} type={BioType} — employee not found",
                        deviceSerial, entry.Pin, entry.BioType);
                    continue;
                }

                var marker = $"FID={entry.FingerIndex};BioType={entry.BioType}";
                if (entry.MajorVer is int majorVer) marker += $";MajorVer={majorVer}";
                if (entry.MinorVer is int minorVer) marker += $";MinorVer={minorVer}";
                if (entry.Format is int format) marker += $";Format={format}";
                var remarks = $"{marker}; from {reader.DeviceSerial}";
                if (templateType == BiometricTemplateType.Face
                    && !FaceTemplatePolicy.IsDoorFace(bytes, remarks))
                {
                    _logger.LogWarning(
                        "Ignored face that cannot open a door SN={Serial} PIN={Pin} bytes={Bytes} marker={Marker}",
                        deviceSerial, entry.Pin, bytes.Length, marker);
                    continue;
                }

                var existing = await _unitOfWork.BiometricTemplates.GetQueryable()
                    .FirstOrDefaultAsync(t =>
                        t.EmployeeId == employee.Id
                        && !t.isDeleted
                        && t.TemplateType == templateType
                        && t.remarks != null
                        && t.remarks.Contains($"FID={entry.FingerIndex}")
                        && t.remarks.Contains($"BioType={entry.BioType}"), ct);

                var changed = true;
                if (existing != null)
                {
                    var keepSource = templateType == BiometricTemplateType.Face
                        && FaceTemplatePolicy.IsDoorFace(existing.TemplateData, existing.remarks)
                        && IsSameDoorCapture(existing.TemplateData, bytes);
                    if (keepSource)
                    {
                        changed = false;
                        existing.remarks = FaceTemplatePolicy.WithConfirmation(
                            existing.remarks ?? remarks, reader.DeviceSerial);
                        existing.updatedOn = DateTime.Now;
                        _unitOfWork.BiometricTemplates.Update(existing);
                        _logger.LogWarning(
                            "Face confirmed SN={Serial} PIN={Pin} bytes={Bytes}",
                            deviceSerial, entry.Pin, bytes.Length);
                    }
                    else
                    {
                        changed = existing.TemplateData == null
                            || !existing.TemplateData.AsSpan().SequenceEqual(bytes);
                        existing.TemplateData = bytes;
                        existing.updatedOn = DateTime.Now;
                        existing.remarks = templateType == BiometricTemplateType.Face
                            ? FaceTemplatePolicy.WithConfirmation(remarks, reader.DeviceSerial)
                            : remarks;
                        _unitOfWork.BiometricTemplates.Update(existing);
                    }
                }
                else
                {
                    _unitOfWork.BiometricTemplates.Insert(new BiometricTemplate
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = employee.Id,
                        TemplateType = templateType,
                        TemplateData = bytes,
                        IsApproved = true,
                        ApprovedBy = $"ZKTeco/{reader.DeviceSerial}",
                        ApprovedOn = DateTime.Now,
                        remarks = templateType == BiometricTemplateType.Face
                            ? FaceTemplatePolicy.WithConfirmation(remarks, reader.DeviceSerial)
                            : remarks,
                        createdOn = DateTime.Now
                    });
                }

                if (changed)
                    changedEmployees.Add(employee.Id);

                saved++;
                if (templateType == BiometricTemplateType.Face) faceCount++;
                else handCount++;
            }

            if (saved > 0)
            {
                await _unitOfWork.SaveAsync();
                _logger.LogInformation(
                    "BIO import SN={Serial} saved={Saved} hands={Hands} faces={Faces}",
                    deviceSerial, saved, handCount, faceCount);
                foreach (var employeeId in changedEmployees)
                {
                    try
                    {
                        await _deviceSync.PushTemplatesExceptSourceAsync(employeeId, deviceSerial, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Push templates after bio import failed PIN employee={EmployeeId}", employeeId);
                    }
                }
            }

            return saved;
        }

        private static bool IsSameDoorCapture(byte[]? stored, byte[] incoming)
        {
            if (stored == null || stored.Length == 0 || incoming.Length == 0)
                return false;
            if (stored.AsSpan().SequenceEqual(incoming))
                return true;
            var delta = Math.Abs(stored.Length - incoming.Length);
            var limit = Math.Max(32, stored.Length / 20);
            return delta <= limit;
        }

        private static string PreviewBody(string body, int max)
        {
            if (string.IsNullOrEmpty(body)) return "(empty)";
            var text = body.Replace('\t', '|').Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= max ? text : text[..max] + "…";
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

        private async Task TryCompletePendingEnrollsAsync(string deviceSerial, string body, CancellationToken ct)
        {
            try
            {
                await _deviceSync.CompletePendingEnrollsFromDeviceUsersAsync(deviceSerial, body, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Complete pending Acc enroll failed SN={Serial}", deviceSerial);
            }
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
