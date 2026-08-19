using System;

namespace Core.Entities
{
    /// <summary>
    /// Clean 1:1 "unified" detail record for a vehicle permit.
    /// </summary>
    public class VehiclePermitDetail : Base
    {
        public Guid PermitId { get; set; }
        public Permit? Permit { get; set; }

        public string PlateNumber { get; set; } = "";
        public string? VehicleType { get; set; }
        public string? DriverName { get; set; }
        public string? LicenseNo { get; set; }
    }
}
