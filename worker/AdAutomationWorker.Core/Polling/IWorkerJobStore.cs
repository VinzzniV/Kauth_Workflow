using AdAutomationWorker.Core.Handlers;

namespace AdAutomationWorker.Core.Polling;

// Vertrag fuer alle SQL-Operationen, die der Worker gegen die DB ausfuehrt. Die einzige
// produktive Implementierung ist PostgresWorkerJobStore; Tests koennen einen In-Memory-Stub
// einsetzen, ohne Postgres zu brauchen — und genau das macht der Verifier-Test-Lauf unter
// Linux moeglich.
public interface IWorkerJobStore
{
    // Two-Phase-Claim mit FOR UPDATE SKIP LOCKED:
    //   1. SELECT … FROM automation_jobs_windows_worker WHERE status='pending' AND available_at <= NOW()
    //      LIMIT 1 FOR UPDATE SKIP LOCKED.
    //   2. UPDATE auf automation_jobs: status='running', claimed_at=NOW(), claimed_by=@workerId,
    //      heartbeat_at=NOW(); plus Attempt-Row in automation_job_attempts.
    // Alles in derselben Transaktion. Liefert null, wenn nichts pending ist.
    Task<WorkerJobClaim?> ClaimNextPendingJobAsync(string workerId, CancellationToken cancellationToken);

    // Setzt verwaiste running-Jobs (heartbeat_at < NOW() - staleTimeout) zurueck auf pending +
    // Claim-Spalten geleert. Liefert die Anzahl der zurueckgesetzten Jobs.
    Task<int> ReleaseStaleClaimsAsync(TimeSpan staleTimeout, CancellationToken cancellationToken);

    // Heartbeat-Tick. Nur erfolgreich, wenn der claimed_by-Worker noch derselbe ist — verhindert,
    // dass ein neuer Worker, der den Job nach Stale-Release uebernommen hat, vom alten Worker
    // ueberschrieben wird. Liefert die Anzahl der aktualisierten Zeilen (0 = Lease verloren).
    Task<int> UpdateHeartbeatAsync(long jobId, string workerId, CancellationToken cancellationToken);

    // Setzt automation_jobs.status='succeeded' + completed_at, schliesst den Attempt mit
    // status='succeeded' + output_json + completed_at, schreibt alle Logs in einer Transaktion.
    Task MarkJobSucceededAsync(
        long jobId,
        int attemptNumber,
        System.Text.Json.JsonElement? output,
        IReadOnlyList<WorkerLogEntry> logs,
        CancellationToken cancellationToken);

    // Setzt automation_jobs.status='failed' + completed_at, schliesst den Attempt mit
    // status='failed' + error_message + completed_at, schreibt alle Logs in einer Transaktion.
    // Retry-/Final-Entscheidung trifft die Linux-API anhand WorkflowAutomationRetrySettings, nicht der Worker.
    Task MarkJobFailedAsync(
        long jobId,
        int attemptNumber,
        string errorMessage,
        IReadOnlyList<WorkerLogEntry> logs,
        CancellationToken cancellationToken);
}
