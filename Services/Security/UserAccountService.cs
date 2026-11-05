using Core.Entities;
using Core.Entities.Security;
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

        public UserAccountService(UserManager<User> userManager, IDbContextFactory<DataContext> dbFactory)
        {
            _userManager = userManager;
            _dbFactory = dbFactory;
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

        public async Task<(bool Ok, string Message)> UpdateAccessLevelAsync(string userId, Guid accessLevelId)
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
            return result.Succeeded
                ? (true, "تم تحديث مستوى الدخول.")
                : (false, string.Join(" | ", result.Errors.Select(e => e.Description)));
        }

        public async Task<(bool Ok, string Message, string? TempPassword)> ResetPasswordAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return (false, "المستخدم غير موجود.", null);

            var temp = "Pms@" + Random.Shared.Next(100000, 999999);
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, temp);
            return result.Succeeded
                ? (true, "تم تصفير كلمة المرور.", temp)
                : (false, string.Join(" | ", result.Errors.Select(e => e.Description)), null);
        }

        public async Task<(bool Ok, string Message)> DeleteAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return (false, "المستخدم غير موجود.");

            var result = await _userManager.DeleteAsync(user);
            return result.Succeeded
                ? (true, "تم حذف المستخدم.")
                : (false, string.Join(" | ", result.Errors.Select(e => e.Description)));
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
