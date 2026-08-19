using System;
using static Core.Enums.BaseEnums;

namespace Core.Entities
{
    /// <summary>
    /// Unified movement/access event log for both Phase 1 (biometric employee
    /// access) and Phase 2 (permit based gate movements).
    /// At least one of <see cref="EmployeeId"/> or <see cref="PermitId"/> should be set.
    /// </summary>
    public class MovementLog : Base
    {
        public Guid? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public Guid? PermitId { get; set; }
        public Permit? Permit { get; set; }

        public Guid GateId { get; set; }
        public Gate? Gate { get; set; }

        public Guid? ReaderId { get; set; }
        public Reader? Reader { get; set; }

        /// <summary>ZKTeco device PIN / user id when employee is not mapped yet.</summary>
        public string? DevicePin { get; set; }

        public MovementDirection Direction { get; set; } = MovementDirection.In;
        public DateTime EventTime { get; set; } = DateTime.Now;

        public MovementSource Source { get; set; } = MovementSource.Biometric;

        /// <summary>Reason supplied when the movement was recorded manually.</summary>
        public string? ManualReason { get; set; }

        public string? RecordedBy { get; set; }
    }
}
