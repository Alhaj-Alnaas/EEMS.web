using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Entities
{
    public class Base
    {

        public Guid Id { get; set; }//= Guid.NewGuid();
        public string? createdBy { get; set; } = ""; 
        public DateTime createdOn { get; set; } = DateTime.Now;
        public string? updatedBy { get; set; } = "";
        public DateTime? updatedOn { get; set; }= DateTime.Now;
        public Boolean isDeleted { get; set; }=false;
        public string? deletedBy { get; set; } = "";
        public DateTime? deletedOn { get; set; }
        public string? remarks { get; set; } = "";


    }
}
