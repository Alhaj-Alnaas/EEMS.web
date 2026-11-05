using System.Security.Claims;
using Core.Entities;
using Core.Security;
using DataAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace PMS.web.Identity
{
    public class PmsUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<User, IdentityRole>
    {
        private readonly IDbContextFactory<DataContext> _dbFactory;

        public PmsUserClaimsPrincipalFactory(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options,
            IDbContextFactory<DataContext> dbFactory)
            : base(userManager, roleManager, options)
        {
            _dbFactory = dbFactory;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            if (!string.IsNullOrWhiteSpace(user.FullName))
                identity.AddClaim(new Claim(SystemTaskCodes.FullNameClaimType, user.FullName.Trim()));

            if (!string.IsNullOrWhiteSpace(user.JobtypeName))
                identity.AddClaim(new Claim(SystemTaskCodes.JobTitleClaimType, user.JobtypeName.Trim()));

            // Always stamp legacy type so authorization can bridge UserType → AccessLevel.
            var legacyType = user.UserType?.Trim();
            if (!string.IsNullOrEmpty(legacyType))
                identity.AddClaim(new Claim("pms_legacy_usertype", legacyType));

            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                var level = await db.Users
                    .Where(u => u.Id == user.Id)
                    .Select(u => u.AccessLevel)
                    .FirstOrDefaultAsync();

                if (level == null || !level.IsActive || level.isDeleted)
                {
                    // Transition bootstrap: legacy Admin keeps full task access until AccessLevel is set.
                    GrantLegacyBootstrapClaims(identity, legacyType);
                    return identity;
                }

                if (!string.IsNullOrWhiteSpace(level.Name))
                    identity.AddClaim(new Claim(SystemTaskCodes.AccessLevelNameClaimType, level.Name.Trim()));

                if (level.IsGateOfficer)
                    identity.AddClaim(new Claim(SystemTaskCodes.GateOfficerClaimType, "true"));

                var taskCodes = await db.AccessLevelTasks
                    .Where(t => t.AccessLevelId == level.Id)
                    .Select(t => t.SystemTask.Code)
                    .ToListAsync();

                if (taskCodes.Count == 0)
                {
                    // Level exists but has no tasks yet (incomplete seed) — keep Admin usable.
                    GrantLegacyBootstrapClaims(identity, legacyType);
                    return identity;
                }

                foreach (var code in taskCodes)
                    identity.AddClaim(new Claim(SystemTaskCodes.ClaimType, code));
            }
            catch
            {
                // Schema/seed may not be ready yet; still allow legacy Admin through.
                GrantLegacyBootstrapClaims(identity, legacyType);
            }

            return identity;
        }

        /// <summary>
        /// During UserType → AccessLevel migration, Administrators must still open
        /// /admin/users and /admin/access-levels (and the rest of the app).
        /// </summary>
        private static void GrantLegacyBootstrapClaims(ClaimsIdentity identity, string? legacyType)
        {
            if (!string.Equals(legacyType, "Admin", StringComparison.OrdinalIgnoreCase))
                return;

            if (!identity.HasClaim(c => c.Type == SystemTaskCodes.AccessLevelNameClaimType))
                identity.AddClaim(new Claim(SystemTaskCodes.AccessLevelNameClaimType, "مدير النظام"));

            foreach (var def in SystemTaskCatalog.All)
            {
                if (!identity.HasClaim(SystemTaskCodes.ClaimType, def.Code))
                    identity.AddClaim(new Claim(SystemTaskCodes.ClaimType, def.Code));
            }
        }
    }
}
