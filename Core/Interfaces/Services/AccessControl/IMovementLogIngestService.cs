namespace Core.Interfaces.Services.AccessControl
{
    public interface IMovementLogIngestService
    {
        /// <summary>Parse ATTLOG body from a reader Push upload and persist new movement rows.</summary>
        Task<int> ImportAttLogAsync(string deviceSerial, string body, CancellationToken ct = default);
    }
}
