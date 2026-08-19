using System.Collections.Generic;

namespace Core.Entities
{
    public class Gate : Base
    {
        public string no { get; set; } = "";
        public string description { get; set; } = "";
        public bool isActive { get; set; }

        public ICollection<PermitType> PermitTypes { get; set; } = new List<PermitType>();
        public ICollection<Reader> Readers { get; set; } = new List<Reader>();
    }
}
