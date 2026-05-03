using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API;

internal sealed class RotationNotificationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<RotationNotificationHostedService> logger) : BackgroundService
{
    // Hard upper bound for a single sweep. Without this guard, a stalled DB query
    // or hanging external dependency would block the worker for the full 24-hour
    // tick — silent notification delay with no log trace. The timeout fires
    // OperationCanceledException, which is logged + retried via the existing
    // 5-minute backoff path.
    private static readonly TimeSpan SweepTimeout = TimeSpan.FromHours(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var rotationNotificationService = scope.ServiceProvider.GetRequiredService<IRotationNotificationService>();
                using var sweepCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                sweepCts.CancelAfter(SweepTimeout);
                await rotationNotificationService.ExecuteDailySweepAsync(sweepCts.Token);
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException ex)
            {
                logger.LogWarning(ex, "Rotation notification sweep exceeded {TimeoutMinutes}-minute timeout; retrying after backoff.", SweepTimeout.TotalMinutes);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Rotation notification worker loop failed.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
