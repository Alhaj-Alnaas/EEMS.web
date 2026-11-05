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
    /// Empty <c>name</c> is normal — PIN is used as display name.
    /// </summary>
    public static class ZkTecoUserInfoParser
    {
        public static IReadOnlyList<ZkTecoUserInfoEntry> Parse(string? body, string? table = null)
        {
            if (string.IsNullOrWhiteSpace(body)) return Array.Empty<ZkTecoUserInfoEntry>();

            var isUserTable = IsUserTableName(table);
            var list = new List<ZkTecoUserInfoEntry>();
            foreach (var raw in body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim().Trim('\ufeff');
                if (line.Length == 0) continue;

                var fields = ZkTecoRecordLine.ParseFields(line, out var recordType);

                // Templates / bio payloads — never treat as users.
                if (fields.ContainsKey("TMP") || fields.ContainsKey("Tmp") || fields.ContainsKey("Template"))
                    continue;
                if (recordType is "FP" or "FINGERTMP" or "BIODATA" or "FACE" or "BIOPHOTO" or "USERPIC")
                    continue;

                // Skip bio rows that carry Type=1..10 but are NOT user records
                // (do not skip when recordType/table clearly says USER).
                var isExplicitUser = isUserTable || recordType is "USER" or "USERINFO";
                if (!isExplicitUser
                    && fields.ContainsKey("Type")
                    && fields.TryGetValue("Type", out var typeRaw)
                    && int.TryParse(typeRaw, out var bioType)
                    && bioType is >= 1 and <= 10)
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

                // SpeedFace user lines often have empty name — uid/privilege/table are enough.
                var isUserRecord = isExplicitUser
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

        /// <summary>Every Acc <c>user uid=</c> slot, including blank PIN rows (needed for next-uid).</summary>
        public static IReadOnlyList<(int Uid, string Pin)> ParseAccSlots(string? body)
        {
            var list = new List<(int Uid, string Pin)>();
            if (string.IsNullOrWhiteSpace(body)) return list;

            foreach (var raw in body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = ZkTecoRecordLine.ParseFields(raw.Trim(), out _);
                if (!TryField(fields, out var uidRaw, "UID", "Uid") || string.IsNullOrWhiteSpace(uidRaw))
                    continue;
                if (!int.TryParse(uidRaw, out var uid) || uid <= 0)
                    continue;

                TryField(fields, out var pin, "PIN", "Pin");
                list.Add((uid, (pin ?? "").Trim()));
            }

            return list;
        }

        public static bool IsUserTableName(string? table)
        {
            if (string.IsNullOrWhiteSpace(table)) return false;
            var t = table.Trim();
            return t.Equals("USERINFO", StringComparison.OrdinalIgnoreCase)
                || t.Equals("USER", StringComparison.OrdinalIgnoreCase)
                || t.Equals("tabledata", StringComparison.OrdinalIgnoreCase);
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
