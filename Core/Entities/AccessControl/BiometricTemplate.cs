using System;
using static Core.Enums.BaseEnums;

namespace Core.Entities
{
    /// <summary>
    /// Phase 1: a fingerprint/face biometric template captured for an employee.
    /// </summary>
    public class BiometricTemplate : Base
    {
        public Guid EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public BiometricTemplateType TemplateType { get; set; } = BiometricTemplateType.Fingerprint;
        public byte[]? TemplateData { get; set; }

        public bool IsApproved { get; set; } = false;
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedOn { get; set; }
    }
}
