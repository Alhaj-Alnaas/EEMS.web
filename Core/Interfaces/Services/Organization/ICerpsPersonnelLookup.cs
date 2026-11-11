using Core.Models;

namespace Core.Interfaces.Services.Organization
{
    public interface ICerpsPersonnelLookup
    {
        Task<HrEmployeeLookupResult> LookupAsync(string fileNumber, CancellationToken ct = default);
    }
}
