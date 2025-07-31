using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class GateUsedFor: Base
    {
        public Guid gateid { get; set; }
        public Gate gate { get; set; }  
        public string usingFor { get; set; }

    }
}
