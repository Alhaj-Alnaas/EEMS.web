using System.Text.RegularExpressions;

namespace Services.AccessControl
{
    /// <summary>
    /// SpeedFace / acc Security PUSH 3.1.2 user lines are lowercase and keyed by
    /// internal <c>uid</c> plus <c>pin</c>. Updating without <c>uid</c> inserts a blank user.
    /// </summary>
    internal static class ZkTecoAccUserCommand
    {
        private static readonly Regex UidInRemarks = new(@"ZkUid=(\S+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string QueryAllUsers() => "DATA QUERY tablename=user,fielddesc=*";

        public static string Update(string pin, string name, string disable, string uid)
        {
            return "DATA UPDATE user " +
                   $"uid={uid}\tcardno=\tpin={pin}\tpassword=\tgroup=1\tstarttime=0\tendtime=0\t" +
                   $"name={name}\tprivilege=0\tdisable={disable}\tverify=0";
        }

        public static string? TryGetUid(string? remarks)
        {
            if (string.IsNullOrWhiteSpace(remarks)) return null;
            var m = UidInRemarks.Match(remarks);
            return m.Success ? m.Groups[1].Value : null;
        }

        public static string UpsertUid(string? remarks, string uid)
        {
            var token = "ZkUid=" + uid.Trim();
            if (string.IsNullOrWhiteSpace(remarks))
                return token;
            if (UidInRemarks.IsMatch(remarks))
                return UidInRemarks.Replace(remarks, token);
            return remarks.TrimEnd(' ', ';') + "; " + token;
        }
    }
}
