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
            int userCount, int faceCount, int fingerprintCount,
            int cardCount, int attLogCount, string? firmwareVersion)
        {
            var existing = await _unitOfWork.ReaderDeviceStats.GetQueryable()
                .FirstOrDefaultAsync(s => s.ReaderId == readerId && !s.isDeleted);

            if (existing != null)
            {
                existing.UserCount = userCount;
                existing.FaceCount = faceCount;
                existing.FingerprintCount = fingerprintCount;
                existing.CardCount = cardCount;
                existing.AttLogCount = attLogCount;
                existing.FirmwareVersion = firmwareVersion;
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
                    UserCount = userCount,
                    FaceCount = faceCount,
                    FingerprintCount = fingerprintCount,
                    CardCount = cardCount,
                    AttLogCount = attLogCount,
                    FirmwareVersion = firmwareVersion,
                    FetchedAt = DateTime.Now,
                    createdOn = DateTime.Now
                });
            }

            await _unitOfWork.SaveAsync();
            _logger.LogInformation(
                "Stats updated reader={ReaderId} users={Users} faces={Faces} fp={FP} cards={Cards} att={Att}",
                readerId, userCount, faceCount, fingerprintCount, cardCount, attLogCount);
        }

        public async Task EnqueueFullSyncAsync(Guid readerId)
        {
            var pending = await _commands.GetPendingByReaderAsync(readerId, 50);
            var types = pending.Select(c => c.CommandType).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var subCommands = new[]
            {
                DeviceCommandType.QueryDeviceInfo,
                DeviceCommandType.QueryAttLog,
                DeviceCommandType.QueryUserInfo,
                DeviceCommandType.QueryFingerTmp,
                DeviceCommandType.QueryBioData
            };

            var enqueued = 0;
            foreach (var cmd in subCommands)
            {
                if (types.Contains(cmd.ToString()))
                    continue;
                await _commands.EnqueueAsync(readerId, cmd);
                enqueued++;
            }

            if (!types.Contains(nameof(DeviceCommandType.Sync)))
                await _commands.EnqueueAsync(readerId, DeviceCommandType.Sync, "INFO");

            _logger.LogInformation("FullSync: enqueued {Count} sub-commands for reader {ReaderId}", enqueued, readerId);
        }

        public async Task EnqueueFullSyncAllAsync()
        {
            var readers = await _readers.GetAllAsync();
            foreach (var reader in readers)
            {
                await EnqueueFullSyncAsync(reader.Id);
            }
        }

        public async Task EnqueueCommandsAsync(Guid readerId, params DeviceCommandType[] types)
        {
            var pending = await _commands.GetPendingByReaderAsync(readerId, 50);
            var existingTypes = pending.Select(c => c.CommandType).ToHashSet(StringComparer.OrdinalIgnoreCase);

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

        public async Task EnqueueCommandsAllAsync(params DeviceCommandType[] types)
        {
            var readers = await _readers.GetAllAsync();
            _logger.LogWarning(
                "EnqueueCommandsAllAsync: totalReaders={Total} ids=[{Ids}] active=[{Active}] types=[{Types}]",
                readers.Count,
                string.Join(", ", readers.Select(r => $"{r.DeviceSerial}:{r.Id}:{(r.IsActive ? "Y" : "N")}")),
                readers.Count(r => r.IsActive),
                string.Join(", ", types));

            foreach (var reader in readers)
            {
                await EnqueueCommandsAsync(reader.Id, types);
            }
        }
    }
}
