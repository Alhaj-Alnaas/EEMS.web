using System;
using static Core.Enums.BaseEnums;

namespace Core.Entities
{
    /// <summary>
    /// A single ordered step inside a <see cref="WorkflowDefinition"/>.
    /// </summary>
    public class WorkflowStep : Base
    {
        public Guid WorkflowDefinitionId { get; set; }
        public WorkflowDefinition? WorkflowDefinition { get; set; }

        public int StepOrder { get; set; }
        public WorkflowStepType StepType { get; set; }
        public string? RoleName { get; set; }
        public int SlaHours { get; set; } = 24;
        public bool IsRequired { get; set; } = true;
    }
}
