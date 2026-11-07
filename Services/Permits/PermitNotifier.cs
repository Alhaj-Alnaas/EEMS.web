using Core.Entities;
using Core.Interfaces.Services.Permits;
using Core.Interfaces.Services.Workflow;
using DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Services.Permits
{
    public class PermitNotifier : IPermitNotifier
    {
        private readonly IDbContextFactory<DataContext> _dbFactory;
        private readonly INotificationService _notifications;

        public PermitNotifier(IDbContextFactory<DataContext> dbFactory, INotificationService notifications)
        {
            _dbFactory = dbFactory;
            _notifications = notifications;
        }

        public async Task NotifyAsync(
            Permit permit,
            string title,
            string body,
            string? exceptUserId = null,
            IEnumerable<string>? alsoUserIds = null)
        {
            if (permit == null || string.IsNullOrWhiteSpace(title))
                return;

            var recipients = new HashSet<string>(StringComparer.Ordinal);
            await using var db = await _dbFactory.CreateDbContextAsync();

            if (!string.IsNullOrWhiteSpace(permit.createdBy))
            {
                var creatorId = await db.Users.AsNoTracking()
                    .Where(u => u.UserName == permit.createdBy)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync();
                if (!string.IsNullOrEmpty(creatorId))
                    recipients.Add(creatorId);
            }

            var approverIds = await db.PermitApprovals.AsNoTracking()
                .Where(a => a.PermitId == permit.Id && a.ApproverUserId != "")
                .Select(a => a.ApproverUserId)
                .Distinct()
                .ToListAsync();
            foreach (var approverId in approverIds)
            {
                if (!string.IsNullOrWhiteSpace(approverId))
                    recipients.Add(approverId);
            }

            if (!string.IsNullOrEmpty(exceptUserId))
                recipients.Remove(exceptUserId);

            if (alsoUserIds != null)
            {
                foreach (var userId in alsoUserIds)
                {
                    if (!string.IsNullOrWhiteSpace(userId))
                        recipients.Add(userId);
                }
            }

            foreach (var userId in recipients)
                await _notifications.NotifyAsync(userId, title, body, permit.Id);
        }
    }
}
