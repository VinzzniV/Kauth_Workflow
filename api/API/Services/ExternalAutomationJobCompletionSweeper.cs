using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API;

// Etappe 9a Schritt 2: Sweeper-HostedService fuer extern-finalisierte Automation-Jobs.
//
// Der Windows-Worker schreibt automation_jobs.status = 'succeeded'/'failed' + Attempt + Logs
// direkt in die DB. Damit der Workflow am Automation-Node weitergeht, muss die Linux-API
// die Folge-Logik (Folge-Actions / Node-Done / AdvanceRuntime; bzw. Retry / Final-Fail)
// anstossen. Genau das macht dieser Sweeper.
//
// Polling-Loop alle 5s mit atomarem Two-Phase-Claim auf completion_claimed_at:
//   1. ClaimExternalCompletionsBatchAsync(limit) holt bis zu 50 Jobs (FOR UPDATE SKIP LOCKED),
//      setzt completion_claimed_at = NOW(). Mehrfach-API-Instanzen oder ueberlappende Sweep-
//      Zyklen koennen sich nicht denselben Job greifen.
//   2. Pro Job: je nach status OnExternalAutomationJobSucceededAsync oder
//      OnExternalAutomationJobFailedAsync. Die Lifecycle-Methoden setzen
//      completion_processed_at = NOW() + completion_claimed_at = NULL in derselben Transaktion.
//
// Exception in Phase 2 -> completion_processed_at bleibt NULL, completion_claimed_at bleibt
// gesetzt. Nach 5min Stale-Timeout greift der naechste Sweep automatisch wieder zu.
internal sealed class ExternalAutomationJobCompletionSweeper(
    IServiceScopeFactory scopeFactory,
    ILogger<ExternalAutomationJobCompletionSweeper> logger) : BackgroundService
{
    internal const int BatchLimit = 50;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Warten bis der Host vollstaendig hochgefahren ist (analog zu WorkflowAutomationHostedService).
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var automationRepo = scope.ServiceProvider.GetRequiredService<IWorkflowAutomationRepository>();
                var lifecycle = scope.ServiceProvider.GetRequiredService<IWorkflowLifecycleService>();

                var processedCount = await ProcessNextBatchAsync(automationRepo, lifecycle, logger, stoppingToken);
                if (processedCount == 0)
                {
                    await Task.Delay(PollingInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "External-completion sweeper loop failed.");
                await Task.Delay(FailureBackoff, stoppingToken);
            }
        }
    }

    // Testbar isolierte Process-Schleife pro Sweep-Zyklus. Liefert die Anzahl der angefassten Jobs.
    internal static async Task<int> ProcessNextBatchAsync(
        IWorkflowAutomationRepository automationRepo,
        IWorkflowLifecycleService lifecycle,
        ILogger logger,
        CancellationToken stoppingToken)
    {
        var claims = await automationRepo.ClaimExternalCompletionsBatchAsync(BatchLimit, stoppingToken);
        if (claims.Count == 0)
        {
            return 0;
        }

        foreach (var claim in claims)
        {
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                if (string.Equals(claim.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    await lifecycle.OnExternalAutomationJobSucceededAsync(claim.JobId, stoppingToken);
                }
                else if (string.Equals(claim.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    await lifecycle.OnExternalAutomationJobFailedAsync(claim.JobId, stoppingToken);
                }
                else
                {
                    logger.LogWarning(
                        "External completion claim for job {JobId} has unexpected status '{Status}' — skipping.",
                        claim.JobId,
                        claim.Status);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // completion_processed_at bleibt NULL, completion_claimed_at bleibt gesetzt.
                // Nach 5min Timeout greift der naechste Sweep automatisch wieder.
                logger.LogError(
                    ex,
                    "Processing external completion for job {JobId} (status={Status}) failed; will retry after stale-claim timeout.",
                    claim.JobId,
                    claim.Status);
            }
        }

        return claims.Count;
    }
}
