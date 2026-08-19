using System;
using System.Collections.Generic;
using static Core.Enums.BaseEnums;

namespace Core.Entities
{
    /// <summary>
    /// Defines a configurable, versioned approval workflow for a given
    /// permit classification (materials/visitors/cars/employee access).
    /// </summary>
    public class WorkflowDefinition : Base
    {
        public string Name { get; set; } = "";
        public PermitClassification PermitClassification { get; set; }
        public bool IsActive { get; set; } = true;
        public int Version { get; set; } = 1;

        public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
    }
}
