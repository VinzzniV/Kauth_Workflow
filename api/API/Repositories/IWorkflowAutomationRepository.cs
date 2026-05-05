namespace API;

internal interface IWorkflowAutomationRepository
{
    Task<ClaimedAutomationJobRecord?> ClaimNextPendingAutomationJob(CancellationToken cancellationToken = default);
    Task CompleteAutomationJobFailure(
        ClaimedAutomationJobRecord job,
        string errorMessage,
        bool shouldRetry,
        DateTime? retryAvailableAt,
        IReadOnlyList<WorkflowAutomationLogEntry> logs,
        CancellationToken cancellationToken = default);
}

internal sealed class ClaimedAutomationJobRecord
{
    public required long JobId { get; init; }
    public required long WorkflowId { get; init; }
    public required Guid WorkflowUid { get; init; }
    public required long WorkflowNodeInstanceId { get; init; }
    public required long WorkflowNodeId { get; init; }
    public required string NodeKey { get; init; }
    public required string NodeType { get; init; }
    public required long WorkflowNodeActionId { get; init; }
    public required int ExecutionOrder { get; init; }
    public required string OnErrorBehavior { get; init; }
    public required long ActionDefinitionId { get; init; }
    public required string ActionKey { get; init; }
    public required string ActionName { get; init; }
    public required string HandlerType { get; init; }
    public required bool IsIdempotent { get; init; }
    public int AttemptNumber { get; set; }
    public required long? CreatedByUserId { get; init; }
    public System.Text.Json.JsonElement? Payload { get; init; }
}
