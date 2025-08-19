using Core.Entities;

namespace Core.Entities
{
    public class GatePermitTypes : Base
    {
        public Guid gatesId { get; set; }
        public int permitTypesId { get; set; }
        public Gate Gate { get; set; }
        public PermitType PermitType { get; set; }
    }
}