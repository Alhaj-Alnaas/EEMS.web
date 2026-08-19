using System;

namespace Core.Entities
{
    /// <summary>
    /// Clean 1:1 "unified" detail record for a visitor permit.
    /// </summary>
    public class VisitorPermitDetail : Base
    {
        public Guid PermitId { get; set; }
        public Permit? Permit { get; set; }

        public string VisitorName { get; set; } = "";
        public string? IdNumber { get; set; }
        public string? Nationality { get; set; }
        public string? Purpose { get; set; }

        public Guid? HostEmployeeId { get; set; }
        public Employee? HostEmployee { get; set; }

        public string? Phone { get; set; }
    }
}
