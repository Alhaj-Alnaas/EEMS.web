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
                }
                await _unitOfWork.SaveAsync();
            }

            return await _unitOfWork.Employees.GetQueryable()
                .AsNoTracking()
                .Where(e => !e.isDeleted && e.FileNumber != null && e.FileNumber != "")
                .Include(e => e.Department)
                .OrderBy(e => e.FullName)
                .ToListAsync();
        }

        public async Task<Employee?> GetByIdAsync(Guid id)
        {
            return await _unitOfWork.Employees.GetQueryable()
                .AsNoTracking()
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == id && !e.isDeleted);
        }

        public async Task<Employee?> GetByFileNumberAsync(string fileNumber)
        {
            if (string.IsNullOrWhiteSpace(fileNumber))
                return null;

            var pin = fileNumber.Trim();
            return await _unitOfWork.Employees.GetQueryable()
                .AsNoTracking()
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => !e.isDeleted && e.FileNumber == pin);
        }

        public async Task<List<Employee>> GetByDepartmentAsync(Guid departmentId)
        {
            return await _unitOfWork.Employees.GetQueryable()
                .AsNoTracking()
                .Where(e => e.DepartmentId == departmentId && !e.isDeleted)
                .ToListAsync();
        }

        public async Task<int> InsertAsync(Employee employee, IReadOnlyList<Guid>? readerIds = null)
        {
            _unitOfWork.Employees.Insert(employee);
            await _unitOfWork.SaveAsync();

            if (readerIds is { Count: > 0 })
                return await TryEnrollAsync(employee, readerIds);

            return 0;
        }

        public async Task<int> UpdateAsync(Employee employee, IReadOnlyList<Guid>? enrollReaderIds = null)
        {
            var tracked = await _unitOfWork.Employees.GetQueryable()
                .FirstOrDefaultAsync(e => e.Id == employee.Id && !e.isDeleted)
                ?? throw new InvalidOperationException("الموظف غير موجود للتعديل.");

            ApplyRegistrationFields(tracked, employee);
            tracked.updatedOn = DateTime.Now;
            await _unitOfWork.SaveAsync();

            if (enrollReaderIds is { Count: > 0 })
                return await TryEnrollAsync(tracked, enrollReaderIds);

            return await TryStatusSyncAsync(tracked);
        }

        public async Task<(int Queued, bool WasUpdate)> UpsertByFileNumberAsync(
            Employee incoming,
            IReadOnlyList<Guid>? enrollReaderIds = null,
            bool clearPhoto = false)
        {
            var pin = incoming.FileNumber?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(pin))
                throw new InvalidOperationException("الرقم الوظيفي مطلوب.");

            incoming.FileNumber = pin;

            var tracked = await _unitOfWork.Employees.GetQueryable()
                .FirstOrDefaultAsync(e => !e.isDeleted && e.FileNumber == pin);

            if (tracked != null)
            {
                // تعديل موظف موجود  
                if (clearPhoto)
                    incoming.PhotoPath = null;
                else if (string.IsNullOrWhiteSpace(incoming.PhotoPath)
                    && !string.IsNullOrWhiteSpace(tracked.PhotoPath))
                {
                    incoming.PhotoPath = tracked.PhotoPath;
                }

                ApplyRegistrationFields(tracked, incoming);
                if (clearPhoto)
                    tracked.PhotoPath = null;
                tracked.updatedOn = DateTime.Now;
                await _unitOfWork.SaveAsync();

                incoming.Id = tracked.Id;
                incoming.PhotoPath = tracked.PhotoPath;
                incoming.remarks = tracked.remarks;

                var queued = enrollReaderIds is { Count: > 0 }
                    ? await TryEnrollAsync(tracked, enrollReaderIds)
                    : await TryStatusSyncAsync(tracked);

                return (queued, true);
            }

            if (incoming.Id == Guid.Empty)
                incoming.Id = Guid.NewGuid();
            incoming.createdOn = DateTime.Now;
            incoming.isDeleted = false;

            _unitOfWork.Employees.Insert(incoming);
            await _unitOfWork.SaveAsync();

            var insertedQueued = enrollReaderIds is { Count: > 0 }
                ? await TryEnrollAsync(incoming, enrollReaderIds)
                : 0;

            return (insertedQueued, false);
        }

        public async Task<int> UpdateActiveStatusAsync(Guid employeeId, bool isActive)
        {
            var tracked = await _unitOfWork.Employees.GetQueryable()
                .FirstOrDefaultAsync(e => e.Id == employeeId && !e.isDeleted)
                ?? throw new InvalidOperationException("الموظف غير موجود.");

            if (tracked.IsActive == isActive)
                return await TryStatusSyncAsync(tracked);

            tracked.IsActive = isActive;
            tracked.updatedOn = DateTime.Now;
            await _unitOfWork.SaveAsync();
            return await TryStatusSyncAsync(tracked);
        }

        public async Task DeleteAsync(Guid id)
        {
            var employee = await _unitOfWork.Employees.GetQueryable()
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return;

            var movements = await _unitOfWork.MovementLogs.GetQueryable()
                .CountAsync(m => m.EmployeeId == id && !m.isDeleted);
            if (movements > 0)
                throw new InvalidOperationException("لا يمكن حذف الموظف لأن له سجل حركة في النظام.");

            var permits = await _unitOfWork.Permits.GetQueryable()
                .CountAsync(p => !p.isDeleted && (p.RequesterEmployeeId == id || p.HostEmployeeId == id));
            if (permits > 0)
                throw new InvalidOperationException("لا يمكن حذف الموظف لارتباطه بتصاريح مسجلة.");

            employee.isDeleted = true;
            employee.deletedOn = DateTime.Now;
            await _unitOfWork.SaveAsync();
            await TryStatusSyncAsync(employee);
        }

        private static void ApplyRegistrationFields(Employee target, Employee source)
        {
            target.FileNumber = source.FileNumber?.Trim() ?? target.FileNumber;
            target.FullName = source.FullName ?? "";
            target.JobTitle = source.JobTitle;
            target.DepartmentName = source.DepartmentName;
            target.JobStatus = source.JobStatus;
            target.Phone = source.Phone;
            target.Email = source.Email;
            target.Address = source.Address;
            target.CardNumber = source.CardNumber;
            target.Nationality = source.Nationality;
            target.BloodType = source.BloodType;
            target.DepartmentId = source.DepartmentId;
            target.IsActive = source.IsActive;
            target.HostCapable = source.HostCapable;
            if (!string.IsNullOrWhiteSpace(source.PhotoPath))
                target.PhotoPath = source.PhotoPath;
        }

        private async Task<int> TryEnrollAsync(Employee employee, IReadOnlyList<Guid> readerIds)
        {
            return await _deviceSync.EnrollEmployeeOnReadersAsync(employee, readerIds);
        }

        private async Task<int> TryStatusSyncAsync(Employee employee)
        {
            try
            {
                return await _deviceSync.SyncEmployeeStatusToReadersAsync(employee);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync employee status {Id} PIN={Pin}",
                    employee.Id, employee.FileNumber);
                return 0;
            }
        }
    }
}
