using Core.Entities;
using Core.Interfaces.Services.Organization;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Services.Organization
{
    public class DepartmentMasterService : IDepartmentMasterService
    {
        private readonly IUnitOfWork _unitOfWork;

        public DepartmentMasterService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<Department>> GetAllAsync()
        {
            return await _unitOfWork.Departments.GetQueryable()
                .Where(d => !d.isDeleted)
                .OrderBy(d => d.NameAr)
                .ToListAsync();
        }

        public async Task<Department?> GetByIdAsync(Guid id)
        {
            return await _unitOfWork.Departments.GetByIdAsync(id);
        }

        public async Task InsertAsync(Department department)
        {
            _unitOfWork.Departments.Insert(department);
            await _unitOfWork.SaveAsync();
        }

        public async Task UpdateAsync(Department department)
        {
            _unitOfWork.Departments.Update(department);
            await _unitOfWork.SaveAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var department = await _unitOfWork.Departments.GetByIdAsync(id);
            if (department == null) return;
            department.isDeleted = true;
            department.deletedOn = DateTime.Now;
            _unitOfWork.Departments.Update(department);
            await _unitOfWork.SaveAsync();
        }
    }
}
