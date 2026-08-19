using System;
using static Core.Enums.BaseEnums;

namespace Core.Entities
{
    /// <summary>
    /// Phase 1: a command queued/sent to a biometric reader via Push Protocol (DDD DeviceCommands).
    /// </summary>
    public class DeviceCommand : Base
    {
        public Guid ReaderId { get; set; }
        public Reader? Reader { get; set; }

        /// <summary>Stored as the enum name (e.g. AddTemplate, TestConnection).</summary>
        public string CommandType { get; set; } = nameof(DeviceCommandType.Sync);

        public string? Payload { get; set; }

        public DeviceCommandStatus Status { get; set; } = DeviceCommandStatus.Pending;

        public int RetryCount { get; set; }

        public string? LastError { get; set; }

        /// <summary>Numeric id returned to the device as C:{id}:... in Push Protocol.</summary>
        public int ProtocolCommandId { get; set; }

        public DateTime? ScheduledOn { get; set; }
        public DateTime? SentOn { get; set; }
        public DateTime? CompletedOn { get; set; }
    }
}
