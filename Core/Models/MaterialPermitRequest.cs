using static Core.Enums.BaseEnums;

namespace Core.Models
{
    /// <summary>
    /// Header + line items for a materials/equipment permit.
    /// Line items stay on the legacy movement tables; validity, purpose and
    /// workflow status follow the Phase 2 analysis model.
    /// </summary>
    public class MaterialPermitRequest
    {
        public string PermitNumber { get; set; } = "";
        public EnumPermitType Type { get; set; } = EnumPermitType.Export;
        public bool IsTemp { get; set; }
        public DateTime PermitDate { get; set; } = DateTime.Today;
        public string? HourOfEntry { get; set; }
        public DateTime ValidTo { get; set; } = DateTime.Today;
        public string? ExpectedExecutionTime { get; set; }
        public bool ShiftApproval { get; set; }
        public string RequestingDepartmentCode { get; set; } = "";
        public string? AuthorizedParty { get; set; }
        public string? Phone { get; set; }
        public string? MoveFrom { get; set; }
        public string? MoveTo { get; set; }
        public string? Purpose { get; set; }
        public string? Notes { get; set; }
        public List<EquipMatiMovmentViewModel> Items { get; set; } = new();
        public List<CarMovmentViewModel> Cars { get; set; } = new();
    }
}
