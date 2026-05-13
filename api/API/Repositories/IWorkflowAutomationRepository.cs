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
    Task UnclaimAutomationJobAsync(long jobId, CancellationToken cancellationToken = default);

    // Etappe 9a Schritt 2: Sweeper + External-Completion (Worker)
    Task<IReadOnlyList<ExternalCompletionClaim>> ClaimExternalCompletionsBatchAsync(int limit, CancellationToken cancellationToken = default);
    Task<ExternalCompletionContext?> LoadExternalCompletionContextAsync(long jobId, CancellationToken cancellationToken = default);
    Task ApplyExternalCompletionSuccessAsync(long jobId, CancellationToken cancellationToken = default);
    Task ApplyExternalCompletionFailureAsync(long jobId, WorkflowAutomationRetryOutcome outcome, CancellationToken cancellationToken = default);

    // Etappe 9a Schritt 4 Sub-B: zentraler Stale-Worker-Claim-Sweep. Setzt running-Jobs mit
    // target_runtime IS NOT NULL und heartbeat_at < NOW() - staleTimeout zurueck auf pending.
    // Belt-and-Suspenders neben dem Worker-Lazy-Cleanup; greift, wenn alle Worker tot sind.
    Task<int> ReleaseStaleWorkerClaimsAsync(TimeSpan staleTimeout, CancellationToken cancellationToken = default);
}

internal sealed class ExternalCompletionClaim
{
    public required long JobId { get; init; }
    public required string Status { get; init; }
}

internal sealed class ExternalCompletionContext
{
    public required long JobId { get; init; }
    public required string Status { get; init; }
    public required int AttemptNumber { get; init; }
    public required bool IsIdempotent { get; init; }
    public string? FailureKind { get; init; }
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
