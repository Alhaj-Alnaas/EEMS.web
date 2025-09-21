using Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EEMS.web.ViewModels
{
    public class PermitViewModel
    {
        public string No { get; set; }
        public string ReqDepartment { get; set; }
        public string Classification { get; set; }
        public string Type { get; set; }
        public bool ReqDepApproval { get; set; }
        public bool SecuDepApproval { get; set; }
        public int GateId { get; set; }
        public List<SelectListItem> Gates { get; set; } = new();
        public char Status { get; set; }
        public string StatusDescription { get; set; }
        public bool IsClosed { get; set; }
        public bool IsTemp { get; set; }
        public DateTime ReturnDate { get; set; }
        public string CloseOn { get; set; }
        public string MoveFrom { get; set; }
        public string MoveTo { get; set; }
        public string RequoidedAs { get; set; }
        public string PhoneNo { get; set; }
        public DateTime Date { get; set; }
        public string HourOfEntry { get; set; }

        // الحركات المرتبطة
        public ICollection<CarMovment> Cars { get; set; } = new List<CarMovment>();
        public ICollection<HumanMovment> Humans { get; set; } = new List<HumanMovment>();
        public ICollection<EquipMatiMovment> EquipmentsAndMatirials { get; set; } = new List<EquipMatiMovment>();
    }

    public class CarMovmentViewModel
    {
        public string CarType { get; set; }
        public string CarNo { get; set; }
        public string DriverId { get; set; }
        public string DriverName { get; set; }
        public string DriverOrg { get; set; }
        public string driverNationality { get; set; }
        public string licenseNo { get; set; }
    }

    //public class HumanMovmentViewModel
    //{
    //    public string FullName { get; set; }
    //    public string IdNumber { get; set; }
    //}

    public class EquipMatiMovmentViewModel
    {
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public DateTime returnDate { get; set; }
    }
}
