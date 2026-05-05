using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed class PostgresWorkflowNotificationDispatchOperations : IWorkflowNotificationDispatchOperations
{
    public Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowNotifications(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        int departmentId,
        bool requiresSupervisorStep,
        string processTypeKey,
        string processTypeName)
        => CreateWorkflowNotificationsAsync(connection, transaction, workflowId, departmentId, requiresSupervisorStep, processTypeKey, processTypeName);

    internal static async Task<(string Key, string Name)> LoadWorkflowProcessTypeForNotificationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = @"
SELECT
    COALESCE(NULLIF(BTRIM(d.definition_key), ''), pt.definition_key),
    COALESCE(NULLIF(BTRIM(d.name), ''), pt.name)
FROM workflows w
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
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

    internal static async Task<long> InsertWorkflowNotificationAsync(
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

    internal static async Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowNotificationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        int departmentId,
        bool requiresSupervisorStep,
        string processTypeKey,
        string processTypeName)
    {
        var recipients = await ResolveWorkflowCreatedRecipientsAsync(
            connection,
            transaction,
            departmentId,
            requiresSupervisorStep,
            processTypeKey);

        var targets = new List<WorkflowNotificationDispatchTarget>();
        foreach (var recipient in recipients)
        {
            var notificationId = await InsertWorkflowNotificationAsync(
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
                LegacyProcessTypeKey = processTypeKey,
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

    internal static async Task<Dictionary<long, (string DisplayName, string Email, string IdentityKey, string PreferredPath)>> ResolveWorkflowCreatedRecipientsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId,
        bool requiresSupervisorStep,
        string processTypeKey)
    {
        var recipients = new Dictionary<long, (string DisplayName, string Email, string IdentityKey, string PreferredPath)>();

        var workflowCreatedResponsibilityId = await ResolveWorkflowCreatedResponsibilityIdAsync(
            connection,
            transaction,
            processTypeKey);
        if (workflowCreatedResponsibilityId.HasValue)
        {
            var workflowCreatedUserId = await PostgresRepositorySharedHelpers.ResolvePrimaryAssigneeUserId(
                connection,
                transaction,
                workflowCreatedResponsibilityId.Value,
                departmentId);
            if (workflowCreatedUserId.HasValue)
            {
                var workflowCreatedRecipient = await PostgresRepositorySharedHelpers.LoadActiveUserNotificationRecipient(
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
            var departmentSelectionAssignment = await PostgresRepositorySharedHelpers.ResolveDepartmentRequirementSelectionAssignment(
                connection,
                transaction,
                departmentId);

            if (departmentSelectionAssignment.UserId.HasValue)
            {
                var departmentRecipient = await PostgresRepositorySharedHelpers.LoadActiveUserNotificationRecipient(
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

    internal static async Task<List<WorkflowNotificationDispatchTarget>> BuildReadyTaskNotificationPreviewTargetsAsync(
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
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
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

        var recipients = await PostgresRepositorySharedHelpers.LoadActiveUserNotificationRecipientsBulk(
            connection,
            transaction,
            taskTitlesByRecipient.Keys.ToArray());

        var targets = new List<WorkflowNotificationDispatchTarget>();
        foreach (var recipientUserId in taskTitlesByRecipient.Keys.OrderBy(id => id))
        {
            if (!taskTitlesByRecipient.TryGetValue(recipientUserId, out var recipientTasks) || recipientTasks.Count == 0)
            {
                continue;
            }

            if (!recipients.TryGetValue(recipientUserId, out var recipient))
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
                    LegacyProcessTypeKey = processTypeKey,
                    ProcessTypeName = processTypeName,
                    RecipientUserId = recipientUserId,
                    RecipientIdentityKey = recipient.IdentityKey,
                    TargetName = recipient.DisplayName,
                    TargetEmail = recipient.Email,
                    TaskTitle = recipientTask.TaskTitle,
                    PreferredPath = recipient.PreferredPath
                });
            }
        }

        return targets;
    }

    internal static async Task<int?> ResolveWorkflowCreatedResponsibilityIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string processTypeKey)
    {
        var preferredResponsibilityId = await LoadWorkflowCreatedResponsibilityIdFromProcessTasksAsync(
            connection,
            transaction,
            processTypeKey);
        if (preferredResponsibilityId.HasValue)
        {
            return preferredResponsibilityId.Value;
        }

        return await PostgresRepositorySharedHelpers.LoadResponsibilityIdByKey(connection, transaction, "hr_workflow_initiator");
    }

    internal static async Task<int?> LoadWorkflowCreatedResponsibilityIdFromProcessTasksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string processTypeKey)
    {
        // LA5: Specs haengen am measure-Node der published Version.
        const string sql = @"
SELECT s.default_responsibility_id
FROM workflow_node_task_specs s
JOIN workflow_nodes n ON n.id = s.workflow_node_id
JOIN workflow_definition_versions v ON v.id = n.workflow_definition_version_id
JOIN workflow_definitions wd ON wd.id = v.workflow_definition_id
WHERE wd.definition_key = @processTypeKey
  AND v.published_at IS NOT NULL
  AND n.node_type LIKE 'measure_%'
  AND s.default_responsibility_id IS NOT NULL
ORDER BY s.sort_order, s.id
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("processTypeKey", processTypeKey);
        var scalar = await command.ExecuteScalarAsync();
        return scalar is null || scalar is DBNull ? null : (int?)scalar;
    }

    internal static async Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotificationsAsync(
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
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
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

        var recipients = await PostgresRepositorySharedHelpers.LoadActiveUserNotificationRecipientsBulk(
            connection,
            transaction,
            taskTitlesByRecipient.Keys.ToArray());

        var targets = new List<WorkflowNotificationDispatchTarget>();
        foreach (var recipientUserId in taskTitlesByRecipient.Keys.OrderBy(id => id))
        {
            if (!taskTitlesByRecipient.TryGetValue(recipientUserId, out var recipientTasks) || recipientTasks.Count == 0)
            {
                continue;
            }

            if (!recipients.TryGetValue(recipientUserId, out var recipient))
            {
                continue;
            }

            foreach (var recipientTask in recipientTasks)
            {
                var notificationId = await InsertWorkflowNotificationAsync(
                    connection,
                    transaction,
                    workflowId,
                    recipientTask.WorkflowTaskId,
                    recipientUserId,
                    recipient.DisplayName,
                    recipient.Email,
                    "task_ready");

                targets.Add(new WorkflowNotificationDispatchTarget
                {
                    NotificationId = notificationId,
                    WorkflowTaskId = recipientTask.WorkflowTaskId,
                    NotificationType = "task_ready",
                    LegacyProcessTypeKey = processTypeKey,
                    ProcessTypeName = processTypeName,
                    RecipientUserId = recipientUserId,
                    RecipientIdentityKey = recipient.IdentityKey,
                    TargetName = recipient.DisplayName,
                    TargetEmail = recipient.Email,
                    TaskTitle = recipientTask.TaskTitle,
                    PreferredPath = recipient.PreferredPath
                });
            }
        }

        return targets;
    }
}
