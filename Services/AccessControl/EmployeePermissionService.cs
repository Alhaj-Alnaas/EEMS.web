using Core.Entities;
using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Services.AccessControl
{
    public class EmployeePermissionService : IEmployeePermissionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public EmployeePermissionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<EmployeePermission>> GetByEmployeeAsync(Guid employeeId)
        {
            return await _unitOfWork.EmployeePermissions.GetQueryable()
                .Where(p => p.EmployeeId == employeeId)
                .ToListAsync();
        }

        public async Task<List<EmployeePermission>> GetByGateAsync(Guid gateId)
        {
            return await _unitOfWork.EmployeePermissions.GetQueryable()
                .Where(p => p.GateId == gateId)
                .ToListAsync();
        }

        public async Task GrantAsync(EmployeePermission permission)
        {
            _unitOfWork.EmployeePermissions.Insert(permission);
            await _unitOfWork.SaveAsync();
        }

        public async Task RevokeAsync(Guid id)
        {
            var permission = await _unitOfWork.EmployeePermissions.GetByIdAsync(id);
            if (permission == null) return;
            permission.IsActive = false;
            _unitOfWork.EmployeePermissions.Update(permission);
            await _unitOfWork.SaveAsync();
        }

        public async Task<bool> HasAccessAsync(Guid employeeId, Guid gateId, DateTime atTime)
        {
            var timeOfDay = atTime.TimeOfDay;
            return await _unitOfWork.EmployeePermissions.GetQueryable().AnyAsync(p =>
                p.EmployeeId == employeeId &&
                p.GateId == gateId &&
                p.IsActive &&
                (p.ValidFrom == null || p.ValidFrom <= atTime) &&
                (p.ValidTo == null || p.ValidTo >= atTime) &&
                (p.TimeWindowStart == null || p.TimeWindowStart <= timeOfDay) &&
                (p.TimeWindowEnd == null || p.TimeWindowEnd >= timeOfDay));
        }
    }
}
