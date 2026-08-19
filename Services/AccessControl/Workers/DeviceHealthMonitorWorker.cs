using Core.Interfaces.Services.AccessControl;
using Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Core.Enums.BaseEnums;

namespace Services.AccessControl.Workers
{
    /// <summary>
    /// Marks readers Offline when heartbeat is stale; leaves Faulty untouched until recovery.
    /// </summary>
    public class DeviceHealthMonitorWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<ZkTecoOptions> _options;
        private readonly ILogger<DeviceHealthMonitorWorker> _logger;

        public DeviceHealthMonitorWorker(
            IServiceScopeFactory scopeFactory,
            IOptions<ZkTecoOptions> options,
            ILogger<DeviceHealthMonitorWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(10, _options.Value.HealthIntervalSeconds));
            _logger.LogInformation("DeviceHealthMonitorWorker started (interval {Interval}s)", interval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "DeviceHealthMonitorWorker cycle failed");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }

        private async Task CheckAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var readers = scope.ServiceProvider.GetRequiredService<IReaderService>();
            var opts = _options.Value;
            var cutoff = DateTime.Now.AddSeconds(-Math.Max(30, opts.OfflineAfterSeconds));

            var all = await readers.GetAllAsync();
            foreach (var reader in all.Where(r => r.IsActive && !r.isDeleted))
            {
                ct.ThrowIfCancellationRequested();

                if (reader.Status == ReaderStatus.Faulty)
                    continue;

                var stale = reader.LastHeartbeat == null || reader.LastHeartbeat < cutoff;
                if (stale && reader.Status == ReaderStatus.Online)
                {
                    await readers.SetStatusAsync(reader.Id, ReaderStatus.Offline, "Heartbeat timeout");
                    _logger.LogWarning(
                        "Reader {Serial} marked Offline (last heartbeat {Hb})",
                        reader.DeviceSerial, reader.LastHeartbeat);
                }
            }
        }
    }
}
