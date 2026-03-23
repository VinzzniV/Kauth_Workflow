using Npgsql;
using NpgsqlTypes;

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

        var targets = await CreateReadyTaskNotifications(connection, transaction, workflowId.Value);
        await transaction.CommitAsync();
        return targets;
    }

    public async Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowCompletionNotifications(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string workflowSql = @"
SELECT id, created_by_user_id, status
FROM workflows
WHERE uid = @workflowUid
LIMIT 1
FOR UPDATE;";

        long? workflowId = null;
        long? createdByUserId = null;
        string? workflowStatus = null;

        await using (var workflowCommand = new NpgsqlCommand(workflowSql, connection, transaction))
        {
            workflowCommand.Parameters.AddWithValue("workflowUid", workflowUid);
            await using var reader = await workflowCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                workflowId = reader.GetInt64(0);
                createdByUserId = reader.IsDBNull(1) ? null : reader.GetInt64(1);
                workflowStatus = reader.GetString(2);
            }
        }

        if (!workflowId.HasValue
            || !createdByUserId.HasValue
            || !string.Equals(workflowStatus, "completed", StringComparison.OrdinalIgnoreCase))
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

        var recipient = await LoadActiveUserNotificationRecipient(connection, transaction, createdByUserId.Value);
        if (!recipient.HasValue)
        {
            await transaction.RollbackAsync();
            return [];
        }

        var inserted = await InsertWorkflowNotification(
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
                NotificationId = inserted.NotificationId,
                WorkflowTaskId = null,
                NotificationType = "workflow_completed",
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

    private static async Task<(long UserId, string DisplayName, string Email, string IdentityKey, string PreferredPath)?> LoadActiveUserNotificationRecipient(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId)
    {
        const string sql = @"
WITH effective_roles AS (
    SELECT r.role_key
    FROM app_user_roles ur
    JOIN app_roles r ON r.id = ur.app_role_id
    WHERE ur.app_user_id = @userId
      AND r.is_active = TRUE
      AND r.role_kind = 'system'
    UNION
    SELECT r.role_key
    FROM app_user_groups ug
    JOIN app_groups g ON g.id = ug.app_group_id
    JOIN app_group_roles gr ON gr.app_group_id = ug.app_group_id
    JOIN app_roles r ON r.id = gr.app_role_id
    WHERE ug.app_user_id = @userId
      AND g.is_active = TRUE
      AND r.is_active = TRUE
      AND r.role_kind = 'system'
)
SELECT
    u.id,
    u.display_name,
    COALESCE(NULLIF(BTRIM(u.notification_email), ''), u.email) AS target_email,
    COALESCE(NULLIF(BTRIM(u.external_key), ''), u.email) AS identity_key,
    EXISTS (
        SELECT 1
        FROM effective_roles er
        WHERE er.role_key IN ('auth_hr', 'auth_admin', 'auth_reader')
    ) AS can_access_workflow_overview,
    EXISTS (
        SELECT 1
        FROM effective_roles er
        WHERE er.role_key = 'auth_manager'
    ) AS can_access_supervisor
FROM app_users u
WHERE u.id = @userId
  AND u.is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var canAccessWorkflowOverview = reader.GetBoolean(4);
        var canAccessSupervisor = reader.GetBoolean(5);

        var preferredPath = canAccessWorkflowOverview
            ? "/workflows"
            : canAccessSupervisor
                ? "/supervisor"
                : "/tasks/my";

        return (reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), preferredPath);
    }

    private static async Task<WorkflowNotificationDispatchTarget> InsertWorkflowNotification(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        long? workflowTaskId,
        long recipientUserId,
        string targetName,
        string targetEmail,
        string notificationType)
    {
        const string sql = @"
INSERT INTO workflow_notifications (
    workflow_id,
    workflow_task_id,
    recipient_user_id,
    target_email,
    target_name,
    notification_type,
    status
)
VALUES (
    @workflowId,
    @workflowTaskId,
    @recipientUserId,
    @targetEmail,
    @targetName,
    @notificationType,
    'pending'
)
RETURNING id, workflow_task_id, notification_type, target_name, target_email;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.Add("workflowTaskId", NpgsqlDbType.Bigint).Value = (object?)workflowTaskId ?? DBNull.Value;
        command.Parameters.AddWithValue("recipientUserId", recipientUserId);
        command.Parameters.AddWithValue("targetEmail", targetEmail);
        command.Parameters.AddWithValue("targetName", targetName);
        command.Parameters.AddWithValue("notificationType", notificationType);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new WorkflowNotificationDispatchTarget
        {
            NotificationId = reader.GetInt64(0),
            WorkflowTaskId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
            NotificationType = reader.GetString(2),
            RecipientUserId = recipientUserId,
            RecipientIdentityKey = null,
            TargetName = reader.GetString(3),
            TargetEmail = reader.GetString(4),
            TaskTitle = null,
            PreferredPath = null
        };
    }

    private static async Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowNotifications(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        int departmentId)
    {
        var recipients = new Dictionary<long, (string DisplayName, string Email, string IdentityKey, string PreferredPath)>();

        var hrResponsibilityId = await LoadResponsibilityIdByKey(connection, transaction, "hr_onboarding");
        if (hrResponsibilityId.HasValue)
        {
            var hrUserId = await ResolvePrimaryAssigneeUserId(connection, transaction, hrResponsibilityId.Value, departmentId);
            if (hrUserId.HasValue)
            {
                var hrRecipient = await LoadActiveUserNotificationRecipient(connection, transaction, hrUserId.Value);
                if (hrRecipient.HasValue)
                {
                    recipients[hrRecipient.Value.UserId] = (
                        hrRecipient.Value.DisplayName,
                        hrRecipient.Value.Email,
                        hrRecipient.Value.IdentityKey,
                        hrRecipient.Value.PreferredPath);
                }
            }
        }

        var departmentSelectionAssignment = await ResolveDepartmentRequirementSelectionAssignment(
            connection,
            transaction,
            departmentId);

        if (departmentSelectionAssignment.UserId.HasValue)
        {
            var departmentRecipient = await LoadActiveUserNotificationRecipient(
                connection,
                transaction,
                departmentSelectionAssignment.UserId.Value);

            if (departmentRecipient.HasValue)
            {
                recipients[departmentRecipient.Value.UserId] = (
                    departmentRecipient.Value.DisplayName,
                    departmentRecipient.Value.Email,
                    departmentRecipient.Value.IdentityKey,
                    departmentRecipient.Value.PreferredPath);
            }
        }

        var targets = new List<WorkflowNotificationDispatchTarget>();
        foreach (var recipient in recipients)
        {
            targets.Add(await InsertWorkflowNotification(
                connection,
                transaction,
                workflowId,
                null,
                recipient.Key,
                recipient.Value.DisplayName,
                recipient.Value.Email,
                "workflow_created"));

            targets[^1] = new WorkflowNotificationDispatchTarget
            {
                NotificationId = targets[^1].NotificationId,
                WorkflowTaskId = null,
                NotificationType = "workflow_created",
                RecipientUserId = recipient.Key,
                RecipientIdentityKey = recipient.Value.IdentityKey,
                TargetName = recipient.Value.DisplayName,
                TargetEmail = recipient.Value.Email,
                TaskTitle = null,
                PreferredPath = recipient.Value.PreferredPath
            };
        }

        return targets;
    }

    private static async Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotifications(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string taskSql = @"
SELECT
    ta.assignee_user_id,
    wt.id,
    wt.title
FROM workflow_tasks wt
JOIN task_assignments ta
    ON ta.workflow_task_id = wt.id
   AND ta.is_primary = TRUE
WHERE wt.workflow_id = @workflowId
  AND wt.status IN ('open', 'ready')
  AND wt.task_key <> @supervisorTaskKey
  AND ta.assignee_user_id IS NOT NULL
  AND (
      (
          wt.ready_at IS NOT NULL
          AND NOT EXISTS (
              SELECT 1
              FROM workflow_notifications n
              WHERE n.workflow_task_id = wt.id
                AND n.recipient_user_id = ta.assignee_user_id
                AND n.notification_type = 'task_ready'
                AND n.status <> 'disabled'
                AND n.created_at >= wt.ready_at
          )
      )
      OR (
          wt.ready_at IS NULL
          AND NOT EXISTS (
              SELECT 1
              FROM workflow_notifications n
              WHERE n.workflow_task_id = wt.id
                AND n.recipient_user_id = ta.assignee_user_id
                AND n.notification_type = 'task_ready'
                AND n.status <> 'disabled'
          )
      )
  )
ORDER BY ta.assignee_user_id, wt.sort_order, wt.id;";

        var taskTitlesByRecipient = new Dictionary<long, List<(long WorkflowTaskId, string TaskTitle)>>();

        await using (var taskCommand = new NpgsqlCommand(taskSql, connection, transaction))
        {
            taskCommand.Parameters.AddWithValue("workflowId", workflowId);
            taskCommand.Parameters.AddWithValue("supervisorTaskKey", SupervisorRequirementTaskKey);

            await using var reader = await taskCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var recipientUserId = reader.GetInt64(0);
                if (!taskTitlesByRecipient.TryGetValue(recipientUserId, out var recipientTasks))
                {
                    recipientTasks = new List<(long WorkflowTaskId, string TaskTitle)>();
                    taskTitlesByRecipient[recipientUserId] = recipientTasks;
                }

                recipientTasks.Add((reader.GetInt64(1), reader.GetString(2)));
            }
        }

        if (taskTitlesByRecipient.Count == 0)
        {
            return [];
        }

        var targets = new List<WorkflowNotificationDispatchTarget>();
        foreach (var recipientUserId in taskTitlesByRecipient.Keys.OrderBy(id => id))
        {
            if (!taskTitlesByRecipient.TryGetValue(recipientUserId, out var recipientTasks) || recipientTasks.Count == 0)
            {
                continue;
            }

            var recipient = await LoadActiveUserNotificationRecipient(connection, transaction, recipientUserId);
            if (!recipient.HasValue)
            {
                continue;
            }

            foreach (var recipientTask in recipientTasks)
            {
                var inserted = await InsertWorkflowNotification(
                    connection,
                    transaction,
                    workflowId,
                    recipientTask.WorkflowTaskId,
                    recipientUserId,
                    recipient.Value.DisplayName,
                    recipient.Value.Email,
                    "task_ready");

                targets.Add(new WorkflowNotificationDispatchTarget
                {
                    NotificationId = inserted.NotificationId,
                    WorkflowTaskId = recipientTask.WorkflowTaskId,
                    NotificationType = "task_ready",
                    RecipientUserId = recipientUserId,
                    RecipientIdentityKey = recipient.Value.IdentityKey,
                    TargetName = recipient.Value.DisplayName,
                    TargetEmail = recipient.Value.Email,
                    TaskTitle = recipientTask.TaskTitle,
                    PreferredPath = recipient.Value.PreferredPath
                });
            }
        }

        return targets;
    }
}
