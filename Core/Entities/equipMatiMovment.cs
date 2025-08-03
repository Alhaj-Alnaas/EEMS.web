using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class EquipMatiMovment:Base
    {
        public Guid permitId { get; set; }
        public Permit permit { get; set; }
        public string description { get; set; }
        public string qty { get; set; }
        public DateTime returnDate { get; set; }


    }
}
