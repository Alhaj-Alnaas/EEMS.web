namespace PMS.web.Helpers
{
    public static class FieldRules
    {
        public static string Digits(string? value)
            => new string((value ?? "").Where(char.IsDigit).ToArray());

        public static string Phone(string? value)
            => new string((value ?? "").Where(c => char.IsDigit(c) || c is '+' or '-' or ' ').ToArray());

        public static string Ip(string? value)
            => new string((value ?? "").Where(c => char.IsDigit(c) || c == '.').ToArray());

        public static bool IsIpv4(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            var parts = value.Trim().Split('.');
            if (parts.Length != 4)
                return false;

            foreach (var part in parts)
            {
                if (part.Length is 0 or > 3 || !int.TryParse(part, out var n) || n is < 0 or > 255)
                    return false;
            }

            return true;
        }
    }
}
