using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class Gates:Base
    {
        public string no { get; set; }
        public string description  { get; set; }
        public string usedFor   { get; set; }

    }
}
