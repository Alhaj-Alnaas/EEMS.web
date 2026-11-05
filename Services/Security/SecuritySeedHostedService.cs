using Core.Entities.Security;

using Core.Interfaces.Services.Security;

using Core.Security;

using DataAccess;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Hosting;

using Microsoft.Extensions.Logging;



namespace Services.Security

{

    /// <summary>Ensures system task catalog and default access levels exist.</summary>

    public class SecuritySeedHostedService : IHostedService

    {

        private readonly IServiceProvider _services;

        private readonly ILogger<SecuritySeedHostedService> _logger;



        public SecuritySeedHostedService(IServiceProvider services, ILogger<SecuritySeedHostedService> logger)

        {

            _services = services;

            _logger = logger;

        }



        public async Task StartAsync(CancellationToken cancellationToken)

        {

            try

            {

                using var scope = _services.CreateScope();

                var tasks = scope.ServiceProvider.GetRequiredService<ISystemTaskQueryService>();

                await tasks.EnsureCatalogSeededAsync();



                var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DataContext>>();

                await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

                await EnsureDefaultLevelsAsync(db, cancellationToken);

                await RenameLegacyGateLevelAsync(db, cancellationToken);

                await EnsureAdminLevelHasCriticalTasksAsync(db, cancellationToken);

                await MigrateLegacyUserTypesAsync(db, cancellationToken);



                var levelCount = await db.AccessLevels.CountAsync(a => !a.isDeleted, cancellationToken);

                var unmapped = await db.Users.CountAsync(u => u.AccessLevelId == null, cancellationToken);

                _logger.LogInformation(

                    "Security seed OK: {Levels} access levels, {Unmapped} users still without AccessLevelId",

                    levelCount, unmapped);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex,

                    "Security seed failed — AccessLevels/SystemTasks may be missing. Apply migration AccessLevelsAndSystemTasks and restart.");

            }

        }



        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private static async Task RenameLegacyGateLevelAsync(DataContext db, CancellationToken ct)
        {
            var legacy = await db.AccessLevels
                .Where(a => !a.isDeleted && a.Name == "ضابط أمن (بوابة)")
                .ToListAsync(ct);
            if (legacy.Count == 0) return;

            foreach (var level in legacy)
            {
                level.Name = "أمن بوابة";
                level.updatedOn = DateTime.Now;
                level.updatedBy = "seed";
            }

            await db.SaveChangesAsync(ct);
        }

        private static async Task EnsureDefaultLevelsAsync(DataContext db, CancellationToken ct)
        {
            if (await db.AccessLevels.AnyAsync(a => !a.isDeleted, ct))
                return;



            var allTasks = await db.SystemTasks.Where(t => t.IsActive && !t.isDeleted).ToListAsync(ct);

            var byCode = allTasks.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);



            AccessLevel Make(string name, string desc, int sort, bool gate, bool alerts, params string[] codes)

            {

                var level = new AccessLevel

                {

                    Id = Guid.NewGuid(),

                    Name = name,

                    Description = desc,

                    SortOrder = sort,

                    IsActive = true,

                    IsGateOfficer = gate,

                    ReceivesDeviceAlerts = alerts,

                    createdBy = "seed",

                    createdOn = DateTime.Now

                };

                foreach (var code in codes)

                {

                    if (byCode.TryGetValue(code, out var task))

                    {

                        level.AccessLevelTasks.Add(new AccessLevelTask

                        {

                            AccessLevelId = level.Id,

                            SystemTaskId = task.Id

                        });

                    }

                }

                return level;

            }



            var allCodes = SystemTaskCatalog.All.Select(t => t.Code).ToArray();



            db.AccessLevels.Add(Make(

                "مدير النظام",

                "صلاحيات كاملة على النظام بما فيها إدارة المستخدمين ومستويات الدخول",

                10, false, true, allCodes));



            db.AccessLevels.Add(Make(

                "مستخدم تطبيق",

                "معظم وظائف النظام دون إدارة المستخدمين ومستويات الدخول",

                20, false, false,

                allCodes.Where(c => c is not (SystemTaskCodes.AdminUsers or SystemTaskCodes.AdminAccessLevels)).ToArray()));



