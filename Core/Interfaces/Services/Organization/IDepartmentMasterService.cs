using Core.Entities;

namespace Core.Interfaces.Services.Organization
{
    /// <summary>
    /// Phase 1 organizational department master CRUD. Distinct from the legacy
    /// <see cref="Core.Interfaces.Services.IDepartmentService"/> which reads
    /// departments from the external stored procedure.
    /// </summary>
    public interface IDepartmentMasterService
    {
        Task<List<Department>> GetAllAsync();
        Task<Department?> GetByIdAsync(Guid id);
        Task InsertAsync(Department department);
        Task UpdateAsync(Department department);
        Task DeleteAsync(Guid id);
    }
}
