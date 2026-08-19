using Core.Interfaces.Services.AccessControl;
using Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

            var lines = new List<string>
            {
                $"GET OPTION FROM: {sn}",
                "Stamp=9999",
                "OpStamp=9999",
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
                lines.Add("PushOptions=UserCount,FPCount,FaceCount,FingerFunOn,FaceFunOn,~DeviceName,FirmVer");
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
                "PushOptions=UserCount,FPCount,FaceCount,FingerFunOn,FaceFunOn,~DeviceName,FirmVer"
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
            if (string.Equals(table, "options", StringComparison.OrdinalIgnoreCase)
                || ZkTecoInfoParser.LooksLikeInfo(body))
            {
                _syncState.UpdateFromInfo(sn, body);
                var snap = _syncState.GetSnapshot(sn);
                var hasCounts = snap?.UserCount != null
                    || snap?.FingerprintCount != null
                    || snap?.FaceCount != null;
                outcome = hasCounts
                    ? $"إحصائيات الجهاز: مستخدمون={snap!.UserCount} · بصمات={snap.FingerprintCount} · وجوه={snap.FaceCount}"
                    : "خيارات الجهاز (اسم/إصدار فقط — بانتظار الأعداد)";

                if (hasCounts && reader != null && snap != null)
                {
                    await _statsService.UpsertAsync(reader.Id, sn,
                        snap.UserCount ?? 0, snap.FaceCount ?? 0,
                        snap.FingerprintCount ?? 0, 0,
                        snap.AttLogCount ?? 0, snap.FirmwareVersion);
                }

                if (!hasCounts && reader != null)
                    await EnsureDeviceInfoCommandAsync(reader.Id);
            }
            else if (IsAttLog(table, body) || IsRtLog(table))
            {
                var imported = await _attLogIngest.ImportAttLogAsync(serial, body, ct);
                outcome = $"ATTLOG: {imported} حركة";
                _logger.LogInformation("ATTLOG import SN={Serial} rows={Count}", serial, imported);
            }
            else if (IsRealtime(table))
            {
                outcome = $"Realtime ({table}) — ignored";
            }
            else
            {
                var bio = await _biometricIngest.ImportCombinedAsync(serial, table, body, ct);
                outcome = bio.UsersUpserted > 0 || bio.TemplatesSaved > 0
                    ? $"موظفون: {bio.UsersUpserted} · بصمات: {bio.TemplatesSaved}"
                    : body.Length == 0
                        ? "جسم الطلب فارغ"
                        : "لم يُتعرَّف على أي سجل";

                _logger.LogWarning(
                    "Biometric import SN={Serial} table={Table} users={Users} templates={Templates} preview={Preview}",
                    serial, table ?? "-", bio.UsersUpserted, bio.TemplatesSaved, Preview(body, 300));

                if (reader != null && (bio.UsersUpserted > 0 || bio.TemplatesSaved > 0))
                    await RefreshStatsFromDbAsync(reader.Id, sn);
            }

            // Some devices (incl. some SpeedFace firmwares) may not send `/iclock/devicecmd` ACK
            // for every DATA QUERY result. When we receive recognizable data tables, mark the
            // latest matching Sent command as acknowledged to prevent timeouts/retries.
            if (reader != null)
                await TryAcknowledgeLatestSentByTableAsync(reader.Id, table);

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

            // A named non-attendance table (OPERLOG, USERINFO, BIODATA, ...) is never attendance data.
            if (!string.IsNullOrWhiteSpace(table))
                return false;

            // Some firmware posts without table name — detect tab-separated attendance rows.
            // Exclude USERINFO / FINGERTMP key=value formats.
            if (string.IsNullOrWhiteSpace(body)) return false;
            var first = body.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (first == null || !first.Contains('\t')) return false;
            if (first.Contains("PIN=", StringComparison.OrdinalIgnoreCase)
                || first.Contains("TMP=", StringComparison.OrdinalIgnoreCase)
                || first.Contains("Name=", StringComparison.OrdinalIgnoreCase))
                return false;

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
                _syncState.UpdateFromInfo(serial.Trim(), info);
                _logger.LogInformation("ZKTeco INFO from {Serial}: {Info}", serial, info);

                var snap = _syncState.GetSnapshot(serial.Trim());
                if (snap != null && (snap.UserCount != null || snap.FingerprintCount != null || snap.FaceCount != null))
                {
                    await _statsService.UpsertAsync(reader.Id, serial.Trim(),
                        snap.UserCount ?? 0, snap.FaceCount ?? 0,
                        snap.FingerprintCount ?? 0, 0,
                        snap.AttLogCount ?? 0, snap.FirmwareVersion);
                }
            }

            return await DeliverPendingCommandsAsync(reader, serial, ct);
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
            var pending = (await _commands.GetPendingByReaderAsync(reader.Id, 500))
                .OrderBy(c => CommandPriority(c.CommandType))
                .ThenBy(c => c.createdOn)
                .ToList();

            _logger.LogInformation(
                "DeliverPending SN={Serial} readerId={ReaderId} pendingCount={Count} types=[{Types}]",
                serial, reader.Id, pending.Count,
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
                var line = FormatCommand(protocolId, cmd.CommandType, cmd.Payload);
                await _commands.MarkSentAsync(cmd.Id, protocolId);
                lines.Add(line);
                _logger.LogInformation(
                    "ZKTeco command sent SN={Serial} id={Id} type={Type} line={Line}",
                    serial, protocolId, cmd.CommandType, line);
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

            _logger.LogWarning("ZKTeco devicecmd SN={Serial} body={Body}", serial, body);

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
                DeviceCommandType.QueryAttLog =>
                    "DATA QUERY tablename=transaction,fielddesc=*",
                DeviceCommandType.QueryUserInfo => FormatQueryUserInfo(payload),
                DeviceCommandType.QueryFingerTmp => string.IsNullOrWhiteSpace(payload)
                    ? "DATA QUERY tablename=templatev10,fielddesc=*"
                    : $"DATA QUERY tablename=templatev10,fielddesc=*,filter=pin={payload.Trim()}",
                DeviceCommandType.QueryBioData => string.IsNullOrWhiteSpace(payload)
                    ? "DATA QUERY tablename=biodata,fielddesc=*,filter=Type=1"
                    : $"DATA QUERY tablename=biodata,fielddesc=*,filter=Type=1\tPin={payload.Trim()}",
                DeviceCommandType.QueryDeviceInfo =>
                    "GET OPTION FROM: UserCount,FPCount,FaceCount,~DeviceName,FirmVer",
                DeviceCommandType.UpdateUserInfo => FormatUserInfoUpdate(payload),
                DeviceCommandType.SyncAllData => "CHECK",
                DeviceCommandType.FullSync => "CHECK",
                DeviceCommandType.AddTemplate => string.IsNullOrWhiteSpace(payload) ? "DATA UPDATE USERINFO" : $"DATA {payload}",
                DeviceCommandType.RemoveTemplate => string.IsNullOrWhiteSpace(payload) ? "DATA DELETE USERINFO" : $"DATA {payload}",
                DeviceCommandType.AddPermission => string.IsNullOrWhiteSpace(payload) ? "DATA UPDATE USERINFO" : $"DATA {payload}",
                DeviceCommandType.RemovePermission => string.IsNullOrWhiteSpace(payload) ? "DATA DELETE USERINFO" : $"DATA {payload}",
                _ => payload ?? "INFO"
            };

            return $"C:{protocolCommandId}:{body}";
        }

        private static int CommandPriority(string commandType)
        {
            if (string.Equals(commandType, nameof(DeviceCommandType.UpdateUserInfo), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.QueryUserInfo), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.AddTemplate), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.AddPermission), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.RemoveTemplate), StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandType, nameof(DeviceCommandType.RemovePermission), StringComparison.OrdinalIgnoreCase))
                return 0;
            return 1;
        }

        private static bool IsUnsafeAccUserUpdate(string commandType, string? payload)
        {
            if (!string.Equals(commandType, nameof(DeviceCommandType.UpdateUserInfo), StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrWhiteSpace(payload))
                return true;
            var text = payload.Trim();
            if (!text.Contains("UPDATE user", StringComparison.OrdinalIgnoreCase))
                return false;
            return !text.Contains("uid=", StringComparison.OrdinalIgnoreCase);
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
            if (string.IsNullOrWhiteSpace(payload))
                return "DATA UPDATE USERINFO";
            var text = payload.Trim();
            if (text.StartsWith("DATA ", StringComparison.OrdinalIgnoreCase))
                return text;
            return $"DATA UPDATE USERINFO {text}";
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

        public async Task UpdateQueryDataCountAsync(string serial, string tableName, int count)
        {
            var sn = serial.Trim();
            var reader = await _readers.GetBySerialAsync(sn);
            if (reader == null) return;

            var tbl = tableName.ToLowerInvariant();
            var existing = await _statsService.GetByReaderAsync(reader.Id);

            var userCount = existing?.UserCount ?? 0;
            var faceCount = existing?.FaceCount ?? 0;
            var fpCount = existing?.FingerprintCount ?? 0;
            var attCount = existing?.AttLogCount ?? 0;
            var fw = existing?.FirmwareVersion;

            switch (tbl)
            {
                case "user": userCount = count; break;
                case "templatev10": fpCount = count; break;
                case "biodata": faceCount = count; break;
                case "transaction": attCount = count; break;
                case "options":
                    var snap = _syncState.GetSnapshot(sn);
                    fw = snap?.FirmwareVersion ?? fw;
                    return; // options don't carry a meaningful count
            }

            await _statsService.UpsertAsync(reader.Id, sn, userCount, faceCount, fpCount, 0, attCount, fw);
            _logger.LogInformation(
                "Stats from querydata SN={Serial} table={Table} count={Count}", sn, tableName, count);
        }

        private static bool IsRtLog(string? table)
            => string.Equals(table, "rtlog", StringComparison.OrdinalIgnoreCase);

        private static bool IsRealtime(string? table)
            => string.Equals(table, "rtstate", StringComparison.OrdinalIgnoreCase);

        private async Task RefreshStatsFromDbAsync(Guid readerId, string serial)
        {
            try
            {
                var snap = _syncState.GetSnapshot(serial);
                if (snap != null && (snap.UserCount != null || snap.FingerprintCount != null || snap.FaceCount != null))
                {
                    await _statsService.UpsertAsync(readerId, serial,
                        snap.UserCount ?? 0,
                        snap.FaceCount ?? 0,
                        snap.FingerprintCount ?? 0,
                        0,
                        snap.AttLogCount ?? 0,
                        snap.FirmwareVersion);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh stats from DB for {Serial}", serial);
            }
        }

        private async Task TryAcknowledgeLatestSentByTableAsync(Guid readerId, string? table)
        {
            if (readerId == Guid.Empty || string.IsNullOrWhiteSpace(table))
                return;

            var t = table.Trim().ToLowerInvariant();

            DeviceCommandType? expected = t switch
            {
                "user" or "userinfo" => DeviceCommandType.QueryUserInfo,
                "templatev10" or "fingertmp" => DeviceCommandType.QueryFingerTmp,
                "biodata" => DeviceCommandType.QueryBioData,
                "transaction" or "attlog" => DeviceCommandType.QueryAttLog,
                _ => null
            };

            if (expected == null) return;

            var recent = await _commands.GetByReaderAsync(readerId);
            var cmd = recent
                .Where(c =>
                    !c.isDeleted
                    && c.Status == DeviceCommandStatus.Sent
                    && string.Equals(c.CommandType, expected.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.SentOn ?? c.createdOn)
                .FirstOrDefault();

            if (cmd == null) return;

            await _commands.UpdateStatusAsync(cmd.Id, DeviceCommandStatus.Acknowledged);
            _logger.LogWarning(
                "Auto-ACK: Marked {CommandType} as Acknowledged due to received table={Table}",
                expected.Value, table);
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
