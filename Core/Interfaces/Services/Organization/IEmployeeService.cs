using Core.Entities;

namespace Core.Interfaces.Services.Organization
{
    /// <summary>Phase 1: CRUD + lookup operations for employees.</summary>
    public interface IEmployeeService
    {
        Task<List<Employee>> GetAllAsync();
        Task<Employee?> GetByIdAsync(Guid id);
        Task<Employee?> GetByFileNumberAsync(string fileNumber);
        Task<List<Employee>> GetByDepartmentAsync(Guid departmentId);
        Task<int> InsertAsync(Employee employee);
        Task<int> UpdateAsync(Employee employee);
        Task DeleteAsync(Guid id);
    }
}
