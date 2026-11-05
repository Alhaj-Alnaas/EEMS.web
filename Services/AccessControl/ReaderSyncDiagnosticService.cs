using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.UnitOfWork;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using static Core.Enums.BaseEnums;

namespace Services.AccessControl
{
    public class ReaderSyncDiagnosticService : IReaderSyncDiagnosticService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDeviceCommandService _commands;
        private readonly IReaderService _readers;
        private readonly IReaderDeviceStatsService _stats;
        private readonly IDevicePushLogService _pushLog;
        private readonly IZkTecoPushService _push;

        public ReaderSyncDiagnosticService(
            IUnitOfWork unitOfWork,
            IDeviceCommandService commands,
            IReaderService readers,
            IReaderDeviceStatsService stats,
            IDevicePushLogService pushLog,
            IZkTecoPushService push)
        {
            _unitOfWork = unitOfWork;
            _commands = commands;
            _readers = readers;
            _stats = stats;
            _pushLog = pushLog;
            _push = push;
        }

        public async Task<IReadOnlyList<ReaderSyncDiagnosticStep>> DiagnoseEmployeeSyncAsync(CancellationToken ct = default)
        {
            var steps = new List<ReaderSyncDiagnosticStep>();
            var allReaders = await _readers.GetAllAsync();
            if (allReaders.Count == 0)
            {
                steps.Add(new(0, "القارئات", "fail", "لا توجد قارئات مسجّلة في النظام."));
                return steps;
            }

            foreach (var reader in allReaders)
            {
                var prefix = allReaders.Count > 1 ? $"[{reader.DeviceSerial}] " : "";

                // Step 1: command queue
                var latest = (await _commands.GetByReaderAsync(reader.Id))
                    .Where(c => c.CommandType == nameof(DeviceCommandType.QueryUserInfo))
                    .OrderByDescending(c => c.createdOn)
                    .FirstOrDefault();

                var pending = await _commands.GetPendingByReaderAsync(reader.Id, 20);
                var pendingUser = pending.Count(c => c.CommandType == nameof(DeviceCommandType.QueryUserInfo));

                if (latest == null)
                {
                    steps.Add(new(1, $"{prefix}1. إرسال الأمر", "fail",
                        "لم يُضف أمر QueryUserInfo بعد. اضغط «مزامنة من القارئات»."));
                }
                else
                {
                    var cmdLine = latest.ProtocolCommandId > 0
                        ? _push.FormatCommand(latest.ProtocolCommandId, latest.CommandType, latest.Payload)
                        : _push.FormatCommand(1, latest.CommandType, latest.Payload);

                    steps.Add(new(1, $"{prefix}1. إرسال الأمر", StatusForCommand(latest.Status),
                        $"آخر أمر: {latest.Status} · {latest.createdOn:yyyy-MM-dd HH:mm:ss}\n" +
                        $"الصيغة: {cmdLine}\n" +
                        $"معلق الآن: {pendingUser}"));
                }

                // Step 2: device response / ACK
                if (latest == null)
                {
                    steps.Add(new(2, $"{prefix}2. استجابة القارئة", "pending", "بانتظار إضافة الأمر."));
                }
                else if (latest.Status == DeviceCommandStatus.Pending)
                {
                    steps.Add(new(2, $"{prefix}2. استجابة القارئة", "pending",
                        "الأمر في الطابور — بانتظار أن تستعلم القارئة (/iclock/getrequest)."));
                }
                else if (latest.Status == DeviceCommandStatus.Sent)
                {
                    steps.Add(new(2, $"{prefix}2. استجابة القارئة", "pending",
                        $"أُرسل protocolId={latest.ProtocolCommandId} في {latest.SentOn:HH:mm:ss} — بانتظار ACK أو وصول البيانات."));
                }
                else if (latest.Status == DeviceCommandStatus.Failed)
                {
                    steps.Add(new(2, $"{prefix}2. استجابة القارئة", "fail",
                        $"فشل: {latest.LastError ?? "غير معروف"}"));
                }
                else
                {
                    steps.Add(new(2, $"{prefix}2. استجابة القارئة", "ok",
                        $"تم تأكيد الأمر (Acknowledged) في {latest.CompletedOn:yyyy-MM-dd HH:mm:ss}"));
                }

                // Step 3: data payload from device
                var pushes = _pushLog.GetRecent(50)
                    .Where(p => string.Equals(p.DeviceSerial, reader.DeviceSerial, StringComparison.OrdinalIgnoreCase))
                    .Where(p => IsUserTable(p.Table))
                    .Take(3)
                    .ToList();

                var deviceStats = await _stats.GetByReaderAsync(reader.Id);
                var deviceUserCount = deviceStats?.UserCount ?? 0;

                if (pushes.Count == 0)
                {
                    steps.Add(new(3, $"{prefix}3. بيانات القارئة", "fail",
                        $"لم تصل أي push لجدول user/userinfo في هذه الجلسة.\n" +
                        $"عدد المستخدمين على القارئة (إحصائيات): {deviceUserCount}"));
                }
                else
                {
                    var last = pushes[0];
                    var status = last.ByteCount > 0 && last.LineCount > 0 ? "ok" : "warn";
                    steps.Add(new(3, $"{prefix}3. بيانات القارئة", status,
                        $"آخر push: {last.ReceivedAt:HH:mm:ss} · table={last.Table} · bytes={last.ByteCount} · lines={last.LineCount}\n" +
                        $"النتيجة: {last.Outcome}\n" +
                        $"معاينة: {last.Preview}\n" +
                        $"عدد المستخدمين على القارئة: {deviceUserCount}"));
                }

                // Step 4: DB storage
                var empCount = await _unitOfWork.Employees.GetQueryable()
                    .CountAsync(e => !e.isDeleted, ct);
                var fromReader = await _unitOfWork.Employees.GetQueryable()
                    .CountAsync(e => !e.isDeleted
                        && e.remarks != null
                        && e.remarks.Contains(reader.DeviceSerial), ct);

                var lastUserPush = pushes.FirstOrDefault();
                var storageDetail =
                    $"إجمالي الموظفين في DB: {empCount}\n" +
                    $"مستوردون من {reader.DeviceSerial}: {fromReader}";

                if (lastUserPush != null)
                {
                    storageDetail += $"\nآخر استيراد: {lastUserPush.Outcome}";
                    if (lastUserPush.Outcome.Contains("موظفون: 0", StringComparison.Ordinal)
                        || lastUserPush.ByteCount > 0 && lastUserPush.Outcome.Contains("جسم الطلب فارغ", StringComparison.Ordinal))
                    {
                        storageDetail += "\nملاحظة: إن وصلت بيانات بلا اسم سابقاً قد تكون تُجاهلت — أعد المزامنة بعد التحديث.";
                    }
                }

                steps.Add(new(4, $"{prefix}4. تخزين الموظفين",
                    fromReader > 0 || empCount > 0 ? "ok" : "warn",
                    storageDetail));
            }

            return steps;
        }

        private static bool IsUserTable(string table)
        {
            var t = table.Trim().ToLowerInvariant();
            return t is "user" or "userinfo" or "tabledata";
        }

        private static string StatusForCommand(DeviceCommandStatus status) => status switch
        {
            DeviceCommandStatus.Pending => "pending",
            DeviceCommandStatus.Sent => "pending",
            DeviceCommandStatus.Acknowledged => "ok",
            DeviceCommandStatus.Failed => "fail",
            _ => "warn"
        };
    }
}
