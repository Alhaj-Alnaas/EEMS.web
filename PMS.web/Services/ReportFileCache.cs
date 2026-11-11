using System.Collections.Concurrent;

namespace PMS.web.Reporting
{
    public interface IReportFileCache
    {
        string Put(byte[] bytes);
        bool TryGet(string id, out byte[] bytes);
    }

    public sealed class ReportFileCache : IReportFileCache
    {
        private readonly ConcurrentDictionary<string, Entry> _items = new();

        public string Put(byte[] bytes)
        {
            var now = DateTime.UtcNow;
            foreach (var item in _items)
            {
                if (item.Value.Expires <= now)
                    _items.TryRemove(item.Key, out _);
            }

            var id = Guid.NewGuid().ToString("N");
            _items[id] = new Entry(bytes, now.AddMinutes(5));
            return id;
        }

        public bool TryGet(string id, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (!_items.TryGetValue(id, out var entry) || entry.Expires <= DateTime.UtcNow)
                return false;
            bytes = entry.Bytes;
            return true;
        }

        private sealed record Entry(byte[] Bytes, DateTime Expires);
    }
}
