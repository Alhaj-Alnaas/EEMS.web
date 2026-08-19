namespace Core.Options
{
    /// <summary>Configuration for ZKTeco Push Protocol (ADMS) and device health monitoring.</summary>
    public class ZkTecoOptions
    {
        public const string SectionName = "ZkTeco";

        /// <summary>Seconds without heartbeat before a reader is marked Offline.</summary>
        public int OfflineAfterSeconds { get; set; } = 120;

        /// <summary>Seconds a Sent command may wait for ACK before retry/fail.</summary>
        public int CommandAckTimeoutSeconds { get; set; } = 90;

        /// <summary>Maximum retries for a failed device command.</summary>
        public int MaxCommandRetries { get; set; } = 3;

        /// <summary>Base delay (seconds) between retries; multiplied by RetryCount.</summary>
        public int RetryBaseDelaySeconds { get; set; } = 30;

        /// <summary>Consecutive failures after which a reader is marked Faulty.</summary>
        public int FaultyAfterFailures { get; set; } = 5;

        /// <summary>Background worker poll interval (seconds).</summary>
        public int WorkerIntervalSeconds { get; set; } = 15;

        /// <summary>Health monitor interval (seconds).</summary>
        public int HealthIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Dump raw TCP bytes per connection (logger "ZkTeco.Tcp", Debug level).
        /// Distinguishes "device never connected" from "device connected but request rejected".
        /// </summary>
        public bool LogRawTcp { get; set; }
        public int SyncIntervalMinutes { get; set; } = 30;
    }
}