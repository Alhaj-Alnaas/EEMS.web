namespace Core.Entities
{
    /// <summary>Contractor worker. PermitNo is the worker number and the printed permit number.</summary>
    public class LaborWorker : Base
    {
        public Guid CompanyId { get; set; }
        public LaborCompany? Company { get; set; }

        public string PermitNo { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Nationality { get; set; }
        public string? PassportNo { get; set; }

        /// <summary><see cref="Core.Common.LaborWorkerState"/>.</summary>
        public string Status { get; set; } = "Active";

        public string? Address { get; set; }
        public string? Notes { get; set; }

        /// <summary>First successful print. A printed number is kept when the roster is renumbered.</summary>
        public DateTime? PrintedAt { get; set; }
    }
}
