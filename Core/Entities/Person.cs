using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class Person:Base
    {
        public string idNumber { get; set; }
        public string name { get; set; }
        public string orginaization { get; set; }
        public string nattiunality { get; set; }
    }
}
