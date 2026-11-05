namespace Core.Entities.Security
{
    /// <summary>Configurable login/access level that grants a set of system tasks.</summary>
    public class AccessLevel : Base
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>When true, login requires selecting a gate (gate officer behaviour).</summary>
        public bool IsGateOfficer { get; set; }

        /// <summary>Receives device health / reader alerts.</summary>
        public bool ReceivesDeviceAlerts { get; set; }

        public ICollection<AccessLevelTask> AccessLevelTasks { get; set; } = new List<AccessLevelTask>();
    }
}
