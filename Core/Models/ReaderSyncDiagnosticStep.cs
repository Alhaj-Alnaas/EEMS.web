namespace Core.Models
{
    /// <summary>One step in the employee sync pipeline diagnostic.</summary>
    public sealed record ReaderSyncDiagnosticStep(
        int Step,
        string Title,
        string Status,
        string Detail);
}
