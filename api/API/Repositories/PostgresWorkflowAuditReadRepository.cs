using Npgsql;

namespace API;

internal sealed class PostgresWorkflowAuditReadRepository : IWorkflowAuditReadRepository
{
    public async Task<List<WorkflowAuditEntryDto>> GetWorkflowAuditLog(Guid workflowUid, int limit = 200, int offset = 0)
    {
        await using var connection = new NpgsqlConnection(LifecycleRuntimeSettingsResolver.GetRequiredConnectionString());
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
}
