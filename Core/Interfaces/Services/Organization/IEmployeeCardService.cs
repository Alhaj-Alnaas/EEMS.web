using Core.Entities;

namespace Core.Interfaces.Services.Organization
{
    public interface IEmployeeCardService
    {
        Task<EmployeeCard> RecordPrintAsync(Guid employeeId, string? printedBy, CancellationToken ct = default);
        Task<List<EmployeeCard>> GetByEmployeeAsync(Guid employeeId);
    }
}
