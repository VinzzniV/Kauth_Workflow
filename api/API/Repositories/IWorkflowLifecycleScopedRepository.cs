using Npgsql;

namespace API;

// Scoped operations: caller (WorkflowLifecycleService oder Repo-Wrapper) besitzt Conn+Tx
// und gibt sie als Parameter rein.
internal interface IWorkflowLifecycleScopedRepository
{
    // null = nicht gefunden / gesperrt. Non-null = Task-Level-Work erledigt; Flags zeigen,
    // welche Runtime-Seite der Aufrufer anschliessend in derselben Transaktion ausfuehren soll.
    Task<TaskStatusUpdateScopeResult?> UpdateTaskStatusInScope(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        string normalizedStatus,
        long actorUserId,
        CancellationToken cancellationToken = default);

    // null = nicht gefunden / gesperrt.
    Task<DecideTaskApprovalScopeResult?> DecideTaskApprovalInScope(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        TaskApprovalDecisionRequest request,
        long actorUserId,
        CancellationToken cancellationToken = default);

    Task CompleteAutomationJobSuccessInScope(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ClaimedAutomationJobRecord job,
        WorkflowAutomationHandlerResult result,
        CancellationToken cancellationToken = default);

    Task CompleteRuntimeTaskNodeInScope(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        Guid workflowUid,
        long nodeInstanceId,
        long actorUserId,
        string? comment = null,
        CancellationToken cancellationToken = default);

    Task TryAdvanceRuntimeSetupInScope(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        Guid workflowUid,
        long actorUserId,
        CancellationToken cancellationToken = default);

    Task ApplyApprovalNodeDecisionInScope(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        Guid workflowUid,
        long nodeInstanceId,
        bool approved,
        long actorUserId,
        CancellationToken cancellationToken = default);
}
