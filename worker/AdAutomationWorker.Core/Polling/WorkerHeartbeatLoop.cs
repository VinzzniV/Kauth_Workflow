using Microsoft.Extensions.Logging;

namespace AdAutomationWorker.Core.Polling;

// Begleitschleife waehrend der Handler laeuft. Setzt im konfigurierten Intervall heartbeat_at,
// damit der Linux-API-Stale-Sweep (5 min Default) den laufenden Job nicht versehentlich zurueckholt.
//
// Verlorenes Lease: UpdateHeartbeatAsync liefert 0 zurueck, wenn claimed_by zwischenzeitlich
// geleert oder umgeschrieben wurde. Wir loggen ein Warning und brechen den Loop ab — der Host
// erkennt das ueber den Cancellation-Token und kann den Handler-Abbruch entscheiden, oder
// laesst ihn weiterlaufen und ignoriert das Ergebnis am Ende (wird in einer spaeteren Etappe
// haerter abgesichert).
public sealed class WorkerHeartbeatLoop
{
    private readonly IWorkerJobStore store;
    private readonly ILogger<WorkerHeartbeatLoop>? logger;

    public WorkerHeartbeatLoop(IWorkerJobStore store, ILogger<WorkerHeartbeatLoop>? logger = null)
    {
        this.store = store;
        this.logger = logger;
    }

    public async Task RunAsync(long jobId, string workerId, TimeSpan interval, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationToken);
                var affected = await store.UpdateHeartbeatAsync(jobId, workerId, cancellationToken);
                if (affected == 0)
                {
                    logger?.LogWarning(
                        "Heartbeat for job {JobId} no longer accepted — lease lost (claimed_by changed or status not 'running').",
                        jobId);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normales Beenden, kein Fehler.
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Heartbeat loop for job {JobId} crashed.", jobId);
        }
    }
}
