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

        /// <summary>Internal-use permits record each exit and the later return.</summary>
        public bool InternalUse { get; set; } = true;

        /// <summary>How many exit/return cycles this permit allows. Chosen when the permit is created.</summary>
        public int TripCount { get; set; } = 1;
    }
}
