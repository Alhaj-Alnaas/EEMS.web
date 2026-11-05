using Core.Entities;

namespace Core.Models
{
    public class PermitExecutionRow
    {
        public Permit Permit { get; set; } = null!;
        public bool PassedGuard { get; set; }
        public string ExecutionScope { get; set; } = "";
        public string NextStep { get; set; } = "";
    }
}
