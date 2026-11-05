namespace Core.Security
{
    /// <summary>Stable task codes used for authorization, menu filtering, and seeding.</summary>
    public static class SystemTaskCodes
    {
        public const string Dashboard = "DASHBOARD";

        public const string EmployeesList = "EMPLOYEES_LIST";
        public const string EmployeesRegister = "EMPLOYEES_REGISTER";
        public const string DevicesReaders = "DEVICES_READERS";
        public const string DevicesMonitor = "DEVICES_MONITOR";
        public const string GatePermissions = "GATE_PERMISSIONS";
        public const string Movements = "MOVEMENTS";

        public const string PermitsList = "PERMITS_LIST";
        public const string PermitsMaterialsCreate = "PERMITS_MATERIALS_CREATE";
        public const string PermitsVisitorsCreate = "PERMITS_VISITORS_CREATE";
        public const string PermitsVehiclesCreate = "PERMITS_VEHICLES_CREATE";
        public const string GateOfficer = "GATE_OFFICER";
        public const string Workflows = "WORKFLOWS";

        public const string AdminGates = "ADMIN_GATES";
        public const string AdminUsers = "ADMIN_USERS";
        public const string AdminAccessLevels = "ADMIN_ACCESS_LEVELS";

        public const string Reports = "REPORTS";

        public const string ClaimType = "pms_task";
        public const string GateOfficerClaimType = "pms_gate_officer";
        public const string FullNameClaimType = "pms_full_name";
        public const string AccessLevelNameClaimType = "pms_access_level";
        public const string JobTitleClaimType = "pms_job_title";
        public const string SelectedGateIdClaimType = "pms_gate_id";
        public const string SelectedGateNameClaimType = "pms_gate_name";
    }
}
