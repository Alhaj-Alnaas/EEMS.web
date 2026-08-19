namespace Services.AccessControl
{
    /// <summary>
    /// Helpers for ZKTeco ADMS record lines such as:
    ///   USER PIN=1\tName=Ali\tPri=0\tCard=123
    ///   FP PIN=1\tFID=0\tValid=1\tTMP=&lt;base64&gt;
    ///   BIODATA Pin=1\tNo=0\tValid=1\tTmp=&lt;base64&gt;
    /// The leading record-type token must be stripped before key=value parsing.
    /// </summary>
    internal static class ZkTecoRecordLine
    {
        private static readonly string[] KnownPrefixes =
        {
            "USERINFO", "USERPIC", "USER", "FINGERTMP", "BIODATA", "BIOPHOTO",
            "FACE", "FP", "OPLOG", "OPERLOG", "ATTLOG", "TABLE"
        };

        /// <summary>Parses tab/space separated key=value pairs, ignoring any record-type prefix.</summary>
        public static Dictionary<string, string> ParseFields(string line, out string? recordType)
        {
            recordType = null;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(line)) return dict;

            var work = line.Trim();

            foreach (var prefix in KnownPrefixes)
            {
                if (work.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase)
                    || work.StartsWith(prefix + "\t", StringComparison.OrdinalIgnoreCase))
                {
                    recordType = prefix;
                    work = work[prefix.Length..].TrimStart();
                    break;
                }
            }

            var tokens = work.Contains('\t')
                ? work.Split('\t', StringSplitOptions.RemoveEmptyEntries)
                : work.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var token in tokens)
            {
                var idx = token.IndexOf('=');
                if (idx <= 0) continue;

                var key = token[..idx].Trim();
                var value = token[(idx + 1)..].Trim();

                // Guard against an unlisted prefix glued to the first key ("XYZ PIN").
                var space = key.LastIndexOf(' ');
                if (space >= 0)
                {
                    recordType ??= key[..space].Trim();
                    key = key[(space + 1)..].Trim();
                }

                if (key.Length > 0)
                    dict[key] = value;
            }

            return dict;
        }
    }
}
