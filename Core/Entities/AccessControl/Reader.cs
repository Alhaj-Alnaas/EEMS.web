using System;
using static Core.Enums.BaseEnums;

namespace Core.Entities
{
    /// <summary>
    /// Phase 1: a biometric reader/terminal device installed at a gate (DDD Readers).
    /// </summary>
    public class Reader : Base
    {
        public Guid GateId { get; set; }
        public Gate? Gate { get; set; }

        /// <summary>Display name shown in the UI.</summary>
        public string Name { get; set; } = "";

        /// <summary>ZKTeco device serial number (unique).</summary>
        public string DeviceSerial { get; set; } = "";

        public string? IpAddress { get; set; }

        /// <summary>Optional LAN port (common ZK default 4370); Push Protocol uses HTTP ADMS.</summary>
        public int? Port { get; set; } = 80;

        public string? Model { get; set; }

        public ReaderStatus Status { get; set; } = ReaderStatus.Offline;

        public DateTime? LastHeartbeat { get; set; }

        /// <summary>Last successful sync of templates/permissions/logs.</summary>
        public DateTime? LastSyncAt { get; set; }

        /// <summary>Consecutive failed command / health checks — used to mark Faulty.</summary>
        public int ConsecutiveFailures { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
