using Core.Entities;
using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static Core.Enums.BaseEnums;

namespace Services.AccessControl
{
    public class ReaderDeviceStatsService : IReaderDeviceStatsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDeviceCommandService _commands;
        private readonly IReaderService _readers;
        private readonly ILogger<ReaderDeviceStatsService> _logger;

        public ReaderDeviceStatsService(
            IUnitOfWork unitOfWork,
            IDeviceCommandService commands,
            IReaderService readers,
            ILogger<ReaderDeviceStatsService> logger)
        {
            _unitOfWork = unitOfWork;
            _commands = commands;
            _readers = readers;
            _logger = logger;
        }

        public async Task<ReaderDeviceStats?> GetByReaderAsync(Guid readerId)
        {
            return await _unitOfWork.ReaderDeviceStats.GetQueryable()
                .Where(s => s.ReaderId == readerId && !s.isDeleted)
                .OrderByDescending(s => s.FetchedAt)
                .FirstOrDefaultAsync();
        }

        public async Task UpsertAsync(Guid readerId, string deviceSerial,
            int? userCount = null, int? faceCount = null, int? fingerprintCount = null,
            int? cardCount = null, int? attLogCount = null, string? firmwareVersion = null)
        {
            var existing = await _unitOfWork.ReaderDeviceStats.GetQueryable()
                .FirstOrDefaultAsync(s => s.ReaderId == readerId && !s.isDeleted);

            if (existing != null)
            {
                if (userCount.HasValue) existing.UserCount = userCount.Value;
                if (faceCount.HasValue) existing.FaceCount = faceCount.Value;
                if (fingerprintCount.HasValue) existing.FingerprintCount = fingerprintCount.Value;
                if (cardCount.HasValue) existing.CardCount = cardCount.Value;
                if (attLogCount.HasValue) existing.AttLogCount = attLogCount.Value;
                if (firmwareVersion != null) existing.FirmwareVersion = firmwareVersion;
                existing.FetchedAt = DateTime.Now;
                existing.updatedOn = DateTime.Now;
                _unitOfWork.ReaderDeviceStats.Update(existing);
            }
            else
            {
                _unitOfWork.ReaderDeviceStats.Insert(new ReaderDeviceStats
                {
                    Id = Guid.NewGuid(),
                    ReaderId = readerId,
                    DeviceSerial = deviceSerial,
                    UserCount = userCount ?? 0,
                    FaceCount = faceCount ?? 0,
                    FingerprintCount = fingerprintCount ?? 0,
                    CardCount = cardCount ?? 0,
                    AttLogCount = attLogCount ?? 0,
                    FirmwareVersion = firmwareVersion,
                    FetchedAt = DateTime.Now,
                    createdOn = DateTime.Now
                });
            }

            await _unitOfWork.SaveAsync();
            var row = existing ?? await _unitOfWork.ReaderDeviceStats.GetQueryable()
                .FirstAsync(s => s.ReaderId == readerId && !s.isDeleted);
            _logger.LogInformation(
                "Stats updated reader={ReaderId} users={Users} faces={Faces} fp={FP} cards={Cards} att={Att}",
                readerId, row.UserCount, row.FaceCount, row.FingerprintCount, row.CardCount, row.AttLogCount);
        }

        public async Task EnqueueFullSyncAsync(Guid readerId)
        {
            // INFO first — SpeedFace returns FaceCount + PvCount (palm) in INFO response body.
            await EnqueueIfMissingAsync(readerId, DeviceCommandType.Sync, "INFO");
            await EnqueueIfMissingAsync(readerId, DeviceCommandType.QueryDeviceInfo);
            await EnqueueIfMissingAsync(readerId, DeviceCommandType.QueryAttLog);
            await EnqueueIfMissingAsync(readerId, DeviceCommandType.QueryUserInfo);
        }

        private async Task EnqueueIfMissingAsync(
            Guid readerId, DeviceCommandType type, string? payload = null)
        {
            var pending = await _commands.GetPendingByReaderAsync(readerId, 50);
            var typeName = type.ToString();
            if (pending.Any(c =>
                    c.CommandType == typeName
                    && string.Equals(c.Payload ?? "", payload ?? "", StringComparison.Ordinal)))
                return;

            await _commands.EnqueueAsync(readerId, type, payload);
        }

        public async Task EnqueueFullSyncAllAsync()
        {
            var readers = await _readers.GetAllAsync();
            foreach (var reader in readers)
            {
                await EnqueueFullSyncAsync(reader.Id);
            }
        }

        public async Task EnqueueCommandsAsync(Guid readerId, bool force = false, params DeviceCommandType[] types)
        {
            HashSet<string> existingTypes;
            if (force)
            {
                existingTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                var pending = await _commands.GetPendingByReaderAsync(readerId, 50);
                existingTypes = pending.Select(c => c.CommandType).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            var enqueued = new List<string>();
            var skipped = new List<string>();
            foreach (var type in types)
            {
                if (existingTypes.Contains(type.ToString()))
                {
                    skipped.Add(type.ToString());
                    continue;
                }
                await _commands.EnqueueAsync(readerId, type);
                enqueued.Add(type.ToString());
            }

            // Verify queue state right after enqueue (helps debugging when device still shows pendingCount=0).
            var pendingAfter = await _commands.GetPendingByReaderAsync(readerId, 100);
            var pendingAfterTypes = pendingAfter.Select(c => c.CommandType).ToList();

            _logger.LogWarning(
                "EnqueueCommands readerId={ReaderId} enqueued=[{Enqueued}] skipped=[{Skipped}] pendingAfterCount={PendingCount} pendingAfterTypes=[{PendingTypes}]",
                readerId,
                string.Join(", ", enqueued),
                string.Join(", ", skipped),
                pendingAfter.Count,
                string.Join(", ", pendingAfterTypes));
        }

        public async Task EnqueueCommandsAllAsync(bool force = false, params DeviceCommandType[] types)
        {
            var readers = await _readers.GetAllAsync();
            _logger.LogWarning(
                "EnqueueCommandsAllAsync: force={Force} totalReaders={Total} ids=[{Ids}] types=[{Types}]",
                force,
                readers.Count,
                string.Join(", ", readers.Select(r => $"{r.DeviceSerial}:{r.Id}")),
                string.Join(", ", types));

            foreach (var reader in readers)
            {
                await EnqueueCommandsAsync(reader.Id, force, types);
            }
        }
    }
}
