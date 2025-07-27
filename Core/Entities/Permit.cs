using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class Permit
    {
        public string no { get; set; }
        public string classification { get; set; }
        public string type { get; set; }
        public string reqDepApprovaltype { get; set; }
        public string secuDepApproval { get; set; }
        public int gateId { get; set; }
        public string status { get; set; }
        public bool isClosed { get; set; }
        public string closeOn { get; set; }
        public string moveFrom { get; set; }
        public string moveTo { get; set; }
        public string requoidedAs { get; set; }
        public string phoneNo { get; set; }
        public DateTime date { get; set; }
        public string hourOfEntry { get; set; }
        public string hourOfEixt { get; set; }
        public equipMatiMovment equipsMatiMovment { get; set; }
        public humenMovment humenMovment { get; set; }



    }
}
