namespace Core.Security
{
    public sealed record SystemTaskDefinition(
        string Code,
        string NameAr,
        string Category,
        string? Route,
        int SortOrder);

    public static class SystemTaskCatalog
    {
        public static IReadOnlyList<SystemTaskDefinition> All { get; } =
        [
            new(SystemTaskCodes.Dashboard, "لوحة الرئيسية", "عامة", "/dashboard", 10),

            new(SystemTaskCodes.EmployeesList, "الموظفون", "المرحلة الأولى", "/employees", 100),
            new(SystemTaskCodes.EmployeesRegister, "تسجيل موظف", "المرحلة الأولى", "/employees/register", 110),
            new(SystemTaskCodes.DevicesReaders, "أجهزة القراءة", "المرحلة الأولى", "/devices/readers", 120),
            new(SystemTaskCodes.DevicesMonitor, "مراقبة الأجهزة", "المرحلة الأولى", "/devices/monitor", 130),
            new(SystemTaskCodes.GatePermissions, "صلاحيات عبور البوابات", "المرحلة الأولى", "/access/permissions", 140),
            new(SystemTaskCodes.Movements, "سجل الحركة", "المرحلة الأولى", "/movements", 150),

            new(SystemTaskCodes.PermitsList, "عرض التصاريح", "المرحلة الثانية", "/permits", 200),
            new(SystemTaskCodes.PermitsMaterialsCreate, "تصريح مواد جديد", "المرحلة الثانية", "/permits/materials/create", 210),
            new(SystemTaskCodes.PermitsVisitorsCreate, "تصريح زوار جديد", "المرحلة الثانية", "/permits/visitors/create", 220),
            new(SystemTaskCodes.PermitsVehiclesCreate, "تصريح سيارات جديد", "المرحلة الثانية", "/permits/vehicles/create", 230),
            new(SystemTaskCodes.GateOfficer, "نقطة تفتيش البوابة", "المرحلة الثانية", "/gate-officer", 240),
            new(SystemTaskCodes.Workflows, "مسارات الاعتماد", "المرحلة الثانية", "/admin/workflows", 250),

            new(SystemTaskCodes.AdminGates, "إدارة البوابات", "الإدارة", "/admin/gates", 300),
            new(SystemTaskCodes.AdminUsers, "إدارة المستخدمين", "الإدارة", "/admin/users", 310),
            new(SystemTaskCodes.AdminAccessLevels, "مستويات الدخول", "الإدارة", "/admin/access-levels", 320),

            new(SystemTaskCodes.Reports, "التقارير", "تقارير", "/reports", 400),
        ];
    }
}
