using System;
using System.Collections.Generic;

namespace Core.Entities
{
    /// <summary>
    /// Phase 1: an employee enrolled for biometric access control.
    /// </summary>
    public class Employee : Base
    {
        public string FileNumber { get; set; } = "";
        public string FullName { get; set; } = "";
        public Guid? DepartmentId { get; set; }
        public Department? Department { get; set; }
        public string? JobStatus { get; set; }
        public string? Nationality { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>Whether this employee may act as a "host" for visitor permits.</summary>
        public bool HostCapable { get; set; } = false;

        public ICollection<BiometricTemplate> BiometricTemplates { get; set; } = new List<BiometricTemplate>();
        public ICollection<EmployeePermission> Permissions { get; set; } = new List<EmployeePermission>();
        public ICollection<MovementLog> MovementLogs { get; set; } = new List<MovementLog>();
    }
}
