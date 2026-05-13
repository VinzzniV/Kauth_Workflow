namespace API;

// Konfigurierbare Retry-Strategie fuer fehlgeschlagene Idempotent-Automation-Jobs.
// Default: nach Versuch 1 → 1min warten, nach Versuch 2 → 5min, nach Versuch 3+ → 5min,
// max. 3 Versuche. Override via Konstruktor oder DI ermoeglicht Anpassung pro Deployment.
internal sealed class WorkflowAutomationRetrySettings
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan FirstRetryDelay { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan SubsequentRetryDelay { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan ResolveRetryDelay(int attemptNumber)
        => ResolveRetryDelay(attemptNumber, subsequentDelayOverride: null);

    // Etappe 9a Schritt 7 Sub-A: per-Action-Subsequent-Delay-Override. NULL = globalen
    // SubsequentRetryDelay verwenden. FirstRetryDelay bleibt global -- der erste Retry nach 60s
    // ist sinnvoll auch bei langlaufenden Sync-Wait-Pfaden (erster Check direkt nach kurzem Lag).
    public TimeSpan ResolveRetryDelay(int attemptNumber, TimeSpan? subsequentDelayOverride)
    {
        return attemptNumber switch
        {
            <= 1 => FirstRetryDelay,
            _ => subsequentDelayOverride is { } overrideValue && overrideValue > TimeSpan.Zero
                ? overrideValue
                : SubsequentRetryDelay
        };
    }
}
