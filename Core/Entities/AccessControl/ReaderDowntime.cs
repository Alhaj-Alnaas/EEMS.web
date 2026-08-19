using System;
using static Core.Enums.BaseEnums;

namespace Core.Entities
{
    /// <summary>
    /// Records a reader outage window: from StartedAt until EndedAt (null = still down).
    /// </summary>
    public class ReaderDowntime : Base
    {
        public Guid ReaderId { get; set; }
        public Reader? Reader { get; set; }

        /// <summary>When the outage began (typically last heartbeat or detection time).</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>When the reader recovered; null while still Offline/Faulty.</summary>
        public DateTime? EndedAt { get; set; }

        public ReaderStatus StatusDuringOutage { get; set; } = ReaderStatus.Offline;

        public string? Reason { get; set; }

        public bool AlertSent { get; set; }
    }
}
