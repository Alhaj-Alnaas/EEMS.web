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

        /// <summary>Blood type / group for badge printing (e.g. O+).</summary>
        public string? BloodType { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>Whether this employee may act as a "host" for visitor permits.</summary>
        public bool HostCapable { get; set; } = false;

        /// <summary>Job title / designation from HR (e.g. jobtypeName).</summary>
        public string? JobTitle { get; set; }

        /// <summary>Residential address used on printed employee cards.</summary>
        public string? Address { get; set; }

        public string? Phone { get; set; }
        public string? Email { get; set; }

        /// <summary>Web-relative path to badge photo, e.g. /uploads/employees/12345.jpg</summary>
        public string? PhotoPath { get; set; }

        /// <summary>Physical/access card number (CardNo) for device enrollment.</summary>
        public string? CardNumber { get; set; }

        /// <summary>Department name from HR when no matching Department master row exists.</summary>
        public string? DepartmentName { get; set; }

        public ICollection<BiometricTemplate> BiometricTemplates { get; set; } = new List<BiometricTemplate>();
        public ICollection<EmployeePermission> Permissions { get; set; } = new List<EmployeePermission>();
        public ICollection<MovementLog> MovementLogs { get; set; } = new List<MovementLog>();
        public ICollection<EmployeeCard> Cards { get; set; } = new List<EmployeeCard>();
    }
}
