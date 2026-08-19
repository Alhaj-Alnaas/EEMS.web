using System;

namespace Core.Entities
{
    /// <summary>
    /// Phase 1: grants an employee access through a specific gate within a
    /// validity window and optional daily time window.
    /// </summary>
    public class EmployeePermission : Base
    {
        public Guid EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public Guid GateId { get; set; }
        public Gate? Gate { get; set; }

        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }

        public TimeSpan? TimeWindowStart { get; set; }
        public TimeSpan? TimeWindowEnd { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
