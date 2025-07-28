using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class Base
    {

        public Guid Id { get; set; }    
        public string createdBy { get; set; }  
        public DateTime createdOn { get; set; }
        public string updatedBy { get; set; }
        public DateTime updatedOn { get; set; }
        public Boolean isDeleted { get; set; }
        public string deletedBy { get; set; }
        public DateTime deletedOn { get; set; }




    }
}
