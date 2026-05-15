namespace API;

internal interface IWorkflowAutomationActionHandler
{
    string ActionKey { get; }
    Task<WorkflowAutomationHandlerResult> ExecuteAsync(
        WorkflowAutomationHandlerContext context,
        CancellationToken cancellationToken = default);

    Task<AutomationLinuxPlanResult> PlanAsync(
        AutomationPlanContext ctx,
        CancellationToken ct = default)
        => Task.FromResult(AutomationLinuxPlanResult.NotSupported(ActionKey));
}

internal sealed class WorkflowAutomationHandlerContext
{
    public required long JobId { get; init; }
    public required Guid WorkflowUid { get; init; }
    public required string ActionKey { get; init; }
    public required int AttemptNumber { get; init; }
    public System.Text.Json.JsonElement? Payload { get; init; }
}

// Result eines Linux-side Action-Handlers. Etappe 9a Schritt 5 Sub-A: erweitert um strukturierten
// Failure-Pfad (IsSuccess + ErrorMessage + FailureKind), damit Linux-Handler `failure_kind` setzen
// koennen — analog zum Worker-Pfad. Default IsSuccess=true → bestehende simulierte Handler bleiben
// rueckwaertskompatibel (sie initialisieren das Record via Object-Init mit Output+Logs).
internal sealed class WorkflowAutomationHandlerResult
{
    public bool IsSuccess { get; init; } = true;
    public string? ErrorMessage { get; init; }
    public string? FailureKind { get; init; }
    public System.Text.Json.JsonElement? Output { get; init; }
    public required IReadOnlyList<WorkflowAutomationLogEntry> Logs { get; init; }

    public static WorkflowAutomationHandlerResult Success(
        System.Text.Json.JsonElement? output,
        IReadOnlyList<WorkflowAutomationLogEntry> logs)
        => new() { IsSuccess = true, Output = output, Logs = logs };

    public static WorkflowAutomationHandlerResult Failure(
        string errorMessage,
        IReadOnlyList<WorkflowAutomationLogEntry> logs,
        string? failureKind = null,
        System.Text.Json.JsonElement? output = null)
        => new() { IsSuccess = false, ErrorMessage = errorMessage, FailureKind = failureKind, Output = output, Logs = logs };
}

internal sealed class WorkflowAutomationLogEntry
{
    public required string Level { get; init; }
    public required string Message { get; init; }
    public System.Text.Json.JsonElement? Details { get; init; }
}
