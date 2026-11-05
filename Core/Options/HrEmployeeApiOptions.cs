namespace Core.Options
{
    /// <summary>External HR employee lookup API (same source used by legacy UserManagement).</summary>
    public class HrEmployeeApiOptions
    {
        public const string SectionName = "HrEmployeeApi";

        /// <summary>Base URL without trailing slash, e.g. http://10.10.102.7:8080/api/Employee</summary>
        public string BaseUrl { get; set; } = "http://10.10.102.7:8080/api/Employee";

        /// <summary>HTTP timeout in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// Optional sparse-fieldset query appended to GET /Employee/{empNo}
        /// e.g. Address,Nationality,BloodType — ignored by APIs that do not support it.
        /// </summary>
        public string? ExtraFields { get; set; } = "Address,Nationality,BloodType";
    }
}
