namespace Services.AccessControl
{
    public sealed record ZkTecoUserInfoEntry(
        string Pin,
        string Name,
        string? DeviceName,
        string? Card,
        string? Uid,
        string RawLine);

    /// <summary>
    /// Extracts user records from ZKTeco ADMS pushes (USERINFO table or USER lines inside OPERLOG).
    /// SpeedFace acc firmware sends: <c>user uid=1	cardno=	pin=1	name=	privilege=0</c>
    /// </summary>
    public static class ZkTecoUserInfoParser
    {
        public static IReadOnlyList<ZkTecoUserInfoEntry> Parse(string? body, string? table = null)
        {
            if (string.IsNullOrWhiteSpace(body)) return Array.Empty<ZkTecoUserInfoEntry>();

            var list = new List<ZkTecoUserInfoEntry>();
            foreach (var raw in body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;

                var fields = ZkTecoRecordLine.ParseFields(line, out var recordType);

                if (fields.ContainsKey("TMP") || fields.ContainsKey("Tmp")) continue;
                if (recordType is "FP" or "FINGERTMP" or "BIODATA" or "FACE") continue;
                if (fields.ContainsKey("Type") && fields.TryGetValue("Type", out var typeRaw)
                    && int.TryParse(typeRaw, out var bioType) && bioType is >= 1 and <= 10)
                    continue;

                if (!TryField(fields, out var pin, "PIN", "Pin") || string.IsNullOrWhiteSpace(pin))
                    continue;

                TryField(fields, out var deviceName, "Name");
                TryField(fields, out var card, "Card", "CardNo", "CardNumber");
                TryField(fields, out var uid, "UID", "Uid");

                var hasName = !string.IsNullOrWhiteSpace(deviceName);
                var hasCard = !string.IsNullOrWhiteSpace(card);
                var hasPrivilege = fields.ContainsKey("Privilege") || fields.ContainsKey("Pri");
                var hasUid = !string.IsNullOrWhiteSpace(uid);

                var isUserTable = string.Equals(table, "USERINFO", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(table, "USER", StringComparison.OrdinalIgnoreCase);
                var isUserRecord = isUserTable
                    || recordType is "USER" or "USERINFO"
                    || hasName || hasCard || hasPrivilege || hasUid;
                if (!isUserRecord) continue;

                var pinTrim = pin.Trim();
                list.Add(new ZkTecoUserInfoEntry(
                    Pin: pinTrim,
                    Name: hasName ? deviceName!.Trim() : pinTrim,
                    DeviceName: hasName ? deviceName!.Trim() : null,
                    Card: hasCard ? card!.Trim() : null,
                    Uid: hasUid ? uid!.Trim() : null,
                    RawLine: line));
            }

            return list;
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
    }
}
