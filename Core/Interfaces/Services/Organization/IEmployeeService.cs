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

        /// <summary>Insert employee; optionally enroll on selected readers (app → device).</summary>
        Task<int> InsertAsync(Employee employee, IReadOnlyList<Guid>? readerIds = null);

        /// <summary>Update employee and push status/name to known readers. Optional enroll on extra readers.</summary>
        Task<int> UpdateAsync(Employee employee, IReadOnlyList<Guid>? enrollReaderIds = null);

        /// <summary>
        /// Insert or update by FileNumber without EF tracking conflicts.
        /// Returns (queuedCommands, wasUpdate).
        /// </summary>
        Task<(int Queued, bool WasUpdate)> UpsertByFileNumberAsync(
            Employee incoming,
            IReadOnlyList<Guid>? enrollReaderIds = null,
            bool clearPhoto = false);

        /// <summary>Update IsActive only and push disable/enable to enrolled readers.</summary>
        Task<int> UpdateActiveStatusAsync(Guid employeeId, bool isActive);

        Task DeleteAsync(Guid id);
    }
}
