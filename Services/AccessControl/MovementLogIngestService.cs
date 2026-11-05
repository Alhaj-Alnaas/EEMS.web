using System.Globalization;
using Core.Entities;
using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static Core.Enums.BaseEnums;

namespace Services.AccessControl
{
    public class MovementLogIngestService : IMovementLogIngestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MovementLogIngestService> _logger;

        public MovementLogIngestService(IUnitOfWork unitOfWork, ILogger<MovementLogIngestService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<int> ImportAttLogAsync(string deviceSerial, string body, CancellationToken ct = default)
        {
            var reader = await _unitOfWork.Readers.GetQueryable()
                .Include(r => r.Gate)
                .FirstOrDefaultAsync(r => r.DeviceSerial == deviceSerial.Trim() && !r.isDeleted, ct);

            if (reader == null)
            {
                _logger.LogWarning("ATTLOG ignored — unknown reader SN={Serial}", deviceSerial);
                return 0;
            }

            var entries = ZkTecoAttLogParser.Parse(body);
            if (entries.Count == 0) return 0;

            var employeeMap = await BuildEmployeePinMapAsync(ct);
            var existingKeys = await LoadExistingKeysAsync(reader.Id, entries, ct);

            var saved = 0;
            var skipped = 0;
            var skippedInactive = 0;
            var cardBackfilled = false;
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();

                var pin = NormalizePin(entry.Pin);
                if (string.IsNullOrWhiteSpace(pin))
                    continue;

                var eventTime = TruncateToSeconds(entry.EventTime);

                Guid? employeeId = null;
                bool? isActive = null;
                if (employeeMap.TryGetValue(pin, out var mapped))
                {
                    employeeId = mapped.Id;
                    isActive = mapped.IsActive;
                }
                else if (long.TryParse(pin, out var pinNum) && employeeMap.TryGetValue(pinNum.ToString(), out mapped))
                {
                    employeeId = mapped.Id;
                    isActive = mapped.IsActive;
                }

                // Backfill even when the movement row is a duplicate (saved may stay 0).
                if (employeeId.HasValue && isActive != false && !string.IsNullOrWhiteSpace(entry.CardNo)
                    && await TryBackfillCardNumberAsync(employeeId.Value, entry.CardNo, ct))
                    cardBackfilled = true;

                if (IsDuplicate(existingKeys, entry.DeviceIndex, pin, eventTime))
                {
                    skipped++;
                    continue;
                }

                if (employeeId == null)
                {
                    _logger.LogWarning(
                        "ATTLOG PIN={Pin} SN={Serial} — لا يوجد موظف مطابق في Employees.FileNumber",
                        pin, deviceSerial);
                }
                else if (isActive == false)
                {
                    // موظف غير نشط: لا تُسجَّل حركة الدخول (القارئة يفترض أن ترفضه بعد المزامنة)
                    skippedInactive++;
                    RememberKey(existingKeys, entry.DeviceIndex, pin, eventTime);
                    _logger.LogInformation(
                        "ATTLOG skipped inactive employee PIN={Pin} SN={Serial} index={Index}",
                        pin, deviceSerial, entry.DeviceIndex);
                    continue;
                }

                var log = new MovementLog
                {
                    Id = Guid.NewGuid(),
                    ReaderId = reader.Id,
                    GateId = reader.GateId,
                    EmployeeId = employeeId,
                    DevicePin = pin,
                    DeviceTxnIndex = entry.DeviceIndex,
                    EventTime = eventTime,
                    // Direction comes from the reader's configured type (دخول/خروج), not device status codes.
                    Direction = reader.Direction,
                    Source = MovementSource.Biometric,
                    RecordedBy = $"ZKTeco/{reader.DeviceSerial}",
                    remarks = entry.RawLine,
                    createdOn = DateTime.Now
                };

                _unitOfWork.MovementLogs.Insert(log);
                RememberKey(existingKeys, entry.DeviceIndex, pin, eventTime);
                saved++;
            }

            if (saved > 0 || cardBackfilled)
                await _unitOfWork.SaveAsync();

            if (saved > 0 || skipped > 0 || skippedInactive > 0)
            {
                _logger.LogInformation(
                    "ATTLOG SN={Serial}: imported={Saved} skippedDuplicates={Skipped} skippedInactive={Inactive} parsed={Parsed}",
                    deviceSerial, saved, skipped, skippedInactive, entries.Count);
            }

            return saved;
        }

