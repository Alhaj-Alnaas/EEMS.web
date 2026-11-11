namespace Core.Common
{
    public static class AuditArea
    {
        public const string Permits = "Permits";
        public const string Administration = "Administration";

        public static string Label(string? code) => code switch
        {
            Administration => "إدارة",
            _ => "تصاريح"
        };
    }

    public static class AuditAction
    {
        public const string Add = "Add";
        public const string Edit = "Edit";
        public const string Delete = "Delete";
        public const string Cancel = "Cancel";
        public const string Approve = "Approve";
        public const string Reject = "Reject";
        public const string Return = "Return";
        public const string Manual = "Manual";
        public const string Settings = "Settings";
        public const string Enroll = "Enroll";

        public static string Label(string? code) => code switch
        {
            Add => "إضافة",
            Edit => "تعديل",
            Delete => "حذف",
            Cancel => "إلغاء",
            Approve => "اعتماد",
            Reject => "رفض",
            Return => "ترجيع",
            Manual => "تسجيل حركة",
            Settings => "إعدادات",
            Enroll => "تسجيل على البصمة",
            _ => code ?? ""
        };

        public static IReadOnlyList<(string Code, string Label)> Choices { get; } =
        [
            (Add, "إضافة"),
            (Edit, "تعديل"),
            (Delete, "حذف"),
            (Cancel, "إلغاء"),
            (Approve, "اعتماد"),
            (Reject, "رفض"),
            (Return, "ترجيع"),
            (Manual, "تسجيل حركة"),
            (Settings, "إعدادات"),
            (Enroll, "تسجيل على البصمة")
        ];
    }
}
