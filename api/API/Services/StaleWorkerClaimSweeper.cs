using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API;

// Etappe 9a Schritt 4 Sub-B: Belt-and-Suspenders-Stale-Sweep auf Linux-API-Seite.
//
// Worker raeumt sich heute selbst auf (PostgresWorkerJobStore.ReleaseStaleClaimsAsync vor jedem
// Polling-Zyklus). Solange mindestens ein Worker laeuft, ist das ausreichend. Wenn alle Worker
// gleichzeitig tot/gestoppt sind, bleiben `running`-Jobs in der DB haengen und kein Worker
// holt sie zurueck. Genau dafuer ist dieser Sweeper da: er laeuft als API-HostedService
// unabhaengig vom Worker und setzt verwaiste running-Jobs (heartbeat_at < NOW() - staleTimeout)
// auf pending zurueck.
//
// Polling-Intervall 60s; Stale-Timeout aus WorkerLeaseSettings (default 5min). Bei Exception
// FailureBackoff 60s.
internal sealed class StaleWorkerClaimSweeper(
    IServiceScopeFactory scopeFactory,
    WorkerLeaseSettings leaseSettings,
    ILogger<StaleWorkerClaimSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial-Delay damit der Host vollstaendig hochgefahren ist und die DB-Verbindung steht.
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var automationRepo = scope.ServiceProvider.GetRequiredService<IWorkflowAutomationRepository>();

                var released = await ProcessNextSweepAsync(automationRepo, leaseSettings.StaleClaimTimeout, logger, stoppingToken);
                if (released > 0)
                {
                    logger.LogInformation("Released {Count} stale worker claim(s) after {Timeout}.", released, leaseSettings.StaleClaimTimeout);
                }

                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Stale-worker-claim sweeper loop failed.");
                await Task.Delay(FailureBackoff, stoppingToken);
            }
        }
    }

    // Testbar isolierte Sweep-Logik. Liefert die Anzahl der zurueckgesetzten Jobs.
    internal static async Task<int> ProcessNextSweepAsync(
        IWorkflowAutomationRepository automationRepo,
        TimeSpan staleTimeout,
        ILogger logger,
        CancellationToken stoppingToken)
    {
        try
        {
            return await automationRepo.ReleaseStaleWorkerClaimsAsync(staleTimeout, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stale-worker-claim sweep cycle threw.");
            return 0;
        }
    }
}
