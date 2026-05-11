using Npgsql;

namespace API;

// Eigenstaendiges Read-Repository fuer Notification-Dispatch-Daten.
// Implementiert sowohl die Preview-Reads (Template-Service) als auch die generischen Notification-Reads
// (RuntimeService, Endpoint-Replays). Schreib-Pfade leben weiter im PostgresWorkflowRepository-Partial
// (CreateReady*, CreateWorkflowCompletion*, ApplyNotificationDispatchResults).
internal sealed class PostgresWorkflowNotificationReadRepository : INotificationTemplatePreviewRepository, IWorkflowNotificationReadRepository
{
    private static string GetConnectionString()
    {
        return LifecycleRuntimeSettingsResolver.GetRequiredConnectionString();
    }

    public async Task<List<WorkflowNotificationDispatchTarget>> GetWorkflowCreatedPreviewTargets(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string workflowSql = @"
SELECT
    w.id,
    w.department_id,
    pt.requires_supervisor_step,
    COALESCE(NULLIF(BTRIM(d.definition_key), ''), pt.definition_key),
    COALESCE(NULLIF(BTRIM(d.name), ''), pt.name)
FROM workflows w
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
LEFT JOIN workflow_definitions d ON d.id = v.workflow_definition_id
WHERE w.uid = @workflowUid
LIMIT 1
FOR UPDATE OF w;";

        long? workflowId = null;
        int? departmentId = null;
        bool requiresSupervisorStep = false;
        string? processTypeKey = null;
        string? processTypeName = null;

        await using (var workflowCommand = new NpgsqlCommand(workflowSql, connection, transaction))
        {
            workflowCommand.Parameters.AddWithValue("workflowUid", workflowUid);
            await using var reader = await workflowCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                workflowId = reader.GetInt64(0);
                departmentId = reader.GetInt32(1);
                requiresSupervisorStep = reader.GetBoolean(2);
                processTypeKey = reader.GetString(3);
                processTypeName = reader.GetString(4);
            }
        }

        if (!workflowId.HasValue
            || !departmentId.HasValue
            || string.IsNullOrWhiteSpace(processTypeKey)
            || string.IsNullOrWhiteSpace(processTypeName))
        {
            await transaction.RollbackAsync();
            return [];
        }

        var recipients = await PostgresWorkflowNotificationDispatchOperations.ResolveWorkflowCreatedRecipientsAsync(
            connection,
            transaction,
            departmentId.Value,
            requiresSupervisorStep,
            processTypeKey);
        var targets = recipients
            .OrderBy(entry => entry.Key)
            .Select(entry => new WorkflowNotificationDispatchTarget
            {
                NotificationId = 0,
                WorkflowTaskId = null,
                NotificationType = "workflow_created",
                WorkflowDefinitionKey = processTypeKey,
                WorkflowDefinitionName = processTypeName,
                RecipientUserId = entry.Key,
                RecipientIdentityKey = entry.Value.IdentityKey,
                TargetName = entry.Value.DisplayName,
                TargetEmail = entry.Value.Email,
                TaskTitle = null,
                PreferredPath = entry.Value.PreferredPath
            })
            .ToList();

        await transaction.CommitAsync();
        return targets;
    }

    public async Task<List<WorkflowNotificationDispatchTarget>> GetWorkflowCreatedNotificationDispatchTargets(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string workflowSql = @"
SELECT
    w.id,
    COALESCE(NULLIF(BTRIM(d.definition_key), ''), pt.definition_key),
    COALESCE(NULLIF(BTRIM(d.name), ''), pt.name)
FROM workflows w
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
LEFT JOIN workflow_definitions d ON d.id = v.workflow_definition_id
WHERE w.uid = @workflowUid
LIMIT 1
FOR UPDATE OF w;";

        long? workflowId = null;
        string? processTypeKey = null;
        string? processTypeName = null;

        await using (var workflowCommand = new NpgsqlCommand(workflowSql, connection, transaction))
        {
            workflowCommand.Parameters.AddWithValue("workflowUid", workflowUid);
            await using var reader = await workflowCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                workflowId = reader.GetInt64(0);
                processTypeKey = reader.GetString(1);
                processTypeName = reader.GetString(2);
            }
        }

        if (!workflowId.HasValue
            || string.IsNullOrWhiteSpace(processTypeKey)
            || string.IsNullOrWhiteSpace(processTypeName))
        {
            await transaction.RollbackAsync();
            return [];
        }

        const string notificationSql = @"
