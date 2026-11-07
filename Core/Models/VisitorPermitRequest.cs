using static Core.Enums.BaseEnums;

namespace Core.Models
{
    public class VisitorPermitRequest
    {
        public string PermitNumber { get; set; } = "";
        public bool IsTemp { get; set; }
        public DateTime PermitDate { get; set; } = DateTime.Today;
        public string? HourOfEntry { get; set; }
        public DateTime ValidTo { get; set; } = DateTime.Today;
        public string? ExpectedExecutionTime { get; set; }
        public bool ShiftApproval { get; set; }
        public string RequestingDepartmentCode { get; set; } = "";
        public string? Phone { get; set; }
        public string? MoveFrom { get; set; }
        public string? MoveTo { get; set; }
        public string? Notes { get; set; }

        public string? HostFileNumber { get; set; }
        public Guid? HostEmployeeId { get; set; }
        public string? HostName { get; set; }
        public string? HostDepartment { get; set; }
        public string? VisitorOrganization { get; set; }
        public string? ExpectedVisitTime { get; set; }
        public string? ExpectedDepartureTime { get; set; }
        public string? Purpose { get; set; }

        public List<VisitorLineViewModel> Visitors { get; set; } = new();
        public List<CarMovmentViewModel> Vehicles { get; set; } = new();
    }

    public class VisitorLineViewModel
    {
        public string Name { get; set; } = "";
        public string IdNumber { get; set; } = "";
        public Nationality Nationality { get; set; } = Nationality.ليبي;
    }
}
