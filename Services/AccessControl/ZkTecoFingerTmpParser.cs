namespace Services.AccessControl
{
    public sealed record ZkTecoFingerTmpEntry(
        string Pin,
        int FingerIndex,
        bool Valid,
        string TemplateBase64,
        string RawLine);

    /// <summary>
    /// Extracts biometric templates from ZKTeco ADMS pushes:
    /// FINGERTMP / BIODATA tables, or FP / BIODATA lines inside OPERLOG.
    /// </summary>
    public static class ZkTecoFingerTmpParser
    {
        public static IReadOnlyList<ZkTecoFingerTmpEntry> Parse(string? body, string? table = null)
        {
            if (string.IsNullOrWhiteSpace(body)) return Array.Empty<ZkTecoFingerTmpEntry>();

            var isBioTable = string.Equals(table, "BIODATA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(table, "FINGERTMP", StringComparison.OrdinalIgnoreCase)
                || string.Equals(table, "templatev10", StringComparison.OrdinalIgnoreCase);

            var list = new List<ZkTecoFingerTmpEntry>();
            foreach (var raw in body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;

                var fields = ZkTecoRecordLine.ParseFields(line, out _);

                if (!TryGet(fields, out var pin, "PIN", "Pin") || string.IsNullOrWhiteSpace(pin))
                {
                    if (!isBioTable) continue;
                    continue;
                }
                if (!TryGet(fields, out var tmp, "TMP", "Tmp", "Template") || string.IsNullOrWhiteSpace(tmp))
                    continue;

                var fingerIndex = 0;
                if (TryGet(fields, out var fidRaw, "FID", "No", "Index", "FingerID"))
                    int.TryParse(fidRaw, out fingerIndex);

                var valid = true;
                if (TryGet(fields, out var validRaw, "Valid") && int.TryParse(validRaw, out var v))
                    valid = v != 0;

                list.Add(new ZkTecoFingerTmpEntry(
                    Pin: pin!.Trim(),
                    FingerIndex: fingerIndex,
                    Valid: valid,
                    TemplateBase64: tmp!.Trim(),
                    RawLine: line));
            }

            return list;
        }

        private static bool TryGet(Dictionary<string, string> fields, out string? value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (fields.TryGetValue(key, out var found))
                {
                    value = found;
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
