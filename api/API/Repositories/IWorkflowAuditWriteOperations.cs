using Npgsql;

namespace API;

internal interface IWorkflowAuditWriteOperations
{
    Task InsertAuditEntry(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        long? taskId,
        long? actorUserId,
        string eventType,
        string? oldValue,
        string? newValue,
        string? detail = null);
}
