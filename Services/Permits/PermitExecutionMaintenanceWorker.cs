using Core.Interfaces.Services.Permits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Services.Permits
{
    /// <summary>
    /// Closes permits whose validity ended before execution started, and reminds officials about movements left open.
    /// </summary>
    public class PermitExecutionMaintenanceWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PermitExecutionMaintenanceWorker> _logger;

        public PermitExecutionMaintenanceWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<PermitExecutionMaintenanceWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var execution = scope.ServiceProvider.GetRequiredService<IPermitExecutionService>();
                    await execution.RunMaintenanceAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Permit execution maintenance failed");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
