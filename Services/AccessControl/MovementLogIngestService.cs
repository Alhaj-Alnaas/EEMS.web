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

            var saved = 0;
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();

                var exists = await _unitOfWork.MovementLogs.GetQueryable()
                    .AnyAsync(m =>
                        m.ReaderId == reader.Id
                        && m.DevicePin == entry.Pin
                        && m.EventTime == entry.EventTime
                        && !m.isDeleted, ct);
                if (exists) continue;

                var employee = await _unitOfWork.Employees.GetQueryable()
                    .FirstOrDefaultAsync(e => e.FileNumber == entry.Pin && !e.isDeleted, ct);

                var log = new MovementLog
                {
                    Id = Guid.NewGuid(),
                    ReaderId = reader.Id,
                    GateId = reader.GateId,
                    EmployeeId = employee?.Id,
                    DevicePin = entry.Pin,
                    EventTime = entry.EventTime,
                    Direction = MapDirection(entry.Status),
                    Source = MovementSource.Biometric,
                    RecordedBy = $"ZKTeco/{reader.DeviceSerial}",
                    remarks = entry.RawLine,
                    createdOn = DateTime.Now
                };

                _unitOfWork.MovementLogs.Insert(log);
                saved++;
            }

            if (saved > 0)
            {
                await _unitOfWork.SaveAsync();
                _logger.LogInformation(
                    "Imported {Count} ATTLOG rows from reader {Serial}", saved, deviceSerial);
            }

            return saved;
        }

        private static MovementDirection MapDirection(int status) =>
            status switch
            {
                1 => MovementDirection.Out,
                _ => MovementDirection.In
            };
    }
}
