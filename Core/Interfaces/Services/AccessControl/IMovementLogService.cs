using Core.Entities;

namespace Core.Interfaces.Services.AccessControl
{
    /// <summary>Unified movement/access log reads and manual entry recording.</summary>
    public interface IMovementLogService
    {
        Task<List<MovementLog>> GetRecentAsync(int take = 100);
        Task<List<MovementLog>> GetByEmployeeAsync(Guid employeeId);
        Task<List<MovementLog>> GetByPermitAsync(Guid permitId);
        Task<List<MovementLog>> GetByGateAsync(Guid gateId, DateTime? from = null, DateTime? to = null);
        Task RecordAsync(MovementLog log);
    }
}
