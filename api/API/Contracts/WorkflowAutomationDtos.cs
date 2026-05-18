using System.Text.Json;

namespace API;

public sealed class ActionDefinitionDto
{
    public required long Id { get; init; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string HandlerType { get; init; }
    public required bool IsSimulated { get; init; }
    public JsonElement? ParameterSchema { get; init; }
    public required bool IsActive { get; init; }
    public required bool RequiresApproval { get; init; }
    public required bool IsIdempotent { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed class AutomationJobDetailDto
{
    public required long Id { get; init; }
    public required long WorkflowId { get; init; }
    public required long WorkflowNodeInstanceId { get; init; }
    public required string NodeKey { get; init; }
    public required string ActionKey { get; init; }
    public required string ActionName { get; init; }
    public required int ExecutionOrder { get; init; }
    public required string OnErrorBehavior { get; init; }
    public required string Status { get; init; }
    public JsonElement? Payload { get; init; }
    public DateTime? AvailableAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required List<AutomationJobAttemptDto> Attempts { get; init; }
    public required List<AutomationJobLogDto> Logs { get; init; }
}

public sealed class AutomationJobAttemptDto
{
    public required long Id { get; init; }
    public required int AttemptNumber { get; init; }
    public required string Status { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    // Slice 7: failure_kind aus automation_job_attempts (permanent | transient | null).
    // Wird gebraucht, damit der Approval-Live-Log Permanent- vs Transient-Failures
    // im UI unterscheiden kann (sonst sieht ein Retry-Loop aus wie ein finaler Fehler).
    public string? FailureKind { get; init; }
}

public sealed class AutomationJobLogDto
{
    public required long Id { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
    public JsonElement? Details { get; init; }
    public required DateTime CreatedAt { get; init; }
}
