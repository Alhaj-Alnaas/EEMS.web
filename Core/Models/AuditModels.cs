namespace Core.Models
{
    public class AuditQuery
    {
        public string? Area { get; set; }
        public string? Action { get; set; }
        public string? FileNumber { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public class AuditRow
    {
        public DateTime OccurredAt { get; set; }
        public string Area { get; set; } = "";
        public string AreaLabel { get; set; } = "";
        public string Action { get; set; } = "";
        public string ActionLabel { get; set; } = "";
        public string Summary { get; set; } = "";
        public string FileNumber { get; set; } = "";
        public string ActorName { get; set; } = "";
        public string GateName { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string NetworkAddress { get; set; } = "";
        public string MacAddress { get; set; } = "";
    }
}
