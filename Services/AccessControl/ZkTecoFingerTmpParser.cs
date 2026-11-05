namespace Services.AccessControl
{
    public sealed record ZkTecoFingerTmpEntry(
        string Pin,
        int FingerIndex,
        bool Valid,
        string TemplateBase64,
        int BioType,
        string RawLine,
        int? MajorVer = null,
        int? MinorVer = null,
        int? Format = null);

    /// <summary>
    /// Extracts biometric templates from ZKTeco ADMS pushes (BIODATA / FINGERTMP / templatev10).
    /// SpeedFace biodata Type codes: 1=fingerprint, 7=palm, 9=visible-light face.
    /// </summary>
    public static class ZkTecoFingerTmpParser
    {
        public static IReadOnlyList<ZkTecoFingerTmpEntry> Parse(string? body, string? table = null)
        {
            if (string.IsNullOrWhiteSpace(body)) return Array.Empty<ZkTecoFingerTmpEntry>();

            var isBioTable = string.Equals(table, "BIODATA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(table, "FINGERTMP", StringComparison.OrdinalIgnoreCase)
                || string.Equals(table, "templatev10", StringComparison.OrdinalIgnoreCase)
                || string.Equals(table, "BIOPHOTO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(table, "tabledata", StringComparison.OrdinalIgnoreCase);

            var list = new List<ZkTecoFingerTmpEntry>();
            foreach (var raw in body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;

                var fields = ZkTecoRecordLine.ParseFields(line, out var recordType);

                if (recordType is "USER" or "USERINFO" or "ATTLOG" or "OPLOG")
                    continue;

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

                // Default: fingerprint if no Type (classic FINGERTMP / templatev10).
                var bioType = 1;
                if (TryGet(fields, out var typeRaw, "Type") && int.TryParse(typeRaw, out var parsedType))
                    bioType = parsedType;

                int? major = TryInt(fields, "MajorVer");
                int? minor = TryInt(fields, "MinorVer");
                int? format = TryInt(fields, "Format");

                list.Add(new ZkTecoFingerTmpEntry(
                    Pin: pin!.Trim(),
                    FingerIndex: fingerIndex,
                    Valid: valid,
                    TemplateBase64: tmp!.Trim(),
                    BioType: bioType,
                    RawLine: line,
                    MajorVer: major,
                    MinorVer: minor,
                    Format: format));
            }

            return list;
        }

        /// <summary>Hand-related: fingerprint / finger-vein / palm.</summary>
        public static bool IsHandBioType(int bioType) => bioType is 1 or 2 or 7 or 10;

        /// <summary>Palm print / palm vein (SpeedFace M4 hand biometric).</summary>
        public static bool IsPalmBioType(int bioType) => bioType is 7 or 10;

        /// <summary>Face-related templates.</summary>
        public static bool IsFaceBioType(int bioType) => bioType is 3 or 8 or 9;

        /// <summary>Count enrolled users (distinct PIN) per bio category in a biodata payload.</summary>
        public static (int FaceUsers, int PalmUsers) CountDistinctEnrollments(string? body, string? table = null)
        {
            var entries = Parse(body, table);
            if (entries.Count == 0) return (0, 0);

            var faceUsers = entries
                .Where(e => e.Valid && IsFaceBioType(e.BioType))
                .Select(e => e.Pin)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var palmUsers = entries
                .Where(e => e.Valid && IsPalmBioType(e.BioType))
                .Select(e => e.Pin)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            return (faceUsers, palmUsers);
        }

        private static int? TryInt(Dictionary<string, string> fields, string key)
        {
            if (!TryGet(fields, out var raw, key) || !int.TryParse(raw, out var value))
                return null;
            return value;
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
