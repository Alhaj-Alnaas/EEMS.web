using System.Text.RegularExpressions;

namespace Services.AccessControl
{
    /// <summary>
    /// A face may be copied when it is the binary template the reader stored.
    /// These SpeedFace units enroll a complete Type=9 MajorVer=39 template of 272 bytes
    /// (header 6A950110, length field equals the buffer). A base64 string stored as text
    /// (bytes start with AAAA) is not a template and must not be copied.
    /// </summary>
    public static class FaceTemplatePolicy
    {
        public static bool IsDoorFace(byte[]? data, string? remarks)
        {
            if (data == null || data.Length < 16 || IsStoredBase64Text(data))
                return false;
            if (!TryBioType(remarks, out var bioType) || bioType is not (3 or 8 or 9))
                return false;
            return HasMatchingTemplateLength(data);
        }

        /// <summary>ZK face header 6A950110, bytes 4–5 little-endian = full template length.</summary>
        public static bool HasMatchingTemplateLength(byte[] data)
        {
            if (data.Length < 8)
                return false;
            if (data[0] != 0x6A || data[1] != 0x95 || data[2] != 0x01 || data[3] != 0x10)
                return false;
            var declared = data[4] | (data[5] << 8);
            return declared == data.Length;
        }

        public static bool IsStoredBase64Text(byte[] data)
        {
            var sample = Math.Min(data.Length, 48);
            if (sample < 16)
                return false;
            for (var i = 0; i < sample; i++)
            {
                var b = data[i];
                var base64Char = b is (>= (byte)'A' and <= (byte)'Z')
                    or (>= (byte)'a' and <= (byte)'z')
                    or (>= (byte)'0' and <= (byte)'9')
                    or (byte)'+' or (byte)'/' or (byte)'=';
                if (!base64Char)
                    return false;
            }
            return true;
        }

        public static bool HasConfirmation(string? remarks, string serial)
        {
            if (string.IsNullOrWhiteSpace(remarks) || string.IsNullOrWhiteSpace(serial))
                return false;
            var match = Regex.Match(remarks, @"Confirmed=([A-Za-z0-9,]+)", RegexOptions.IgnoreCase);
            if (!match.Success)
                return false;
            var wanted = serial.Trim();
            return match.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Any(s => s.Equals(wanted, StringComparison.OrdinalIgnoreCase));
        }

        public static string WithConfirmation(string? remarks, string serial)
        {
            serial = serial.Trim();
            var text = remarks?.Trim() ?? "";
            var match = Regex.Match(text, @"Confirmed=([A-Za-z0-9,]+)", RegexOptions.IgnoreCase);
            if (!match.Success)
                return string.IsNullOrEmpty(text) ? $"Confirmed={serial}" : text + $"; Confirmed={serial}";

            var list = match.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (list.Any(s => s.Equals(serial, StringComparison.OrdinalIgnoreCase)))
                return text;
            list.Add(serial);
            return text[..match.Index] + "Confirmed=" + string.Join(',', list) + text[(match.Index + match.Length)..];
        }

        public static string WithoutConfirmations(string? remarks, ISet<string> serials)
        {
            if (string.IsNullOrWhiteSpace(remarks))
                return remarks ?? "";
            var match = Regex.Match(remarks, @"Confirmed=([A-Za-z0-9,]+)", RegexOptions.IgnoreCase);
            if (!match.Success)
                return remarks;

            var kept = match.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Where(s => !serials.Contains(s))
                .ToList();
            var replacement = kept.Count == 0 ? "" : "Confirmed=" + string.Join(',', kept);
            var text = remarks.Remove(match.Index, match.Length).Insert(match.Index, replacement);
            return text.Replace(";;", ";", StringComparison.Ordinal).Trim(' ', ';');
        }

        private static bool TryBioType(string? remarks, out int bioType)
        {
            bioType = 0;
            var value = TryInt(remarks, "BioType");
            if (value is not int parsed)
                return false;
            bioType = parsed;
            return true;
        }

        private static int? TryInt(string? remarks, string key)
        {
            if (string.IsNullOrWhiteSpace(remarks))
                return null;
            var match = Regex.Match(remarks, key + @"=(\d+)", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : null;
        }
    }
}
