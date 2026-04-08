namespace API;

internal interface IWorkflowAutomationActionHandler
{
    string ActionKey { get; }
    Task<WorkflowAutomationHandlerResult> ExecuteAsync(
        WorkflowAutomationHandlerContext context,
        CancellationToken cancellationToken = default);
}

internal sealed class WorkflowAutomationHandlerContext
{
    public required long JobId { get; init; }
    public required Guid WorkflowUid { get; init; }
    public required string ActionKey { get; init; }
    public required int AttemptNumber { get; init; }
    public System.Text.Json.JsonElement? Payload { get; init; }
}

internal sealed class WorkflowAutomationHandlerResult
{
    public System.Text.Json.JsonElement? Output { get; init; }
    public required IReadOnlyList<WorkflowAutomationLogEntry> Logs { get; init; }
}

internal sealed class WorkflowAutomationLogEntry
{
    public required string Level { get; init; }
    public required string Message { get; init; }
    public System.Text.Json.JsonElement? Details { get; init; }
}
