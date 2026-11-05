namespace Services.AccessControl
{
    /// <summary>
    /// ZKTeco Acc / Security PUSH time encoding (Appendix 5 &amp; 6).
    /// Used for transaction.time_second and similar Acc fields.
    /// </summary>
    internal static class ZkTecoAccTime
    {
        /// <summary>Decode Acc <c>time_second</c> to local DateTime (seconds precision).</summary>
        public static bool TryDecode(long timeSecond, out DateTime value)
        {
            value = default;
            if (timeSecond < 0 || timeSecond > uint.MaxValue)
                return false;

            try
            {
                var tt = (uint)timeSecond;
                var sec = (int)(tt % 60);
                tt /= 60;
                var min = (int)(tt % 60);
                tt /= 60;
                var hour = (int)(tt % 24);
                tt /= 24;
                var day = (int)(tt % 31) + 1;
                tt /= 31;
                var mon = (int)(tt % 12) + 1;
                tt /= 12;
                var year = (int)tt + 2000;

                if (year is < 2000 or > 2100 || mon is < 1 or > 12 || day is < 1 or > 31)
                    return false;
                if (hour is < 0 or > 23 || min is < 0 or > 59 || sec is < 0 or > 59)
                    return false;

                // Acc day/month packing allows invalid calendar dates (e.g. Feb 31) — clamp via DateTime ctor.
                value = new DateTime(year, mon, Math.Min(day, DateTime.DaysInMonth(year, mon)), hour, min, sec);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryDecode(string? raw, out DateTime value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            return long.TryParse(raw.Trim(), out var n) && TryDecode(n, out value);
        }
    }
}
