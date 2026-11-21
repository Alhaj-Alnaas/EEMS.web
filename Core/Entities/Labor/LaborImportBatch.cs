namespace Core.Entities
{
    /// <summary>Staged Excel add/match. Nothing is written to workers until the batch is applied.</summary>
    public class LaborImportBatch : Base
    {
        public Guid CompanyId { get; set; }
        public LaborCompany? Company { get; set; }

        /// <summary><see cref="Core.Common.LaborBatchMode"/>.</summary>
        public string Mode { get; set; } = "";

        /// <summary><see cref="Core.Common.LaborBatchState"/>.</summary>
        public string State { get; set; } = "Draft";

        public string? FileName { get; set; }
        public DateTime? AppliedOn { get; set; }

        public ICollection<LaborImportLine> Lines { get; set; } = new List<LaborImportLine>();
    }
}
