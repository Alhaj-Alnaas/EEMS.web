namespace Core.Entities.Security
{
    /// <summary>Catalog entry for an application screen/feature. Seeded by the system; not user-editable.</summary>
    public class SystemTask : Base
    {
        public string Code { get; set; } = "";
        public string NameAr { get; set; } = "";
        public string Category { get; set; } = "";
        public string? Route { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<AccessLevelTask> AccessLevelTasks { get; set; } = new List<AccessLevelTask>();
    }
}
