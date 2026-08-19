using Core.Interfaces.Services.AccessControl;
using Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Services.AccessControl.Workers
{
    public class ReaderSyncSchedulerWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReaderSyncSchedulerWorker> _logger;
        private readonly int _intervalMinutes;

        public ReaderSyncSchedulerWorker(
            IServiceScopeFactory scopeFactory,
            IOptions<ZkTecoOptions> options,
            ILogger<ReaderSyncSchedulerWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _intervalMinutes = Math.Max(1, options.Value.SyncIntervalMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReaderSyncScheduler started — interval={Min}m", _intervalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(_intervalMinutes), stoppingToken);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var statsService = scope.ServiceProvider.GetRequiredService<IReaderDeviceStatsService>();
                    await statsService.EnqueueFullSyncAllAsync();
                    _logger.LogInformation("Periodic FullSync enqueued for all active readers");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "ReaderSyncScheduler error");
                }
            }
        }
    }
}
