using Core.Entities;
using Core.Interfaces.Services.Workflow;
using Core.Interfaces.UnitOfWork;
using DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Services.Workflow
{
    public class NotificationService : INotificationService
    {
        private static readonly string[] DeviceAdminUserTypes = { "Admin", "SaftyUser" };

        private readonly IUnitOfWork _unitOfWork;
        private readonly IDbContextFactory<DataContext> _dbFactory;

        public NotificationService(IUnitOfWork unitOfWork, IDbContextFactory<DataContext> dbFactory)
        {
            _unitOfWork = unitOfWork;
            _dbFactory = dbFactory;
        }

        public async Task<List<Notification>> GetForUserAsync(string userId, bool unreadOnly = false)
        {
            var query = _unitOfWork.Notifications.GetQueryable()
                .Include(n => n.RelatedReader)
                .Where(n => n.UserId == userId && !n.isDeleted);
            if (unreadOnly) query = query.Where(n => !n.IsRead);
            return await query.OrderByDescending(n => n.createdOn).ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _unitOfWork.Notifications.GetQueryable()
                .CountAsync(n => n.UserId == userId && !n.IsRead && !n.isDeleted);
        }

        public async Task<List<Notification>> GetRecentAsync(int take = 50, bool unreadOnly = false)
        {
            var query = _unitOfWork.Notifications.GetQueryable()
                .Include(n => n.RelatedReader)
                .Where(n => !n.isDeleted);
            if (unreadOnly) query = query.Where(n => !n.IsRead);
            return await query.OrderByDescending(n => n.createdOn).Take(take).ToListAsync();
        }

        public async Task NotifyAsync(
            string userId,
            string title,
            string body,
            Guid? relatedPermitId = null,
            Guid? relatedReaderId = null)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Body = body,
                RelatedPermitId = relatedPermitId,
                RelatedReaderId = relatedReaderId,
                createdOn = DateTime.Now
            };

            _unitOfWork.Notifications.Insert(notification);
            await _unitOfWork.SaveAsync();
        }

        public async Task NotifyDeviceAdminsAsync(string title, string body, Guid? relatedReaderId = null)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var adminIds = await db.Users
                .Where(u => DeviceAdminUserTypes.Contains(u.UserType))
                .Select(u => u.Id)
                .ToListAsync();

            if (adminIds.Count == 0)
            {
                adminIds = await db.Users.Select(u => u.Id).Take(20).ToListAsync();
            }

            foreach (var userId in adminIds.Distinct())
            {
                await NotifyAsync(userId, title, body, relatedPermitId: null, relatedReaderId: relatedReaderId);
            }
        }

        public async Task MarkAsReadAsync(Guid notificationId)
        {
            var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId);
            if (notification == null) return;
            notification.IsRead = true;
            notification.updatedOn = DateTime.Now;
            _unitOfWork.Notifications.Update(notification);
            await _unitOfWork.SaveAsync();
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var items = await _unitOfWork.Notifications.GetQueryable()
                .Where(n => n.UserId == userId && !n.IsRead && !n.isDeleted)
                .ToListAsync();
            foreach (var n in items)
            {
                n.IsRead = true;
                n.updatedOn = DateTime.Now;
                _unitOfWork.Notifications.Update(n);
            }
            if (items.Count > 0)
                await _unitOfWork.SaveAsync();
        }
    }
}
