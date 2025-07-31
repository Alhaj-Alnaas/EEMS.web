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
        public string no { get; set; }
        public string classification { get; set; }
        public string type { get; set; }
        public bool reqDepApproval { get; set; }
        public bool secuDepApproval { get; set; }
        public int gateId { get; set; }
        public char status { get; set; }
        public string statusDescription { get; set; }
        public bool isClosed { get; set; }
        public string closeOn { get; set; }
        public string moveFrom { get; set; }
        public string moveTo { get; set; }
        public string requoidedAs { get; set; }
        public string phoneNo { get; set; }
        public DateTime date { get; set; }
        public string hourOfEntry { get; set; }
        public virtual ICollection<ProcedureMovment> Procedures { get; set; }
        public virtual ICollection<CarMovment> Cars { get; set; }
        public virtual ICollection<HumanMovment> Humans { get; set; }
        public virtual ICollection<EquipMatiMovment> EquipmentsAndMatirials { get; set; }
    }
       
}
