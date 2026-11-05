using System;

namespace Core.Entities
{
    /// <summary>
    /// Gates where a permit classification may be executed when its path does not include the guard section.
    /// </summary>
    public class PermitClassificationGate
    {
        public Guid Id { get; set; }
        public string Classification { get; set; } = "";
        public Guid GateId { get; set; }
        public Gate? Gate { get; set; }
    }
}
