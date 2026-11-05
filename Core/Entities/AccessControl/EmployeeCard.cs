using System;

namespace Core.Entities
{
    /// <summary>
    /// Printed employee access/ID card record (phase-2 of employee registration).
    /// </summary>
    public class EmployeeCard : Base
    {
        public Guid EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        /// <summary>Card serial / badge number printed on the physical card.</summary>
        public string CardNumber { get; set; } = "";

        /// <summary>Snapshot of photo path at print time.</summary>
        public string? PhotoPath { get; set; }

        public DateTime PrintedOn { get; set; } = DateTime.Now;
        public string? PrintedBy { get; set; }

        /// <summary>Active, Reprint, Cancelled</summary>
        public string Status { get; set; } = "Active";
    }
}
