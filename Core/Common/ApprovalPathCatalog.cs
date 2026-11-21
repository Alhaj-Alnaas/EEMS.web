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
            WorkflowStepType.SectionHead,
            WorkflowStepType.AdministrationManager,
            WorkflowStepType.GeneralManager,
            WorkflowStepType.SectorSupervisor,
            WorkflowStepType.ElectronicMonitoring,
            WorkflowStepType.SecurityAffairs,
            WorkflowStepType.GuardSection
        };

        public static bool IsPathStep(WorkflowStepType type) =>
            type is WorkflowStepType.NoApproval
                or WorkflowStepType.DirectSupervisor
                or WorkflowStepType.HigherSupervisor
                or WorkflowStepType.SectionHead
                or WorkflowStepType.AdministrationManager
                or WorkflowStepType.GeneralManager
                or WorkflowStepType.SectorSupervisor
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
            WorkflowStepType.SectionHead => "اعتماد رئيس القسم",
            WorkflowStepType.AdministrationManager => "اعتماد مدير الإدارة",
            WorkflowStepType.GeneralManager => "اعتماد المدير العام",
            WorkflowStepType.SectorSupervisor => "اعتماد مشرف القطاع",
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
            WorkflowStepType.NoApproval => "لا يمر بخطوات المسار ولا يذهب إلى المناوب. غير القيادي يبقى مرهوناً باعتماد رئيسه المباشر ثم ينتقل إلى التنفيذ، والقيادي ينتقل إلى التنفيذ مباشرة.",
            WorkflowStepType.SectionHead => "تُطبَّق إذا كانت أعلى من الرئيس المباشر. غير القيادي يمر برئيسه المباشر أولاً ولو لم تُفعَّل هذه الخطوة. القيادي يبدأ من أول خطوة بعد مستواه. القسم بلا إدارة يُعامل كإدارة.",
            WorkflowStepType.AdministrationManager => "تُطبَّق إذا كانت أعلى من الرئيس المباشر. قسم بلا إدارة يُعامل رئيسه كمدير إدارة، والقيادي في هذا المستوى يبدأ مما بعده.",
            WorkflowStepType.GeneralManager => "المدير العام. تُتخطى الخطوة عندما لا توجد إدارة عامة فوق الإدارة.",
            WorkflowStepType.SectorSupervisor => "مشرف القطاع فوق الإدارة أو الإدارة العامة.",
            WorkflowStepType.ElectronicMonitoring => "رئيس قسم المراقبة. لغير القيادي الداخل من الشؤون الأمنية هي أول خطوة: المراقبة، ثم مدير الإدارة، ثم الحراسة. القيادي يُتخطى مستواه.",
            WorkflowStepType.SecurityAffairs => "مدير إدارة الشؤون الأمنية. القيادي في هذا المنصب ينتقل الاعتماد إلى رئيس قسم الحراسة.",
            WorkflowStepType.GuardSection => "رئيس قسم الحراسة، وآخر خطوة في مسار الشؤون الأمنية. إذا كان المُدخل هو رئيس القسم تُتخطى الخطوة.",
            _ => ""
        };

        public static string ClassificationTitle(PermitClassification classification) => classification switch
        {
            PermitClassification.Materials => "مواد",
            PermitClassification.Cars => "مركبات",
            PermitClassification.Visitors => "زوار",
            PermitClassification.EmployeeAccess => "طلب إصدار بطاقة تصريح",
            _ => classification.ToString()
        };
    }
}
