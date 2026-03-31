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
        if (!_runtimeSettings.EntraAuthEnabled)
        {
            _logger.LogInformation("Scheduled directory sync is disabled because Entra auth is not enabled.");
            return;
        }

        if (!_runtimeSettings.DirectorySyncScheduled)
        {
            _logger.LogInformation("Scheduled directory sync is disabled via DIRECTORY_SYNC_SCHEDULED=false.");
            return;
        }

        var interval = TimeSpan.FromMinutes(_runtimeSettings.DirectorySyncIntervalMinutes);
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
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Scheduled directory sync failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
