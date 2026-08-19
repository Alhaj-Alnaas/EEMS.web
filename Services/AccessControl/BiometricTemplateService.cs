using Core.Entities;
using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Services.AccessControl
{
    public class BiometricTemplateService : IBiometricTemplateService
    {
        private readonly IUnitOfWork _unitOfWork;

        public BiometricTemplateService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<BiometricTemplate>> GetByEmployeeAsync(Guid employeeId)
        {
            return await _unitOfWork.BiometricTemplates.GetQueryable()
                .Where(b => b.EmployeeId == employeeId)
                .ToListAsync();
        }

        public async Task<BiometricTemplate?> GetByIdAsync(Guid id)
        {
            return await _unitOfWork.BiometricTemplates.GetByIdAsync(id);
        }

        public async Task<List<BiometricTemplate>> GetRecentAsync(int take = 100)
        {
            return await _unitOfWork.BiometricTemplates.GetQueryable()
                .Include(b => b.Employee)
                .Where(b => !b.isDeleted)
                .OrderByDescending(b => b.createdOn)
                .Take(take)
                .ToListAsync();
        }

        public async Task EnrollAsync(BiometricTemplate template)
        {
            _unitOfWork.BiometricTemplates.Insert(template);
            await _unitOfWork.SaveAsync();
        }

        public async Task ApproveAsync(Guid id, string approvedBy)
        {
            var template = await _unitOfWork.BiometricTemplates.GetByIdAsync(id);
            if (template == null) return;
            template.IsApproved = true;
            template.ApprovedBy = approvedBy;
            template.ApprovedOn = DateTime.Now;
            _unitOfWork.BiometricTemplates.Update(template);
            await _unitOfWork.SaveAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var template = await _unitOfWork.BiometricTemplates.GetByIdAsync(id);
            if (template == null) return;
            _unitOfWork.BiometricTemplates.Delete(template);
            await _unitOfWork.SaveAsync();
        }
    }
}
