using System.Text.RegularExpressions;

namespace Services.AccessControl
{
    /// <summary>
    /// SpeedFace / Acc push user commands.
    /// SpeedFace Acc user lines match device-created rows:
    /// <c>user uid=… pin=… name= (empty) privilege=0 disable=0 verify=0</c>.
    /// Never send attendance USERINFO — it creates blank Acc slots with a default palm.
    /// </summary>
    public static class ZkTecoAccUserCommand
    {
        private static readonly Regex UidInRemarks = new(@"ZkUid=(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ReadersInRemarks = new(@"ZkReaders=([^\s;]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string QueryAllUsers() => "DATA QUERY tablename=user,fielddesc=*";

        public static string QueryByPin(string pin)
            => $"DATA QUERY tablename=user,fielddesc=*,filter=pin={SanitizeToken(pin)}";

        public const string WaitUidPrefix = "WAIT_UID";

        public static string WaitForDeviceUid(string pin, string disable, Guid employeeId)
            => $"{WaitUidPrefix}\tpin={SanitizeToken(pin)}\tdisable={(disable == "1" ? "1" : "0")}\temployeeId={employeeId:D}";

        public static bool IsWaitForUid(string? payload)
            => !string.IsNullOrWhiteSpace(payload)
               && payload.StartsWith(WaitUidPrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// SpeedFace M4 Access Control Role (manual):
        /// 1) current time in a valid user/group time period
        /// 2) user's Access Group is in the door unlock combination
        /// Default combo is group 1. Privilege / IsAuthorize are not the door gate.
        /// Enable: Group=1 (in combo). Disable: Group=99 (not in default combo).
        /// TZ stays 0000000100000000 so Time Rule [1] is not used as the switch
        /// (zeros are ignored; changing TZ did not uncheck Rule [1] on this unit).
        /// </summary>
        public static string AccUserLine(string pin, string? name, string disable, int? uid = null)
        {
            pin = SanitizeToken(pin);
            name = SanitizeName(name);

            if (string.IsNullOrWhiteSpace(pin))
                throw new ArgumentException("PIN is required to enroll a user.");

            // Do not send CardNo= — an empty CardNo= on UPDATE clears the badge on M4.
            // uid is sent only when creating a missing user. UPDATE by Pin= alone
            // returns -1004 on this firmware when the PIN is not on the device.
            var group = disable == "1" ? "99" : "1";
            var uidPart = uid is > 0 ? $"uid={uid.Value}\t" : "";

            return "DATA UPDATE user " + uidPart +
                   $"Pin={pin}\tPassword=\tGroup={group}\tStartTime=0\tEndTime=0\t" +
                   $"Name={name}\tPrivilege=0\tTZ=0000000100000000";
        }

        /// <summary>
        /// ZAM180-NF upsert. Adds the PIN when missing and updates the same PIN when present.
        /// <c>DATA UPDATE user</c> on this firmware only edits an existing row (<c>-1004</c> otherwise).
        /// </summary>
        public static string UserInfoUpsertLine(string pin, string? name, string? card = null)
        {
            pin = SanitizeToken(pin);
            name = SanitizeName(name);
            card = SanitizeToken(card);
            if (string.IsNullOrWhiteSpace(pin))
                throw new ArgumentException("PIN is required to enroll a user.");

            var cardPart = string.IsNullOrWhiteSpace(card) ? "" : $"\tCard={card}";
            return $"DATA UPDATE USERINFO PIN={pin}\tName={name}\tPrivilege=0{cardPart}";
        }

        /// <summary>
        /// Push one stored face, palm, or finger template.
        /// The same Pin + No + Type updates that slot instead of adding a second copy.
        /// </summary>
        public static string BiodataUpdateLine(
            string pin,
            int fingerIndex,
            int bioType,
            string templateBase64,
            int? majorVer = null,
            int? minorVer = null,
            int? format = null)
        {
            pin = SanitizeToken(pin);
            if (string.IsNullOrWhiteSpace(pin))
                throw new ArgumentException("PIN is required.");
            if (string.IsNullOrWhiteSpace(templateBase64))
                throw new ArgumentException("Template is required.");

            var (defaultMajor, defaultMinor) = bioType switch
            {
                9 or 8 or 3 => (58, 10),
                7 or 10 => (12, 0),
                _ => (10, 0)
            };
            var major = majorVer ?? defaultMajor;
            var minor = minorVer ?? defaultMinor;
            var fmt = format ?? 0;

            return "UPDATE biodata " +
                   $"Pin={pin}\tNo={fingerIndex}\tIndex={fingerIndex}\tValid=1\tDuress=0\tType={bioType}\t" +
                   $"MajorVer={major}\tMinorVer={minor}\tFormat={fmt}\tTmp={templateBase64.Trim()}";
        }

        public static bool IsCreateUserLine(string? payload)
            => !string.IsNullOrWhiteSpace(payload)
               && Regex.IsMatch(payload, @"\buid=\d+", RegexOptions.IgnoreCase)
               && Regex.IsMatch(payload, @"\bPin=\S+", RegexOptions.IgnoreCase);

        /// <summary>
        /// Security PUSH door right. Always UPDATE — never DELETE userauthorize.
        /// AuthorizeDoorId is a door bitmask (1 = first door).
        /// EndTime=1 → expired window (deny). EndTime=0 → no expiry (allow).
        /// </summary>
        public static string AccAuthorizeLine(string pin, bool enabled)
        {
            pin = SanitizeToken(pin);
            if (string.IsNullOrWhiteSpace(pin))
                throw new ArgumentException("PIN is required.");
            var endTime = enabled ? "0" : "1";
            return "DATA UPDATE userauthorize " +
                   $"Pin={pin}\tAuthorizeTimezoneId=1\tAuthorizeDoorId=1\tStartTime=0\tEndTime={endTime}";
        }

        public static string AccQueryAuthorizeLine(string pin)
            => $"DATA QUERY tablename=userauthorize,fielddesc=*,filter=pin={SanitizeToken(pin)}";

        /// <summary>
        /// Recreate Time Rules after DELETE TIMEZONE wiped them on this M4.
        /// UPDATE only — never DELETE TIMEZONE.
        /// </summary>
        public static IReadOnlyList<string> AccRestoreTimeRuleLines()
        {
            var time = string.Concat(Enumerable.Repeat("00002359", 7));
            var lines = new List<string>(24);
            for (var id = 1; id <= 24; id++)
                lines.Add($"DATA UPDATE TIMEZONE TZID={id}\tTIME={time}");
            return lines;
        }

        public static string Update(string pin, string name, string disable, string uid)
            => AccUserLine(pin, name, disable);

        /// <summary>Keep Arabic/letters; strip separators that would break Acc field parsing.</summary>
        private static string SanitizeName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var cleaned = value.Trim()
                .Replace("\t", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("=", " ");
            var chars = cleaned.Where(c => !char.IsControl(c)).ToArray();
            cleaned = new string(chars).Trim();
            if (cleaned.Length > 24)
                cleaned = cleaned[..24].Trim();
            return cleaned;
        }

        private static string SanitizeToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var chars = value.Trim()
                .Replace("\t", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("=", " ")
                .ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (char.IsControl(chars[i]))
                    chars[i] = ' ';
            }
            return new string(chars).Trim();
        }

        public static string? TryGetUid(string? remarks)
        {
            if (string.IsNullOrWhiteSpace(remarks)) return null;
            var m = UidInRemarks.Match(remarks);
            return m.Success ? m.Groups[1].Value : null;
        }

        public static string UpsertUid(string? remarks, string uid)
        {
            uid = new string(uid.Where(char.IsDigit).ToArray());
            var token = "ZkUid=" + uid;
            if (string.IsNullOrWhiteSpace(remarks))
                return token;
            if (UidInRemarks.IsMatch(remarks))
                return UidInRemarks.Replace(remarks, token);
            return remarks.TrimEnd(' ', ';') + "; " + token;
        }

        public static IReadOnlyList<string> TryGetEnrolledSerials(string? remarks)
        {
            if (string.IsNullOrWhiteSpace(remarks)) return Array.Empty<string>();
            var m = ReadersInRemarks.Match(remarks);
            if (!m.Success) return Array.Empty<string>();
            return m.Groups[1].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string UpsertEnrolledSerials(string? remarks, IEnumerable<string> serials)
        {
            var merged = TryGetEnrolledSerials(remarks)
                .Concat(serials.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var token = "ZkReaders=" + string.Join(',', merged);
            if (string.IsNullOrWhiteSpace(remarks))
                return token;
            if (ReadersInRemarks.IsMatch(remarks))
                return ReadersInRemarks.Replace(remarks, token);
            return remarks.TrimEnd(' ', ';') + "; " + token;
        }

        /// <summary>Stable Acc uid for one PIN on one reader, so a retry updates the same row.</summary>
        public static int? TryGetReaderSlot(string? remarks, string deviceSerial)
        {
            if (string.IsNullOrWhiteSpace(remarks) || string.IsNullOrWhiteSpace(deviceSerial))
                return null;
            var m = Regex.Match(
                remarks,
                @"ZkSlot:" + Regex.Escape(deviceSerial.Trim()) + @"=(\d+)",
                RegexOptions.IgnoreCase);
            return m.Success && int.TryParse(m.Groups[1].Value, out var uid) && uid > 0 ? uid : null;
        }

        public static string UpsertReaderSlot(string? remarks, string deviceSerial, int uid)
        {
            var token = "ZkSlot:" + deviceSerial.Trim() + "=" + uid;
            var pattern = @"ZkSlot:" + Regex.Escape(deviceSerial.Trim()) + @"=\d+";
            if (string.IsNullOrWhiteSpace(remarks))
                return token;
            if (Regex.IsMatch(remarks, pattern, RegexOptions.IgnoreCase))
                return Regex.Replace(remarks, pattern, token, RegexOptions.IgnoreCase);
            return remarks.TrimEnd(' ', ';') + "; " + token;
        }

        public static int? TryGetNextUid(string? remarks)
        {
            if (string.IsNullOrWhiteSpace(remarks)) return null;
            var m = Regex.Match(remarks, @"AccNextUid=(\d+)", RegexOptions.IgnoreCase);
            return m.Success && int.TryParse(m.Groups[1].Value, out var uid) && uid > 0 ? uid : null;
        }

        public static string UpsertNextUid(string? remarks, int nextUid)
        {
            var token = "AccNextUid=" + nextUid;
            if (string.IsNullOrWhiteSpace(remarks))
                return token;
            if (Regex.IsMatch(remarks, @"AccNextUid=\d+", RegexOptions.IgnoreCase))
                return Regex.Replace(remarks, @"AccNextUid=\d+", token, RegexOptions.IgnoreCase);
            return remarks.TrimEnd(' ', ';') + "; " + token;
        }
    }
}
