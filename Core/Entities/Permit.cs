using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class Permit : Base
    {
        public string? reqDepartment { get; set; } = "";
        public string no { get; set; } = "";
        public string? classification { get; set; } = "";
        public string? type { get; set; } = "";
        public bool reqDepApproval { get; set; }=false;
        public bool secuSectionApproval { get; set; } = false;
        public bool PermintsSectionApproval { get; set; } = false;
        public int gateId { get; set; }
        public char status { get; set; } = 'I';
        public string? statusDescription { get; set; } = "";
        public bool isClosed { get; set; }=false;
        public DateTime? closeOn { get; set; } = DateTime.Now;
        public string? moveFrom { get; set; } = "";
        public string? moveTo { get; set; } = "";
        public string? requoidedAs { get; set; } = "";
        public string? phoneNo { get; set; } = "";
        public string? OrgDescription { get; set; } = "";
        public bool IsTemp { get; set; } = false;
        public DateTime date { get; set; } = DateTime.Now;
        public string? hourOfEntry { get; set; } = "00:00"; 
        public ICollection<EquipMatiMovment> EquipmentsAndMatirials { get; set; } = new List<EquipMatiMovment>();
        public ICollection<CarMovment> Cars { get; set; } = new List<CarMovment>();
        public ICollection<HumanMovment> Humans { get; set; } = new List<HumanMovment>();
        public ICollection<ProcedureMovment> Procedures { get; set; } = new List<ProcedureMovment>();
    }
       
}
