namespace Core.Entities
{
    public class LaborImportLine : Base
    {
        public Guid BatchId { get; set; }
        public LaborImportBatch? Batch { get; set; }

        /// <summary><see cref="Core.Common.LaborLineAction"/>.</summary>
        public string Action { get; set; } = "";

        public Guid? WorkerId { get; set; }
        public string? CurrentPermitNo { get; set; }
        public string? ProposedPermitNo { get; set; }
        public string FullName { get; set; } = "";
        public string? Nationality { get; set; }
        public string? PassportNo { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public string? CurrentStatus { get; set; }
        public string? ProposedStatus { get; set; }
        public int SortOrder { get; set; }
    }
}