        /// <summary>
        /// يصلح السجلات: PIN الخاطئ، EmployeeId، EventTime من time_second، وDeviceTxnIndex من remarks.
        /// </summary>
        public async Task<int> RepairUnlinkedAsync(CancellationToken ct = default)
        {
            var orphans = await _unitOfWork.MovementLogs.GetQueryable()
                .Where(m => !m.isDeleted
                            && (m.EmployeeId == null
                                || m.DeviceTxnIndex == null
                                || (m.DevicePin != null && m.DevicePin.StartsWith("time="))
                                || (m.remarks != null && m.remarks.Contains("time_second="))))
                .OrderByDescending(m => m.createdOn)
                .Take(5000)
                .ToListAsync(ct);

            if (orphans.Count == 0) return 0;

            var employeeMap = await BuildEmployeePinMapAsync(ct);
            var fixedCount = 0;
            foreach (var log in orphans)
            {
                ct.ThrowIfCancellationRequested();

                var pin = NormalizePin(log.DevicePin);
                if (string.IsNullOrWhiteSpace(pin) || pin.Contains('=') || pin.StartsWith("time", StringComparison.OrdinalIgnoreCase))
                {
                    var fromRemarks = ExtractPinFromRaw(log.remarks);
                    if (!string.IsNullOrWhiteSpace(fromRemarks))
                        pin = fromRemarks;
                }

                if (string.IsNullOrWhiteSpace(pin) || pin is "0" or "00")
                    continue;

                Guid? resolved = null;
                if (employeeMap.TryGetValue(pin, out var mapped))
                    resolved = mapped.Id;
                else if (long.TryParse(pin, out var pinNum) && employeeMap.TryGetValue(pinNum.ToString(), out mapped))
                    resolved = mapped.Id;

                var changed = false;

                if (!string.Equals(log.DevicePin, pin, StringComparison.Ordinal))
                {
                    log.DevicePin = pin;
                    changed = true;
                }

                if (resolved != null && log.EmployeeId != resolved)
                {
                    log.EmployeeId = resolved;
                    changed = true;
                }

                if (TryExtractTxnIndex(log.remarks, out var txnIndex)
                    && log.DeviceTxnIndex != txnIndex)
                {
                    var clashInBatch = orphans.Any(o =>
                        o.Id != log.Id
                        && o.ReaderId == log.ReaderId
                        && o.DeviceTxnIndex == txnIndex);
                    var clashInDb = !clashInBatch && await _unitOfWork.MovementLogs.GetQueryable()
                        .AnyAsync(m => m.Id != log.Id
                                       && m.ReaderId == log.ReaderId
                                       && m.DeviceTxnIndex == txnIndex
                                       && !m.isDeleted, ct);
                    if (!clashInBatch && !clashInDb)
                    {
                        log.DeviceTxnIndex = txnIndex;
                        changed = true;
                    }
                }

                if (TryExtractEventTime(log.remarks, out var eventTime)
                    && TruncateToSeconds(log.EventTime) != eventTime)
                {
                    log.EventTime = eventTime;
                    changed = true;
                }

                if (changed)
                {
                    log.updatedOn = DateTime.Now;
                    fixedCount++;
                }
            }

            if (fixedCount > 0)
            {
                await _unitOfWork.SaveAsync();
                _logger.LogInformation("Repaired {Count} MovementLog rows (PIN/EmployeeId/EventTime/Index)", fixedCount);
            }

            return fixedCount;
        }

        public async Task<int?> GetMaxDeviceTxnIndexAsync(Guid readerId, CancellationToken ct = default)
        {
            return await _unitOfWork.MovementLogs.GetQueryable()
                .Where(m => m.ReaderId == readerId && !m.isDeleted && m.DeviceTxnIndex != null)
                .MaxAsync(m => (int?)m.DeviceTxnIndex, ct);
        }

        private async Task<(HashSet<int> Indexes, HashSet<string> PinTimes)> LoadExistingKeysAsync(
            Guid readerId,
            IReadOnlyList<Core.Models.ZkAttLogEntry> entries,
            CancellationToken ct)
        {
            var indexes = new HashSet<int>();
            var pinTimes = new HashSet<string>(StringComparer.Ordinal);

            var deviceIndexes = entries
                .Where(e => e.DeviceIndex.HasValue)
                .Select(e => e.DeviceIndex!.Value)
                .Distinct()
                .ToList();

            if (deviceIndexes.Count > 0)
            {
                var existingIdx = await _unitOfWork.MovementLogs.GetQueryable()
                    .AsNoTracking()
                    .Where(m => m.ReaderId == readerId
                                && !m.isDeleted
                                && m.DeviceTxnIndex != null
                                && deviceIndexes.Contains(m.DeviceTxnIndex.Value))
                    .Select(m => m.DeviceTxnIndex!.Value)
                    .ToListAsync(ct);
                foreach (var i in existingIdx)
                    indexes.Add(i);
            }

            var minTime = entries.Min(e => e.EventTime).AddMinutes(-1);
            var maxTime = entries.Max(e => e.EventTime).AddMinutes(1);
            var pins = entries.Select(e => NormalizePin(e.Pin)).Where(p => p.Length > 0).Distinct().ToList();

            var existingRows = await _unitOfWork.MovementLogs.GetQueryable()
                .AsNoTracking()
                .Where(m => m.ReaderId == readerId
                            && !m.isDeleted
                            && m.DevicePin != null
                            && pins.Contains(m.DevicePin)
                            && m.EventTime >= minTime
                            && m.EventTime <= maxTime)
                .Select(m => new { m.DevicePin, m.EventTime, m.DeviceTxnIndex })
                .ToListAsync(ct);

            foreach (var row in existingRows)
            {
                if (row.DeviceTxnIndex.HasValue)
                    indexes.Add(row.DeviceTxnIndex.Value);
                pinTimes.Add(PinTimeKey(row.DevicePin!, TruncateToSeconds(row.EventTime)));
            }

            return (indexes, pinTimes);
        }

