namespace Core.Models
{
    /// <summary>Normalized HR employee payload returned by <c>IHrEmployeeLookupService</c>.</summary>
    public class HrEmployeeLookupResult
    {
        public bool Success { get; set; }
        public bool AlreadyRegistered { get; set; }
        public string? Error { get; set; }

        public string? FileNumber { get; set; }
        public string? FullName { get; set; }
        public string? DepartmentName { get; set; }
        /// <summary>Parent administration name from personnel affairs.</summary>
        public string? AdministrationName { get; set; }
        public string? JobTitle { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? JobStatus { get; set; }
        /// <summary>Arabic job-status description from personnel affairs.</summary>
        public string? JobStatusName { get; set; }
        public string? ResponsibilityCode { get; set; }
        public int? RespCodeId { get; set; }
        public int? JobCatId { get; set; }
        public int? DesignationId { get; set; } = 0;

        /// <summary>Residential / home address when provided by HR.</summary>
        public string? Address { get; set; }
        public string? City { get; set; }
        /// <summary>Nationality when provided by HR.</summary>
        public string? Nationality { get; set; }

        /// <summary>Blood type / group when provided by HR (e.g. O+).</summary>
        public string? BloodType { get; set; }
    }
}
