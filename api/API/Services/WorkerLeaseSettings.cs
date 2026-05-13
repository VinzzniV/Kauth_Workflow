namespace API;

// Etappe 9a Schritt 4 Sub-B: Lease-Konfig fuer den Linux-API Stale-Worker-Claim-Sweep.
// `StaleClaimTimeout` muss konsistent mit dem Worker-internen Wert (PostgresWorkerJobStore +
// WorkerSettings.StaleClaimTimeoutMinutes) sein. Default 5 Minuten — gleicher Wert wie im
// Worker-Skeleton (siehe Hybrid-Worker-Sub-Architektur). Polling-Interval ist Sweeper-eigene
// Verantwortung (60s default, siehe StaleWorkerClaimSweeper).
internal sealed class WorkerLeaseSettings
{
    public TimeSpan StaleClaimTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
