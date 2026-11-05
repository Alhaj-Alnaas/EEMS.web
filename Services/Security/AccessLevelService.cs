using Core.Entities.Security;
using Core.Interfaces.Services.Security;
using Core.Interfaces.UnitOfWork;
using Core.Models.Security;
using Core.Security;
using DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Services.Security
{
    public class SystemTaskQueryService : ISystemTaskQueryService
    {
        private readonly IDbContextFactory<DataContext> _dbFactory;

        public SystemTaskQueryService(IDbContextFactory<DataContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task EnsureCatalogSeededAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var existing = await db.SystemTasks.Select(t => t.Code).ToListAsync();
            var missing = SystemTaskCatalog.All.Where(d => !existing.Contains(d.Code)).ToList();
            if (missing.Count == 0) return;

            foreach (var def in missing)
            {
                db.SystemTasks.Add(new SystemTask
                {
                    Id = Guid.NewGuid(),
                    Code = def.Code,
                    NameAr = def.NameAr,
                    Category = def.Category,
                    Route = def.Route,
                    SortOrder = def.SortOrder,
                    IsActive = true,
                    createdOn = DateTime.Now,
                    createdBy = "seed"
                });
            }

            await db.SaveChangesAsync();
        }

        public async Task<List<SystemTask>> GetAllActiveAsync()
        {
            await EnsureCatalogSeededAsync();
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.SystemTasks
                .Where(t => t.IsActive && !t.isDeleted)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.NameAr)
                .ToListAsync();
        }
    }

    public class AccessLevelService : IAccessLevelService
    {
        private readonly IDbContextFactory<DataContext> _dbFactory;
        private readonly ISystemTaskQueryService _tasks;

        public AccessLevelService(IDbContextFactory<DataContext> dbFactory, ISystemTaskQueryService tasks)
        {
            _dbFactory = dbFactory;
            _tasks = tasks;
        }

        public async Task<List<AccessLevel>> GetAllAsync(bool includeInactive = false)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var q = db.AccessLevels.AsNoTracking().Where(a => !a.isDeleted);
            if (!includeInactive) q = q.Where(a => a.IsActive);
            return await q.OrderBy(a => a.SortOrder).ThenBy(a => a.Name).ToListAsync();
        }

        public async Task<AccessLevel?> GetByIdAsync(Guid id, bool includeTasks = true)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var q = db.AccessLevels.AsQueryable();
            if (includeTasks)
            {
                q = q.Include(a => a.AccessLevelTasks).ThenInclude(t => t.SystemTask);
            }

            return await q.FirstOrDefaultAsync(a => a.Id == id && !a.isDeleted);
        }

        public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var q = db.AccessLevels.Where(a => !a.isDeleted && a.Name == name);
            if (excludeId.HasValue) q = q.Where(a => a.Id != excludeId.Value);
            return await q.AnyAsync();
        }

        public async Task<int> CountUsersAsync(Guid accessLevelId)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.Users.CountAsync(u => u.AccessLevelId == accessLevelId);
        }

        public async Task<AccessLevel> CreateAsync(AccessLevelEditModel model, string? userName)
        {
            await _tasks.EnsureCatalogSeededAsync();
            if (await NameExistsAsync(model.Name))
                throw new InvalidOperationException("يوجد مستوى بنفس الاسم مسبقاً.");

            await using var db = await _dbFactory.CreateDbContextAsync();
            var entity = new AccessLevel
            {
                Id = Guid.NewGuid(),
                Name = model.Name.Trim(),
                Description = model.Description?.Trim(),
                SortOrder = model.SortOrder,
                IsActive = model.IsActive,
                IsGateOfficer = model.IsGateOfficer,
                ReceivesDeviceAlerts = model.ReceivesDeviceAlerts,
                createdBy = userName,
                createdOn = DateTime.Now
            };

            foreach (var taskId in model.SelectedTaskIds.Distinct())
            {
                entity.AccessLevelTasks.Add(new AccessLevelTask
                {
                    AccessLevelId = entity.Id,
                    SystemTaskId = taskId
                });
            }

            db.AccessLevels.Add(entity);
            await db.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(AccessLevelEditModel model, string? userName)
        {
            if (!model.Id.HasValue)
                throw new ArgumentException("معرف المستوى مطلوب.");

            if (await NameExistsAsync(model.Name, model.Id))
                throw new InvalidOperationException("يوجد مستوى بنفس الاسم مسبقاً.");

            await using var db = await _dbFactory.CreateDbContextAsync();
            var entity = await db.AccessLevels
                .Include(a => a.AccessLevelTasks)
                .FirstOrDefaultAsync(a => a.Id == model.Id && !a.isDeleted)
                ?? throw new InvalidOperationException("المستوى غير موجود.");

            entity.Name = model.Name.Trim();
            entity.Description = model.Description?.Trim();
            entity.SortOrder = model.SortOrder;
            entity.IsActive = model.IsActive;
            entity.IsGateOfficer = model.IsGateOfficer;
            entity.ReceivesDeviceAlerts = model.ReceivesDeviceAlerts;
            entity.updatedBy = userName;
            entity.updatedOn = DateTime.Now;

            db.AccessLevelTasks.RemoveRange(entity.AccessLevelTasks);
            entity.AccessLevelTasks.Clear();
            foreach (var taskId in model.SelectedTaskIds.Distinct())
            {
                entity.AccessLevelTasks.Add(new AccessLevelTask
                {
                    AccessLevelId = entity.Id,
                    SystemTaskId = taskId
                });
            }

            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id, string? userName)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var usersCount = await db.Users.CountAsync(u => u.AccessLevelId == id);
            if (usersCount > 0)
                throw new InvalidOperationException($"لا يمكن حذف المستوى لارتباطه بـ {usersCount} مستخدم.");

            var entity = await db.AccessLevels.FirstOrDefaultAsync(a => a.Id == id && !a.isDeleted)
                ?? throw new InvalidOperationException("المستوى غير موجود.");

            entity.isDeleted = true;
            entity.deletedBy = userName;
            entity.deletedOn = DateTime.Now;
            entity.IsActive = false;
            await db.SaveChangesAsync();
        }
    }
}
