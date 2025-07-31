using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class ProcedureMovment:Base
    {
       public Guid permitId  { get; set;  }
        public Permit permit { get; set; }
        public string donedBy { get; set; }
       public DateTime doneOn { get; set; }
       public string doneAs { get; set; }
       public string shift { get; set; }
       public char procedureType { get; set; } 
    }
}
