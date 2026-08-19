using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Core.Interfaces.Services.AccessControl;
using Core.Models;

namespace Services.AccessControl
{
    public class ReaderSyncStateService : IReaderSyncStateService
    {
        private const int MaxRaw = 500;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _stamps = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, ReaderDeviceSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, (string SessionId, string RegistryCode)> _sessions = new(StringComparer.OrdinalIgnoreCase);

        public void ResetStamps(string deviceSerial)
        {
            var key = Normalize(deviceSerial);
            _stamps[key] = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string GetStamp(string deviceSerial, string tableName)
        {
            var key = Normalize(deviceSerial);
            if (_stamps.TryGetValue(key, out var tableStamps)
                && tableStamps.TryGetValue(tableName, out var stamp)
                && !string.IsNullOrWhiteSpace(stamp))
                return stamp;
            return "0";
        }

        public void UpdateStamp(string deviceSerial, string tableName, string? stamp)
        {
            if (string.IsNullOrWhiteSpace(stamp) || stamp.Equals("None", StringComparison.OrdinalIgnoreCase))
                return;

            var key = Normalize(deviceSerial);
            var tableStamps = _stamps.GetOrAdd(key, _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            tableStamps[tableName] = stamp.Trim();
        }

        public (string SessionId, string RegistryCode) GetOrCreateSession(string deviceSerial)
        {
            var key = Normalize(deviceSerial);
            return _sessions.GetOrAdd(key, _ =>
            {
                var sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                var registryCode = GenerateRegistryCode(12);
                return (sessionId, registryCode);
            });
        }

        public void UpdateFromInfo(string deviceSerial, string? info)
        {
            if (string.IsNullOrWhiteSpace(info)) return;

            var key = Normalize(deviceSerial);
            _snapshots.TryGetValue(key, out var previous);

            var csv = TryParseCsvInfo(info);
            var userCount = TryInt(info, "UserCount", "Users") ?? csv?.UserCount ?? previous?.UserCount;
            var fpCount = TryInt(info, "FPCount", "FingerPrintCount", "FingerprintCount") ?? csv?.FingerprintCount ?? previous?.FingerprintCount;
            var faceCount = TryInt(info, "FaceCount") ?? csv?.FaceCount ?? previous?.FaceCount;
            var attCount = TryInt(info, "AttLogCount", "TransactionCount") ?? csv?.AttLogCount ?? previous?.AttLogCount;
            var fw = TryString(info, "FirmVer", "FWVersion", "FirmwareVersion") ?? csv?.FirmwareVersion ?? previous?.FirmwareVersion;
            var raw = info.Length > MaxRaw ? info[..MaxRaw] + "…" : info;

            _snapshots[key] = new ReaderDeviceSnapshot(
                UpdatedAt: DateTime.Now,
                DeviceSerial: key,
                UserCount: userCount,
                FingerprintCount: fpCount,
                FaceCount: faceCount,
                AttLogCount: attCount,
                FirmwareVersion: fw,
                RawInfo: raw);
        }

        public ReaderDeviceSnapshot? GetSnapshot(string deviceSerial)
        {
            _snapshots.TryGetValue(Normalize(deviceSerial), out var snap);
            return snap;
        }

        public IReadOnlyList<ReaderDeviceSnapshot> GetAllSnapshots()
            => _snapshots.Values.OrderBy(s => s.DeviceSerial).ToList();

        private static string Normalize(string serial) => serial.Trim();

        private static string GenerateRegistryCode(int length)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
            var bytes = RandomNumberGenerator.GetBytes(length);
            var chars = new char[length];
            for (var i = 0; i < length; i++)
                chars[i] = alphabet[bytes[i] % alphabet.Length];
            return new string(chars);
        }

        /// <summary>
        /// getrequest INFO=users,fpCount,attCount,ip,fwVersion[,faceCount]
        /// </summary>
        private static (int? UserCount, int? FingerprintCount, int? AttLogCount, string? FirmwareVersion, int? FaceCount)? TryParseCsvInfo(string info)
        {
            var text = info.Trim().Trim('"');
            if (text.Contains('=') || !text.Contains(','))
                return null;

            var parts = text.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || !int.TryParse(parts[0], out var users))
                return null;
            if (!int.TryParse(parts[1], out var fps))
                return null;

            int? att = parts.Length > 2 && int.TryParse(parts[2], out var attN) ? attN : null;
            var fw = parts.Length > 4 && !int.TryParse(parts[4], out _) ? parts[4] : null;
            int? faces = parts.Length > 5 && int.TryParse(parts[5], out var faceN) ? faceN : null;
            return (users, fps, att, fw, faces);
        }

        private static int? TryInt(string info, params string[] keys)
        {
            foreach (var key in keys)
            {
                var m = Regex.Match(
                    info,
                    $@"(?:^|[,;\s\n\r])~?{Regex.Escape(key)}=(\d+)",
                    RegexOptions.IgnoreCase);
                if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
                    return n;
            }
            return null;
        }

        private static string? TryString(string info, params string[] keys)
        {
            foreach (var key in keys)
            {
                var m = Regex.Match(
                    info,
                    $@"(?:^|[,;\s\n\r])~?{Regex.Escape(key)}=([^,;\r\n]+)",
                    RegexOptions.IgnoreCase);
                if (m.Success)
                    return m.Groups[1].Value.Trim();
            }
            return null;
        }
    }
}
