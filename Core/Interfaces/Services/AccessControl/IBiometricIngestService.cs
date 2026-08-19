using Core.Entities;
using Core.Interfaces.Services.AccessControl;

namespace Core.Interfaces.Services.AccessControl
{
    public sealed record BiometricIngestResult(int UsersUpserted, int TemplatesSaved, int TemplatesSkipped);

    /// <summary>Imports USERINFO / FINGERTMP payloads pushed by ZKTeco readers.</summary>
    public interface IBiometricIngestService
    {
        Task<int> ImportUserInfoAsync(string deviceSerial, string? table, string body, CancellationToken ct = default);
        Task<int> ImportFingerTmpAsync(string deviceSerial, string? table, string body, CancellationToken ct = default);
        Task<BiometricIngestResult> ImportCombinedAsync(string deviceSerial, string? table, string body, CancellationToken ct = default);
    }
}
