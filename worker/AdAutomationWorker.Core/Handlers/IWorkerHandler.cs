using System.Text.Json;

namespace AdAutomationWorker.Core.Handlers;

// Worker-eigenes Handler-Interface. Bewusste Spiegelung zu IWorkflowAutomationActionHandler in
// der Linux-API — gleiche Methodensignatur, eigene Records, kein Shared-Library-Risiko. Damit
// koennen Worker und Linux-API getrennt versioniert werden, ohne Vertragsbruch zur Compile-Zeit.
public interface IWorkerHandler
{
    string ActionKey { get; }

    Task<WorkerHandlerResult> ExecuteAsync(WorkerHandlerContext context, CancellationToken cancellationToken);
}

public sealed record WorkerHandlerContext
{
    public required long JobId { get; init; }
    public required Guid WorkflowUid { get; init; }
    public required long WorkflowNodeInstanceId { get; init; }
    public required string ActionKey { get; init; }
    public required JsonElement Payload { get; init; }
    public required int AttemptNumber { get; init; }
}

public sealed record WorkerHandlerResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public JsonElement? Output { get; init; }
    public IReadOnlyList<WorkerLogEntry> Logs { get; init; } = Array.Empty<WorkerLogEntry>();

    // Klassifikation des Failures fuer die Linux-API-Retry-Policy. "permanent" -> sofort
    // FinalFail (ueberschreibt is_idempotent + attemptNumber); "transient" oder null ->
    // bestehende Retry-Logik. Beim Success-Result irrelevant (immer null).
    public string? FailureKind { get; init; }

    public static WorkerHandlerResult Success(JsonElement? output, IReadOnlyList<WorkerLogEntry> logs)
        => new() { IsSuccess = true, Output = output, Logs = logs };

    public static WorkerHandlerResult Failure(string errorMessage, IReadOnlyList<WorkerLogEntry> logs, string? failureKind = null, JsonElement? output = null)
        => new() { IsSuccess = false, ErrorMessage = errorMessage, Logs = logs, FailureKind = failureKind, Output = output };
}

// Konstanten fuer FailureKind. String-basiert auf DB-Seite (varchar+Check), damit Linux- und
// Worker-Handler gleichen Vertrag teilen, ohne Enum-Cross-Compile.
public static class WorkerFailureKinds
{
    public const string Permanent = "permanent";
    public const string Transient = "transient";
}

public sealed record WorkerLogEntry
{
    public required string Level { get; init; }
    public required string Message { get; init; }
    public JsonElement? Details { get; init; }
}
