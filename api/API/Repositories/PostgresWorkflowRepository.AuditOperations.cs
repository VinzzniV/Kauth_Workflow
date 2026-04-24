using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private static async Task InsertAuditEntry(
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

    public async Task<List<WorkflowAuditEntryDto>> GetWorkflowAuditLog(Guid workflowUid, int limit = 200, int offset = 0)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    al.id,
    al.event_type,
    al.created_at,
    al.task_id,
    wt.task_key,
    wt.title,
    al.actor_user_id,
    actor.display_name,
    al.old_value,
    al.new_value,
    al.detail
FROM workflows w
JOIN workflow_audit_log al ON al.workflow_id = w.id
LEFT JOIN workflow_tasks wt ON wt.id = al.task_id
LEFT JOIN app_users actor ON actor.id = al.actor_user_id
WHERE w.uid = @workflowUid
ORDER BY al.created_at DESC, al.id DESC
LIMIT @limit
OFFSET @offset;";

        var entries = new List<WorkflowAuditEntryDto>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workflowUid", workflowUid);
        command.Parameters.AddWithValue("limit", limit);
        command.Parameters.AddWithValue("offset", offset);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new WorkflowAuditEntryDto
            {
                Id = reader.GetInt64(0),
                EventType = reader.GetString(1),
                CreatedAt = reader.GetDateTime(2),
                TaskId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                TaskKey = reader.IsDBNull(4) ? null : reader.GetString(4),
                TaskTitle = reader.IsDBNull(5) ? null : reader.GetString(5),
                ActorUserId = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                ActorUserName = reader.IsDBNull(7) ? null : reader.GetString(7),
                OldValue = reader.IsDBNull(8) ? null : reader.GetString(8),
                NewValue = reader.IsDBNull(9) ? null : reader.GetString(9),
                Detail = reader.IsDBNull(10) ? null : reader.GetString(10)
            });
        }

        return entries;
    }

    private static string BuildTaskStatusAuditDetail(string taskTitle)
    {
        return $"Task: {taskTitle}";
    }

    private static async Task<string?> LoadPrimaryTaskAssignmentAuditLabel(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId)
    {
        const string sql = @"
SELECT COALESCE(
    u.display_name,
    CASE
        WHEN r.id IS NULL THEN NULL
        WHEN d.name IS NULL OR d.name = '' THEN r.name
        ELSE d.name || ' - ' || r.name
    END
) AS assignee_label
FROM task_assignments ta
LEFT JOIN app_users u ON u.id = ta.assignee_user_id
LEFT JOIN app_responsibilities r ON r.id = ta.assignee_responsibility_id
LEFT JOIN departments d ON d.id = r.department_id
WHERE ta.workflow_task_id = @taskId
  AND ta.is_primary = TRUE
ORDER BY ta.id DESC
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);
        return await command.ExecuteScalarAsync() as string;
    }
}
