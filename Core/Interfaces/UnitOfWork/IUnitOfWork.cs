using Core.Entities;
using Core.Entities.DTOs;
using Core.Interfaces.Repositories;
using Core.Interfaces.Repositories;
using System.Threading.Tasks;

namespace Core.Interfaces.UnitOfWork
{
    public interface IUnitOfWork
    {
        // Legacy Phase 2 repositories
        IGenericRepository<Gate> Gates { get; }
        IGenericRepository<PermitType> PermitTypes { get; }
        IGenericRepository<Permit> Permits { get; }
        IGenericRepository<ProcedureMovment> ProceduresMovment { get; }

        // Phase 1: Access Control repositories
        IGenericRepository<Department> Departments { get; }
        IGenericRepository<Employee> Employees { get; }
        IGenericRepository<Reader> Readers { get; }
        IGenericRepository<ReaderDowntime> ReaderDowntimes { get; }
        IGenericRepository<MovementLog> MovementLogs { get; }
        IGenericRepository<DeviceCommand> DeviceCommands { get; }
        IGenericRepository<EmployeePermission> EmployeePermissions { get; }
        IGenericRepository<BiometricTemplate> BiometricTemplates { get; }
        IGenericRepository<ReaderDeviceStats> ReaderDeviceStats { get; }

        // Phase 2: Workflow repositories
        IGenericRepository<WorkflowDefinition> WorkflowDefinitions { get; }
        IGenericRepository<WorkflowStep> WorkflowSteps { get; }
        IGenericRepository<PermitApproval> PermitApprovals { get; }
        IGenericRepository<Notification> Notifications { get; }

        IStoredProcedureRepository StoredProcedures { get; }

        Task SaveAsync();
    }
}
