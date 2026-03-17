using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private sealed class WorkflowListMetadata
    {
        public int TotalTaskCount { get; set; }
        public int OpenTaskCount { get; set; }
        public int DoneTaskCount { get; set; }
        public int EndedTaskCount { get; set; }
        public Dictionary<string, WorkflowResponsibilityOptionDto> ResponsibilityOptions { get; } =
            new(StringComparer.Ordinal);
    }

    // Listen- und Detailabfragen fuer die Frontend-Uebersichten.
    public async Task<List<WorkflowListItemDto>> GetWorkflows()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    w.id,
    w.uid,
    w.first_name,
    w.last_name,
    w.employee_number,
    w.badge_number,
    d.id,
    d.name,
    r.id,
    r.name,
    w.status,
    w.created_at,
    COUNT(n.id) FILTER (WHERE n.status = 'pending') AS pending_notifications,
    COUNT(n.id) FILTER (WHERE n.status = 'failed') AS failed_notifications
FROM workflows w
JOIN departments d ON d.id = w.department_id
JOIN app_roles r ON r.id = w.onboarding_role_id
LEFT JOIN workflow_notifications n ON n.workflow_id = w.id
GROUP BY w.id, d.id, d.name, r.id, r.name
ORDER BY w.created_at DESC;";

        var workflowRows = new List<(
            long WorkflowId,
            Guid Uid,
            string FirstName,
            string LastName,
            int EmployeeNumber,
            int BadgeNumber,
            int DepartmentId,
            string DepartmentName,
            int RoleId,
            string RoleName,
            string WorkflowStatus,
            DateTime CreatedAt,
            int PendingNotifications,
            int FailedNotifications)>();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            workflowRows.Add((
                reader.GetInt64(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetString(7),
                reader.GetInt32(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetDateTime(11),
                reader.GetInt32(12),
                reader.GetInt32(13)));
        }

        var metadataByWorkflowId = await LoadWorkflowListMetadata(connection, workflowRows.Select(row => row.WorkflowId).ToArray());

        return workflowRows
            .Select(row =>
            {
                metadataByWorkflowId.TryGetValue(row.WorkflowId, out var metadata);
                var workflowStatus = row.WorkflowStatus;

                return new WorkflowListItemDto
                {
                    Uid = row.Uid,
                    FirstName = row.FirstName,
                    LastName = row.LastName,
                    EmployeeNumber = row.EmployeeNumber,
                    BadgeNumber = row.BadgeNumber,
                    DepartmentId = row.DepartmentId,
                    DepartmentName = row.DepartmentName,
                    RoleId = row.RoleId,
                    RoleName = row.RoleName,
                    Status = WorkflowStatusRules.ToLegacyStatus(workflowStatus),
                    WorkflowStatus = workflowStatus,
                    CreatedAt = row.CreatedAt,
                    PendingNotifications = row.PendingNotifications,
                    FailedNotifications = row.FailedNotifications,
                    TaskSummary = BuildWorkflowListTaskSummary(metadata),
                    ResponsibilityOptions = metadata?.ResponsibilityOptions.Values
                        .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                        .ToList()
                        ?? new List<WorkflowResponsibilityOptionDto>()
                };
            })
            .ToList();
    }

    public async Task<WorkflowDetailDto?> GetWorkflowByUid(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string workflowSql = @"
SELECT
    w.id,
    w.uid,
    w.first_name,
    w.last_name,
    w.employee_number,
    w.badge_number,
    d.id,
    d.name,
    r.id,
    r.name,
    w.status,
    w.created_at
FROM workflows w
JOIN departments d ON d.id = w.department_id
JOIN app_roles r ON r.id = w.onboarding_role_id
WHERE w.uid = @uid
LIMIT 1;";

        long workflowId;
        WorkflowDetailDto workflow;

        await using (var workflowCommand = new NpgsqlCommand(workflowSql, connection))
        {
            workflowCommand.Parameters.AddWithValue("uid", workflowUid);
            await using var reader = await workflowCommand.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            workflowId = reader.GetInt64(0);
            var workflowStatus = reader.GetString(10);

            workflow = new WorkflowDetailDto
            {
                Uid = reader.GetGuid(1),
                FirstName = reader.GetString(2),
                LastName = reader.GetString(3),
                EmployeeNumber = reader.GetInt32(4),
                BadgeNumber = reader.GetInt32(5),
                DepartmentId = reader.GetInt32(6),
                DepartmentName = reader.GetString(7),
                RoleId = reader.GetInt32(8),
                RoleName = reader.GetString(9),
                Status = WorkflowStatusRules.ToLegacyStatus(workflowStatus),
                WorkflowStatus = workflowStatus,
                CreatedAt = reader.GetDateTime(11),
                Requirements = new List<WorkflowRequirementSnapshotDto>(),
                Tasks = new List<WorkflowTaskDto>(),
                Notifications = new List<WorkflowNotificationDto>()
            };
        }

        await LoadWorkflowRequirements(connection, workflowId, workflow.Requirements);
        await LoadWorkflowTasks(connection, workflowId, workflow.Tasks);
        await LoadWorkflowNotifications(connection, workflowId, workflow.Notifications);

        return workflow;
    }

    // Aufgaben werden fuer Fachbereiche immer im Kontext ihres Workflows geladen.
    public async Task<HashSet<int>> GetRequirementSelectionDepartmentIds(long userId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
WITH explicit_departments AS (
    SELECT ds.department_id
    FROM department_settings ds
    LEFT JOIN people requirement_person ON requirement_person.id = ds.requirement_approver_person_id
    LEFT JOIN people lead_person ON lead_person.id = ds.department_lead_person_id
    WHERE requirement_person.app_user_id = @userId
       OR (requirement_person.app_user_id IS NULL AND lead_person.app_user_id = @userId)
),
fallback_departments AS (
    SELECT r.department_id
    FROM app_user_responsibilities ur
    JOIN app_responsibilities r ON r.id = ur.app_responsibility_id
    WHERE ur.app_user_id = @userId
      AND r.responsibility_type = 'department_lead'
      AND r.department_id IS NOT NULL
      AND r.is_active = TRUE

    UNION

    SELECT r.department_id
    FROM app_user_groups ug
    JOIN app_group_responsibilities gr ON gr.app_group_id = ug.app_group_id
    JOIN app_responsibilities r ON r.id = gr.app_responsibility_id
    WHERE ug.app_user_id = @userId
      AND r.responsibility_type = 'department_lead'
      AND r.department_id IS NOT NULL
      AND r.is_active = TRUE
)
SELECT department_id FROM explicit_departments
UNION
SELECT department_id FROM fallback_departments;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        var departmentIds = new HashSet<int>();
        while (await reader.ReadAsync())
        {
            departmentIds.Add(reader.GetInt32(0));
        }

        return departmentIds;
    }

    public async Task<List<TaskWithWorkflowDto>> GetTasks()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        return await LoadTasks(connection, null, null);
    }

    public async Task<TaskWithWorkflowDto?> GetTaskById(long taskId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var tasks = await LoadTasks(connection, null, taskId);
        return tasks.FirstOrDefault();
    }

    private static async Task<Dictionary<long, WorkflowListMetadata>> LoadWorkflowListMetadata(
        NpgsqlConnection connection,
        IReadOnlyList<long> workflowIds)
    {
        var metadataByWorkflowId = new Dictionary<long, WorkflowListMetadata>();
        if (workflowIds.Count == 0)
        {
            return metadataByWorkflowId;
        }

        const string sql = @"
SELECT
    wt.workflow_id,
    wt.status,
    selected_assignment.assignment_type,
    selected_responsibility.responsibility_key,
    selected_responsibility.name
FROM workflow_tasks wt
LEFT JOIN LATERAL (
    SELECT
        ta.assignment_type,
        ta.assignee_responsibility_id
    FROM task_assignments ta
    WHERE ta.workflow_task_id = wt.id
    ORDER BY ta.is_primary DESC, ta.id
    LIMIT 1
) selected_assignment ON TRUE
LEFT JOIN app_responsibilities selected_responsibility
    ON selected_responsibility.id = selected_assignment.assignee_responsibility_id
WHERE wt.workflow_id = ANY(@workflowIds)
  AND wt.task_key <> @legacyTaskKey;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add("workflowIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = workflowIds;
        command.Parameters.AddWithValue("legacyTaskKey", LegacySupervisorHandoverTaskKey);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var workflowId = reader.GetInt64(0);
            if (!metadataByWorkflowId.TryGetValue(workflowId, out var metadata))
            {
                metadata = new WorkflowListMetadata();
                metadataByWorkflowId[workflowId] = metadata;
            }

            metadata.TotalTaskCount += 1;

            var taskStatus = reader.GetString(1);
            if (taskStatus.Equals("done", StringComparison.OrdinalIgnoreCase))
            {
                metadata.DoneTaskCount += 1;
            }
            else if (taskStatus.Equals("cancelled", StringComparison.OrdinalIgnoreCase)
                || taskStatus.Equals("skipped", StringComparison.OrdinalIgnoreCase))
            {
                metadata.EndedTaskCount += 1;
            }
            else if (taskStatus.Equals("open", StringComparison.OrdinalIgnoreCase)
                || taskStatus.Equals("ready", StringComparison.OrdinalIgnoreCase)
                || taskStatus.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
                || taskStatus.Equals("blocked", StringComparison.OrdinalIgnoreCase))
            {
                metadata.OpenTaskCount += 1;
            }

            var option = BuildWorkflowResponsibilityOption(
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4));
            metadata.ResponsibilityOptions[option.Value] = option;
        }

        return metadataByWorkflowId;
    }

    private static string BuildWorkflowListTaskSummary(WorkflowListMetadata? metadata)
    {
        if (metadata is null || metadata.TotalTaskCount == 0)
        {
            return "Keine Aufgaben";
        }

        return $"Offen: {metadata.OpenTaskCount} | Erledigt: {metadata.DoneTaskCount} | Beendet: {metadata.EndedTaskCount}";
    }

    private static WorkflowResponsibilityOptionDto BuildWorkflowResponsibilityOption(
        string? assignmentType,
        string? responsibilityKey,
        string? responsibilityName)
    {
        if (!string.IsNullOrWhiteSpace(responsibilityKey) && !string.IsNullOrWhiteSpace(responsibilityName))
        {
            return new WorkflowResponsibilityOptionDto
            {
                Value = responsibilityKey,
                Label = responsibilityName
            };
        }

        if (!string.IsNullOrWhiteSpace(responsibilityName))
        {
            return new WorkflowResponsibilityOptionDto
            {
                Value = responsibilityName,
                Label = responsibilityName
            };
        }

        if (string.Equals(assignmentType, "user", StringComparison.OrdinalIgnoreCase))
        {
            return new WorkflowResponsibilityOptionDto
            {
                Value = "__direct_user__",
                Label = "Direkt zugewiesen"
            };
        }

        if (string.IsNullOrWhiteSpace(assignmentType))
        {
            return new WorkflowResponsibilityOptionDto
            {
                Value = "__unassigned__",
                Label = "Nicht zugewiesen"
            };
        }

        return new WorkflowResponsibilityOptionDto
        {
            Value = "__unassigned__",
            Label = "Ohne Zuständigkeits-Zuordnung"
        };
    }
}
