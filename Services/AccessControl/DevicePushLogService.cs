using System.Collections.Concurrent;
using Core.Interfaces.Services.AccessControl;
using Core.Models;

namespace Services.AccessControl
{
    /// <summary>Ring buffer of raw device pushes. Singleton — cleared on app restart.</summary>
    public class DevicePushLogService : IDevicePushLogService
    {
        private const int MaxEntries = 100;
        private readonly ConcurrentQueue<DevicePushEvent> _entries = new();

        public void Record(DevicePushEvent entry)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > MaxEntries && _entries.TryDequeue(out _))
            {
            }
        }

        public IReadOnlyList<DevicePushEvent> GetRecent(int take = 30)
        {
            return _entries
                .OrderByDescending(e => e.ReceivedAt)
                .Take(Math.Max(1, take))
                .ToList();
        }

        public void Clear()
        {
            while (_entries.TryDequeue(out _))
            {
            }
        }
    }
}
