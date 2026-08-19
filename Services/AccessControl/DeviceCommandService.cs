using Core.Entities;
using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using static Core.Enums.BaseEnums;

namespace Services.AccessControl
{
    public class DeviceCommandService : IDeviceCommandService
    {
        private readonly IUnitOfWork _unitOfWork;
        private static int _protocolSeq;

        public DeviceCommandService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<DeviceCommand>> GetPendingAsync(int take = 50)
        {
            var now = DateTime.Now;
            return await _unitOfWork.DeviceCommands.GetQueryable()
                .Include(c => c.Reader)
                .Where(c => !c.isDeleted
                    && c.Status == DeviceCommandStatus.Pending
                    && (c.ScheduledOn == null || c.ScheduledOn <= now))
                .OrderBy(c => c.createdOn)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<DeviceCommand>> GetByReaderAsync(Guid readerId)
        {
            return await _unitOfWork.DeviceCommands.GetQueryable()
                .Where(c => c.ReaderId == readerId && !c.isDeleted)
                .OrderByDescending(c => c.createdOn)
                .Take(100)
                .ToListAsync();
        }

        public async Task<List<DeviceCommand>> GetPendingByReaderAsync(Guid readerId, int take = 50)
        {
            var now = DateTime.Now;
            return await _unitOfWork.DeviceCommands.GetQueryable()
                .Where(c => !c.isDeleted
                    && c.ReaderId == readerId
                    && c.Status == DeviceCommandStatus.Pending
                    && (c.ScheduledOn == null || c.ScheduledOn <= now))
                .OrderBy(c => c.createdOn)
                .Take(Math.Max(1, take))
                .ToListAsync();
        }

        public async Task<DeviceCommand?> GetByProtocolIdAsync(Guid readerId, int protocolCommandId)
        {
            return await _unitOfWork.DeviceCommands.GetQueryable()
                .FirstOrDefaultAsync(c =>
                    c.ReaderId == readerId
                    && c.ProtocolCommandId == protocolCommandId
                    && !c.isDeleted);
        }

        public async Task<DeviceCommand> EnqueueAsync(
            Guid readerId,
            DeviceCommandType type,
            string? payload = null,
            DateTime? scheduledOn = null)
        {
            var command = new DeviceCommand
            {
                Id = Guid.NewGuid(),
                ReaderId = readerId,
                CommandType = type.ToString(),
                Payload = payload,
                Status = DeviceCommandStatus.Pending,
                ScheduledOn = scheduledOn,
                createdOn = DateTime.Now
            };
            _unitOfWork.DeviceCommands.Insert(command);
            await _unitOfWork.SaveAsync();
            return command;
        }

        public async Task UpdateStatusAsync(Guid id, DeviceCommandStatus status, string? error = null)
        {
            var command = await _unitOfWork.DeviceCommands.GetByIdAsync(id);
            if (command == null) return;
            command.Status = status;
            command.LastError = error;
            if (status is DeviceCommandStatus.Acknowledged or DeviceCommandStatus.Failed)
                command.CompletedOn = DateTime.Now;
            if (status == DeviceCommandStatus.Failed)
                command.RetryCount++;
            command.updatedOn = DateTime.Now;
            _unitOfWork.DeviceCommands.Update(command);
            await _unitOfWork.SaveAsync();
        }

        public async Task MarkSentAsync(Guid id, int protocolCommandId)
        {
            var command = await _unitOfWork.DeviceCommands.GetByIdAsync(id);
            if (command == null) return;
            command.Status = DeviceCommandStatus.Sent;
            command.ProtocolCommandId = protocolCommandId;
            command.SentOn = DateTime.Now;
            command.updatedOn = DateTime.Now;
            _unitOfWork.DeviceCommands.Update(command);
            await _unitOfWork.SaveAsync();
        }

        public async Task<List<DeviceCommand>> GetTimedOutSentAsync(TimeSpan timeout)
        {
            var cutoff = DateTime.Now - timeout;
            return await _unitOfWork.DeviceCommands.GetQueryable()
                .Include(c => c.Reader)
                .Where(c => !c.isDeleted
                    && c.Status == DeviceCommandStatus.Sent
                    && c.SentOn != null
                    && c.SentOn < cutoff)
                .ToListAsync();
        }

        public async Task ScheduleRetryAsync(Guid id, TimeSpan delay, string? error = null)
        {
            var command = await _unitOfWork.DeviceCommands.GetByIdAsync(id);
            if (command == null) return;
            command.Status = DeviceCommandStatus.Pending;
            command.RetryCount++;
            command.LastError = error;
            command.ScheduledOn = DateTime.Now.Add(delay);
            command.SentOn = null;
            command.updatedOn = DateTime.Now;
            _unitOfWork.DeviceCommands.Update(command);
            await _unitOfWork.SaveAsync();
        }

        public async Task<int> CancelStaleAsync(TimeSpan maxAge)
        {
            var cutoff = DateTime.Now - maxAge;
            var stale = await _unitOfWork.DeviceCommands.GetQueryable()
                .Where(c => !c.isDeleted
                    && (c.Status == DeviceCommandStatus.Pending || c.Status == DeviceCommandStatus.Sent)
                    && c.createdOn < cutoff)
                .ToListAsync();

            foreach (var cmd in stale)
            {
                cmd.Status = DeviceCommandStatus.Failed;
                cmd.LastError = "Cancelled: stale command";
                cmd.CompletedOn = DateTime.Now;
                cmd.updatedOn = DateTime.Now;
                _unitOfWork.DeviceCommands.Update(cmd);
            }

            if (stale.Count > 0)
                await _unitOfWork.SaveAsync();

            return stale.Count;
        }

        /// <summary>Allocates a positive protocol command id for Push C:{id}: lines.</summary>
        public static int NextProtocolCommandId()
        {
            var id = Interlocked.Increment(ref _protocolSeq);
            if (id <= 0)
            {
                Interlocked.Exchange(ref _protocolSeq, 1);
                id = 1;
            }
            return id;
        }
    }
}
