namespace API;

// Gemeinsamer Helper fuer die Retry-Entscheidung. Wird sowohl vom lokalen
// Linux-Failure-Pfad (WorkflowAutomationService) als auch vom externen
// Worker-Failure-Pfad (WorkflowLifecycleService.OnExternalAutomationJobFailedAsync)
// genutzt — damit keine zwei Welten fuer Worker- vs. Linux-Handler-Failures
// entstehen. Migrationspfad-Etappe 9a Schritt 2.
//
// Eingangsdaten: WorkflowAutomationRetrySettings + die Nummer des soeben
// fehlgeschlagenen Versuchs. Liefert eine Discriminated-Union-aehnliche
// Outcome-Struktur (Retry mit Delay vs. FinalFail).
internal static class WorkflowAutomationRetryPolicy
{
    public static WorkflowAutomationRetryOutcome EvaluateRetryOutcome(
        WorkflowAutomationRetrySettings settings,
        int attemptNumber,
        bool isIdempotent)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Nicht-idempotente Handler bekommen keinen Retry — sonst riskiert man
        // doppelseitige Effekte (z. B. zwei AD-User mit demselben Namen).
        if (!isIdempotent)
        {
            return WorkflowAutomationRetryOutcome.FinalFail();
        }

        if (attemptNumber < settings.MaxAttempts)
        {
            return WorkflowAutomationRetryOutcome.RetryAfter(settings.ResolveRetryDelay(attemptNumber));
        }

        return WorkflowAutomationRetryOutcome.FinalFail();
    }
}

internal sealed class WorkflowAutomationRetryOutcome
{
    public enum OutcomeKind
    {
        RetryAfter,
        FinalFail
    }

    public required OutcomeKind Kind { get; init; }
    public TimeSpan Delay { get; init; }

    public static WorkflowAutomationRetryOutcome RetryAfter(TimeSpan delay) =>
        new() { Kind = OutcomeKind.RetryAfter, Delay = delay };

    public static WorkflowAutomationRetryOutcome FinalFail() =>
        new() { Kind = OutcomeKind.FinalFail };
}
