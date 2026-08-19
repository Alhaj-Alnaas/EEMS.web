using System;
using static Core.Enums.BaseEnums;

namespace Core.Entities
{
    /// <summary>
    /// Records a single approve/reject/return decision made against a permit,
    /// optionally tied to a specific <see cref="WorkflowStep"/>.
    /// </summary>
    public class PermitApproval : Base
    {
        public Guid PermitId { get; set; }
        public Permit? Permit { get; set; }

        public Guid? WorkflowStepId { get; set; }
        public WorkflowStep? WorkflowStep { get; set; }

        public string ApproverUserId { get; set; } = "";
        public ApprovalDecision Decision { get; set; }
        public string? Comment { get; set; }
        public DateTime DecidedOn { get; set; } = DateTime.Now;
    }
}
