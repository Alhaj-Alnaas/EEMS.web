using Core.Entities;

using Core.Security;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.DependencyInjection;



namespace PMS.web.Authorization

{

    public sealed class TaskRequirement : IAuthorizationRequirement

    {

        public TaskRequirement(string taskCode) => TaskCode = taskCode;

        public string TaskCode { get; }

    }



    public sealed class TaskAuthorizationHandler : AuthorizationHandler<TaskRequirement>

    {

        private readonly IConfiguration _configuration;

        private readonly IServiceScopeFactory _scopeFactory;



        public TaskAuthorizationHandler(IConfiguration configuration, IServiceScopeFactory scopeFactory)

        {

            _configuration = configuration;

            _scopeFactory = scopeFactory;

        }



        protected override async Task HandleRequirementAsync(

            AuthorizationHandlerContext context,

            TaskRequirement requirement)

        {

            if (_configuration.GetValue("DesignMode:DisableAuthentication", false))

            {

                context.Succeed(requirement);

                return;

            }



            if (context.User.HasClaim(SystemTaskCodes.ClaimType, requirement.TaskCode))

            {

                context.Succeed(requirement);

                return;

            }



            // Transition safety net for /admin/users and /admin/access-levels:

            // legacy Admin (claim or DB UserType) must not be locked out while AccessLevel migrates.

            if (IsAdminManagementTask(requirement.TaskCode)

                && await IsLegacyAdminAsync(context))

            {

                context.Succeed(requirement);

            }

        }



        private async Task<bool> IsLegacyAdminAsync(AuthorizationHandlerContext context)

        {

            if (context.User.HasClaim("pms_legacy_usertype", "Admin"))

                return true;



            if (context.User.Identity?.IsAuthenticated != true)

                return false;



            try

            {

                using var scope = _scopeFactory.CreateScope();

                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

                var user = await userManager.GetUserAsync(context.User);

                return string.Equals(user?.UserType?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase);

            }

            catch

            {

                return false;

            }

        }



        private static bool IsAdminManagementTask(string taskCode) =>

            string.Equals(taskCode, SystemTaskCodes.AdminUsers, StringComparison.OrdinalIgnoreCase)

            || string.Equals(taskCode, SystemTaskCodes.AdminAccessLevels, StringComparison.OrdinalIgnoreCase)

            || string.Equals(taskCode, SystemTaskCodes.AdminGates, StringComparison.OrdinalIgnoreCase)

            || string.Equals(taskCode, SystemTaskCodes.Dashboard, StringComparison.OrdinalIgnoreCase);

    }



    public static class TaskPolicies

    {

        public const string Prefix = "Task:";



        public static string Name(string taskCode) => Prefix + taskCode;



        // Compile-time constants for [Authorize(Policy = ...)] attributes

        public const string Dashboard = Prefix + SystemTaskCodes.Dashboard;

        public const string EmployeesList = Prefix + SystemTaskCodes.EmployeesList;

        public const string EmployeesRegister = Prefix + SystemTaskCodes.EmployeesRegister;

        public const string DevicesReaders = Prefix + SystemTaskCodes.DevicesReaders;

        public const string DevicesMonitor = Prefix + SystemTaskCodes.DevicesMonitor;

        public const string GatePermissions = Prefix + SystemTaskCodes.GatePermissions;

        public const string Movements = Prefix + SystemTaskCodes.Movements;

        public const string PermitsList = Prefix + SystemTaskCodes.PermitsList;

        public const string PermitsMaterialsCreate = Prefix + SystemTaskCodes.PermitsMaterialsCreate;

        public const string PermitsVisitorsCreate = Prefix + SystemTaskCodes.PermitsVisitorsCreate;

        public const string PermitsVehiclesCreate = Prefix + SystemTaskCodes.PermitsVehiclesCreate;

        public const string GateOfficer = Prefix + SystemTaskCodes.GateOfficer;

        public const string Workflows = Prefix + SystemTaskCodes.Workflows;

        public const string AdminGates = Prefix + SystemTaskCodes.AdminGates;

        public const string AdminUsers = Prefix + SystemTaskCodes.AdminUsers;

        public const string AdminAccessLevels = Prefix + SystemTaskCodes.AdminAccessLevels;

        public const string Reports = Prefix + SystemTaskCodes.Reports;



        public static void Register(AuthorizationOptions options)

        {

            foreach (var def in SystemTaskCatalog.All)

            {

                options.AddPolicy(Name(def.Code), policy =>

                    policy.Requirements.Add(new TaskRequirement(def.Code)));

            }

        }

    }

}


