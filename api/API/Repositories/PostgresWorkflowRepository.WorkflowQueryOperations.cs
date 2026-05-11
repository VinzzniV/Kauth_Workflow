using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private sealed class WorkflowListMetadata
    {
        public int TotalTaskCount { get; set; }
        public int OpenTaskCount { get; set; }
        public int InProgressTaskCount { get; set; }
        public int BlockedTaskCount { get; set; }
        public int DoneTaskCount { get; set; }
        public int RequiredTotalTaskCount { get; set; }
        public int RequiredOpenTaskCount { get; set; }
        public int RequiredInProgressTaskCount { get; set; }
        public int RequiredBlockedTaskCount { get; set; }
        public int RequiredDoneTaskCount { get; set; }
        public int DepartmentTotalTaskCount { get; set; }
        public int DepartmentOpenTaskCount { get; set; }
        public int DepartmentInProgressTaskCount { get; set; }
        public int DepartmentBlockedTaskCount { get; set; }
        public int DepartmentDoneTaskCount { get; set; }
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
    pt.definition_key,
    pt.name,
    pt.requires_target_person,
    r.id,
    r.name,
    w.status,
    w.completed_at,
    w.deadline_date,
    w.created_at,
    COUNT(n.id) FILTER (WHERE n.status = 'pending') AS pending_notifications,
    COUNT(n.id) FILTER (WHERE n.status = 'failed') AS failed_notifications
FROM workflows w
JOIN departments d ON d.id = w.department_id
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
JOIN app_roles r ON r.id = w.position_role_id
LEFT JOIN workflow_notifications n ON n.workflow_id = w.id
WHERE w.archived_at IS NULL
GROUP BY w.id, d.id, d.name, pt.definition_key, pt.name, pt.requires_target_person, r.id, r.name, w.deadline_date
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
            string LegacyProcessTypeKey,
            string ProcessTypeName,
            bool ProcessTypeRequiresTargetPerson,
            int RoleId,
            string RoleName,
            string WorkflowStatus,
            DateTime? CompletedAt,
            DateOnly? DeadlineDate,
            DateTime CreatedAt,
            int PendingNotifications,
            int FailedNotifications)>();

        await using (var command = new NpgsqlCommand(sql, connection))
        {
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
                    reader.GetString(8),
                    reader.GetString(9),
                    reader.GetBoolean(10),
                    reader.GetInt32(11),
                    reader.GetString(12),
                    reader.GetString(13),
                    reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                    reader.IsDBNull(15) ? null : reader.GetFieldValue<DateOnly>(15),
                    reader.GetDateTime(16),
                    reader.GetInt32(17),
                    reader.GetInt32(18)));
            }
        }

        var workflowIds = workflowRows.Select(row => row.WorkflowId).ToArray();
        var definitions = await LoadAnswerDefinitionRecords(connection, null, null);
        var requirementSummariesByWorkflowId = await LoadWorkflowRequirementSummaries(connection, workflowIds, definitions);
        var metadataByWorkflowId = await LoadWorkflowListMetadata(connection, workflowIds);

        return workflowRows
            .Select(row =>
            {
                metadataByWorkflowId.TryGetValue(row.WorkflowId, out var metadata);
                requirementSummariesByWorkflowId.TryGetValue(row.WorkflowId, out var requirementSummary);
                var workflowStatus = row.WorkflowStatus;
                var taskMetrics = BuildWorkflowTaskMetrics(metadata);

                return new WorkflowListItemDto
                {
                    Uid = row.Uid,
                    FirstName = row.FirstName,
                    LastName = row.LastName,
                    EmployeeNumber = row.EmployeeNumber,
                    BadgeNumber = row.BadgeNumber,
                    DepartmentId = row.DepartmentId,
                    DepartmentName = row.DepartmentName,
                    WorkflowDefinition = new WorkflowDefinitionRefDto
                    {
                        Key = row.LegacyProcessTypeKey,
                        Name = row.ProcessTypeName,
                        RequiresTargetPerson = row.ProcessTypeRequiresTargetPerson
                    },
                    RoleId = row.RoleId,
                    RoleName = row.RoleName,
                    WorkflowStatus = workflowStatus,
                    CompletedAt = row.CompletedAt,
                    DeadlineDate = row.DeadlineDate,
                    CreatedAt = row.CreatedAt,
                    PendingNotifications = row.PendingNotifications,
                    FailedNotifications = row.FailedNotifications,
                    RequirementSummary = requirementSummary ?? WorkflowSummaryBuilder.CreateEmptyRequirementSummary(),
                    TaskMetrics = taskMetrics,
                    TaskSummary = WorkflowSummaryBuilder.BuildTaskSummaryText(taskMetrics),
                    ResponsibilityOptions = metadata?.ResponsibilityOptions.Values
                        .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                        .ToList()
                        ?? new List<WorkflowResponsibilityOptionDto>()
                };
            })
            .ToList();
    }

    // Gefilterte und paginierte Workflow-Liste – Filterung und Paginierung erfolgen auf DB-Ebene.
    public async Task<WorkflowListResult> GetFilteredWorkflows(WorkflowListQuery query, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        var workflowRows = await QueryFilteredWorkflowRows(connection, query, cancellationToken);
        var workflowIds = workflowRows.Select(row => row.WorkflowId).ToList();
        var totalCount = workflowRows.Count > 0 ? workflowRows[0].TotalCount : 0;

        var definitions = await LoadAnswerDefinitionRecords(connection, null, null, cancellationToken);
        var requirementSummaries = await LoadWorkflowRequirementSummaries(connection, workflowIds, definitions, cancellationToken);
        var metadataMap = await LoadWorkflowListMetadata(connection, workflowIds, cancellationToken);

        var items = workflowRows.Select(row =>
        {
            metadataMap.TryGetValue(row.WorkflowId, out var metadata);
            requirementSummaries.TryGetValue(row.WorkflowId, out var requirementSummary);
            var taskMetrics = BuildWorkflowTaskMetrics(metadata);

            return new WorkflowListItemDto
            {
                Uid = row.Uid,
                FirstName = row.FirstName,
                LastName = row.LastName,
                EmployeeNumber = row.EmployeeNumber,
                BadgeNumber = row.BadgeNumber,
                DepartmentId = row.DepartmentId,
                DepartmentName = row.DepartmentName,
                WorkflowDefinition = new WorkflowDefinitionRefDto
                {
                    Key = row.LegacyProcessTypeKey,
                    Name = row.ProcessTypeName,
                    RequiresTargetPerson = row.ProcessTypeRequiresTargetPerson
                },
                RoleId = row.RoleId,
                RoleName = row.RoleName,
                WorkflowStatus = row.WorkflowStatus,
                CompletedAt = row.CompletedAt,
                DeadlineDate = row.DeadlineDate,
                CreatedAt = row.CreatedAt,
                PendingNotifications = row.PendingNotifications,
                FailedNotifications = row.FailedNotifications,
                RequirementSummary = requirementSummary ?? WorkflowSummaryBuilder.CreateEmptyRequirementSummary(),
                TaskMetrics = taskMetrics,
                TaskSummary = WorkflowSummaryBuilder.BuildTaskSummaryText(taskMetrics),
                ResponsibilityOptions = metadata?.ResponsibilityOptions.Values
                    .OrderBy(o => o.Label, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
                    ?? new List<WorkflowResponsibilityOptionDto>()
            };
        }).ToList();

        if (!query.IncludeFilterOptions)
        {
            return new WorkflowListResult { Items = items, TotalCount = totalCount };
        }

        var deptOptions = await QueryDepartmentOptions(connection, query, cancellationToken);
        var respOptions = await QueryResponsibilityOptions(connection, query, cancellationToken);

        return new WorkflowListResult
        {
            Items = items,
            TotalCount = totalCount,
            DepartmentOptions = deptOptions,
            ResponsibilityOptions = respOptions
        };
    }

    private sealed record FilteredWorkflowRow(
        long WorkflowId, Guid Uid, string FirstName, string LastName,
        int EmployeeNumber, int BadgeNumber, int DepartmentId, string DepartmentName,
        string LegacyProcessTypeKey, string ProcessTypeName, bool ProcessTypeRequiresTargetPerson,
        int RoleId, string RoleName, string WorkflowStatus, DateTime? CompletedAt, DateOnly? DeadlineDate,
        DateTime CreatedAt, int PendingNotifications, int FailedNotifications, int TotalCount);

    private static async Task<List<FilteredWorkflowRow>> QueryFilteredWorkflowRows(
        NpgsqlConnection connection,
        WorkflowListQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand();
        command.Connection = connection;

        var conditions = BuildWorkflowFilterConditions(command, query, includeDept: true, includeResp: true);
        var whereClause = "WHERE " + string.Join("\n  AND ", conditions);
        var limitClause = query.Limit.HasValue ? $"\nLIMIT {query.Limit.Value}" : "";
        var offsetClause = query.Offset > 0 ? $"\nOFFSET {query.Offset}" : "";

        command.CommandText = $@"
SELECT
    w.id, w.uid, w.first_name, w.last_name, w.employee_number, w.badge_number,
    d.id, d.name,
    pt.definition_key, pt.name, pt.requires_target_person,
    r.id, r.name,
    w.status, w.completed_at, w.deadline_date, w.created_at,
    COUNT(n.id) FILTER (WHERE n.status = 'pending') AS pending_notifications,
    COUNT(n.id) FILTER (WHERE n.status = 'failed') AS failed_notifications,
    COUNT(*) OVER() AS total_count
FROM workflows w
JOIN departments d ON d.id = w.department_id
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
JOIN app_roles r ON r.id = w.position_role_id
LEFT JOIN workflow_notifications n ON n.workflow_id = w.id
{whereClause}
GROUP BY w.id, d.id, d.name, pt.definition_key, pt.name, pt.requires_target_person, r.id, r.name, w.deadline_date
ORDER BY w.created_at DESC{limitClause}{offsetClause};";

        var rows = new List<FilteredWorkflowRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new FilteredWorkflowRow(
                reader.GetInt64(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetString(7),
                reader.GetString(8), reader.GetString(9), reader.GetBoolean(10),
                reader.GetInt32(11), reader.GetString(12), reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                reader.IsDBNull(15) ? null : reader.GetFieldValue<DateOnly>(15),
                reader.GetDateTime(16), reader.GetInt32(17), reader.GetInt32(18),
                reader.GetInt32(19)));
        }

        return rows;
    }

    private static async Task<List<DepartmentDto>> QueryDepartmentOptions(
        NpgsqlConnection connection,
        WorkflowListQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand();
        command.Connection = connection;

        var conditions = BuildWorkflowFilterConditions(command, query, includeDept: false, includeResp: false);
        var whereClause = "WHERE " + string.Join("\n  AND ", conditions);

        command.CommandText = $@"
SELECT DISTINCT d.id, d.name
FROM workflows w
JOIN departments d ON d.id = w.department_id
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
JOIN app_roles r ON r.id = w.position_role_id
{whereClause}
ORDER BY d.name;";

        var options = new List<DepartmentDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new DepartmentDto { Id = reader.GetInt32(0), Name = reader.GetString(1) });
        }

        return options;
    }

    private static async Task<List<WorkflowResponsibilityOptionDto>> QueryResponsibilityOptions(
        NpgsqlConnection connection,
        WorkflowListQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand();
        command.Connection = connection;

        var conditions = BuildWorkflowFilterConditions(command, query, includeDept: true, includeResp: false);
        var whereClause = "WHERE " + string.Join("\n  AND ", conditions);

        command.CommandText = $@"
SELECT DISTINCT
    sa.assignment_type,
    ar.responsibility_key,
    ar.name
FROM workflows w
JOIN departments d ON d.id = w.department_id
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
JOIN app_roles r ON r.id = w.position_role_id
JOIN workflow_tasks wt ON wt.workflow_id = w.id
LEFT JOIN LATERAL (
    SELECT ta.assignment_type, ta.assignee_responsibility_id
    FROM task_assignments ta
    WHERE ta.workflow_task_id = wt.id
    ORDER BY ta.is_primary DESC, ta.id
    LIMIT 1
) sa ON TRUE
LEFT JOIN app_responsibilities ar ON ar.id = sa.assignee_responsibility_id
{whereClause};";

        var optionsMap = new Dictionary<string, WorkflowResponsibilityOptionDto>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var option = BuildWorkflowResponsibilityOption(
                reader.IsDBNull(0) ? null : reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2));
            optionsMap[option.Value] = option;
        }

        return optionsMap.Values
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> BuildWorkflowFilterConditions(
        NpgsqlCommand command,
        WorkflowListQuery query,
        bool includeDept,
        bool includeResp)
    {
        var conditions = new List<string>
        {
            "w.archived_at IS NULL"
        };

        if (query.ReaderOnly)
        {
            conditions.Add("w.status = 'completed'");
        }

        if (query.ObservableDepartmentIds is { Count: > 0 })
        {
            conditions.Add("w.department_id = ANY(@observableDepartmentIds)");
            command.Parameters.Add("observableDepartmentIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value =
                query.ObservableDepartmentIds.ToArray();
        }
        else if (query.ObservableDepartmentIds is { Count: 0 })
        {
            conditions.Add("FALSE");
        }

        if (query.Status is not null)
        {
            conditions.Add("w.status = @status");
            command.Parameters.AddWithValue("status", query.Status);
        }

        if (query.WorkflowDefinitionKey is not null)
        {
            conditions.Add(@"EXISTS (
                SELECT 1 FROM workflow_definition_versions wdv
                JOIN workflow_definitions wd ON wd.id = wdv.workflow_definition_id
                WHERE wdv.id = w.workflow_definition_version_id
                AND LOWER(wd.key) = @workflowDefinitionKey
            )");
            command.Parameters.AddWithValue("workflowDefinitionKey", query.WorkflowDefinitionKey);
        }

        if (query.Search is not null)
        {
            conditions.Add(@"(
                LOWER(w.first_name || ' ' || w.last_name) LIKE '%' || @search || '%'
                OR LOWER(d.name) LIKE '%' || @search || '%'
                OR LOWER(r.name) LIKE '%' || @search || '%'
                OR LOWER(w.uid::text) LIKE '%' || @search || '%'
                OR w.employee_number::text LIKE '%' || @search || '%'
            )");
            command.Parameters.AddWithValue("search", query.Search);
        }

        if (includeDept && query.DepartmentId.HasValue)
        {
            conditions.Add("w.department_id = @departmentId");
            command.Parameters.AddWithValue("departmentId", query.DepartmentId.Value);
        }

        if (includeResp && query.Responsibility is not null)
        {
            conditions.Add(@"EXISTS (
                SELECT 1 FROM workflow_tasks wt_r
                LEFT JOIN LATERAL (
                    SELECT ta_r.assignment_type, ta_r.assignee_responsibility_id
                    FROM task_assignments ta_r
                    WHERE ta_r.workflow_task_id = wt_r.id
                    ORDER BY ta_r.is_primary DESC, ta_r.id
                    LIMIT 1
                ) sa_r ON TRUE
                LEFT JOIN app_responsibilities ar_r ON ar_r.id = sa_r.assignee_responsibility_id
                WHERE wt_r.workflow_id = w.id
                AND LOWER(CASE
                    WHEN ar_r.responsibility_key IS NOT NULL AND TRIM(ar_r.responsibility_key) <> ''
                         AND ar_r.name IS NOT NULL AND TRIM(ar_r.name) <> ''
                        THEN ar_r.responsibility_key
                    WHEN ar_r.name IS NOT NULL AND TRIM(ar_r.name) <> ''
                        THEN ar_r.name
                    WHEN LOWER(sa_r.assignment_type) = 'user'
                        THEN '__direct_user__'
                    ELSE '__unassigned__'
                END) = LOWER(@responsibility)
            )");
            command.Parameters.AddWithValue("responsibility", query.Responsibility);
        }

        return conditions;
    }

    public async Task<WorkflowDetailDto?> GetWorkflowByUid(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string workflowSql = @"
SELECT
    w.id,
    w.uid,
    w.workflow_definition_id,
    w.first_name,
    w.last_name,
    w.employee_number,
    w.badge_number,
    d.id,
    d.name,
    pt.definition_key,
    pt.name,
    pt.requires_target_person,
    r.id,
    r.name,
    w.status,
    w.deadline_date,
    w.created_at,
    w.archived_at,
    w.target_person_id
FROM workflows w
JOIN departments d ON d.id = w.department_id
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
JOIN app_roles r ON r.id = w.position_role_id
WHERE w.uid = @uid
LIMIT 1;";

        long workflowId;
        int workflowDefinitionId;
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
            workflowDefinitionId = reader.GetInt32(2);
            var workflowStatus = reader.GetString(14);

            workflow = new WorkflowDetailDto
            {
                Uid = reader.GetGuid(1),
                FirstName = reader.GetString(3),
                LastName = reader.GetString(4),
                EmployeeNumber = reader.GetInt32(5),
                BadgeNumber = reader.GetInt32(6),
                DepartmentId = reader.GetInt32(7),
                DepartmentName = reader.GetString(8),
                WorkflowDefinition = new WorkflowDefinitionRefDto
                {
                    Key = reader.GetString(9),
                    Name = reader.GetString(10),
                    RequiresTargetPerson = reader.GetBoolean(11)
                },
                RoleId = reader.GetInt32(12),
                RoleName = reader.GetString(13),
                WorkflowStatus = workflowStatus,
                DeadlineDate = reader.IsDBNull(15) ? null : reader.GetFieldValue<DateOnly>(15),
                CreatedAt = reader.GetDateTime(16),
                ArchivedAt = reader.IsDBNull(17) ? null : reader.GetDateTime(17),
                TargetPersonId = reader.IsDBNull(18) ? null : reader.GetInt64(18),
                Requirements = new List<WorkflowRequirementSnapshotDto>(),
                RequirementSummary = WorkflowSummaryBuilder.CreateEmptyRequirementSummary(),
                Tasks = new List<WorkflowTaskDto>(),
                TaskMetrics = WorkflowSummaryBuilder.CreateEmptyTaskMetrics(),
                TaskAreas = new List<WorkflowTaskAreaSummaryDto>(),
                Notifications = new List<WorkflowNotificationDto>()
            };
        }

        await LoadWorkflowRequirements(connection, workflowId, workflowDefinitionId, workflow.Requirements);
        await LoadWorkflowTasks(connection, workflowId, workflow.Tasks);
        await LoadWorkflowNotifications(connection, workflowId, workflow.Notifications);
        workflow.RequirementSummary = WorkflowSummaryBuilder.BuildRequirementSummary(workflow.Requirements);
        workflow.TaskMetrics = WorkflowSummaryBuilder.BuildTaskMetrics(workflow.Tasks);
        workflow.TaskAreas = WorkflowSummaryBuilder.BuildTaskAreaSummaries(workflow.Tasks);

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
}
