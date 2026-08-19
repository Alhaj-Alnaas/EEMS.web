using Core.Entities;

namespace Core.Interfaces.Services.AccessControl
{
    /// <summary>Phase 1: enroll/approve biometric templates for employees.</summary>
    public interface IBiometricTemplateService
    {
        Task<List<BiometricTemplate>> GetByEmployeeAsync(Guid employeeId);
        Task<BiometricTemplate?> GetByIdAsync(Guid id);
        Task<List<BiometricTemplate>> GetRecentAsync(int take = 100);
        Task EnrollAsync(BiometricTemplate template);
        Task ApproveAsync(Guid id, string approvedBy);
        Task DeleteAsync(Guid id);
    }
}
