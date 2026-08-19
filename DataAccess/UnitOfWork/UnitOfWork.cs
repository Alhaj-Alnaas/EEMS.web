using Core.Entities;
using Core.Interfaces.Repositories;
using DataAccess.Repositories;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork, IDisposable, IAsyncDisposable
    {
        private readonly DataContext _context;
        private readonly bool _ownsContext;

        private IGenericRepository<Gate> _gates;
        private IGenericRepository<PermitType> _permitTypes;
        private IGenericRepository<Permit> _permits;
        private IGenericRepository<ProcedureMovment> _proceduresMovment;
        private IStoredProcedureRepository _storedProcedures;

        private IGenericRepository<Department> _departments;
        private IGenericRepository<Employee> _employees;
        private IGenericRepository<Reader> _readers;
        private IGenericRepository<ReaderDowntime> _readerDowntimes;
        private IGenericRepository<MovementLog> _movementLogs;
        private IGenericRepository<DeviceCommand> _deviceCommands;
        private IGenericRepository<EmployeePermission> _employeePermissions;
        private IGenericRepository<BiometricTemplate> _biometricTemplates;
        private IGenericRepository<ReaderDeviceStats> _readerDeviceStats;

        private IGenericRepository<WorkflowDefinition> _workflowDefinitions;
        private IGenericRepository<WorkflowStep> _workflowSteps;
        private IGenericRepository<PermitApproval> _permitApprovals;
        private IGenericRepository<Notification> _notifications;

        /// <summary>
        /// Blazor-safe: each UnitOfWork owns a dedicated DbContext from the factory
        /// so concurrent layout/page queries do not share one context instance.
        /// </summary>
        public UnitOfWork(IDbContextFactory<DataContext> contextFactory)
        {
            _context = contextFactory.CreateDbContext();
            _ownsContext = true;
        }

        public IGenericRepository<Gate> Gates => _gates ??= new GenericRepository<Gate>(_context);
        public IGenericRepository<PermitType> PermitTypes => _permitTypes ??= new GenericRepository<PermitType>(_context);
        public IGenericRepository<Permit> Permits => _permits ??= new GenericRepository<Permit>(_context);
        public IGenericRepository<ProcedureMovment> ProceduresMovment => _proceduresMovment ??= new GenericRepository<ProcedureMovment>(_context);

        public IGenericRepository<Department> Departments => _departments ??= new GenericRepository<Department>(_context);
        public IGenericRepository<Employee> Employees => _employees ??= new GenericRepository<Employee>(_context);
        public IGenericRepository<Reader> Readers => _readers ??= new GenericRepository<Reader>(_context);
        public IGenericRepository<ReaderDowntime> ReaderDowntimes => _readerDowntimes ??= new GenericRepository<ReaderDowntime>(_context);
        public IGenericRepository<MovementLog> MovementLogs => _movementLogs ??= new GenericRepository<MovementLog>(_context);
        public IGenericRepository<DeviceCommand> DeviceCommands => _deviceCommands ??= new GenericRepository<DeviceCommand>(_context);
        public IGenericRepository<EmployeePermission> EmployeePermissions => _employeePermissions ??= new GenericRepository<EmployeePermission>(_context);
        public IGenericRepository<BiometricTemplate> BiometricTemplates => _biometricTemplates ??= new GenericRepository<BiometricTemplate>(_context);
        public IGenericRepository<ReaderDeviceStats> ReaderDeviceStats => _readerDeviceStats ??= new GenericRepository<ReaderDeviceStats>(_context);

        public IGenericRepository<WorkflowDefinition> WorkflowDefinitions => _workflowDefinitions ??= new GenericRepository<WorkflowDefinition>(_context);
        public IGenericRepository<WorkflowStep> WorkflowSteps => _workflowSteps ??= new GenericRepository<WorkflowStep>(_context);
        public IGenericRepository<PermitApproval> PermitApprovals => _permitApprovals ??= new GenericRepository<PermitApproval>(_context);
        public IGenericRepository<Notification> Notifications => _notifications ??= new GenericRepository<Notification>(_context);

        public IStoredProcedureRepository StoredProcedures
            => _storedProcedures ??= new StoredProcedureRepository(_context);

        public async Task SaveAsync() => await _context.SaveChangesAsync();

        public void Dispose()
        {
            if (_ownsContext)
                _context.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_ownsContext)
                await _context.DisposeAsync();
        }
    }
}
