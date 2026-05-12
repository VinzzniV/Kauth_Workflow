using System.Text.Json;

namespace AdAutomationWorker.Core.Handlers.Simulated;

// Skeleton-Handler ohne AD-Zugriff. Beweist den Transport- und Audit-Pfad ende-zu-ende:
// Worker pickt den Job → ExecuteAsync laeuft → Output landet in automation_job_attempts.output_json,
// Log landet in automation_job_logs, Sweeper triggert Workflow-Fortschalten.
//
// Optionaler Payload-Parameter `delaySeconds` (default 0) erlaubt es, den Handler kuenstlich
// lang laufen zu lassen — damit ist der manuelle Stale-Claim-Test realistisch durchfuehrbar:
// Worker starten, Job mit delaySeconds=120 starten, waehrend des Delays Worker per Ctrl-C killen,
// Heartbeat-Timeout abwarten, Worker neu starten und Re-Claim verifizieren.
public sealed class SimulatedWindowsWorkerPingHandler : IWorkerHandler
{
    public string ActionKey => "simulated_windows_worker_ping";

    public async Task<WorkerHandlerResult> ExecuteAsync(WorkerHandlerContext context, CancellationToken cancellationToken)
    {
        var delaySeconds = ResolveDelaySeconds(context.Payload);
        if (delaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
        }

        var host = Environment.MachineName;
        var output = JsonSerializer.SerializeToElement(new
        {
            result = "pong",
            host,
            delaySeconds
        });

        var log = new WorkerLogEntry
        {
            Level = "info",
            Message = $"Simulated ping on host '{host}' (job {context.JobId}, attempt {context.AttemptNumber}).",
            Details = JsonSerializer.SerializeToElement(new { host, attemptNumber = context.AttemptNumber })
        };

        return WorkerHandlerResult.Success(output, new[] { log });
    }

    private static int ResolveDelaySeconds(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return 0;
        if (!payload.TryGetProperty("delaySeconds", out var delayProperty)) return 0;

        return delayProperty.ValueKind switch
        {
            JsonValueKind.Number when delayProperty.TryGetInt32(out var v) => Math.Max(0, v),
            JsonValueKind.String when int.TryParse(delayProperty.GetString(), out var v) => Math.Max(0, v),
            _ => 0
        };
    }
}
