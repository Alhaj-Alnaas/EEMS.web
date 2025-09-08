using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities.DTOs
{
    public class EmployeeDto
    {
        public string EmployeeNumber { get; set; }
        public string FullName { get; set; }
        public string Department { get; set; }
        public string JobTitle { get; set; }
        public string empEmail { get; set; }
        public string jobStatus { get; set; }
    }
}
