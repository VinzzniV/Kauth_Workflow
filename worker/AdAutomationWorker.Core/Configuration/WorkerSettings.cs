namespace AdAutomationWorker.Core.Configuration;

// Konfiguration des Windows-Workers. Wird per IOptions aus appsettings.json gebunden.
// Defaults entsprechen den Schritt-1-Sub-Entscheidungen (Heartbeat 30s, Stale 5min).
public sealed class WorkerSettings
{
    // Eindeutige Worker-Kennung im Format <hostname>:<pid>:<startup-uuid>. Wird beim Start
    // einmalig gesetzt und in automation_jobs.claimed_by geschrieben.
    public string WorkerId { get; set; } = string.Empty;

    public int PollingIntervalSeconds { get; set; } = 5;

    public int HeartbeatIntervalSeconds { get; set; } = 30;

    public int StaleClaimTimeoutMinutes { get; set; } = 5;

    public int ClaimBatchSize { get; set; } = 1;
}
