using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class humenMovment:Person
    {

        public string permitId { get; set; }
        public string orgToVisit { get; set; }
        public string personToVist { get; set; }


        public string purposeOfVisit { get; set; }
        public string phoneNo { get; set; }

    }
}
