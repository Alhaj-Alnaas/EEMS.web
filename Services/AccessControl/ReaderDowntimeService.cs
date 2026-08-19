using Core.Entities;
using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.Services.Workflow;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static Core.Enums.BaseEnums;

namespace Services.AccessControl
{
    public class ReaderDowntimeService : IReaderDowntimeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;
        private readonly ILogger<ReaderDowntimeService> _logger;

        public ReaderDowntimeService(
            IUnitOfWork unitOfWork,
            INotificationService notifications,
            ILogger<ReaderDowntimeService> logger)
        {
            _unitOfWork = unitOfWork;
            _notifications = notifications;
            _logger = logger;
        }

        public async Task OnStatusChangedAsync(
            Reader reader,
            ReaderStatus previousStatus,
            ReaderStatus newStatus,
            string? reason = null)
        {
            if (previousStatus == newStatus) return;

            var goingDown = newStatus is ReaderStatus.Offline or ReaderStatus.Faulty
                            && previousStatus == ReaderStatus.Online;

            var stayingDown = newStatus is ReaderStatus.Offline or ReaderStatus.Faulty
                              && previousStatus is ReaderStatus.Offline or ReaderStatus.Faulty;

            var recovering = newStatus == ReaderStatus.Online
                             && previousStatus is ReaderStatus.Offline or ReaderStatus.Faulty;

            if (goingDown)
            {
                await OpenDowntimeAsync(reader, newStatus, reason);
            }
            else if (stayingDown)
            {
                var open = await GetOpenAsync(reader.Id);
                if (open != null)
                {
                    open.StatusDuringOutage = newStatus;
                    if (!string.IsNullOrWhiteSpace(reason))
                        open.Reason = reason;
                    open.updatedOn = DateTime.Now;
                    _unitOfWork.ReaderDowntimes.Update(open);
                    await _unitOfWork.SaveAsync();
                }
                else
                {
                    await OpenDowntimeAsync(reader, newStatus, reason);
                }
            }
            else if (recovering)
            {
                await CloseDowntimeAsync(reader);
            }
        }

        public async Task<ReaderDowntime?> GetOpenAsync(Guid readerId)
        {
            return await _unitOfWork.ReaderDowntimes.GetQueryable()
                .Where(d => d.ReaderId == readerId && d.EndedAt == null && !d.isDeleted)
                .OrderByDescending(d => d.StartedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<ReaderDowntime>> GetRecentAsync(int take = 50)
        {
            return await _unitOfWork.ReaderDowntimes.GetQueryable()
                .Include(d => d.Reader)
                .ThenInclude(r => r!.Gate)
                .Where(d => !d.isDeleted)
                .OrderByDescending(d => d.StartedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<ReaderDowntime>> GetByReaderAsync(Guid readerId, int take = 50)
        {
            return await _unitOfWork.ReaderDowntimes.GetQueryable()
                .Where(d => d.ReaderId == readerId && !d.isDeleted)
                .OrderByDescending(d => d.StartedAt)
                .Take(take)
                .ToListAsync();
        }

        private async Task OpenDowntimeAsync(Reader reader, ReaderStatus status, string? reason)
        {
            var existing = await GetOpenAsync(reader.Id);
            if (existing != null) return;

            var startedAt = reader.LastHeartbeat ?? DateTime.Now;
            var downtime = new ReaderDowntime
            {
                Id = Guid.NewGuid(),
                ReaderId = reader.Id,
                StartedAt = startedAt,
                StatusDuringOutage = status,
                Reason = reason ?? (status == ReaderStatus.Faulty ? "جهاز معطل" : "انقطاع الاتصال"),
                createdOn = DateTime.Now,
                AlertSent = false
            };

            _unitOfWork.ReaderDowntimes.Insert(downtime);
            await _unitOfWork.SaveAsync();

            var label = DisplayName(reader);
            var fromText = FormatStamp(startedAt);
            var title = status == ReaderStatus.Faulty
                ? $"قارئة معطّلة: {label}"
                : $"توقف قارئة: {label}";
            var body =
                $"توقفت القارئة «{label}» (SN: {reader.DeviceSerial}) عن العمل.\n" +
                $"من: {fromText}\n" +
                $"إلى: مستمر حتى الآن\n" +
                $"السبب: {downtime.Reason}\n" +
                $"البوابة: {reader.Gate?.no ?? "-"}";

            try
            {
                await _notifications.NotifyDeviceAdminsAsync(title, body, reader.Id);
                downtime.AlertSent = true;
                _unitOfWork.ReaderDowntimes.Update(downtime);
                await _unitOfWork.SaveAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send downtime alert for reader {ReaderId}", reader.Id);
            }
        }

        private async Task CloseDowntimeAsync(Reader reader)
        {
            var open = await GetOpenAsync(reader.Id);
            if (open == null) return;

            var endedAt = DateTime.Now;
            open.EndedAt = endedAt;
            open.updatedOn = endedAt;
            _unitOfWork.ReaderDowntimes.Update(open);
            await _unitOfWork.SaveAsync();

            var label = DisplayName(reader);
            var from = open.StartedAt;
            var duration = FormatDuration(endedAt - from);
            var title = $"عادت القارئة للاتصال: {label}";
            var body =
                $"عادت القارئة «{label}» (SN: {reader.DeviceSerial}) للاتصال.\n" +
                $"من: {FormatStamp(from)}\n" +
                $"إلى: {FormatStamp(endedAt)}\n" +
                $"مدة التوقف: {duration}";

            try
            {
                await _notifications.NotifyDeviceAdminsAsync(title, body, reader.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send recovery alert for reader {ReaderId}", reader.Id);
            }
        }

        private static string DisplayName(Reader reader) =>
            string.IsNullOrWhiteSpace(reader.Name) ? reader.DeviceSerial : reader.Name;

        private static string FormatStamp(DateTime dt) => dt.ToString("yyyy/MM/dd HH:mm");

        private static string FormatDuration(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            if (span.TotalDays >= 1)
                return $"{(int)span.TotalDays} يوم و {span.Hours} ساعة و {span.Minutes} دقيقة";
            if (span.TotalHours >= 1)
                return $"{(int)span.TotalHours} ساعة و {span.Minutes} دقيقة";
            return $"{Math.Max(1, (int)span.TotalMinutes)} دقيقة";
        }
    }
}
