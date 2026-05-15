using System.Text.Json;
using System.Text.Json.Nodes;

namespace AdAutomationWorker.Core.Handlers;

// Worker-eigenes Handler-Interface. Bewusste Spiegelung zu IWorkflowAutomationActionHandler in
// der Linux-API — gleiche Methodensignatur, eigene Records, kein Shared-Library-Risiko. Damit
// koennen Worker und Linux-API getrennt versioniert werden, ohne Vertragsbruch zur Compile-Zeit.
public interface IWorkerHandler
{
    string ActionKey { get; }

    Task<WorkerHandlerResult> ExecuteAsync(WorkerHandlerContext context, CancellationToken cancellationToken);

    Task<WorkerPlanResult> PlanAsync(WorkerPlanContext context, CancellationToken cancellationToken)
        => Task.FromResult(WorkerPlanResult.NotSupported(ActionKey));
}

public sealed record WorkerPlanContext
{
    public required string WorkflowInstanceUid { get; init; }
    public required string ActionKey { get; init; }
    public required JsonElement Payload { get; init; }
}

public sealed record WorkerPlanResult(bool IsSuccess, JsonNode? Plan, string? ErrorMessage)
{
    public static WorkerPlanResult NotSupported(string actionKey) =>
        new(false, null, $"PlanAsync not supported for action '{actionKey}'");

    public static WorkerPlanResult Success(JsonNode plan) =>
        new(true, plan, null);

    public static WorkerPlanResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
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

    // Optionaler atomarer Vault-Schreib-Auftrag (Etappe 9a Schritt 6 Sub-B). Wenn gesetzt,
    // schreibt PostgresWorkerJobStore.MarkJobSucceededAsync den verschluesselten Wert in
    // `temporary_credentials` und patcht `credentialVaultId` im Output-JSON mit der erzeugten
    // UUID — alles in derselben Tx wie Job-Success + Attempt. Damit gibt's keinen Crash-Pfad
    // mit Vault-Waise oder fehlendem Pointer.
    public PendingVaultWrite? VaultWrite { get; init; }

    public static WorkerHandlerResult Success(JsonElement? output, IReadOnlyList<WorkerLogEntry> logs)
        => new() { IsSuccess = true, Output = output, Logs = logs };

    public static WorkerHandlerResult Failure(string errorMessage, IReadOnlyList<WorkerLogEntry> logs, string? failureKind = null, JsonElement? output = null)
        => new() { IsSuccess = false, ErrorMessage = errorMessage, Logs = logs, FailureKind = failureKind, Output = output };
}

// Atomarer Vault-Schreib-Auftrag. Der Handler reicht den Plain-Wert + Pointer-Metadaten
// an den JobStore weiter; der JobStore verschluesselt und schreibt in derselben Tx wie
// die Job-Success-Markierung. Damit verlaesst das Plain-Geheimnis den Worker-Prozess nie
// unverschluesselt -- es lebt nur zwischen Handler-Exit und JobStore-Insert im Heap.
public sealed record PendingVaultWrite
{
    public required string PlainSecret { get; init; }
    public required long WorkflowNodeInstanceId { get; init; }
    public required string CredentialType { get; init; }
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
