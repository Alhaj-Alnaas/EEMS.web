using Core.Entities;
using Core.Extensions;
using static Core.Enums.BaseEnums;

namespace Core.Common
{
    /// <summary>Employee permit-card request: approval then office printing, not a gate movement.</summary>
    public static class EmployeeCardPermit
    {
        public const string Title = "طلب إصدار بطاقة تصريح";

        public static bool Is(Permit? permit) => IsClassification(permit?.classification);

        public static bool IsClassification(string? classification)
        {
            var value = classification?.Trim() ?? "";
            return value.Equals(nameof(PermitClassification.EmployeeAccess), StringComparison.OrdinalIgnoreCase)
                || value is "دخول موظفين" or "طلب إصدار بطاقة تصريح";
        }

        public static string KindLabel(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return "—";
            return type.ToEnumDescription<EmployeeCardRequestKind>();
        }

        public static IReadOnlyList<string> MissingPrintFields(Employee employee)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(employee.FileNumber))
                missing.Add("الرقم الوظيفي");
            if (string.IsNullOrWhiteSpace(employee.FullName))
                missing.Add("الاسم");
            if (string.IsNullOrWhiteSpace(employee.JobTitle))
                missing.Add("الوظيفة");
            if (string.IsNullOrWhiteSpace(employee.Nationality))
                missing.Add("الجنسية");
            if (string.IsNullOrWhiteSpace(employee.Address))
                missing.Add("العنوان");
            if (string.IsNullOrWhiteSpace(employee.BloodType))
                missing.Add("فصيلة الدم");
            if (string.IsNullOrWhiteSpace(employee.PhotoPath))
                missing.Add("الصورة");
            return missing;
        }

        public static string? PrintBlockMessage(Employee? employee)
        {
            if (employee == null || employee.isDeleted)
                return "الموظف غير مسجّل في النظام. سجّله أولاً من إدارة تسجيل الموظفين.";
            if (!employee.IsActive)
                return "الموظف غير نشط، ولا يمكن إصدار بطاقة له.";
            var missing = MissingPrintFields(employee);
            if (missing.Count == 0)
                return null;
            return "بيانات البطاقة ناقصة: " + string.Join("، ", missing) + ". أكمل الحقول من شاشة تسجيل الموظف، ويمكن تحديث الصورة من هذه الشاشة.";
        }
    }
}
