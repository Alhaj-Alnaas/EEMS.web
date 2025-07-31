using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class CarMovment:Person
    {
        public Guid permitId { get; set; }
        public Permit permit { get; set; }
        public string carType { get; set; }
        public string carNo { get; set; }
        public int driverId { get; set; }
        public string driverOrg { get; set; }
        public string driverNationality { get; set; }
        public string licenseNo { get; set; }
    }
}
