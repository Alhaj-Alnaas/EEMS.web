using Core.Entities;

namespace Core.Interfaces.Services.Permits
{
    /// <summary>
    /// Tells the permit preparer and the approval parties that the status changed.
    /// </summary>
    public interface IPermitNotifier
    {
        Task NotifyAsync(
            Permit permit,
            string title,
            string body,
            string? exceptUserId = null,
            IEnumerable<string>? alsoUserIds = null);
    }
}
