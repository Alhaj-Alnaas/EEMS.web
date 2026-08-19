using Core.Models;
using System.Globalization;

namespace Services.AccessControl
{
    internal static class ZkTecoAttLogParser
    {
        private static readonly string[] TimeFormats =
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy/MM/dd HH:mm",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm"
        };

        public static List<ZkAttLogEntry> Parse(string body)
        {
            var results = new List<ZkAttLogEntry>();
            if (string.IsNullOrWhiteSpace(body)) return results;

            foreach (var raw in body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("USER", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("FP", StringComparison.OrdinalIgnoreCase)) continue;

                var entry = ParseLine(line);
                if (entry != null)
                    results.Add(entry);
            }

            return results;
        }

        private static ZkAttLogEntry? ParseLine(string line)
        {
            var parts = line.Contains('\t')
                ? line.Split('\t')
                : line.Split(',', StringSplitOptions.TrimEntries);

            if (parts.Length < 2) return null;

            var pin = parts[0].Trim();
            if (string.IsNullOrEmpty(pin)) return null;

            if (!TryParseTime(parts[1].Trim(), out var eventTime))
                eventTime = DateTime.Now;

            _ = int.TryParse(parts.Length > 2 ? parts[2] : "0", out var status);
            _ = int.TryParse(parts.Length > 3 ? parts[3] : "0", out var verify);

            return new ZkAttLogEntry
            {
                Pin = pin,
                EventTime = eventTime,
                Status = status,
                VerifyMode = verify,
                RawLine = line
            };
        }

        private static bool TryParseTime(string value, out DateTime dt)
        {
            return DateTime.TryParseExact(
                       value,
                       TimeFormats,
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.AssumeLocal,
                       out dt)
                   || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt);
        }
    }
}
