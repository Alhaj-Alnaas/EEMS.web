using Core.Entities;
using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using static Core.Enums.BaseEnums;

namespace Services.AccessControl
{
    public class ReaderService : IReaderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IReaderDowntimeService _downtime;

        public ReaderService(IUnitOfWork unitOfWork, IReaderDowntimeService downtime)
        {
            _unitOfWork = unitOfWork;
            _downtime = downtime;
        }

        public async Task<List<Reader>> GetAllAsync()
        {
            return await _unitOfWork.Readers.GetQueryable()
                .Where(r => !r.isDeleted)
                .Include(r => r.Gate)
                .OrderBy(r => r.Name)
                .ThenBy(r => r.DeviceSerial)
                .ToListAsync();
        }

        public async Task<List<Reader>> GetByGateAsync(Guid gateId)
        {
            return await _unitOfWork.Readers.GetQueryable()
                .Where(r => r.GateId == gateId && !r.isDeleted)
                .ToListAsync();
        }

        public async Task<Reader?> GetByIdAsync(Guid id)
        {
            return await _unitOfWork.Readers.GetByIdAsync(id, q => q.Include(r => r.Gate));
        }

        public async Task<Reader?> GetBySerialAsync(string deviceSerial)
        {
            if (string.IsNullOrWhiteSpace(deviceSerial)) return null;
            var serial = deviceSerial.Trim();
            return await _unitOfWork.Readers.GetQueryable()
                .Include(r => r.Gate)
                .FirstOrDefaultAsync(r => r.DeviceSerial == serial && !r.isDeleted);
        }

        public async Task InsertAsync(Reader reader)
        {
            if (reader.Id == Guid.Empty)
                reader.Id = Guid.NewGuid();
            reader.DeviceSerial = reader.DeviceSerial.Trim();
            reader.Status = ReaderStatus.Offline;
            _unitOfWork.Readers.Insert(reader);
            await _unitOfWork.SaveAsync();
            // No downtime alert on first registration (never was Online).
        }

        public async Task UpdateAsync(Reader reader)
        {
            reader.DeviceSerial = reader.DeviceSerial.Trim();
            reader.updatedOn = DateTime.Now;
            _unitOfWork.Readers.Update(reader);
            await _unitOfWork.SaveAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var reader = await _unitOfWork.Readers.GetByIdAsync(id);
            if (reader == null) return;
            reader.isDeleted = true;
            reader.deletedOn = DateTime.Now;
            _unitOfWork.Readers.Update(reader);
            await _unitOfWork.SaveAsync();
        }

        public async Task ReportHeartbeatAsync(Guid readerId, ReaderStatus status = ReaderStatus.Online)
        {
            var reader = await _unitOfWork.Readers.GetByIdAsync(readerId, q => q.Include(r => r.Gate));
            if (reader == null) return;
            var previous = reader.Status;
            ApplyHeartbeat(reader, status);
            _unitOfWork.Readers.Update(reader);
            await _unitOfWork.SaveAsync();
            await _downtime.OnStatusChangedAsync(reader, previous, reader.Status);
        }

        public async Task ReportHeartbeatBySerialAsync(string deviceSerial, ReaderStatus status = ReaderStatus.Online)
        {
            var reader = await GetBySerialAsync(deviceSerial);
            if (reader == null) return;
            var previous = reader.Status;
            ApplyHeartbeat(reader, status);
            _unitOfWork.Readers.Update(reader);
            await _unitOfWork.SaveAsync();
            await _downtime.OnStatusChangedAsync(reader, previous, reader.Status);
        }

        public async Task SetStatusAsync(Guid readerId, ReaderStatus status, string? error = null)
        {
            var reader = await _unitOfWork.Readers.GetByIdAsync(readerId, q => q.Include(r => r.Gate));
            if (reader == null) return;
            var previous = reader.Status;
            reader.Status = status;
            reader.updatedOn = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(error))
                reader.remarks = error;
            if (status == ReaderStatus.Online)
                reader.ConsecutiveFailures = 0;
            _unitOfWork.Readers.Update(reader);
            await _unitOfWork.SaveAsync();
            await _downtime.OnStatusChangedAsync(reader, previous, status, error);
        }

        public async Task MarkSyncAsync(Guid readerId)
        {
            var reader = await _unitOfWork.Readers.GetByIdAsync(readerId, q => q.Include(r => r.Gate));
            if (reader == null) return;
            var previous = reader.Status;
            reader.LastSyncAt = DateTime.Now;
            reader.Status = ReaderStatus.Online;
            reader.LastHeartbeat = DateTime.Now;
            reader.ConsecutiveFailures = 0;
            _unitOfWork.Readers.Update(reader);
            await _unitOfWork.SaveAsync();
            await _downtime.OnStatusChangedAsync(reader, previous, ReaderStatus.Online, "تمت المزامنة");
        }

        private static void ApplyHeartbeat(Reader reader, ReaderStatus status)
        {
            reader.LastHeartbeat = DateTime.Now;
            reader.Status = status;
            if (status == ReaderStatus.Online)
                reader.ConsecutiveFailures = 0;
            reader.updatedOn = DateTime.Now;
        }
    }
}
