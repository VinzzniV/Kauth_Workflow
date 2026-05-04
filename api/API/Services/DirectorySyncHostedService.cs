using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API;

internal sealed class DirectorySyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LifecycleRuntimeSettings _runtimeSettings;
    private readonly ILogger<DirectorySyncHostedService> _logger;

    public DirectorySyncHostedService(
        IServiceScopeFactory scopeFactory,
        LifecycleRuntimeSettings runtimeSettings,
        ILogger<DirectorySyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _runtimeSettings = runtimeSettings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_runtimeSettings.DirectorySyncEnabled)
        {
            _logger.LogInformation("Scheduled directory sync is disabled because the current auth mode does not use Entra directory data.");
            await WriteHostedLogAsync(
                "info",
                "directory_sync_schedule_disabled",
                "Scheduled directory sync is disabled because Entra directory data is not active.",
                stoppingToken);
            return;
        }

        if (!_runtimeSettings.DirectorySyncScheduled)
        {
            _logger.LogInformation("Scheduled directory sync is disabled via DIRECTORY_SYNC_SCHEDULED=false.");
            await WriteHostedLogAsync(
                "info",
                "directory_sync_schedule_disabled",
                "Scheduled directory sync is disabled via DIRECTORY_SYNC_SCHEDULED=false.",
                stoppingToken);
            return;
        }

        var interval = TimeSpan.FromMinutes(_runtimeSettings.DirectorySyncIntervalMinutes);
        _logger.LogInformation(
            "Scheduled directory sync active. Interval: {IntervalMinutes} min. Configure via DIRECTORY_SYNC_INTERVAL_MINUTES.",
            _runtimeSettings.DirectorySyncIntervalMinutes);
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var directorySyncService = scope.ServiceProvider.GetRequiredService<IDirectorySyncService>();
                var result = await directorySyncService.SyncAllAsync(cancellationToken: stoppingToken);
                if (!string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Scheduled directory sync completed with status {Status}: {Message}",
                        result.Status,
                        result.ErrorMessage);

                    var systemEventLogService = scope.ServiceProvider.GetRequiredService<ISystemEventLogService>();
                    await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                    {
                        Severity = "warning",
                        Source = "directory",
                        Category = "sync",
                        EventKey = "scheduled_directory_sync_non_success",
                        Message = $"Scheduled directory sync completed with status {result.Status}: {result.ErrorMessage}",
                        Details = result
                    }, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Scheduled directory sync failed.");
                await WriteHostedLogAsync(
                    "error",
                    "scheduled_directory_sync_failed",
                    $"Scheduled directory sync failed: {ex.Message}",
                    stoppingToken,
                    new { error = ex.Message, exceptionType = ex.GetType().FullName });
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task WriteHostedLogAsync(
        string severity,
        string eventKey,
        string message,
        CancellationToken cancellationToken,
        object? details = null)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var systemEventLogService = scope.ServiceProvider.GetRequiredService<ISystemEventLogService>();
            await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = severity,
                Source = "directory",
                Category = "hosted_service",
                EventKey = eventKey,
                Message = message,
                Details = details
            }, cancellationToken);
        }
        catch
        {
            // Keep the worker loop alive even if structured logging fails.
        }
    }
}
