using Core.Models;

namespace Core.Interfaces.Services.Organization
{
    /// <summary>مسار تسجيل الموظف: تحقق الهوية من الجهاز وإحصاء قوائم الانتظار.</summary>
    public interface IEmployeeEnrollmentService
    {
        Task<DeviceCredentialStatus> CheckDeviceCredentialAsync(string fileNumber, CancellationToken ct = default);

        Task<EmployeeEnrollmentInfo?> GetEnrollmentInfoAsync(Guid employeeId, CancellationToken ct = default);

        Task<Dictionary<Guid, EmployeeEnrollmentInfo>> GetEnrollmentMapAsync(
            IEnumerable<Guid> employeeIds, CancellationToken ct = default);

        Task<List<EmployeeEnrollmentInfo>> GetQueueAsync(
            EmployeeEnrollmentPhase? phase = null, CancellationToken ct = default);

        static bool HasBasicRegistration(
            string? fullName,
            string? jobTitle,
            string? departmentName,
            string? address,
            string? nationality,
            string? bloodType)
            => !string.IsNullOrWhiteSpace(fullName)
               && (
                   !string.IsNullOrWhiteSpace(jobTitle)
                   || !string.IsNullOrWhiteSpace(departmentName)
                   || !string.IsNullOrWhiteSpace(address)
                   || !string.IsNullOrWhiteSpace(nationality)
                   || !string.IsNullOrWhiteSpace(bloodType)
               );
    }
}
