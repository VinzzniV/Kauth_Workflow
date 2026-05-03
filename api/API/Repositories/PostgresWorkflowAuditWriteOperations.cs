using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed class PostgresWorkflowAuditWriteOperations : IWorkflowAuditWriteOperations
{
    public async Task InsertAuditEntry(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        long? taskId,
        long? actorUserId,
        string eventType,
        string? oldValue,
        string? newValue,
        string? detail = null)
    {
        const string sql = @"
INSERT INTO workflow_audit_log (
    workflow_id,
    task_id,
    actor_user_id,
    event_type,
    old_value,
    new_value,
    detail
)
VALUES (
    @workflowId,
    @taskId,
    @actorUserId,
    @eventType,
    @oldValue,
    @newValue,
    @detail
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.Add("taskId", NpgsqlDbType.Bigint).Value = (object?)taskId ?? DBNull.Value;
        command.Parameters.Add("actorUserId", NpgsqlDbType.Bigint).Value = (object?)actorUserId ?? DBNull.Value;
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.Add("oldValue", NpgsqlDbType.Varchar).Value = (object?)oldValue ?? DBNull.Value;
        command.Parameters.Add("newValue", NpgsqlDbType.Varchar).Value = (object?)newValue ?? DBNull.Value;
        command.Parameters.Add("detail", NpgsqlDbType.Text).Value = (object?)detail ?? DBNull.Value;
        await command.ExecuteNonQueryAsync();
    }
}
