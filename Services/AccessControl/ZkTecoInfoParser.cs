namespace Services.AccessControl
{
    /// <summary>Parses INFO payloads from /iclock/getrequest and INFO command responses.</summary>
    public static class ZkTecoInfoParser
    {
        public static bool LooksLikeInfo(string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return false;
            return body.Contains("UserCount=", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("FPCount=", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("FaceCount=", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("FingerPrintCount=", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("~DeviceName=", StringComparison.OrdinalIgnoreCase)
                   || body.StartsWith("Ver ", StringComparison.OrdinalIgnoreCase);
        }
    }
}
