using Core.Models;

namespace Core.Interfaces.Services.AccessControl
{
    /// <summary>Keeps the last raw device data pushes so imports can be diagnosed from the UI.</summary>
    public interface IDevicePushLogService
    {
        void Record(DevicePushEvent entry);
        IReadOnlyList<DevicePushEvent> GetRecent(int take = 30);
        void Clear();
    }
}
