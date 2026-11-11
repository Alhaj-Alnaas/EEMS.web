using Core.Common;
using Core.Entities;
using Core.Entities.Security;
using Core.Interfaces.Services;
using Core.Interfaces.Services.Security;
using Core.Models.Security;
using Core.Security;
using DataAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Services.Security
{
    public class UserAccountService : IUserAccountService
    {
        private readonly UserManager<User> _userManager;
        private readonly IDbContextFactory<DataContext> _dbFactory;
        private readonly IAuditLogService _audit;

        public UserAccountService(UserManager<User> userManager, IDbContextFactory<DataContext> dbFactory, IAuditLogService audit)
        {
            _userManager = userManager;
            _dbFactory = dbFactory;
            _audit = audit;
        }

        public async Task<List<UserAccountListItem>> SearchAsync(string? fileNumber, int take = 200)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var q = db.Users.AsNoTracking().Include(u => u.AccessLevel).AsQueryable();
            if (!string.IsNullOrWhiteSpace(fileNumber))
                q = q.Where(u => u.FileNumber.Contains(fileNumber));

            return await q.OrderBy(u => u.FullName)
                .Take(take)
                .Select(u => new UserAccountListItem
                {
                    Id = u.Id,
                    FileNumber = u.FileNumber,
                    FullName = u.FullName,
                    Department = u.Department,
                    JobTypeName = u.JobtypeName,
                    JobStatus = u.JobStatus,
                    AccessLevelName = u.AccessLevel != null ? u.AccessLevel.Name : null
                })
                .ToListAsync();
        }

        public async Task<UserAccountDetail?> GetByFileNumberAsync(string fileNumber)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var u = await db.Users.AsNoTracking()
                .Include(x => x.AccessLevel)
                .FirstOrDefaultAsync(x => x.FileNumber == fileNumber);

            if (u == null) return null;

            return new UserAccountDetail
            {
                Id = u.Id,
                EmpFileNo = u.FileNumber,
                EmpName = u.FullName,
                Department = u.Department,
                JobTypeName = u.JobtypeName,
                EmpEmail = u.Email ?? "",
                JobStatus = u.JobStatus,
                AccessLevelId = u.AccessLevelId,
                AccessLevelName = u.AccessLevel?.Name
            };
        }

        public async Task<bool> ExistsByFileNumberAsync(string fileNumber)
        {
            return await _userManager.Users.AnyAsync(u => u.FileNumber == fileNumber || u.UserName == fileNumber);
        }

        public async Task<(bool Ok, string Message, string? UserId)> CreateAsync(CreateUserAccountModel model)
        {
            if (!model.AccessLevelId.HasValue)
                return (false, "يجب اختيار مستوى الدخول.", null);

            if (await ExistsByFileNumberAsync(model.EmpFileNo))
                return (false, "المستخدم موجود مسبقاً بهذا الرقم الوظيفي.", null);

            await using var db = await _dbFactory.CreateDbContextAsync();
            var level = await db.AccessLevels.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == model.AccessLevelId && a.IsActive && !a.isDeleted);
            if (level == null)
                return (false, "مستوى الدخول المحدد غير صالح.", null);

            var user = new User
            {
                UserName = model.EmpFileNo,
                NormalizedUserName = model.EmpFileNo.ToUpperInvariant(),
                Email = string.IsNullOrWhiteSpace(model.EmpEmail) ? null : model.EmpEmail,
                NormalizedEmail = string.IsNullOrWhiteSpace(model.EmpEmail) ? null : model.EmpEmail.ToUpperInvariant(),
                PhoneNumber = model.EmpPhone,
                FileNumber = model.EmpFileNo,
                FullName = model.EmpName,
                ResponsibilityCode = model.EmpResponsibilityCode,
                Department = model.Department,
                RespCodeId = model.RespCodeId ?? 0,
                JobCatId = model.JobCatId ?? 0,
                JobStatus = string.IsNullOrWhiteSpace(model.JobStatus) ? "AE" : model.JobStatus,
                DesignationId = model.DesignationId ?? 0,
                JobtypeName = model.JobTypeName,
                AccessLevelId = level.Id,
                Discriminator = "User",
                UserType = DeriveLegacyUserType(level)
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return (false, string.Join(" | ", result.Errors.Select(e => e.Description)), null);

            return (true, "تم إضافة المستخدم بنجاح.", user.Id);
        }

        public async Task<(bool Ok, string Message)> UpdateAccessLevelAsync(string userId, Guid accessLevelId, string? actorFile = null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return (false, "المستخدم غير موجود.");

            await using var db = await _dbFactory.CreateDbContextAsync();
            var level = await db.AccessLevels.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accessLevelId && a.IsActive && !a.isDeleted);
            if (level == null) return (false, "مستوى الدخول المحدد غير صالح.");

            user.AccessLevelId = level.Id;
            user.UserType = DeriveLegacyUserType(level);
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return (false, string.Join(" | ", result.Errors.Select(e => e.Description)));

            await _audit.WriteAsync(new AuditLogEntry
            {
                SourceKey = "user-level:" + user.Id + ":" + level.Id + ":" + DateTime.Now.Ticks,
                Area = AuditArea.Administration,
                Action = AuditAction.Settings,
                Summary = "تعديل مستوى دخول " + user.FileNumber + " إلى " + level.Name,
                ActorFileNumber = actorFile,
                OccurredAt = DateTime.Now
            });
            return (true, "تم تحديث مستوى الدخول.");
        }

        public async Task<(bool Ok, string Message, string? TempPassword)> ResetPasswordAsync(string userId, string? actorFile = null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return (false, "المستخدم غير موجود.", null);

            var temp = "Pms@" + Random.Shared.Next(100000, 999999);
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, temp);
            if (!result.Succeeded)
                return (false, string.Join(" | ", result.Errors.Select(e => e.Description)), null);

            await _audit.WriteAsync(new AuditLogEntry
            {
                SourceKey = "user-reset:" + user.Id + ":" + DateTime.Now.Ticks,
                Area = AuditArea.Administration,
                Action = AuditAction.Settings,
                Summary = "تصفير كلمة مرور " + user.FileNumber + " " + user.FullName,
                ActorFileNumber = actorFile,
                OccurredAt = DateTime.Now
            });
            return (true, "تم تصفير كلمة المرور.", temp);
        }

        public async Task<(bool Ok, string Message)> DeleteAsync(string userId, string? actorFile = null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return (false, "المستخدم غير موجود.");

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return (false, string.Join(" | ", result.Errors.Select(e => e.Description)));

            await _audit.WriteAsync(new AuditLogEntry
            {
                SourceKey = "user-delete:" + user.Id,
                Area = AuditArea.Administration,
                Action = AuditAction.Delete,
                Summary = "حذف المستخدم " + user.FileNumber + " " + user.FullName,
                ActorFileNumber = actorFile,
                OccurredAt = DateTime.Now
            });
            return (true, "تم حذف المستخدم.");
        }

        private static string DeriveLegacyUserType(AccessLevel level)
        {
            if (level.IsGateOfficer) return "SaftyUser";
            if (level.ReceivesDeviceAlerts) return "Admin";
            return "ApplicationUser";
        }
    }

    public class UserAccessService : IUserAccessService
    {
        private readonly IDbContextFactory<DataContext> _dbFactory;

        public UserAccessService(IDbContextFactory<DataContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<IReadOnlySet<string>> GetTaskCodesForUserAsync(string userId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var userInfo = await db.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.AccessLevelId, u.UserType })
                .FirstOrDefaultAsync();

            if (userInfo == null) return new HashSet<string>();

            if (userInfo.AccessLevelId != null)
            {
                var codes = await db.AccessLevelTasks
                    .Where(t => t.AccessLevelId == userInfo.AccessLevelId)
                    .Select(t => t.SystemTask.Code)
                    .ToListAsync();

                if (codes.Count > 0)
                    return codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            // Transition: legacy Admin with no (or empty) AccessLevel still has full access.
            if (string.Equals(userInfo.UserType?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase))
                return SystemTaskCatalog.All.Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

            return new HashSet<string>();
        }

        public async Task<bool> HasTaskAsync(string userId, string taskCode)
        {
            var codes = await GetTaskCodesForUserAsync(userId);
            return codes.Contains(taskCode);
        }

        public async Task<bool> IsGateOfficerAsync(string userId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Users
                .Where(u => u.Id == userId && u.AccessLevel != null)
                .Select(u => u.AccessLevel!.IsGateOfficer)
                .FirstOrDefaultAsync();
        }
    }
}
