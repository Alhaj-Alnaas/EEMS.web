namespace Core.Security
{
    public sealed record SystemTaskDefinition(
        string Code,
        string NameAr,
        string Category,
        string? Route,
        int SortOrder,
        string Icon = "fas fa-circle",
        bool ExactMatch = false);

    public sealed record SystemMenuGroup(
        string Key,
        string Title,
        string Icon,
        IReadOnlyList<SystemTaskDefinition> Items);

    /// <summary>
    /// Single source for the sidebar and the access-level task list.
    /// A task added here appears in the sidebar and is synced into SystemTasks on startup.
    /// </summary>
    public static class SystemTaskCatalog
    {
        public static IReadOnlyList<SystemTaskDefinition> TopLinks { get; } =
        [
            new(SystemTaskCodes.Dashboard, "الرئيسية", "عامة", "/dashboard", 10, "fas fa-home", true),
        ];

        public static IReadOnlyList<SystemMenuGroup> Groups { get; } =
        [
            new("permits", "التصاريح", "fas fa-file-signature",
            [
                new(SystemTaskCodes.PermitsList, "قائمة التصاريح", "التصاريح", "/permits", 100, "fas fa-list", true),
                new(SystemTaskCodes.PermitsEmployeeCreate, "إصدار تصريح موظف", "التصاريح", "/permits/employee/create", 110, "fas fa-user-check"),
                new(SystemTaskCodes.PermitsLabor, "تصاريح العمالة", "التصاريح", "/permits/labor", 120, "fas fa-helmet-safety"),
                new(SystemTaskCodes.PermitsVisitorsCreate, "تصريح زوار", "التصاريح", "/permits/visitors/create", 130, "fas fa-user-tag"),
                new(SystemTaskCodes.PermitsMaterialsCreate, "تصريح مواد", "التصاريح", "/permits/materials/create", 140, "fas fa-boxes"),
                new(SystemTaskCodes.PermitsVehiclesCreate, "تصريح مركبات", "التصاريح", "/permits/vehicles/create", 150, "fas fa-truck"),
                new(SystemTaskCodes.PermitsApprove, "اعتماد تصريح", "التصاريح", "/permits/approve", 160, "fas fa-stamp"),
                new(SystemTaskCodes.GateOfficer, "تنفيذ تصريح", "التصاريح", "/permits/execute", 170, "fas fa-qrcode"),
            ]),
            new("reports", "التقارير والمتابعة", "fas fa-chart-line",
            [
                new(SystemTaskCodes.ReportsActive, "التصاريح النشطة", "التقارير والمتابعة", "/reports/active-permits", 200, "fas fa-bolt"),
                new(SystemTaskCodes.ReportsOpen, "تصاريح مفتوحة", "التقارير والمتابعة", "/reports/open-permits", 210, "fas fa-hourglass-split"),
                new(SystemTaskCodes.Movements, "سجل الحركة", "التقارير والمتابعة", "/movements", 220, "fas fa-exchange-alt"),
                new(SystemTaskCodes.DevicesMonitor, "حالة الأجهزة", "التقارير والمتابعة", "/devices/monitor", 230, "fas fa-heartbeat"),
                new(SystemTaskCodes.ReportsAttendance, "تقارير الحضور", "التقارير والمتابعة", "/reports/attendance", 240, "fas fa-user-clock"),
                new(SystemTaskCodes.ReportsAudit, "سجل التدقيق", "التقارير والمتابعة", "/reports/audit-log", 250, "fas fa-clipboard-list"),
            ]),
            new("admin", "الإدارة", "fas fa-cogs",
            [
                new(SystemTaskCodes.DevicesReaders, "إدارة القارئات", "الإدارة", "/devices/readers", 300, "fas fa-fingerprint"),
                new(SystemTaskCodes.EmployeesList, "الموظفون", "الإدارة", "/employees", 310, "fas fa-users"),
                new(SystemTaskCodes.AdminGates, "إدارة البوابات", "الإدارة", "/admin/gates", 320, "fas fa-door-open"),
                new(SystemTaskCodes.GatePermissions, "إدارة صلاحيات الدخول", "الإدارة", "/access/permissions", 330, "fas fa-key"),
                new(SystemTaskCodes.Workflows, "إعدادات التصريح", "الإدارة", "/admin/workflows", 340, "fas fa-project-diagram"),
                new(SystemTaskCodes.AdminAccessLevels, "مستويات الدخول", "الإدارة", "/admin/access-levels", 350, "fas fa-shield-alt"),
                new(SystemTaskCodes.AdminUsers, "إدارة المستخدمين", "الإدارة", "/admin/users", 360, "fas fa-user-cog"),
            ]),
        ];

        public static IReadOnlyList<SystemTaskDefinition> All { get; } =
            TopLinks.Concat(Groups.SelectMany(group => group.Items)).ToList();
    }
}
