using Core.Entities;

namespace Core.Interfaces.Services.AccessControl
{
    /// <summary>Phase 1: manage which gates/time-windows an employee may access.</summary>
    public interface IEmployeePermissionService
    {
        Task<List<EmployeePermission>> GetByEmployeeAsync(Guid employeeId);
        Task<List<EmployeePermission>> GetByGateAsync(Guid gateId);
        Task GrantAsync(EmployeePermission permission);
        Task RevokeAsync(Guid id);
        Task<bool> HasAccessAsync(Guid employeeId, Guid gateId, DateTime atTime);
    }
}