            db.AccessLevels.Add(Make(

                "مستخدم عادي",

                "عرض اللوحة والتقارير فقط",

                30, false, false,

                SystemTaskCodes.Dashboard, SystemTaskCodes.Reports, SystemTaskCodes.PermitsList));



            db.AccessLevels.Add(Make(

                "أمن بوابة",

                "نقطة تفتيش البوابة ومراقبة الأجهزة — يتطلب اختيار بوابة عند الدخول",

                40, true, true,

                SystemTaskCodes.Dashboard,

                SystemTaskCodes.DevicesMonitor,

                SystemTaskCodes.GateOfficer,

                SystemTaskCodes.Movements,

                SystemTaskCodes.PermitsList));



            await db.SaveChangesAsync(ct);

        }



        /// <summary>

        /// If default levels already existed with incomplete task links, ensure system-admin

        /// still has ADMIN_USERS / ADMIN_ACCESS_LEVELS (and preferably the full catalog).

        /// </summary>

        private static async Task EnsureAdminLevelHasCriticalTasksAsync(DataContext db, CancellationToken ct)

        {

            var admin = await db.AccessLevels

                .Include(a => a.AccessLevelTasks)

                .ThenInclude(t => t.SystemTask)

                .FirstOrDefaultAsync(a => !a.isDeleted && a.Name == "مدير النظام", ct);



            if (admin == null)

            {

                // Fall back to any alert-receiving non-gate level, or create nothing if levels empty.

                admin = await db.AccessLevels

                    .Include(a => a.AccessLevelTasks)

                    .ThenInclude(t => t.SystemTask)

                    .Where(a => !a.isDeleted && a.ReceivesDeviceAlerts && !a.IsGateOfficer)

                    .OrderBy(a => a.SortOrder)

                    .FirstOrDefaultAsync(ct);

            }



            if (admin == null) return;



            var allTasks = await db.SystemTasks.Where(t => t.IsActive && !t.isDeleted).ToListAsync(ct);

            var byCode = allTasks.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

            var existing = admin.AccessLevelTasks.Select(t => t.SystemTaskId).ToHashSet();



            // Prefer full catalog for named system admin; at minimum critical admin codes.

            var requiredCodes = admin.Name == "مدير النظام"

                ? SystemTaskCatalog.All.Select(t => t.Code).ToArray()

                : new[]

                {

                    SystemTaskCodes.Dashboard,

                    SystemTaskCodes.AdminUsers,

                    SystemTaskCodes.AdminAccessLevels,

                    SystemTaskCodes.AdminGates

                };



            var added = false;

            foreach (var code in requiredCodes)

            {

                if (!byCode.TryGetValue(code, out var task)) continue;

                if (existing.Contains(task.Id)) continue;

                admin.AccessLevelTasks.Add(new AccessLevelTask

                {

                    AccessLevelId = admin.Id,

                    SystemTaskId = task.Id

                });

                existing.Add(task.Id);

                added = true;

            }



            if (added)

                await db.SaveChangesAsync(ct);

        }



        private static async Task MigrateLegacyUserTypesAsync(DataContext db, CancellationToken ct)

        {

            var levels = await db.AccessLevels.Where(a => !a.isDeleted).ToListAsync(ct);

            if (levels.Count == 0) return;



            AccessLevel? Find(string name) =>

                levels.FirstOrDefault(l => l.Name == name);



            var admin = Find("مدير النظام");

            var app = Find("مستخدم تطبيق");

            var bas = Find("مستخدم عادي");

            var gate = Find("أمن بوابة") ?? Find("ضابط أمن (بوابة)");



            var users = await db.Users.Where(u => u.AccessLevelId == null).ToListAsync(ct);

            foreach (var u in users)

            {

                u.AccessLevelId = (u.UserType?.Trim()) switch

                {

                    "Admin" => admin?.Id,

                    "SaftyUser" => gate?.Id,

                    "BaseUser" => bas?.Id,

                    "ApplicationUser" => app?.Id,

                    _ => app?.Id ?? admin?.Id

                } ?? admin?.Id ?? levels[0].Id;

            }



            if (users.Count > 0)

                await db.SaveChangesAsync(ct);

        }

    }

}


