using Npgsql;

namespace API;

internal interface IWorkflowStatusCalculationService
{
    Task<bool> TryLockWorkflowForTaskStatusUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId);

    Task<(long WorkflowId, Guid WorkflowUid, string CurrentStatus, bool IsRequired, string TaskKey, bool IsApprovalTask, string TaskTitle, long? NodeInstanceId, bool IsRuntimeNodeTask)?> LoadTaskStateForUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId);

    Task<bool> AreTaskDependenciesSatisfied(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId);

    Task PersistTaskStatus(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        string nextStatus);

    Task SyncPrimaryAssignmentCompletion(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        string taskStatus);

    Task RecalculateWorkflowTaskAvailability(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId);

    Task RecalculateAndPersistWorkflowStatus(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        long? actorUserId);
}
