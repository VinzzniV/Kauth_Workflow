using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    public async Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotifications(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string workflowSql = @"
SELECT id
FROM workflows
WHERE uid = @workflowUid
LIMIT 1
FOR UPDATE;";

        long? workflowId = null;
        await using (var workflowCommand = new NpgsqlCommand(workflowSql, connection, transaction))
        {
            workflowCommand.Parameters.AddWithValue("workflowUid", workflowUid);
            var scalar = await workflowCommand.ExecuteScalarAsync();
            workflowId = scalar is null ? null : (long?)scalar;
        }

        if (!workflowId.HasValue)
        {
            await transaction.RollbackAsync();
            return [];
        }

        var processType = await PostgresWorkflowNotificationDispatchOperations.LoadWorkflowProcessTypeForNotificationsAsync(connection, transaction, workflowId.Value);
        var targets = await PostgresWorkflowNotificationDispatchOperations.CreateReadyTaskNotificationsAsync(connection, transaction, workflowId.Value, processType.Key, processType.Name);
        await transaction.CommitAsync();
        return targets;
    }

    public async Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowCompletionNotifications(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string workflowSql = @"
SELECT
    w.id,
    w.created_by_user_id,
    w.status,
    COALESCE(NULLIF(BTRIM(d.definition_key), ''), pt.definition_key),
    COALESCE(NULLIF(BTRIM(d.name), ''), pt.name)
FROM workflows w
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
LEFT JOIN workflow_definitions d ON d.id = v.workflow_definition_id
WHERE uid = @workflowUid
LIMIT 1
FOR UPDATE OF w;";

        long? workflowId = null;
        long? createdByUserId = null;
        string? workflowStatus = null;
        string? processTypeKey = null;
        string? processTypeName = null;

        await using (var workflowCommand = new NpgsqlCommand(workflowSql, connection, transaction))
        {
            workflowCommand.Parameters.AddWithValue("workflowUid", workflowUid);
            await using var reader = await workflowCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                workflowId = reader.GetInt64(0);
                createdByUserId = reader.IsDBNull(1) ? null : reader.GetInt64(1);
                workflowStatus = reader.GetString(2);
                processTypeKey = reader.GetString(3);
                processTypeName = reader.GetString(4);
            }
        }

        if (!workflowId.HasValue
            || !createdByUserId.HasValue
            || !string.Equals(workflowStatus, "completed", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(processTypeKey)
            || string.IsNullOrWhiteSpace(processTypeName))
        {
            await transaction.RollbackAsync();
            return [];
        }

        const string existingNotificationSql = @"
SELECT 1
FROM workflow_notifications
WHERE workflow_id = @workflowId
  AND recipient_user_id = @recipientUserId
  AND notification_type = 'workflow_completed'
  AND status <> 'disabled'
LIMIT 1;";

        await using (var existingCommand = new NpgsqlCommand(existingNotificationSql, connection, transaction))
        {
            existingCommand.Parameters.AddWithValue("workflowId", workflowId.Value);
            existingCommand.Parameters.AddWithValue("recipientUserId", createdByUserId.Value);
            var existing = await existingCommand.ExecuteScalarAsync();
            if (existing is not null)
            {
                await transaction.RollbackAsync();
                return [];
            }
        }

        var recipient = await PostgresRepositorySharedHelpers.LoadActiveUserNotificationRecipient(connection, transaction, createdByUserId.Value);
        if (!recipient.HasValue)
        {
            await transaction.RollbackAsync();
            return [];
        }

        var notificationId = await PostgresWorkflowNotificationDispatchOperations.InsertWorkflowNotificationAsync(
            connection,
            transaction,
            workflowId.Value,
            null,
            createdByUserId.Value,
            recipient.Value.DisplayName,
            recipient.Value.Email,
            "workflow_completed");

        await transaction.CommitAsync();

        return
        [
            new WorkflowNotificationDispatchTarget
            {
                NotificationId = notificationId,
                WorkflowTaskId = null,
                NotificationType = "workflow_completed",
                LegacyProcessTypeKey = processTypeKey,
                ProcessTypeName = processTypeName,
                RecipientUserId = createdByUserId.Value,
                RecipientIdentityKey = recipient.Value.IdentityKey,
                TargetName = recipient.Value.DisplayName,
                TargetEmail = recipient.Value.Email,
                TaskTitle = null,
                PreferredPath = recipient.Value.PreferredPath
            }
        ];
    }

    // Benachrichtigungsergebnisse werden gesammelt rueckgeschrieben, damit der Workflow-Verlauf nachvollziehbar bleibt.
    public async Task ApplyNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results)
    {
        if (results.Count == 0)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string updateSql = @"
UPDATE workflow_notifications
SET
    status = @status,
    attempts = attempts + CASE WHEN @attempted THEN 1 ELSE 0 END,
    sent_at = CASE WHEN @status = 'sent' THEN NOW() ELSE sent_at END,
    last_error = @lastError
WHERE id = @notificationId;";

        foreach (var result in results)
        {
            await using var command = new NpgsqlCommand(updateSql, connection, transaction);
            command.Parameters.AddWithValue("status", result.Status);
            command.Parameters.AddWithValue("attempted", result.Attempted);
            command.Parameters.AddWithValue("lastError", (object?)result.ErrorMessage ?? DBNull.Value);
            command.Parameters.AddWithValue("notificationId", result.NotificationId);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }
}
