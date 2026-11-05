using static Core.Enums.BaseEnums;

namespace Core.Models
{
    public class ApprovalPathStepChoice
    {
        public WorkflowStepType Type { get; set; }
        public string Label { get; set; } = "";
        public string Hint { get; set; } = "";
        public bool Enabled { get; set; }
    }

    public class ApprovalPathRow
    {
        public PermitClassification Classification { get; set; }
        public string Title { get; set; } = "";
        public List<ApprovalPathStepChoice> Steps { get; set; } = new();

        /// <summary>Path currently used when a new permit of this type is submitted.</summary>
        public string RuntimeSummary { get; set; } = "لم يُربط بصندوق الاعتماد بعد";
    }
}
