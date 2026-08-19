using Core.Entities;

namespace Core.Interfaces.Services.Workflow
{
    /// <summary>Phase 2: raise and read in-app notifications for users.</summary>
    public interface INotificationService
    {
        Task<List<Notification>> GetForUserAsync(string userId, bool unreadOnly = false);
        Task<int> GetUnreadCountAsync(string userId);
        Task<List<Notification>> GetRecentAsync(int take = 50, bool unreadOnly = false);
        Task NotifyAsync(string userId, string title, string body, Guid? relatedPermitId = null, Guid? relatedReaderId = null);
        Task NotifyDeviceAdminsAsync(string title, string body, Guid? relatedReaderId = null);
        Task MarkAsReadAsync(Guid notificationId);
        Task MarkAllAsReadAsync(string userId);
    }
}
