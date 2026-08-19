using Core.Entities;
using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.Services.Organization;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Services.Organization
{
    public class EmployeeService : IEmployeeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmployeeDeviceSyncService _deviceSync;
        private readonly ILogger<EmployeeService> _logger;

        public EmployeeService(
            IUnitOfWork unitOfWork,
            IEmployeeDeviceSyncService deviceSync,
            ILogger<EmployeeService> logger)
        {
            _unitOfWork = unitOfWork;
            _deviceSync = deviceSync;
            _logger = logger;
        }

        public async Task<List<Employee>> GetAllAsync()
        {
            var stubs = await _unitOfWork.Employees.GetQueryable()
                .Where(e => !e.isDeleted && (e.FileNumber == null || e.FileNumber.Trim() == ""))
                .ToListAsync();
            if (stubs.Count > 0)
            {
                foreach (var stub in stubs)
                {
                    stub.isDeleted = true;
                    stub.deletedOn = DateTime.Now;
                    stub.remarks = string.IsNullOrWhiteSpace(stub.remarks)
                        ? "Removed blank FileNumber stub"
                        : stub.remarks + "; removed blank FileNumber stub";
                    _unitOfWork.Employees.Update(stub);
                }
                await _unitOfWork.SaveAsync();
            }

            return await _unitOfWork.Employees.GetQueryable()
                .Where(e => !e.isDeleted && e.FileNumber != null && e.FileNumber != "")
                .Include(e => e.Department)
                .OrderBy(e => e.FullName)
                .ToListAsync();
        }

        public async Task<Employee?> GetByIdAsync(Guid id)
        {
            return await _unitOfWork.Employees.GetByIdAsync(id, q => q.Include(e => e.Department));
        }

        public async Task<Employee?> GetByFileNumberAsync(string fileNumber)
        {
            return await _unitOfWork.Employees.GetQueryable()
                .FirstOrDefaultAsync(e => e.FileNumber == fileNumber);
        }

        public async Task<List<Employee>> GetByDepartmentAsync(Guid departmentId)
        {
            return await _unitOfWork.Employees.GetQueryable()
                .Where(e => e.DepartmentId == departmentId && !e.isDeleted)
                .ToListAsync();
        }

        public async Task<int> InsertAsync(Employee employee)
        {
            _unitOfWork.Employees.Insert(employee);
            await _unitOfWork.SaveAsync();
            return await TrySyncAsync(employee);
        }

        public async Task<int> UpdateAsync(Employee employee)
        {
            _unitOfWork.Employees.Update(employee);
            await _unitOfWork.SaveAsync();
            return await TrySyncAsync(employee);
        }

        public async Task DeleteAsync(Guid id)
        {
            var employee = await _unitOfWork.Employees.GetByIdAsync(id);
            if (employee == null) return;
            employee.isDeleted = true;
            employee.deletedOn = DateTime.Now;
            _unitOfWork.Employees.Update(employee);
            await _unitOfWork.SaveAsync();
            await TrySyncAsync(employee);
        }

        private async Task<int> TrySyncAsync(Employee employee)
        {
            try
            {
                return await _deviceSync.SyncEmployeeToReadersAsync(employee);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue device sync for employee {Id} PIN={Pin}",
                    employee.Id, employee.FileNumber);
                return 0;
            }
        }
    }
}
