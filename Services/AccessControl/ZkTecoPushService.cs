using Core.Interfaces.Services.AccessControl;
using Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using static Core.Enums.BaseEnums;

namespace Services.AccessControl
{
    /// <summary>
    /// Implements ZKTeco ADMS Push Protocol over HTTP (/iclock/*).
    /// Compatible with devices that poll the server (device-initiated).
    /// </summary>
    public class ZkTecoPushService : IZkTecoPushService
    {
        private readonly IReaderService _readers;
        private readonly IDeviceCommandService _commands;
        private readonly IMovementLogIngestService _attLogIngest;
        private readonly IBiometricIngestService _biometricIngest;
        private readonly IDevicePushLogService _pushLog;
        private readonly IReaderSyncStateService _syncState;
        private readonly IReaderDeviceStatsService _statsService;
        private readonly IEmployeeDeviceSyncService _deviceSync;
        private readonly ZkTecoOptions _options;
        private readonly ILogger<ZkTecoPushService> _logger;

        public ZkTecoPushService(
            IReaderService readers,
            IDeviceCommandService commands,
            IMovementLogIngestService attLogIngest,
            IBiometricIngestService biometricIngest,
            IDevicePushLogService pushLog,
            IReaderSyncStateService syncState,
            IReaderDeviceStatsService statsService,
            IEmployeeDeviceSyncService deviceSync,
            IOptions<ZkTecoOptions> options,
            ILogger<ZkTecoPushService> logger)
        {
            _readers = readers;
            _commands = commands;
            _attLogIngest = attLogIngest;
            _biometricIngest = biometricIngest;
            _pushLog = pushLog;
            _syncState = syncState;
            _statsService = statsService;
            _deviceSync = deviceSync;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<string> HandleHandshakeAsync(
            string serial,
            string? pushver = null,
            string? pushOptionsFlag = null,
            CancellationToken ct = default)
        {
            await TouchDeviceAsync(serial);
            var sn = serial.Trim();
            // Device logs showed: options=all&pushver=3.1.2&PushOptionsFlag=1
            // With PushOptionsFlag=1 the firmware retries handshake forever unless
            // the reply includes PushProtVer. Until then it never calls getrequest.
            var proto = string.IsNullOrWhiteSpace(pushver) ? "2.2.14" : pushver.Trim();
            var wantsPushOptions = pushOptionsFlag == "1";
            var securityPush = wantsPushOptions
                || proto.StartsWith("3.", StringComparison.Ordinal);

            // Security PUSH 3.x: unregistered handshake must be exactly "OK".
            // A GET OPTION FROM body here blocks /iclock/registry → /iclock/push.
            if (securityPush)
            {
                _logger.LogInformation("ZKTeco Security PUSH handshake SN={Serial} proto={Proto} → OK", sn, proto);
                return "OK";
            }

            // Stamp=0 يطلب إعادة إرسال كل السجلات. نستخدم آخر ختم محفوظ؛
            // وإن لم يوجد نستخدم قيمة عالية حتى لا يُعاد تفريغ الحركة عند كل اتصال.
            var attStamp = _syncState.GetStamp(sn, "ATTLOG");
            var opStamp = _syncState.GetStamp(sn, "OPERLOG");
            if (attStamp == "0") attStamp = "9999";
            if (opStamp == "0") opStamp = "9999";

            var lines = new List<string>
            {
                $"GET OPTION FROM: {sn}",
                $"Stamp={attStamp}",
                $"OpStamp={opStamp}",
                "ErrorDelay=30",
                "Delay=10",
                "TransTimes=00:00;14:00",
                "TransInterval=1",
                "TransFlag=TransData AttLog OpLog AttPhoto EnrollUser ChgUser EnrollFP ChgFP UserPic",
                "TimeZone=2",
                "Realtime=1",
                "Encrypt=None",
                $"ServerVer={proto}"
            };

            if (wantsPushOptions)
            {
                lines.Add($"PushProtVer={proto}");
                lines.Add("PushOptionsFlag=1");
                lines.Add("PushOptions=UserCount,FPCount,FaceCount,PvCount,UserCardCount,FingerFunOn,FaceFunOn,PvFunOn,~DeviceName,FirmVer");
                lines.Add("SupportPing=1");
            }

            // Firmware parsers expect LF, not Windows CRLF from StringBuilder.AppendLine.
            return string.Join("\n", lines) + "\n";
        }

        public async Task<(string Body, string SessionId)> HandleRegistryAsync(
            string serial, string body, bool alreadyHasToken, CancellationToken ct = default)
        {
            var sn = serial.Trim();
            var reader = await TouchDeviceAsync(sn);
            if (!string.IsNullOrWhiteSpace(body))
                _syncState.UpdateFromInfo(sn, body);

            var session = _syncState.GetOrCreateSession(sn);
            if (reader != null)
            {
                await _readers.MarkSyncAsync(reader.Id);
                await EnsureDeviceInfoCommandAsync(reader.Id);
            }

            _logger.LogInformation(
                "ZKTeco registry SN={Serial} session={Session} code={Code} token={HasToken} bodyBytes={Len}",
                sn, session.SessionId, session.RegistryCode, alreadyHasToken, body?.Length ?? 0);

            if (alreadyHasToken)
            {
                var registered = new[]
                {
                    "registry=ok",
                    $"RegistryCode={session.RegistryCode}",
                    "ServerVersion=3.0.1",
                    "ServerName=PMS",
                    "PushVersion=3.0.1",
                    "PushProtVer=3.1.2",
                    "ErrorDelay=30",
                    "RequestDelay=10",
                    "TransTimes=00:00;14:00",
                    "TransInterval=1",
                    "TransTables=User Transaction",
                    "Realtime=1",
                    $"SessionID={session.SessionId}",
                    "TimeoutSec=10"
                };
                return (string.Join("\n", registered) + "\n", session.SessionId);
            }

            // First registration: body must be RegistryCode only (Security PUSH 7.4).
            return ($"RegistryCode={session.RegistryCode}", session.SessionId);
        }

        public async Task<(string Body, string SessionId)> HandlePushConfigAsync(
            string serial, CancellationToken ct = default)
        {
            var sn = serial.Trim();
            var reader = await TouchDeviceAsync(sn);
            var session = _syncState.GetOrCreateSession(sn);
            if (reader != null)
            {
                await _readers.MarkSyncAsync(reader.Id);
                await EnsureDeviceInfoCommandAsync(reader.Id);
            }

            _logger.LogInformation("ZKTeco /iclock/push config SN={Serial} session={Session}", sn, session.SessionId);

            var lines = new[]
            {
                "ServerVersion=3.0.1",
                "ServerName=PMS",
                "PushVersion=3.0.1",
                "PushProtVer=3.1.2",
                "ErrorDelay=30",
                "RequestDelay=10",
                "TransTimes=00:00;14:00",
                "TransInterval=1",
                "TransTables=User Transaction",
                "Realtime=1",
                $"SessionID={session.SessionId}",
                "TimeoutSec=10",
                "PushOptionsFlag=1",
                "PushOptions=UserCount,FPCount,FaceCount,PvCount,UserCardCount,FingerFunOn,FaceFunOn,PvFunOn,~DeviceName,FirmVer"
            };
            return (string.Join("\n", lines) + "\n", session.SessionId);
        }

        public async Task<string> HandleDataPushAsync(
            string serial, string? table, string body, string? stamp = null, CancellationToken ct = default)
        {
            var reader = await TouchDeviceAsync(serial);
            if (reader != null)
                await _readers.MarkSyncAsync(reader.Id);

            body ??= string.Empty;
            var sn = serial.Trim();
            if (!string.IsNullOrWhiteSpace(table) && !string.IsNullOrWhiteSpace(stamp))
                _syncState.UpdateStamp(sn, table, stamp);

            _logger.LogInformation(
                "ZKTeco push data SN={Serial} table={Table} stamp={Stamp} bytes={Len}",
                serial, table ?? "-", stamp ?? "-", body.Length);

            string outcome;
            var importedRows = 0;
            if (string.Equals(table, "options", StringComparison.OrdinalIgnoreCase)
                || ZkTecoInfoParser.LooksLikeInfo(body))
            {
                // Only persist counts that were present in THIS payload — never re-write
                // stale FaceCount/FPCount from an older in-memory snapshot.
                var fresh = _syncState.UpdateFromInfo(sn, body);
                var hasCounts = fresh.UserCount != null
                    || fresh.FingerprintCount != null
                    || fresh.FaceCount != null;
                outcome = hasCounts
                    ? $"إحصائيات الجهاز: مستخدمون={fresh.UserCount} · كف={fresh.FingerprintCount} · وجوه={fresh.FaceCount}"
                    : "خيارات الجهاز (اسم/إصدار فقط — بانتظار الأعداد)";

                if (hasCounts && reader != null)
                    await ApplyDeviceStatsFromInfoAsync(reader, sn, body, "options");

                if (!hasCounts && reader != null)
                    await EnsureDeviceInfoCommandAsync(reader.Id);
            }
            else if (IsAttLog(table, body) || IsRtLog(table))
            {
                importedRows = await _attLogIngest.ImportAttLogAsync(serial, body, ct);
                // إصلاح سجلات قديمة/حالية لم يُربط فيها الموظف بسبب صيغة pin الخاطئة
                var repaired = await _attLogIngest.RepairUnlinkedAsync(ct);
                outcome = repaired > 0
                    ? $"ATTLOG: {importedRows} حركة · إصلاح ربط: {repaired}"
                    : $"ATTLOG: {importedRows} حركة";
                _logger.LogInformation("ATTLOG import SN={Serial} rows={Count} repaired={Repaired}",
                    serial, importedRows, repaired);
            }
            else if (IsRealtime(table))
            {
                outcome = $"Realtime ({table}) — ignored";
            }
            else
            {
                var bio = await _biometricIngest.ImportCombinedAsync(serial, table, body, ct);
                importedRows = bio.UsersUpserted + bio.TemplatesSaved;
                outcome = bio.UsersUpserted > 0 || bio.TemplatesSaved > 0
                    ? $"موظفون: {bio.UsersUpserted} · بصمات: {bio.TemplatesSaved}"
                    : body.Length == 0
                        ? "جسم الطلب فارغ"
                        : bio.UsersParsed > 0
                            ? $"تحليل={bio.UsersParsed} مستخدم لكن لم يُحفظ أي تغيير"
                            : "لم يُتعرَّف على أي سجل";

                _logger.LogWarning(
                    "Biometric import SN={Serial} table={Table} users={Users} parsed={Parsed} templates={Templates} preview={Preview}",
                    serial, table ?? "-", bio.UsersUpserted, bio.UsersParsed, bio.TemplatesSaved, Preview(body, 300));
            }

            // Auto-ACK only when actual payload rows were received/parsed — not on count-only announcements.
            if (reader != null)
                await TryAcknowledgeLatestSentByTableAsync(sn, table, body, importedRows);

            RecordPush(sn, table, body, outcome);
            return "OK";
        }

        /// <summary>Modern firmware posts DATA QUERY results to /iclock/querydata.</summary>
        public Task<string> HandleQueryDataAsync(
            string serial, string? table, string? type, string body, CancellationToken ct = default)
            => HandleDataPushAsync(serial, table ?? type, body, stamp: null, ct);

        private static string Preview(string body, int max)
        {
            if (string.IsNullOrEmpty(body)) return "(فارغ)";
            var text = body.Replace("\t", " | ").Trim();
            return text.Length <= max ? text : text[..max] + " …";
        }

        private static bool IsAttLog(string? table, string body)
        {
            if (string.Equals(table, "ATTLOG", StringComparison.OrdinalIgnoreCase))
                return true;

            // Acc / SpeedFace: DATA QUERY tablename=transaction (مزامنة سجل الحركة)
            if (string.Equals(table, "transaction", StringComparison.OrdinalIgnoreCase))
                return true;

            // A named non-attendance table (OPERLOG, USERINFO, BIODATA, ...) is never attendance data.
            if (!string.IsNullOrWhiteSpace(table))
                return false;

            // Some firmware posts without table name — detect attendance rows.
            if (string.IsNullOrWhiteSpace(body)) return false;
            var first = body.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (first == null || !first.Contains('\t')) return false;

            // Acc key=value attendance: time=...\tpin=... OR time_second=...\tpin=...
            if (first.Contains("pin=", StringComparison.OrdinalIgnoreCase)
                && (first.Contains("time=", StringComparison.OrdinalIgnoreCase)
                    || first.Contains("time_second=", StringComparison.OrdinalIgnoreCase)
                    || first.Contains("DateTime=", StringComparison.OrdinalIgnoreCase)
                    || first.Contains("inoutstatus=", StringComparison.OrdinalIgnoreCase)
                    || first.Contains("inoutstate=", StringComparison.OrdinalIgnoreCase)))
                return true;

            // Exclude USERINFO / FINGERTMP key=value formats (not attendance).
            if (first.Contains("TMP=", StringComparison.OrdinalIgnoreCase)
                || first.Contains("Name=", StringComparison.OrdinalIgnoreCase)
                || first.Contains("Privilege=", StringComparison.OrdinalIgnoreCase))
                return false;

            // Classic positional ATTLOG: PIN\tTime\tStatus...
            var parts = first.Split('\t');
            return parts.Length >= 2 && !parts[0].Contains('=');
        }

        public Task<string> HandleGetRequestAsync(string serial, string? info = null, CancellationToken ct = default)
            => HandlePollAsync(serial, info, ct);

        /// <summary>
        /// Heartbeat / command poll. Devices call /iclock/getrequest or /iclock/ping every Delay seconds.
        /// </summary>
        public async Task<string> HandlePollAsync(string serial, string? info = null, CancellationToken ct = default)
        {
            var reader = await TouchDeviceAsync(serial);
            if (reader == null)
            {
                _logger.LogWarning("ZKTeco poll from unknown serial {Serial}", serial);
                return "OK";
            }

            if (!string.IsNullOrWhiteSpace(info))
            {
                var fresh = _syncState.UpdateFromInfo(serial.Trim(), info);
                _logger.LogInformation("ZKTeco INFO from {Serial}: {Info}", serial, info);

                // Positional INFO=users,fp,att,... must not overwrite palm (PvCount)
                // stored from GET OPTION. Only named FPCount/PvCount update that column.
                var namedPalm = info.Contains("PvCount=", StringComparison.OrdinalIgnoreCase)
                    || info.Contains("FPCount=", StringComparison.OrdinalIgnoreCase)
                    || info.Contains("FingerPrintCount=", StringComparison.OrdinalIgnoreCase);
                var cardCount = ZkTecoInfoParser.TryCardCount(info);
                var namedLogs = ZkTecoInfoParser.TryCount(info, "TransactionCount", "AttLogCount");
                var attCount = namedLogs ?? fresh.AttLogCount;
                if (fresh.UserCount != null || fresh.FaceCount != null || namedPalm || cardCount != null || attCount is > 0)
                {
                    await _statsService.UpsertAsync(reader.Id, serial.Trim(),
                        userCount: fresh.UserCount,
                        faceCount: fresh.FaceCount,
                        fingerprintCount: namedPalm ? fresh.FingerprintCount : null,
                        cardCount: cardCount,
                        attLogCount: attCount is > 0 ? attCount : null,
                        firmwareVersion: fresh.FirmwareVersion);
                }
            }

            await EnsureStatsRefreshAsync(reader.Id);
            return await DeliverPendingCommandsAsync(reader, serial, ct);
        }

        private async Task EnsureStatsRefreshAsync(Guid readerId)
        {
            var stats = await _statsService.GetByReaderAsync(readerId);
            if (stats != null && stats.FetchedAt > DateTime.Now.AddMinutes(-5))
                return;

            await EnsureDeviceInfoCommandAsync(readerId);
        }

        private async Task EnsureDeviceInfoCommandAsync(Guid readerId)
        {
            await EnqueueOnceAsync(readerId, DeviceCommandType.QueryDeviceInfo);
            await EnqueueOnceAsync(readerId, DeviceCommandType.Sync, "INFO");
        }

        private async Task EnqueueOnceAsync(Guid readerId, DeviceCommandType type, string? payload = null)
        {
            var typeName = type.ToString();
            var pending = await _commands.GetPendingAsync(50);
            if (pending.Any(c => c.ReaderId == readerId
                && c.CommandType == typeName
                && string.Equals(c.Payload ?? "", payload ?? "", StringComparison.Ordinal)))
                return;

            var recent = await _commands.GetByReaderAsync(readerId);
            if (recent.Any(c =>
                    c.CommandType == typeName
                    && string.Equals(c.Payload ?? "", payload ?? "", StringComparison.Ordinal)
                    && c.createdOn > DateTime.Now.AddMinutes(-3)))
                return;

            await _commands.EnqueueAsync(readerId, type, payload);
        }

        private async Task<string> DeliverPendingCommandsAsync(Core.Entities.Reader reader, string serial, CancellationToken ct)
        {
            var deviceSerial = serial.Trim();
            var allReadersForSerial = (await _readers.GetAllAsync())
                .Where(r => !r.isDeleted && string.Equals(r.DeviceSerial, deviceSerial, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var pending = new List<Core.Entities.DeviceCommand>();
            foreach (var r in allReadersForSerial)
            {
                pending.AddRange(await _commands.GetPendingByReaderAsync(r.Id, 500));
            }

            pending = pending
                .OrderBy(c => CommandPriority(c.CommandType))
                .ThenBy(c => c.createdOn)
                .ToList();

            _logger.LogInformation(
                "DeliverPending SN={Serial} readerId={ReaderId} matchingReaderIds={MatchingReaderIds} pendingCount={Count} types=[{Types}]",
                serial,
                reader.Id,
                string.Join(", ", allReadersForSerial.Select(r => r.Id)),
                pending.Count,
                string.Join(", ", pending.Select(c => c.CommandType)));

            var lines = new List<string>();
            foreach (var cmd in pending)
            {
                if (lines.Count >= 8) break;

                if (string.Equals(cmd.CommandType, nameof(DeviceCommandType.FullSync), StringComparison.OrdinalIgnoreCase))
                {
                    await _commands.UpdateStatusAsync(cmd.Id, DeviceCommandStatus.Acknowledged);
                    await DecomposeFullSyncAsync(reader.Id);
                    continue;
                }

                if (ZkTecoAccUserCommand.IsWaitForUid(cmd.Payload))
                    continue;

                if (IsUnsafeAccUserUpdate(cmd.CommandType, cmd.Payload))
                {
                    await _commands.UpdateStatusAsync(cmd.Id, DeviceCommandStatus.Failed,
                        "Skipped: DATA UPDATE user without uid inserts a blank user on SpeedFace");
                    _logger.LogWarning(
                        "Dropped unsafe user UPDATE SN={Serial} payload={Payload}",
                        serial, cmd.Payload);
                    continue;
                }

                var protocolId = DeviceCommandService.NextProtocolCommandId();
                var payload = cmd.Payload;
                if (string.Equals(cmd.CommandType, nameof(DeviceCommandType.QueryAttLog), StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(payload))
                {
                    // Acc Security PUSH: NewRecord = حركات لم تُرحَّل بعد فقط
                    payload = "NewRecord";
                }

                var line = FormatCommand(protocolId, cmd.CommandType, payload);
                await _commands.MarkSentAsync(cmd.Id, protocolId);
                lines.Add(line);
                _logger.LogInformation(
                    "ZKTeco command sent SN={Serial} commandId={CommandId} protocolId={Id} type={Type} payload={Payload} line={Line}",
                    serial, cmd.Id, protocolId, cmd.CommandType,
                    (cmd.Payload ?? "-").Replace("\t", "\\t"),
                    line.Replace("\t", "\\t"));
            }

            if (lines.Count == 0)
                return "OK";

            // Device firmware expects LF-separated C:id:cmd lines (see PUSH SDK).
            return string.Join('\n', lines) + "\n";
        }

        private async Task DecomposeFullSyncAsync(Guid readerId)
        {
            await _commands.EnqueueAsync(readerId, DeviceCommandType.QueryDeviceInfo);
            await _commands.EnqueueAsync(readerId, DeviceCommandType.Sync, "INFO");
            await _commands.EnqueueAsync(readerId, DeviceCommandType.QueryAttLog);
            await _commands.EnqueueAsync(readerId, DeviceCommandType.QueryUserInfo);
            await _commands.EnqueueAsync(readerId, DeviceCommandType.QueryFingerTmp);
            await _commands.EnqueueAsync(readerId, DeviceCommandType.QueryBioData);
            _logger.LogInformation("FullSync decomposed into 6 sub-commands for reader {ReaderId}", readerId);
        }

        /// <summary>Queues the standard pull sequence after resetting sync stamps.</summary>
        public async Task EnqueueBiometricPullAsync(Guid readerId, string? pin = null, CancellationToken ct = default)
        {
            var reader = await _readers.GetByIdAsync(readerId);
            if (reader == null) return;

            _syncState.ResetStamps(reader.DeviceSerial);
            await _commands.EnqueueAsync(readerId, DeviceCommandType.QueryDeviceInfo);
            await _commands.EnqueueAsync(readerId, DeviceCommandType.Sync, "INFO");
            await _commands.EnqueueAsync(readerId, DeviceCommandType.QueryUserInfo, pin);
            await _commands.EnqueueAsync(readerId, DeviceCommandType.QueryFingerTmp, pin);
            await _commands.EnqueueAsync(readerId, DeviceCommandType.QueryBioData, pin);
        }

        private void RecordPush(string sn, string? table, string body, string outcome)
        {
            _pushLog.Record(new Core.Models.DevicePushEvent(
                ReceivedAt: DateTime.Now,
                DeviceSerial: sn,
                Table: table ?? "-",
                ByteCount: body.Length,
                LineCount: body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length,
                Preview: Preview(body, 600),
                Outcome: outcome));
        }

        public async Task<string> HandleDeviceCmdAsync(string serial, string body, CancellationToken ct = default)
        {
            var reader = await TouchDeviceAsync(serial);
            if (reader == null) return "OK";

            body ??= string.Empty;
            _logger.LogWarning(
                "ZKTeco devicecmd SN={Serial} bodyLen={Len} preview={Preview}",
                serial, body.Length, Preview(body, 180));

            // Counts may sit on the ACK line itself: ID=1&Return=0&CMD=GET OPTION&UserCount=12&FaceCount=5
            if (ZkTecoInfoParser.LooksLikeInfo(body)
                || body.Contains("CMD=INFO", StringComparison.OrdinalIgnoreCase)
                || body.Contains("CMD=GET OPTION", StringComparison.OrdinalIgnoreCase))
                await ApplyDeviceStatsFromInfoAsync(reader, serial.Trim(), body, "devicecmd");

            foreach (var line in body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = ParseKeyValues(line);
                if (!parts.TryGetValue("ID", out var idRaw) || !int.TryParse(idRaw, out var protocolId))
                    continue;

                var cmd = await _commands.GetByProtocolIdAsync(reader.Id, protocolId);
                if (cmd == null) continue;

                var returnCode = parts.TryGetValue("Return", out var r) ? r : "0";
                _logger.LogWarning(
                    "DeviceCmd ACK SN={Serial} ID={Id} Return={Return} CMD={Cmd} type={Type}",
                    serial, protocolId, returnCode,
                    parts.TryGetValue("CMD", out var cmdName) ? cmdName : "-",
                    cmd.CommandType);

                if (returnCode == "0")
                {
                    await _commands.UpdateStatusAsync(cmd.Id, DeviceCommandStatus.Acknowledged);
                    await _readers.MarkSyncAsync(reader.Id);
                }
                else if (int.TryParse(returnCode, out var recordCount)
                         && recordCount > 0
                         && IsFullUserListQuery(cmd))
                {
                    // This firmware reports the user-table size as Return=N, not as an error.
                    await _statsService.UpsertAsync(reader.Id, serial.Trim(), userCount: recordCount);
                    await _commands.UpdateStatusAsync(cmd.Id, DeviceCommandStatus.Acknowledged);
                    await _readers.MarkSyncAsync(reader.Id);
                    _logger.LogWarning(
                        "Query user count SN={Serial} Return={Count} stored as UserCount",
                        serial, recordCount);
                }
                else if (returnCode == "-1004"
                         && string.Equals(cmd.CommandType, nameof(DeviceCommandType.UpdateUserInfo), StringComparison.OrdinalIgnoreCase)
                         && !ZkTecoAccUserCommand.IsCreateUserLine(cmd.Payload))
                {
                    await _commands.UpdateStatusAsync(cmd.Id, DeviceCommandStatus.Failed, "Device Return=-1004");
                    await _deviceSync.BeginCreateAfterMissingUserAsync(reader.Id, cmd.Payload, ct);
                    _logger.LogWarning(
                        "User missing on SN={Serial} — queued create after Return=-1004 payload={Payload}",
                        serial, (cmd.Payload ?? "").Replace("\t", "\\t"));
                }
                else if (returnCode == "-1004" && IsStatsQuery(cmd.CommandType))
                {
                    await _commands.UpdateStatusAsync(cmd.Id, DeviceCommandStatus.Failed, "Device Return=-1004");
                }
                else
                {
                    await _commands.UpdateStatusAsync(cmd.Id, DeviceCommandStatus.Failed, $"Device Return={returnCode}");
                    await IncrementFailureAsync(reader.Id);
                }
            }

            return "OK";
        }

        public string FormatCommand(int protocolCommandId, string commandType, string? payload)
        {
            if (!Enum.TryParse<DeviceCommandType>(commandType, true, out var type))
                type = DeviceCommandType.Sync;

            var body = type switch
            {
                DeviceCommandType.TestConnection => "CHECK",
                DeviceCommandType.Reboot => "REBOOT",
                DeviceCommandType.Sync => string.IsNullOrWhiteSpace(payload) ? "INFO" : payload,
                DeviceCommandType.QueryAttLog => FormatQueryAttLog(payload),
                DeviceCommandType.QueryUserInfo => FormatQueryUserInfo(payload),
                DeviceCommandType.QueryFingerTmp => FormatQueryFingerTmp(payload),
                DeviceCommandType.QueryBioData => FormatQueryBioData(payload),
                DeviceCommandType.QueryDeviceInfo =>
                    "GET OPTION FROM UserCount,FPCount,FaceCount,PvCount,UserCardCount,~DeviceName,FirmVer",
                DeviceCommandType.UpdateUserInfo => FormatUserInfoUpdate(payload),
                DeviceCommandType.SyncAllData => "CHECK",
                DeviceCommandType.FullSync => "CHECK",
                DeviceCommandType.AddTemplate => FormatDataCommand(payload),
                DeviceCommandType.RemoveTemplate => FormatDataCommand(payload),
                DeviceCommandType.AddPermission => string.IsNullOrWhiteSpace(payload) ? "CHECK" : $"DATA {payload}",
                DeviceCommandType.RemovePermission => string.IsNullOrWhiteSpace(payload) ? "CHECK" : $"DATA {payload}",
                _ => payload ?? "INFO"
            };

            return $"C:{protocolCommandId}:{body}";
        }

        private static string FormatDataCommand(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return "CHECK";
            var text = payload.Trim();
            return text.StartsWith("DATA ", StringComparison.OrdinalIgnoreCase) ? text : "DATA " + text;
        }

        private static int CommandPriority(string commandType)
        {
            // Stats must leave on the next poll even when user commands are queued.
            if (string.Equals(commandType, nameof(DeviceCommandType.QueryDeviceInfo), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.Sync), StringComparison.OrdinalIgnoreCase))
                return -1;

            if (string.Equals(commandType, nameof(DeviceCommandType.UpdateUserInfo), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.QueryUserInfo), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.AddTemplate), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.AddPermission), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.RemoveTemplate), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.RemovePermission), StringComparison.OrdinalIgnoreCase))
                return 0;
            return 1;
        }

        private static bool IsFullUserListQuery(Core.Entities.DeviceCommand cmd)
        {
            if (!string.Equals(cmd.CommandType, nameof(DeviceCommandType.QueryUserInfo), StringComparison.OrdinalIgnoreCase))
                return false;
            var payload = cmd.Payload?.Trim();
            return string.IsNullOrEmpty(payload) || payload == "*";
        }

        private static bool IsStatsQuery(string commandType)
        {
            return string.Equals(commandType, nameof(DeviceCommandType.QueryDeviceInfo), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.QueryUserInfo), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.QueryAttLog), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.QueryBioData), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.QueryFingerTmp), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.Sync), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUnsafeAccUserUpdate(string commandType, string? payload)
        {
            if (!string.Equals(commandType, nameof(DeviceCommandType.UpdateUserInfo), StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrWhiteSpace(payload))
                return true;

            var text = payload.Trim();
            // Classic USERINFO without fields inserts blank users on Acc/SpeedFace.
            if (text.Equals("DATA UPDATE USERINFO", StringComparison.OrdinalIgnoreCase))
                return true;

            // Attendance USERINFO without PIN inserts a blank user.
            // ZAM180-NF accepts DATA UPDATE USERINFO PIN=... as add-or-update.
            if (text.Contains("USERINFO", StringComparison.OrdinalIgnoreCase))
                return !Regex.IsMatch(text, @"\bPIN=\S+");
            if (text.StartsWith("PIN=", StringComparison.OrdinalIgnoreCase))
                return true;

            if (text.Contains("userauthorize", StringComparison.OrdinalIgnoreCase))
                return !Regex.IsMatch(text, @"Pin=\S+");

            var isAccUser = Regex.IsMatch(text, @"UPDATE user[\t ]", RegexOptions.IgnoreCase);
            if (!isAccUser)
                return false;

            // uid= without Pin= creates a blank Acc slot + default palm.
            // uid= together with Pin= is the create path for a missing user.
            if (Regex.IsMatch(text, @"uid=", RegexOptions.IgnoreCase)
                && !Regex.IsMatch(text, @"Pin=\S+"))
                return true;
            // Acc Push V.3 key is Pin= (case-sensitive).
            if (!Regex.IsMatch(text, @"Pin=\S+"))
                return true;

            return false;
        }

        private static string FormatQueryAttLog(string? payload)
        {
            // Acc Security PUSH: filter=NewRecord = الحركات الجديدة فقط؛
            // أو filter=index>N بعد آخر فهرس محفوظ؛ أو starttime=...\tendtime=...
            if (!string.IsNullOrWhiteSpace(payload))
            {
                var p = payload.Trim();
                if (p.StartsWith("DATA ", StringComparison.OrdinalIgnoreCase))
                    return p;
                if (p.Contains("tablename=", StringComparison.OrdinalIgnoreCase))
                    return p.StartsWith("DATA", StringComparison.OrdinalIgnoreCase) ? p : $"DATA QUERY {p}";
                return $"DATA QUERY tablename=transaction,fielddesc=*,filter={p}";
            }

            return "DATA QUERY tablename=transaction,fielddesc=*,filter=NewRecord";
        }

        private static string FormatQueryFingerTmp(string? payload)
        {
            // SpeedFace: fingerprints may be biodata Type=1/7 OR classic templatev10.
            // Query all biodata first path is handled by QueryBioData; this keeps templatev10
            // as a secondary source for older firmware.
            if (string.IsNullOrWhiteSpace(payload))
                return "DATA QUERY tablename=templatev10,fielddesc=*";
            return $"DATA QUERY tablename=templatev10,fielddesc=*,filter=pin={payload.Trim()}";
        }

        private static string FormatQueryBioData(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return "DATA QUERY tablename=biodata,fielddesc=*";
            var p = payload.Trim();
            if (p.StartsWith("DATA ", StringComparison.OrdinalIgnoreCase))
                return p;
            if (p.StartsWith("Type=", StringComparison.OrdinalIgnoreCase))
                return $"DATA QUERY tablename=biodata,fielddesc=*,filter={p}";
            return $"DATA QUERY tablename=biodata,fielddesc=*,filter=pin={p}";
        }

        private static string FormatQueryUserInfo(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload) || payload.Trim() == "*")
                return ZkTecoAccUserCommand.QueryAllUsers();
            var text = payload.Trim();
            if (text.StartsWith("DATA ", StringComparison.OrdinalIgnoreCase))
                return text;
            if (text.Contains("tablename=", StringComparison.OrdinalIgnoreCase))
                return "DATA QUERY " + text;
            return $"DATA QUERY tablename=user,fielddesc=*,filter=pin={text}";
        }

        private static string FormatUserInfoUpdate(string? payload)
        {
            // Never emit bare DATA UPDATE USERINFO — SpeedFace creates a blank user.
            if (string.IsNullOrWhiteSpace(payload))
                return "CHECK";
            var text = payload.Trim();
            if (ZkTecoAccUserCommand.IsWaitForUid(text))
                return "CHECK";
            if (Regex.IsMatch(text, @"\bTIMEZONE\b", RegexOptions.IgnoreCase)
                || Regex.IsMatch(text, @"\btimezone[\t ]", RegexOptions.IgnoreCase)
                || Regex.IsMatch(text, @"UPDATE\s+AccGroup", RegexOptions.IgnoreCase)
                || Regex.IsMatch(text, @"UPDATE\s+accgroup", RegexOptions.IgnoreCase))
            {
                return text.StartsWith("DATA ", StringComparison.OrdinalIgnoreCase)
                    ? text
                    : "DATA " + text;
            }
            if (text.Contains("userauthorize", StringComparison.OrdinalIgnoreCase)
                && Regex.IsMatch(text, @"Pin=\S+"))
            {
                return text.StartsWith("DATA ", StringComparison.OrdinalIgnoreCase)
                    ? text
                    : "DATA UPDATE userauthorize " + text;
            }
            if (text.Contains("USERINFO", StringComparison.OrdinalIgnoreCase))
            {
                if (!Regex.IsMatch(text, @"\bPIN=\S+"))
                    return "CHECK";
                return text.StartsWith("DATA ", StringComparison.OrdinalIgnoreCase)
                    ? text
                    : "DATA UPDATE USERINFO " + text;
            }
            if (Regex.IsMatch(text, @"uid=", RegexOptions.IgnoreCase)
                && !Regex.IsMatch(text, @"Pin=\S+"))
                return "CHECK";
            if (text.StartsWith("DATA ", StringComparison.OrdinalIgnoreCase)
                && Regex.IsMatch(text, @"Pin=\S+"))
                return text;
            if (Regex.IsMatch(text, @"Pin=\S+"))
                return text.StartsWith("UPDATE ", StringComparison.OrdinalIgnoreCase)
                    ? "DATA " + text
                    : "DATA UPDATE user " + text;
            return "CHECK";
        }

        private async Task<Core.Entities.Reader?> TouchDeviceAsync(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return null;
            var reader = await _readers.GetBySerialAsync(serial.Trim());
            if (reader == null) return null;
            await _readers.ReportHeartbeatAsync(reader.Id, ReaderStatus.Online);
            return reader;
        }

        private async Task IncrementFailureAsync(Guid readerId)
        {
            var reader = await _readers.GetByIdAsync(readerId);
            if (reader == null) return;
            reader.ConsecutiveFailures++;
            if (reader.ConsecutiveFailures >= _options.FaultyAfterFailures)
                reader.Status = ReaderStatus.Faulty;
            await _readers.UpdateAsync(reader);
        }

        public async Task UpdateQueryDataCountAsync(
            string serial, string tableName, int count, int? protocolCommandId = null)
        {
            var sn = serial.Trim();
            var reader = await _readers.GetBySerialAsync(sn);
            if (reader == null) return;

            var tbl = tableName.ToLowerInvariant();
            var existing = await _statsService.GetByReaderAsync(reader.Id);

            int? userCount = null;
            int? faceCount = null;
            int? fpCount = null;
            int? attCount = null;
            var fw = existing?.FirmwareVersion;

            // When biodata is used for both FP (Type=1) and Face (Type=9), resolve via cmd payload.
            string? cmdType = null;
            string? cmdPayload = null;
            if (protocolCommandId is > 0)
            {
                var cmd = await _commands.GetByProtocolIdAsync(reader.Id, protocolCommandId.Value);
                cmdType = cmd?.CommandType;
                cmdPayload = cmd?.Payload;
            }

            switch (tbl)
            {
                case "user":
                    userCount = count;
                    break;
                case "templatev10":
                case "fingertmp":
                    if (count > 0) fpCount = count;
                    break;
                case "biodata":
                case "biophoto":
                case "face":
                case "tabledata":
                    if (IsBioFilter(cmdPayload, 9) || IsBioFilter(cmdPayload, 8))
                    {
                        if (count > 0) faceCount = count;
                    }
                    else if (IsBioFilter(cmdPayload, 7) || IsBioFilter(cmdPayload, 10)
                             || IsBioFilter(cmdPayload, 1) || IsBioFilter(cmdPayload, 2))
                    {
                        if (count > 0) fpCount = count;
                    }
                    else if (string.Equals(cmdType, nameof(DeviceCommandType.QueryFingerTmp), StringComparison.OrdinalIgnoreCase))
                    {
                        if (count > 0) fpCount = count;
                    }
                    else if (string.IsNullOrWhiteSpace(cmdPayload)
                             && string.Equals(cmdType, nameof(DeviceCommandType.QueryBioData), StringComparison.OrdinalIgnoreCase))
                    {
                        // Unfiltered biodata count is total records — do not map to face/fp stats.
                        _logger.LogInformation(
                            "biodata unfiltered count={Count} SN={Serial} — stats come from body parsing",
                            count, sn);
                    }
                    else if (count > 0)
                    {
                        _logger.LogInformation(
                            "biodata count={Count} SN={Serial} payload={Payload} — not mapped to face/fp stats",
                            count, sn, cmdPayload ?? "-");
                    }
                    else
                    {
                        _logger.LogDebug(
                            "biodata count=0 SN={Serial} payload={Payload} — skipped (SpeedFace filter often returns 0)",
                            sn, cmdPayload ?? "-");
                    }
                    break;
                case "transaction":
                case "attlog":
                    attCount = count;
                    break;
                case "options":
                    // querydata options body may include counts when GET OPTION FROM requests them.
                    return;
            }

            await _statsService.UpsertAsync(reader.Id, sn,
                userCount: userCount,
                faceCount: faceCount,
                fingerprintCount: fpCount,
                attLogCount: attCount,
                firmwareVersion: fw);

            // Keep in-memory snapshot in sync so later INFO heartbeats cannot resurrect stale values.
            if (userCount != null || faceCount != null || fpCount != null || attCount != null)
                _syncState.SetCounts(sn, userCount, faceCount, fpCount, attCount, fw);

            _logger.LogWarning(
                "Stats from querydata SN={Serial} table={Table} count={Count} cmdType={CmdType} payload={Payload} → face={Face} fp={Fp}",
                sn, tableName, count, cmdType ?? "-", cmdPayload ?? "-", faceCount, fpCount);

            // Count response means the DATA QUERY ran successfully — ACK the matching Sent command.
            if (protocolCommandId is > 0)
            {
                var cmd = await _commands.GetByProtocolIdAsync(reader.Id, protocolCommandId.Value);
                if (cmd != null && cmd.Status == DeviceCommandStatus.Sent)
                {
                    await _commands.UpdateStatusAsync(cmd.Id, DeviceCommandStatus.Acknowledged);
                    _logger.LogWarning(
                        "Auto-ACK via querydata count SN={Serial} protocolId={Id} type={Type} count={Count}",
                        sn, protocolCommandId, cmd.CommandType, count);
                }
            }
        }

        /// <summary>
        /// Persist FaceCount / PvCount (palm) / UserCount from device INFO or options payload.
        /// SpeedFace reports authoritative bio counts here — not in filtered biodata querydata.
        /// </summary>
        private async Task ApplyDeviceStatsFromInfoAsync(
            Core.Entities.Reader reader, string serial, string body, string source)
        {
            var infoPayload = ExtractInfoPayload(body);
            if (string.IsNullOrWhiteSpace(infoPayload)) return;

            var fresh = _syncState.UpdateFromInfo(serial, infoPayload);
            var cardCount = ZkTecoInfoParser.TryCardCount(infoPayload);
            var reportedLogs = ZkTecoInfoParser.TryCount(infoPayload, "TransactionCount", "AttLogCount");
            var hasCounts = fresh.UserCount != null
                || fresh.FingerprintCount != null
                || fresh.FaceCount != null
                || cardCount != null
                || reportedLogs is > 0;

            if (!hasCounts)
            {
                _logger.LogInformation(
                    "INFO/options SN={Serial} source={Source} — no counts in payload (len={Len})",
                    serial, source, infoPayload.Length);
                return;
            }

            // TransactionCount=0 on SpeedFace is the unsynced buffer, not the stored total.
            await _statsService.UpsertAsync(reader.Id, serial,
                userCount: fresh.UserCount,
                faceCount: fresh.FaceCount,
                fingerprintCount: fresh.FingerprintCount,
                cardCount: cardCount,
                attLogCount: reportedLogs is > 0 ? reportedLogs : null,
                firmwareVersion: fresh.FirmwareVersion);

            _syncState.SetCounts(serial,
                fresh.UserCount, fresh.FaceCount, fresh.FingerprintCount, firmwareVersion: fresh.FirmwareVersion);

            _logger.LogWarning(
                "Stats from {Source} SN={Serial} users={Users} face={Face} palm(Pv)={Palm} cards={Cards} logs={Logs}",
                source, serial, fresh.UserCount, fresh.FaceCount, fresh.FingerprintCount, cardCount, reportedLogs);
        }

        private static string ExtractInfoPayload(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return "";

            var kept = new List<string>();
            foreach (var raw in body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;

                if (line.Contains('&') || line.Contains("CMD=", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var part in line.Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (IsStatToken(part))
                            kept.Add(part.Trim());
                    }
                    continue;
                }

                kept.Add(line);
            }

            return string.Join('\n', kept);
        }

        private static bool IsStatToken(string token)
        {
            var eq = token.IndexOf('=');
            if (eq <= 0) return false;
            var key = token[..eq].Trim().TrimStart('~');
            return key.Equals("UserCount", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Users", StringComparison.OrdinalIgnoreCase)
                || key.Equals("FPCount", StringComparison.OrdinalIgnoreCase)
                || key.Equals("FingerPrintCount", StringComparison.OrdinalIgnoreCase)
                || key.Equals("FingerprintCount", StringComparison.OrdinalIgnoreCase)
                || key.Equals("PvCount", StringComparison.OrdinalIgnoreCase)
                || key.Equals("FaceCount", StringComparison.OrdinalIgnoreCase)
                || key.Equals("CardCount", StringComparison.OrdinalIgnoreCase)
                || key.Equals("UserCardCount", StringComparison.OrdinalIgnoreCase)
                || key.Equals("FirmVer", StringComparison.OrdinalIgnoreCase)
                || key.Equals("FWVersion", StringComparison.OrdinalIgnoreCase)
                || key.Equals("FirmwareVersion", StringComparison.OrdinalIgnoreCase)
                || key.Equals("DeviceName", StringComparison.OrdinalIgnoreCase)
                || key.Equals("TransactionCount", StringComparison.OrdinalIgnoreCase)
                || key.Equals("AttLogCount", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRtLog(string? table)
            => string.Equals(table, "rtlog", StringComparison.OrdinalIgnoreCase);

        private static bool IsRealtime(string? table)
            => string.Equals(table, "rtstate", StringComparison.OrdinalIgnoreCase);

        private async Task TryAcknowledgeLatestSentByTableAsync(
            string deviceSerial, string? table, string body, int importedRows)
        {
            if (string.IsNullOrWhiteSpace(deviceSerial) || string.IsNullOrWhiteSpace(table))
                return;

            if (string.IsNullOrWhiteSpace(body) && importedRows <= 0)
            {
                _logger.LogInformation(
                    "Auto-ACK skipped SN={Serial} table={Table} — empty body (count-only announcement?)",
                    deviceSerial, table);
                return;
            }

            var t = table.Trim().ToLowerInvariant();

            DeviceCommandType? expected = t switch
            {
                "user" or "userinfo" => DeviceCommandType.QueryUserInfo,
                "templatev10" or "fingertmp" => DeviceCommandType.QueryFingerTmp,
                "biophoto" or "face" => DeviceCommandType.QueryBioData,
                "biodata" => InferBiodataCommandType(body),
                "transaction" or "attlog" => DeviceCommandType.QueryAttLog,
                _ => null
            };

            if (expected == null) return;

            if (expected == DeviceCommandType.QueryUserInfo && importedRows <= 0)
            {
                var parsed = ZkTecoUserInfoParser.Parse(body, table);
                if (parsed.Count == 0)
                {
                    _logger.LogWarning(
                        "Auto-ACK skipped SN={Serial} table={Table} — body received but no user rows parsed",
                        deviceSerial, table);
                    return;
                }
            }

            var readers = await _readers.GetAllAsync();
            var matches = readers
                .Where(r => !r.isDeleted && string.Equals(r.DeviceSerial, deviceSerial.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var r in matches)
            {
                var recent = await _commands.GetByReaderAsync(r.Id);
                var cmd = recent
                    .Where(c =>
                        !c.isDeleted
                        && c.Status == DeviceCommandStatus.Sent
                        && string.Equals(c.CommandType, expected.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(c => c.SentOn ?? c.createdOn)
                    .FirstOrDefault();

                if (cmd == null) continue;

                await _commands.UpdateStatusAsync(cmd.Id, DeviceCommandStatus.Acknowledged);
                _logger.LogWarning(
                    "Auto-ACK: Marked {CommandType} as Acknowledged (readerId={ReaderId}) due to received table={Table}",
                    expected.Value, r.Id, table);
            }
        }

        private static bool IsBioFilter(string? payload, int type)
        {
            if (string.IsNullOrWhiteSpace(payload)) return false;
            return payload.Contains($"Type={type}", StringComparison.OrdinalIgnoreCase);
        }

        private static DeviceCommandType InferBiodataCommandType(string body)
        {
            if (body.Contains("type=1", StringComparison.OrdinalIgnoreCase)
                || body.Contains("Type=1", StringComparison.OrdinalIgnoreCase))
                return DeviceCommandType.QueryFingerTmp;
            return DeviceCommandType.QueryBioData;
        }

        private static Dictionary<string, string> ParseKeyValues(string line)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in line.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = token.IndexOf('=');
                if (idx <= 0) continue;
                dict[token[..idx].Trim()] = token[(idx + 1)..].Trim();
            }
            return dict;
        }
    }
}
