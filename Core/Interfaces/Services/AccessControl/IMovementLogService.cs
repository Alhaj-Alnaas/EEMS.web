using Core.Entities;

namespace Core.Interfaces.Services.AccessControl
{
    /// <summary>Unified movement/access log reads and manual entry recording.</summary>
    public interface IMovementLogService
    {
        Task<List<MovementLog>> GetRecentAsync(int take = 100);
        Task<List<MovementLog>> GetFilteredAsync(
            string? employeeNumber = null,
            Guid? departmentId = null,
            Guid? gateId = null,
            DateTime? from = null,
            DateTime? to = null,
            int take = 200);
        Task<List<MovementLog>> GetByEmployeeAsync(Guid employeeId);
        Task<List<MovementLog>> GetByPermitAsync(Guid permitId);
        Task<List<MovementLog>> GetByGateAsync(Guid gateId, DateTime? from = null, DateTime? to = null);
        Task RecordAsync(MovementLog log);
    }
}
