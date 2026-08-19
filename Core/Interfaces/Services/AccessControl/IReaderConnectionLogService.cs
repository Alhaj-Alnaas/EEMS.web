using Core.Models;

namespace Core.Interfaces.Services.AccessControl
{
    /// <summary>In-memory log of recent /iclock requests (dev/diagnostic).</summary>
    public interface IReaderConnectionLogService
    {
        void Record(ReaderConnectionEvent entry);
        IReadOnlyList<ReaderConnectionEvent> GetRecent(int take = 50);
        ReaderConnectionEvent? GetLastForSerial(string deviceSerial);
        void Clear();
    }
}
