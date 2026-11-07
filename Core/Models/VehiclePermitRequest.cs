namespace Core.Models
{
    /// <summary>
    /// Internal vehicle permit: plate, stay window, responsible party, and how many exit/return trips are allowed.
    /// </summary>
    public class VehiclePermitRequest
    {
        public string PermitNumber { get; set; } = "";
        public bool IsTemp { get; set; }
        public DateTime PermitDate { get; set; } = DateTime.Today;
        public string? HourOfEntry { get; set; }
        public DateTime ValidTo { get; set; } = DateTime.Today;
        public string? ExpectedExecutionTime { get; set; }
        public bool ShiftApproval { get; set; }
        public string RequestingDepartmentCode { get; set; } = "";
        public string? ResponsibleParty { get; set; }
        public string? Phone { get; set; }
        public string? MoveFrom { get; set; }
        public string? MoveTo { get; set; }
        public string? Purpose { get; set; }
        public string? Notes { get; set; }
        public bool InternalUse { get; set; } = true;
        public int TripCount { get; set; } = 1;
        public List<CarMovmentViewModel> Vehicles { get; set; } = new();
    }
}
