using Core.Models;

namespace Core.Interfaces.Services.Organization
{
    /// <summary>Looks up employee master data from the external HR API by file number.</summary>
    public interface IHrEmployeeLookupService
    {
        Task<HrEmployeeLookupResult> LookupAsync(string fileNumber, CancellationToken ct = default);
    }
}
