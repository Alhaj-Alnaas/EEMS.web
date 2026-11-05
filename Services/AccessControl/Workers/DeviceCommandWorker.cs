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
    /// Background worker: retries timed-out Sent commands and re-queues Failed ones
    /// up to MaxCommandRetries; marks readers Faulty after repeated failures.
    /// </summary>
    public class DeviceCommandWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<ZkTecoOptions> _options;
        private readonly ILogger<DeviceCommandWorker> _logger;

        public DeviceCommandWorker(
            IServiceScopeFactory scopeFactory,
            IOptions<ZkTecoOptions> options,
            ILogger<DeviceCommandWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(5, _options.Value.WorkerIntervalSeconds));
            _logger.LogInformation("DeviceCommandWorker started (interval {Interval}s)", interval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "DeviceCommandWorker cycle failed");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }

        private async Task ProcessAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var commands = scope.ServiceProvider.GetRequiredService<IDeviceCommandService>();
            var readers = scope.ServiceProvider.GetRequiredService<IReaderService>();
            var opts = _options.Value;

            var timedOut = await commands.GetTimedOutSentAsync(
                TimeSpan.FromSeconds(Math.Max(15, opts.CommandAckTimeoutSeconds)));

            foreach (var cmd in timedOut)
            {
                ct.ThrowIfCancellationRequested();
                var error = $"ACK timeout after {opts.CommandAckTimeoutSeconds}s";

                // Never re-send user UPDATE/enroll — SpeedFace may create a blank user
                // on each duplicate DATA UPDATE user if parsing glitches or ACK was lost
                // after the device already applied the command.
                if (string.Equals(cmd.CommandType, nameof(DeviceCommandType.UpdateUserInfo), StringComparison.OrdinalIgnoreCase))
                {
                    await commands.UpdateStatusAsync(cmd.Id, DeviceCommandStatus.Acknowledged,
                        error + " (no retry for UpdateUserInfo — avoids blank users)");
                    _logger.LogWarning(
                        "UpdateUserInfo ACK timeout — marked done without retry commandId={CommandId} readerId={ReaderId}",
                        cmd.Id, cmd.ReaderId);
                    continue;
                }

                if (cmd.RetryCount < opts.MaxCommandRetries)
                {
                    var delay = TimeSpan.FromSeconds(opts.RetryBaseDelaySeconds * Math.Max(1, cmd.RetryCount + 1));
                    await commands.ScheduleRetryAsync(cmd.Id, delay, error);
                    _logger.LogWarning(
                        "Command timeout — commandId={CommandId} type={CommandType} readerId={ReaderId} retry {Retry}/{Max} in {Delay}s",
                        cmd.Id, cmd.CommandType, cmd.ReaderId, cmd.RetryCount + 1, opts.MaxCommandRetries, delay.TotalSeconds);
                }
                else
                {
                    await commands.UpdateStatusAsync(cmd.Id, DeviceCommandStatus.Failed, error);
                    if (cmd.ReaderId != Guid.Empty)
                    {
                        var reader = await readers.GetByIdAsync(cmd.ReaderId);
                        if (reader != null)
                        {
                            reader.ConsecutiveFailures++;
                            if (reader.ConsecutiveFailures >= opts.FaultyAfterFailures)
                                await readers.SetStatusAsync(reader.Id, ReaderStatus.Faulty, error);
                            else
                                await readers.UpdateAsync(reader);
                        }
                    }
                    _logger.LogError("Command permanently failed after retries commandId={CommandId} type={CommandType} readerId={ReaderId}",
                        cmd.Id, cmd.CommandType, cmd.ReaderId);
                }
            }
        }
    }
}
