using Core.Entities;
using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Services.AccessControl
{
    public class MovementLogService : IMovementLogService
    {
        private readonly IUnitOfWork _unitOfWork;

        public MovementLogService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<MovementLog>> GetRecentAsync(int take = 100)
        {
            return await _unitOfWork.MovementLogs.GetQueryable()
                .Include(m => m.Employee)
                .Include(m => m.Gate)
                .Include(m => m.Reader)
                .Where(m => !m.isDeleted)
                .OrderByDescending(m => m.EventTime)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<MovementLog>> GetByEmployeeAsync(Guid employeeId)
        {
            return await _unitOfWork.MovementLogs.GetQueryable()
                .Where(m => m.EmployeeId == employeeId)
                .OrderByDescending(m => m.EventTime)
                .ToListAsync();
        }

        public async Task<List<MovementLog>> GetByPermitAsync(Guid permitId)
        {
            return await _unitOfWork.MovementLogs.GetQueryable()
                .Where(m => m.PermitId == permitId)
                .OrderByDescending(m => m.EventTime)
                .ToListAsync();
        }

        public async Task<List<MovementLog>> GetByGateAsync(Guid gateId, DateTime? from = null, DateTime? to = null)
        {
            var query = _unitOfWork.MovementLogs.GetQueryable().Where(m => m.GateId == gateId);
            if (from.HasValue) query = query.Where(m => m.EventTime >= from.Value);
            if (to.HasValue) query = query.Where(m => m.EventTime <= to.Value);
            return await query.OrderByDescending(m => m.EventTime).ToListAsync();
        }

        public async Task RecordAsync(MovementLog log)
        {
            _unitOfWork.MovementLogs.Insert(log);
            await _unitOfWork.SaveAsync();
        }
    }
}
