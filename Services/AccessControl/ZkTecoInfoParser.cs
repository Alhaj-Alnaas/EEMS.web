using System.Text.RegularExpressions;

namespace Services.AccessControl
{
    /// <summary>Parses INFO payloads from /iclock/getrequest and INFO command responses.</summary>
    public static class ZkTecoInfoParser
    {
        public static bool LooksLikeInfo(string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return false;
            return body.Contains("UserCount=", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("FPCount=", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("PvCount=", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("FaceCount=", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("FingerPrintCount=", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("CardCount=", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("UserCardCount=", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("~DeviceName=", StringComparison.OrdinalIgnoreCase)
                   || body.StartsWith("Ver ", StringComparison.OrdinalIgnoreCase);
        }

        public static int? TryCount(string? body, params string[] keys)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            foreach (var key in keys)
            {
                var match = Regex.Match(
                    body,
                    $@"(?:^|[,;&\s\n\r])~?{Regex.Escape(key)}=(\d+)",
                    RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var value))
                    return value;
            }

            return null;
        }

        /// <summary>
        /// Card total from named UserCardCount/CardCount, or SpeedFace heartbeat column 11
        /// (FirmVer,UserCount,FPCount,TransactionCount,IP,...,FaceCount,flags,UserCardCount).
        /// </summary>
        public static int? TryCardCount(string? body)
        {
            var named = TryCount(body, "UserCardCount", "CardCount");
            if (named != null) return named;
            if (string.IsNullOrWhiteSpace(body) || body.Contains('=')) return null;

            var parts = body.Trim().Trim('"').Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 11 || int.TryParse(parts[0], out _))
                return null;
            return int.TryParse(parts[10], out var cards) ? cards : null;
        }
    }
}
