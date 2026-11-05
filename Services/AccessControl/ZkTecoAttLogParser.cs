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
                var line = raw.Trim().Trim('\ufeff');
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
            // Acc Push / SpeedFace: transaction index=..\tpin=..\ttime_second=..\tinoutstate=..
            // or ATTLOG: time=...\tpin=1112\tinoutstatus=0\t...
            if (line.Contains('='))
            {
                var fields = ZkTecoRecordLine.ParseFields(line, out _);
                if (!TryField(fields, out var pin, "PIN", "Pin") || string.IsNullOrWhiteSpace(pin))
                    return null;

                pin = pin.Trim();
                if (pin is "0" or "00")
                    return null;

                if (!TryResolveEventTime(fields, out var eventTime))
                    return null;

                var statusRaw = FirstField(fields,
                    "InOutStatus", "inoutstatus", "InOutState", "inoutstate",
                    "Status", "status") ?? "0";
                _ = int.TryParse(statusRaw, out var status);

                var verifyRaw = FirstField(fields,
                    "VerifyMode", "verifymode", "Verified", "verified",
                    "Verify", "verify", "ver") ?? "0";
                _ = int.TryParse(verifyRaw, out var verify);

                int? deviceIndex = null;
                if (TryField(fields, out var indexRaw, "Index", "index")
                    && int.TryParse(indexRaw!.Trim(), out var idx)
                    && idx >= 0)
                    deviceIndex = idx;

                string? cardNo = null;
                if (TryField(fields, out var cardRaw, "CardNo", "cardno", "Card", "card")
                    && !string.IsNullOrWhiteSpace(cardRaw)
                    && cardRaw.Trim() is not ("0" or "00"))
                    cardNo = cardRaw.Trim();

                return new ZkAttLogEntry
                {
                    Pin = pin,
                    EventTime = eventTime,
                    Status = status,
                    VerifyMode = verify,
                    DeviceIndex = deviceIndex,
                    CardNo = cardNo,
                    RawLine = line
                };
            }

            var parts = line.Contains('\t')
                ? line.Split('\t')
                : line.Split(',', StringSplitOptions.TrimEntries);

            if (parts.Length < 2) return null;

            var legacyPin = parts[0].Trim();
            if (string.IsNullOrEmpty(legacyPin) || legacyPin is "0" or "00")
                return null;
            if (legacyPin.Contains('='))
                return null;

            if (!TryParseTime(parts[1].Trim(), out var eventTimeLegacy))
                return null;

            _ = int.TryParse(parts.Length > 2 ? parts[2] : "0", out var statusLegacy);
            _ = int.TryParse(parts.Length > 3 ? parts[3] : "0", out var verifyLegacy);

            return new ZkAttLogEntry
            {
                Pin = legacyPin,
                EventTime = TruncateToSeconds(eventTimeLegacy),
                Status = statusLegacy,
                VerifyMode = verifyLegacy,
                RawLine = line
            };
        }

        private static bool TryResolveEventTime(Dictionary<string, string> fields, out DateTime eventTime)
        {
            if (TryField(fields, out var accRaw, "time_second", "Time_Second", "TimeSecond")
                && ZkTecoAccTime.TryDecode(accRaw, out eventTime))
            {
                eventTime = TruncateToSeconds(eventTime);
                return true;
            }

            if (TryField(fields, out var timeRaw, "Time", "time", "DateTime", "datetime")
                && TryParseTime(timeRaw!.Trim(), out var parsed))
            {
                eventTime = TruncateToSeconds(parsed);
                return true;
            }

            eventTime = default;
            return false;
        }

        private static bool TryField(Dictionary<string, string> fields, out string? value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (fields.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
                {
                    value = raw;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static string? FirstField(Dictionary<string, string> fields, params string[] keys)
            => TryField(fields, out var value, keys) ? value : null;

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

        private static DateTime TruncateToSeconds(DateTime dt)
            => new(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Kind);
    }
}
