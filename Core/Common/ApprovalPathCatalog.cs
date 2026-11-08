using Core.Entities;
using static Core.Enums.BaseEnums;

namespace Core.Common
{
    /// <summary>
    /// Fixed approval-path steps the system administrator turns on per permit type.
    /// Order is part of the rule and is not rearranged on the screen.
    /// </summary>
    public static class ApprovalPathCatalog
    {
        public static readonly WorkflowStepType[] OrderedSteps =
        {
            WorkflowStepType.NoApproval,
            WorkflowStepType.DirectSupervisor,
            WorkflowStepType.HigherSupervisor,
            WorkflowStepType.ElectronicMonitoring,
            WorkflowStepType.SecurityAffairs,
            WorkflowStepType.GuardSection
        };

        public static bool IsPathStep(WorkflowStepType type) =>
            type is WorkflowStepType.NoApproval
                or WorkflowStepType.DirectSupervisor
                or WorkflowStepType.HigherSupervisor
                or WorkflowStepType.ElectronicMonitoring
                or WorkflowStepType.SecurityAffairs
                or WorkflowStepType.GuardSection;

        public static bool IsConfiguration(IEnumerable<WorkflowStep> steps)
        {
            var active = steps.Where(s => !s.isDeleted).ToList();
            return active.Count > 0 && active.All(s => IsPathStep(s.StepType));
        }

        public static string Label(WorkflowStepType type) => type switch
        {
            WorkflowStepType.NoApproval => "لا يحتاج اعتماد",
            WorkflowStepType.DirectSupervisor => "اعتماد الرئيس المباشر",
            WorkflowStepType.HigherSupervisor => "اعتماد الرئيس الأعلى",
            WorkflowStepType.ElectronicMonitoring => "اعتماد رئيس قسم المراقبة الإلكترونية",
            WorkflowStepType.SecurityAffairs => "اعتماد مدير إدارة الشؤون الأمنية",
            WorkflowStepType.GuardSection => "اعتماد رئيس قسم الحراسة",
            WorkflowStepType.DepartmentApproval => "اعتماد رئيس القسم",
            WorkflowStepType.SecurityApproval => "اعتماد الأمن",
            WorkflowStepType.PermitsSectionApproval => "اعتماد قسم التصاريح",
            _ => type.ToString()
        };

        public static string Hint(WorkflowStepType type) => type switch
        {
            WorkflowStepType.NoApproval => "ينتقل التصريح إلى التنفيذ مباشرة، ويظهر في متابعة الجهات ذات العلاقة.",
            WorkflowStepType.DirectSupervisor => "مسؤول التقسيم الإداري لمن أدخل التصريح. إذا كان المُدخل قيادياً فهو الرئيس المباشر لنفسه، وتُتخطى هذه الخطوة إلى المستوى التالي في المسار. وتُهمل إذا أُدخل التصريح من إدارة الشؤون الأمنية أو أحد أقسامها.",
            WorkflowStepType.HigherSupervisor => "المستوى الإداري الأعلى. لا يُطلب رئيس أعلى إذا أُدخل التصريح من إدارة ليس لها إدارة عامة، أو من إدارة عامة، أو من قطاع؛ واعتماد رئيس ذلك التقسيم هو اعتماد الرئيس الأعلى. وتُهمل هذه الخطوة إذا أُدخل التصريح من إدارة الشؤون الأمنية أو أحد أقسامها.",
            WorkflowStepType.ElectronicMonitoring => "مسؤول التقسيم الإداري ومركز مسؤوليته 45012. إذا أُدخل التصريح من إدارة الشؤون الأمنية أو أحد أقسامها فهذه أول خطوة في المسار.",
            WorkflowStepType.SecurityAffairs => "مدير إدارة الشؤون الأمنية. إذا أُدخل التصريح من هذه الإدارة أو أحد أقسامها يسير الاعتماد: رئيس قسم المراقبة الإلكترونية، ثم مدير إدارة الشؤون الأمنية، ثم رئيس قسم الحراسة.",
            WorkflowStepType.GuardSection => "مسؤول التقسيم الإداري ومركز مسؤوليته 45011. إذا أُدخل التصريح من إدارة الشؤون الأمنية أو أحد أقسامها فهذه آخر خطوة في المسار.",
            _ => ""
        };

        public static string ClassificationTitle(PermitClassification classification) => classification switch
        {
            PermitClassification.Materials => "مواد",
            PermitClassification.Cars => "مركبات",
            PermitClassification.Visitors => "زوار",
            PermitClassification.EmployeeAccess => "دخول موظفين",
            _ => classification.ToString()
        };
    }
}
