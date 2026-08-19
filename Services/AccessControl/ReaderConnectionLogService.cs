using System.Collections.Concurrent;
using Core.Interfaces.Services.AccessControl;
using Core.Models;

namespace Services.AccessControl
{
    /// <summary>
    /// Ring buffer of recent device Push connections. Singleton — survives for app lifetime.
    /// </summary>
    public class ReaderConnectionLogService : IReaderConnectionLogService
    {
        private const int MaxEntries = 200;
        private readonly ConcurrentQueue<ReaderConnectionEvent> _entries = new();

        public void Record(ReaderConnectionEvent entry)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > MaxEntries && _entries.TryDequeue(out _))
            {
            }
        }

        public IReadOnlyList<ReaderConnectionEvent> GetRecent(int take = 50)
        {
            return _entries
                .OrderByDescending(e => e.ReceivedAt)
                .Take(Math.Max(1, take))
                .ToList();
        }

        public ReaderConnectionEvent? GetLastForSerial(string deviceSerial)
        {
            if (string.IsNullOrWhiteSpace(deviceSerial)) return null;
            var sn = deviceSerial.Trim();
            return _entries
                .Where(e => string.Equals(e.DeviceSerial, sn, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.ReceivedAt)
                .FirstOrDefault();
        }

        public void Clear()
        {
            while (_entries.TryDequeue(out _))
            {
            }
        }
    }
}