SELECT
    n.id,
    n.recipient_user_id,
    n.target_name,
    n.target_email
FROM workflow_notifications n
WHERE n.workflow_id = @workflowId
  AND n.notification_type = 'workflow_created'
  AND n.status = 'pending'
ORDER BY n.id;";

        var pendingNotifications = new List<(long NotificationId, long? RecipientUserId, string TargetName, string TargetEmail)>();
        await using (var notificationCommand = new NpgsqlCommand(notificationSql, connection, transaction))
        {
            notificationCommand.Parameters.AddWithValue("workflowId", workflowId.Value);
            await using var reader = await notificationCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                pendingNotifications.Add((
                    reader.GetInt64(0),
                    reader.IsDBNull(1) ? null : reader.GetInt64(1),
                    reader.GetString(2),
                    reader.GetString(3)));
            }
        }

        var targets = new List<WorkflowNotificationDispatchTarget>(pendingNotifications.Count);
        foreach (var pendingNotification in pendingNotifications)
        {
            string? recipientIdentityKey = null;
            string? preferredPath = "/workflows";

            if (pendingNotification.RecipientUserId.HasValue)
            {
                var recipient = await PostgresRepositorySharedHelpers.LoadActiveUserNotificationRecipient(
                    connection,
                    transaction,
                    pendingNotification.RecipientUserId.Value);
                if (recipient.HasValue)
                {
                    recipientIdentityKey = recipient.Value.IdentityKey;
                    preferredPath = recipient.Value.PreferredPath;
                }
            }

            targets.Add(new WorkflowNotificationDispatchTarget
            {
                NotificationId = pendingNotification.NotificationId,
                WorkflowTaskId = null,
                NotificationType = "workflow_created",
                WorkflowDefinitionKey = processTypeKey,
                WorkflowDefinitionName = processTypeName,
                RecipientUserId = pendingNotification.RecipientUserId,
                RecipientIdentityKey = recipientIdentityKey,
                TargetName = pendingNotification.TargetName,
                TargetEmail = pendingNotification.TargetEmail,
                TaskTitle = null,
                PreferredPath = preferredPath
            });
        }

        await transaction.CommitAsync();
        return targets;
    }

    public async Task<List<WorkflowNotificationDispatchTarget>> GetTaskReadyPreviewTargets(Guid workflowUid)
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
        var targets = await PostgresWorkflowNotificationDispatchOperations.BuildReadyTaskNotificationPreviewTargetsAsync(
            connection,
            transaction,
            workflowId.Value,
            processType.Key,
            processType.Name);
        await transaction.CommitAsync();
        return targets;
    }
    public async Task<List<WorkflowNotificationDispatchTarget>> GetWorkflowCompletedPreviewTargets(Guid workflowUid)
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

        await transaction.CommitAsync();

        return
        [
            new WorkflowNotificationDispatchTarget
            {
                NotificationId = 0,
                WorkflowTaskId = null,
                NotificationType = "workflow_completed",
                WorkflowDefinitionKey = processTypeKey,
                WorkflowDefinitionName = processTypeName,
                RecipientUserId = createdByUserId.Value,
                RecipientIdentityKey = recipient.Value.IdentityKey,
                TargetName = recipient.Value.DisplayName,
                TargetEmail = recipient.Value.Email,
                TaskTitle = null,
                PreferredPath = recipient.Value.PreferredPath
            }
        ];
    }

    public async Task<List<Guid>> GetWorkflowUidsWithDisabledNotifications(string notificationType)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT DISTINCT w.uid
FROM workflow_notifications n
JOIN workflows w ON w.id = n.workflow_id
WHERE n.notification_type = @notificationType
  AND n.status = 'disabled'
ORDER BY w.uid;";

        var workflowUids = new List<Guid>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("notificationType", notificationType);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            workflowUids.Add(reader.GetGuid(0));
        }

        return workflowUids;
    }
}
