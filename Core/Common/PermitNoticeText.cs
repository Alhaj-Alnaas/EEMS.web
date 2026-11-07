using static Core.Enums.BaseEnums;

namespace Core.Common
{
    /// <summary>Arabic sentences sent when a permit status changes.</summary>
    public static class PermitNoticeText
    {
        public static string Authority(WorkflowStepType type) => type switch
        {
            WorkflowStepType.DirectSupervisor => "الرئيس المباشر",
            WorkflowStepType.HigherSupervisor => "الرئيس الأعلى",
            WorkflowStepType.ElectronicMonitoring => "رئيس قسم المراقبة الإلكترونية",
            WorkflowStepType.SecurityAffairs => "مدير إدارة الشؤون الأمنية",
            WorkflowStepType.GuardSection => "رئيس قسم الحراسة",
            WorkflowStepType.DepartmentApproval => "رئيس القسم",
            WorkflowStepType.SecurityApproval => "الأمن",
            WorkflowStepType.PermitsSectionApproval => "قسم التصاريح",
            _ => "جهة الاعتماد"
        };

        public static string Submitted(string? number, string waiting) =>
            $"أُحيل التصريح رقم {number} وهو بانتظار {waiting}.";

        public static string Ready(string? number) =>
            $"التصريح رقم {number} لا يحتاج اعتماداً وأصبح جاهزاً للتنفيذ.";

        public static string Approved(string? number, string authority, bool ready, string? nextStep)
        {
            if (ready)
                return $"تم اعتماد التصريح رقم {number} من قبل {authority}، وأصبح جاهزاً للتنفيذ.";
            return $"تم اعتماد التصريح رقم {number} من قبل {authority}، وهو الآن بانتظار {nextStep}.";
        }

        public static string Rejected(string? number, string authority, string? note)
        {
            var text = $"السيد {authority} يرفض التصريح رقم {number}.";
            return WithNote(text, note);
        }

        public static string Returned(string? number, string authority, string? note)
        {
            var text = $"أرجع {authority} التصريح رقم {number} إلى الجهة المُعدّة.";
            return WithNote(text, note);
        }

        public static string Executed(string? number, bool entry, DateTime when, bool closed)
        {
            var what = entry ? "ساعة الدخول" : "ساعة الخروج";
            var text = $"تم تنفيذ التصريح رقم {number} بتسجيل {what} عند الساعة {when:HH:mm}.";
            return closed ? text + " وأُغلق التصريح." : text;
        }

        public static string Expired(string? number) =>
            $"أُغلق التصريح رقم {number} آلياً لتجاوز التاريخ المسموح به للتنفيذ.";

        public static string MaterialsLeftOpen(string? number) =>
            $"أُغلق تصريح المواد رقم {number} بعد تسجيل الدخول دون تسجيل الخروج لتجاوز تاريخ السريان. على الجهة الطالبة إعداد تصريح استخراج.";

        public static string VisitorStillInside(string? number) =>
            $"التصريح رقم {number}: دخل زائر ولم يُسجل خروجه. يبقى التصريح معلقاً في التنفيذ حتى يعالجه مراقب الأمن في البوابة.";

        public static string VehicleStillOut(string? number) =>
            $"التصريح رقم {number}: سجلت المركبة خروجاً ولم يُسجل دخولها. يبقى التصريح معلقاً في التنفيذ حتى يعالجه مراقب الأمن في البوابة.";

        public static string Cancelled(string? number) =>
            $"أُلغي التصريح رقم {number}.";

        private static string WithNote(string text, string? note) =>
            string.IsNullOrWhiteSpace(note) ? text : text + " الملاحظة: " + note.Trim();
    }
}
