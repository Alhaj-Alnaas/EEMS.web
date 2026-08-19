using System;

namespace Core.Entities
{
    /// <summary>
    /// Clean 1:1 "unified" detail record for an equipment/materials permit.
    /// Coexists with the legacy <see cref="EquipMatiMovment"/> line-items which
    /// remain the source of truth for the current materials flow.
    /// </summary>
    public class EquipmentPermitDetail : Base
    {
        public Guid PermitId { get; set; }
        public Permit? Permit { get; set; }

        public string Description { get; set; } = "";
        public int Qty { get; set; }
        public string Unit { get; set; } = "";
    }
}
