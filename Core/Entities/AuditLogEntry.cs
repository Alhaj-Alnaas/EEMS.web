namespace Core.Entities
{
    /// <summary>
    /// One sensitive action. SourceKey keeps a historical reconstruction from
    /// overlapping the same event once it is written here directly.
    /// </summary>
    public class AuditLogEntry : Base
    {
        public string SourceKey { get; set; } = "";
        public string Area { get; set; } = "";
        public string Action { get; set; } = "";
        public string Summary { get; set; } = "";
        public string? ActorFileNumber { get; set; }
        public string? ActorName { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.Now;
        public string? GateName { get; set; }
        public string? DeviceName { get; set; }
        public string? NetworkAddress { get; set; }
        public string? MacAddress { get; set; }
    }
}
