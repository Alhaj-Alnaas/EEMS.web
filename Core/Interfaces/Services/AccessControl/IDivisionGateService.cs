namespace Core.Interfaces.Services.AccessControl
{
    public interface IDivisionGateService
    {
        Task<List<Guid>> GetGateIdsAsync(string responsibilityCode, CancellationToken ct = default);
        Task ReplaceAsync(string responsibilityCode, IReadOnlyCollection<Guid> gateIds, CancellationToken ct = default);
    }
}
