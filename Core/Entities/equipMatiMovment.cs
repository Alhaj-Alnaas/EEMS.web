using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class equipMatiMovment:Base
    {
        public string permitId { get; set; }
        public string description { get; set; }
        public string qty { get; set; }
        public DateTime returnDate { get; set; }


    }
}
