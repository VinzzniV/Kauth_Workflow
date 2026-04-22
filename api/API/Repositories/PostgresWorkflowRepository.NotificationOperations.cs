using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
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
    COALESCE(NULLIF(BTRIM(d.definition_key), ''), pt.key),
    COALESCE(NULLIF(BTRIM(d.name), ''), pt.name)
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
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

        var recipients = await ResolveWorkflowCreatedRecipients(
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
                ProcessTypeKey = processTypeKey,
                ProcessTypeName = processTypeName,
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
    COALESCE(NULLIF(BTRIM(d.definition_key), ''), pt.key),
    COALESCE(NULLIF(BTRIM(d.name), ''), pt.name)
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
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
                var recipient = await LoadActiveUserNotificationRecipient(
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
                ProcessTypeKey = processTypeKey,
                ProcessTypeName = processTypeName,
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

        var processType = await LoadWorkflowProcessTypeForNotifications(connection, transaction, workflowId.Value);
        var targets = await BuildReadyTaskNotificationPreviewTargets(
            connection,
            transaction,
            workflowId.Value,
            processType.Key,
            processType.Name);
        await transaction.CommitAsync();
        return targets;
    }

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

        var processType = await LoadWorkflowProcessTypeForNotifications(connection, transaction, workflowId.Value);
        var targets = await CreateReadyTaskNotifications(connection, transaction, workflowId.Value, processType.Key, processType.Name);
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
    COALESCE(NULLIF(BTRIM(d.definition_key), ''), pt.key),
    COALESCE(NULLIF(BTRIM(d.name), ''), pt.name)
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
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

        var recipient = await LoadActiveUserNotificationRecipient(connection, transaction, createdByUserId.Value);
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
                ProcessTypeKey = processTypeKey,
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
    COALESCE(NULLIF(BTRIM(d.definition_key), ''), pt.key),
    COALESCE(NULLIF(BTRIM(d.name), ''), pt.name)
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
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

        var recipient = await LoadActiveUserNotificationRecipient(connection, transaction, createdByUserId.Value);
        if (!recipient.HasValue)
        {
            await transaction.RollbackAsync();
            return [];
        }

        var notificationId = await InsertWorkflowNotification(
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
                ProcessTypeKey = processTypeKey,
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

    private static async Task<(string Key, string Name)> LoadWorkflowProcessTypeForNotifications(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = @"
SELECT
    COALESCE(NULLIF(BTRIM(d.definition_key), ''), pt.key),
    COALESCE(NULLIF(BTRIM(d.name), ''), pt.name)
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
LEFT JOIN workflow_definitions d ON d.id = v.workflow_definition_id
WHERE w.id = @workflowId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Workflow process type could not be loaded for notifications.");
        }

        return (reader.GetString(0), reader.GetString(1));
    }

    private static async Task<long> InsertWorkflowNotification(
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
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.Add("workflowTaskId", NpgsqlDbType.Bigint).Value = (object?)workflowTaskId ?? DBNull.Value;
        command.Parameters.AddWithValue("recipientUserId", recipientUserId);
        command.Parameters.AddWithValue("targetEmail", targetEmail);
        command.Parameters.AddWithValue("targetName", targetName);
        command.Parameters.AddWithValue("notificationType", notificationType);

        var id = await command.ExecuteScalarAsync();
        return (long)id!;
    }

    private static async Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowNotifications(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        int departmentId,
        bool requiresSupervisorStep,
        string processTypeKey,
        string processTypeName)
    {
        var recipients = await ResolveWorkflowCreatedRecipients(
            connection,
            transaction,
            departmentId,
            requiresSupervisorStep,
            processTypeKey);

        var targets = new List<WorkflowNotificationDispatchTarget>();
        foreach (var recipient in recipients)
        {
            var notificationId = await InsertWorkflowNotification(
                connection,
                transaction,
                workflowId,
                null,
                recipient.Key,
                recipient.Value.DisplayName,
                recipient.Value.Email,
                "workflow_created");

            targets.Add(new WorkflowNotificationDispatchTarget
            {
                NotificationId = notificationId,
                WorkflowTaskId = null,
                NotificationType = "workflow_created",
                ProcessTypeKey = processTypeKey,
                ProcessTypeName = processTypeName,
                RecipientUserId = recipient.Key,
                RecipientIdentityKey = recipient.Value.IdentityKey,
                TargetName = recipient.Value.DisplayName,
                TargetEmail = recipient.Value.Email,
                TaskTitle = null,
                PreferredPath = recipient.Value.PreferredPath
            });
        }

        return targets;
    }

    private static async Task<Dictionary<long, (string DisplayName, string Email, string IdentityKey, string PreferredPath)>> ResolveWorkflowCreatedRecipients(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId,
        bool requiresSupervisorStep,
        string processTypeKey)
    {
        var recipients = new Dictionary<long, (string DisplayName, string Email, string IdentityKey, string PreferredPath)>();

        var workflowCreatedResponsibilityId = await ResolveWorkflowCreatedResponsibilityId(
            connection,
            transaction,
            processTypeKey);
        if (workflowCreatedResponsibilityId.HasValue)
        {
            var workflowCreatedUserId = await ResolvePrimaryAssigneeUserId(
                connection,
                transaction,
                workflowCreatedResponsibilityId.Value,
                departmentId);
            if (workflowCreatedUserId.HasValue)
            {
                var workflowCreatedRecipient = await LoadActiveUserNotificationRecipient(
                    connection,
                    transaction,
                    workflowCreatedUserId.Value);
                if (workflowCreatedRecipient.HasValue)
                {
                    recipients[workflowCreatedRecipient.Value.UserId] = (
                        workflowCreatedRecipient.Value.DisplayName,
                        workflowCreatedRecipient.Value.Email,
                        workflowCreatedRecipient.Value.IdentityKey,
                        workflowCreatedRecipient.Value.PreferredPath);
                }
            }
        }

        if (requiresSupervisorStep)
        {
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
        }

        return recipients;
    }

    private static async Task<List<WorkflowNotificationDispatchTarget>> BuildReadyTaskNotificationPreviewTargets(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        string processTypeKey,
        string processTypeName)
    {
        const string taskSql = @"
SELECT
    ta.assignee_user_id,
    wt.id,
    wt.title
FROM workflow_tasks wt
JOIN workflows w ON w.id = wt.workflow_id
JOIN process_types pt ON pt.id = w.process_type_id
JOIN task_assignments ta
    ON ta.workflow_task_id = wt.id
   AND ta.is_primary = TRUE
WHERE wt.workflow_id = @workflowId
  AND wt.status IN ('open', 'ready')
  AND (
      wt.node_instance_id IS NOT NULL
      OR pt.approval_task_template_key IS NULL
      OR wt.task_key <> pt.approval_task_template_key
  )
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

            await using var reader = await taskCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var recipientUserId = reader.GetInt64(0);
                if (!taskTitlesByRecipient.TryGetValue(recipientUserId, out var recipientTasks))
                {
                    recipientTasks = [];
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
                targets.Add(new WorkflowNotificationDispatchTarget
                {
                    NotificationId = 0,
                    WorkflowTaskId = recipientTask.WorkflowTaskId,
                    NotificationType = "task_ready",
                    ProcessTypeKey = processTypeKey,
                    ProcessTypeName = processTypeName,
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

    private static async Task<int?> ResolveWorkflowCreatedResponsibilityId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string processTypeKey)
    {
        var preferredResponsibilityId = await LoadWorkflowCreatedResponsibilityIdFromProcessTasks(
            connection,
            transaction,
            processTypeKey);
        if (preferredResponsibilityId.HasValue)
        {
            return preferredResponsibilityId.Value;
        }

        return await LoadResponsibilityIdByKey(connection, transaction, "hr_onboarding");
    }

    private static async Task<int?> LoadWorkflowCreatedResponsibilityIdFromProcessTasks(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string processTypeKey)
    {
        const string sql = @"
SELECT tt.default_responsibility_id
FROM task_templates tt
JOIN process_types pt ON pt.id = tt.process_type_id
WHERE pt.key = @processTypeKey
  AND pt.is_active = TRUE
  AND tt.is_active = TRUE
  AND tt.default_responsibility_id IS NOT NULL
ORDER BY tt.sort_order, tt.id
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("processTypeKey", processTypeKey);
        var scalar = await command.ExecuteScalarAsync();
        return scalar is null || scalar is DBNull ? null : (int?)scalar;
    }

    private static async Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotifications(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        string processTypeKey,
        string processTypeName)
    {
        const string taskSql = @"
SELECT
    ta.assignee_user_id,
    wt.id,
    wt.title
FROM workflow_tasks wt
JOIN workflows w ON w.id = wt.workflow_id
JOIN process_types pt ON pt.id = w.process_type_id
JOIN task_assignments ta
    ON ta.workflow_task_id = wt.id
   AND ta.is_primary = TRUE
WHERE wt.workflow_id = @workflowId
  AND wt.status IN ('open', 'ready')
  AND (
      wt.node_instance_id IS NOT NULL
      OR pt.approval_task_template_key IS NULL
      OR wt.task_key <> pt.approval_task_template_key
  )
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
                var notificationId = await InsertWorkflowNotification(
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
                    NotificationId = notificationId,
                    WorkflowTaskId = recipientTask.WorkflowTaskId,
                    NotificationType = "task_ready",
                    ProcessTypeKey = processTypeKey,
                    ProcessTypeName = processTypeName,
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