        private static bool IsDuplicate(
            (HashSet<int> Indexes, HashSet<string> PinTimes) keys,
            int? deviceIndex,
            string pin,
            DateTime eventTime)
        {
            if (deviceIndex.HasValue && keys.Indexes.Contains(deviceIndex.Value))
                return true;
            return keys.PinTimes.Contains(PinTimeKey(pin, eventTime));
        }

        private static void RememberKey(
            (HashSet<int> Indexes, HashSet<string> PinTimes) keys,
            int? deviceIndex,
            string pin,
            DateTime eventTime)
        {
            if (deviceIndex.HasValue)
                keys.Indexes.Add(deviceIndex.Value);
            keys.PinTimes.Add(PinTimeKey(pin, eventTime));
        }

        private static string PinTimeKey(string pin, DateTime eventTime)
            => $"{pin}|{eventTime:yyyy-MM-dd HH:mm:ss}";

        private async Task<Dictionary<string, (Guid Id, bool IsActive)>> BuildEmployeePinMapAsync(CancellationToken ct)
        {
            var employees = await _unitOfWork.Employees.GetQueryable()
                .AsNoTracking()
                .Where(e => !e.isDeleted && e.FileNumber != null && e.FileNumber != "")
                .Select(e => new { e.Id, e.FileNumber, e.IsActive })
                .ToListAsync(ct);

            var map = new Dictionary<string, (Guid Id, bool IsActive)>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in employees)
            {
                var key = e.FileNumber.Trim();
                if (key.Length == 0) continue;
                map[key] = (e.Id, e.IsActive);

                if (long.TryParse(key, out var num))
                {
                    var normalized = num.ToString();
                    if (!map.ContainsKey(normalized))
                        map[normalized] = (e.Id, e.IsActive);
                }
            }

            return map;
        }

        private static string? ExtractPinFromRaw(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var fields = ZkTecoRecordLine.ParseFields(raw, out _);
            if (fields.TryGetValue("Pin", out var pin) || fields.TryGetValue("PIN", out pin))
            {
                pin = NormalizePin(pin);
                if (!string.IsNullOrWhiteSpace(pin) && pin is not ("0" or "00"))
                    return pin;
            }
            return null;
        }

        private static bool TryExtractTxnIndex(string? raw, out int index)
        {
            index = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var fields = ZkTecoRecordLine.ParseFields(raw, out _);
            return (fields.TryGetValue("Index", out var v) || fields.TryGetValue("index", out v))
                   && int.TryParse(v, out index)
                   && index >= 0;
        }

        private static bool TryExtractEventTime(string? raw, out DateTime eventTime)
        {
            eventTime = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var fields = ZkTecoRecordLine.ParseFields(raw, out _);
            if (fields.TryGetValue("time_second", out var ts) || fields.TryGetValue("Time_Second", out ts))
            {
                if (ZkTecoAccTime.TryDecode(ts, out eventTime))
                {
                    eventTime = TruncateToSeconds(eventTime);
                    return true;
                }
            }

            if (fields.TryGetValue("Time", out var time) || fields.TryGetValue("time", out time))
            {
                if (DateTime.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out eventTime))
                {
                    eventTime = TruncateToSeconds(eventTime);
                    return true;
                }
            }

            return false;
        }

        /// <returns>True when CardNumber was written on the tracked employee (caller must SaveAsync).</returns>
        private async Task<bool> TryBackfillCardNumberAsync(Guid employeeId, string cardNo, CancellationToken ct)
        {
            var card = cardNo.Trim();
            if (card.Length == 0 || card is "0" or "00") return false;

            var emp = await _unitOfWork.Employees.GetQueryable()
                .FirstOrDefaultAsync(e => e.Id == employeeId && !e.isDeleted, ct);
            if (emp == null || !string.IsNullOrWhiteSpace(emp.CardNumber))
                return false;

            emp.CardNumber = card;
            emp.updatedOn = DateTime.Now;
            _logger.LogInformation("Backfilled CardNumber={Card} for employee {Id} from ATTLOG", card, employeeId);
            return true;
        }

        private static string NormalizePin(string? pin)
            => (pin ?? "").Trim();

        private static DateTime TruncateToSeconds(DateTime dt)
            => new(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Kind);
    }
}
