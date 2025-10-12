
using Core.Entities;
using Core.Entities.DTOs;
using static Core.Enums.BaseEnums;

namespace EEMS.web.ViewModels
{
    public class PermitViewModel
    {
        public Guid Id { get; set; }
        public string No { get; set; }
        public string Classification { get; set; }
        public EnumPermitType Type { get; set; }
        public string OrgDescription { get; set; }
        public bool IsTemp { get; set; }
        public DateTime? ReturnDate { get; set; }
        public string MoveFrom { get; set; }
        public string MoveTo { get; set; }
        public string Notes { get; set; }
        public string RequoidedAs { get; set; }
        public string PhoneNo { get; set; }
        public DateTime Date { get; set; }
        public string? HourOfEntry { get; set; }
        public char status { get; set; } = 'I';
        // الحركات المرتبطة
        public List<EquipMatiMovmentViewModel> EquipmentsAndMaterials { get; set; } = new();
        public List<CarMovmentViewModel> Cars { get; set; } = new();
        public List<ProcedureMovmentViewModel> Procedures { get; set; } = new ();
        public List<DepartmentDto> Departments { get; set; } = new();
        public List<Gate> Gates { get; set; } = new();
        public string ReqDepartment { get; set; }
        public string EntryGate { get; set; }

    }

    public class CarMovmentViewModel
    {
        public string CarType { get; set; }
        public string CarNo { get; set; }
        public string DriverName { get; set; }
        public string DriverOrg { get; set; }
        public Nationality driverNationality { get; set; }
        public string licenseNo { get; set; }
    }

    public class EquipMatiMovmentViewModel
    {
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public UnitType Unit { get; set; }
        public DateTime? ReturnDate { get; set; }
    }

    public class ProcedureMovmentViewModel
    {
        public string? DonedBy { get; set; }
        public DateTime? DoneOn { get; set; }
        public string? DoneAs { get; set; }
        public string? Remarks { get; set; }
        public string? ProcedureType { get; set; }
    }
}
