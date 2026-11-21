namespace Core.Entities
{
    /// <summary>Contractor company stored in PMS. Separate from HR departments and biometric employees.</summary>
    public class LaborCompany : Base
    {
        public string Number { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Address { get; set; }
        public string? Specialty { get; set; }

        /// <summary><see cref="Core.Common.LaborCompanyState"/>.</summary>
        public string Status { get; set; } = "Active";

        public DateTime? ContractStart { get; set; }
        public DateTime? ContractEnd { get; set; }

        /// <summary>Two digits embedded in the 7-digit permit number. Assigned once and never reused while the company exists.</summary>
        public string PermitCode { get; set; } = "";

        public ICollection<LaborWorker> Workers { get; set; } = new List<LaborWorker>();
    }
}
