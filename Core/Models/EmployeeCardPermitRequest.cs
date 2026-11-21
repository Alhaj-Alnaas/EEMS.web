using static Core.Enums.BaseEnums;

namespace Core.Models
{
    public class EmployeeCardPermitRequest
    {
        public string PermitNumber { get; set; } = "";
        public DateTime PermitDate { get; set; } = DateTime.Today;
        public string FileNumber { get; set; } = "";
        public EmployeeCardRequestKind Kind { get; set; } = EmployeeCardRequestKind.New;
        public string? Notes { get; set; }
    }
}
