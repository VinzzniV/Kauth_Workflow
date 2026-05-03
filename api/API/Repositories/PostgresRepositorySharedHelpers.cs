using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace API;

// Hilfsmethoden, die zwischen PostgresWorkflowRepository und PostgresRotationRepository geteilt werden.
// Diese sind bewusst als internal static gehalten, damit beide Repositories sie ohne gegenseitige
// Klassenkopplung nutzen koennen.
internal static class PostgresRepositorySharedHelpers
{
    public static async Task EnsureAssignableResponsibilityExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT id
FROM app_responsibilities
WHERE id = @responsibilityId
  AND is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("Assignee responsibility is invalid or inactive.");
        }
    }

    public static async Task EnsureAssignableUserExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId)
    {
        const string sql = @"
SELECT id
FROM app_users
WHERE id = @userId
  AND is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);
        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("Assignee user is invalid or inactive.");
        }
    }

    public static async Task EnsureUserHasResponsibility(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId,
        int responsibilityId)
    {
        const string sql = @"
SELECT 1
FROM app_user_responsibilities
WHERE app_user_id = @userId
  AND app_responsibility_id = @responsibilityId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("The selected user is not directly assigned to the selected responsibility.");
        }
    }

    public static async Task<long?> LoadExplicitResponsibilityOwnerUserId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT u.id
FROM system_responsibilities sr
JOIN people p ON p.id = sr.responsible_person_id
JOIN app_users u ON u.id = p.app_user_id
WHERE sr.app_responsibility_id = @responsibilityId
  AND u.is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (long?)scalar;
    }

    public static async Task<int?> LoadResponsibilityDepartmentOverrideId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT COALESCE(sr.responsible_department_id, r.department_id)
FROM app_responsibilities r
LEFT JOIN system_responsibilities sr ON sr.app_responsibility_id = r.id
WHERE r.id = @responsibilityId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null || scalar is DBNull ? null : (int?)scalar;
    }

    public static async Task<long?> ResolvePrimaryAssigneeUserId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId,
        int departmentId)
    {
        var explicitOwnerUserId = await LoadExplicitResponsibilityOwnerUserId(connection, transaction, responsibilityId);
        if (explicitOwnerUserId.HasValue)
        {
            return explicitOwnerUserId.Value;
        }

        var effectiveDepartmentId = await LoadResponsibilityDepartmentOverrideId(connection, transaction, responsibilityId)
            ?? departmentId;

        const string sql = @"
SELECT u.id
FROM app_users u
LEFT JOIN people p ON p.app_user_id = u.id
JOIN app_user_responsibilities ur ON ur.app_user_id = u.id
LEFT JOIN app_responsibilities r ON r.id = ur.app_responsibility_id
WHERE u.is_active = TRUE
  AND ur.app_responsibility_id = @responsibilityId
  AND (
      r.department_id = @effectiveDepartmentId
      OR r.department_id IS NULL
      OR COALESCE(p.department_id, u.department_id) = @effectiveDepartmentId
      OR COALESCE(p.department_id, u.department_id) IS NULL
  )
ORDER BY CASE WHEN COALESCE(p.department_id, u.department_id) = @effectiveDepartmentId THEN 0 ELSE 1 END, u.id
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        command.Parameters.AddWithValue("effectiveDepartmentId", effectiveDepartmentId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (long?)scalar;
    }

    public static async Task<string> LoadAssigneeUserAuditLabel(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId)
    {
        const string sql = @"
SELECT display_name
FROM app_users
WHERE id = @userId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is not string displayName)
        {
            throw new InvalidOperationException("Assignee user label could not be resolved.");
        }

        return displayName;
    }

    public static async Task<string> LoadAssigneeResponsibilityAuditLabel(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT CASE
    WHEN d.name IS NULL OR d.name = '' THEN r.name
    ELSE d.name || ' - ' || r.name
END
FROM app_responsibilities r
LEFT JOIN departments d ON d.id = r.department_id
WHERE r.id = @responsibilityId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is not string responsibilityName)
        {
            throw new InvalidOperationException("Assignee responsibility label could not be resolved.");
        }

        return responsibilityName;
    }

    public static WorkflowTargetPersonSourceDto MapWorkflowTargetPersonSource(NpgsqlDataReader reader)
    {
        return new WorkflowTargetPersonSourceDto
        {
            WorkflowUid = reader.GetGuid(0),
            PersonId = reader.GetInt64(1),
            DisplayName = reader.GetString(2),
            FirstName = reader.GetString(3),
            LastName = reader.GetString(4),
            DepartmentId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            DepartmentName = reader.IsDBNull(6) ? null : reader.GetString(6),
            RoleId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            RoleName = reader.IsDBNull(8) ? null : reader.GetString(8),
            EmployeeNumber = reader.GetInt32(9),
            BadgeNumber = reader.GetInt32(10),
            CompletedAt = reader.GetDateTime(11),
            ArchivedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
        };
    }

    public static async Task InsertAuditEntry(
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

    public static string BuildTaskStatusAuditDetail(string taskTitle)
    {
        return $"Task: {taskTitle}";
    }

    public static async Task<string?> LoadPrimaryTaskAssignmentAuditLabel(
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

    public static async Task<int?> LoadResponsibilityIdByKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string responsibilityKey)
    {
        const string sql = @"
SELECT id
FROM app_responsibilities
WHERE responsibility_key = @responsibilityKey
  AND is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityKey", responsibilityKey);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (int?)scalar;
    }

    public static async Task EnsureValidPositionRole(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int roleId,
        int departmentId)
    {
        const string sql = @"
SELECT r.id
FROM app_roles r
WHERE r.id = @roleId
  AND r.department_id = @departmentId
  AND r.role_kind = 'position'
  AND r.is_active = TRUE;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("roleId", roleId);
        command.Parameters.AddWithValue("departmentId", departmentId);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("Role is invalid for the selected department.");
        }
    }

    public static JsonElement ParseJsonElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public static async Task<WorkflowDefinitionGraphRecord> LoadWorkflowDefinitionGraph(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long versionId)
    {
        const string nodeSql = """
SELECT
    n.id,
    n.node_key,
    n.node_type,
    n.title,
    n.sort_order,
    nc.config_json::text
FROM workflow_nodes n
LEFT JOIN workflow_node_configs nc ON nc.workflow_node_id = n.id
WHERE n.workflow_definition_version_id = @versionId
ORDER BY n.sort_order, n.node_key, n.id;
""";

        var nodes = new List<WorkflowDefinitionNodeRecord>();
        await using (var nodeCommand = new NpgsqlCommand(nodeSql, connection, transaction))
        {
            nodeCommand.Parameters.AddWithValue("versionId", versionId);
            await using var reader = await nodeCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                nodes.Add(new WorkflowDefinitionNodeRecord
                {
                    NodeId = reader.GetInt64(0),
                    NodeKey = reader.GetString(1),
                    NodeType = reader.GetString(2),
                    Title = reader.IsDBNull(3) ? null : reader.GetString(3),
                    SortOrder = reader.GetInt32(4),
                    Config = reader.IsDBNull(5) ? null : ParseJsonElement(reader.GetString(5))
                });
            }
        }

        const string edgeSql = """
SELECT
    e.id,
    e.source_workflow_node_id,
    e.target_workflow_node_id,
    e.priority,
    e.condition_expression
FROM workflow_edges e
WHERE e.workflow_definition_version_id = @versionId
ORDER BY e.source_workflow_node_id, e.priority, e.id;
""";

        var edges = new List<WorkflowDefinitionEdgeRecord>();
        await using (var edgeCommand = new NpgsqlCommand(edgeSql, connection, transaction))
        {
            edgeCommand.Parameters.AddWithValue("versionId", versionId);
            await using var reader = await edgeCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                edges.Add(new WorkflowDefinitionEdgeRecord
                {
                    EdgeId = reader.GetInt64(0),
                    SourceNodeId = reader.GetInt64(1),
                    TargetNodeId = reader.GetInt64(2),
                    Priority = reader.GetInt32(3),
                    ConditionExpression = reader.IsDBNull(4) ? null : reader.GetString(4)
                });
            }
        }

        const string actionSql = """
SELECT
    wna.workflow_node_id,
    wna.id,
    wna.action_definition_id,
    wna.execution_order,
    wna.on_error_behavior,
    wna.input_mapping_json::text,
    ad.action_key,
    ad.name,
    ad.handler_type,
    ad.is_idempotent
FROM workflow_node_actions wna
INNER JOIN action_definitions ad ON ad.id = wna.action_definition_id
INNER JOIN workflow_nodes n ON n.id = wna.workflow_node_id
WHERE n.workflow_definition_version_id = @versionId
ORDER BY wna.workflow_node_id, wna.execution_order, wna.id;
""";

        var nodeActionsByNodeId = new Dictionary<long, List<WorkflowNodeActionRecord>>();
        await using (var actionCommand = new NpgsqlCommand(actionSql, connection, transaction))
        {
            actionCommand.Parameters.AddWithValue("versionId", versionId);
            await using var reader = await actionCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var workflowNodeId = reader.GetInt64(0);
                if (!nodeActionsByNodeId.TryGetValue(workflowNodeId, out var actions))
                {
                    actions = new List<WorkflowNodeActionRecord>();
                    nodeActionsByNodeId.Add(workflowNodeId, actions);
                }

                actions.Add(new WorkflowNodeActionRecord
                {
                    Id = reader.GetInt64(1),
                    ActionDefinitionId = reader.GetInt64(2),
                    ExecutionOrder = reader.GetInt32(3),
                    OnErrorBehavior = reader.GetString(4),
                    InputMapping = reader.IsDBNull(5) ? null : ParseJsonElement(reader.GetString(5)),
                    ActionKey = reader.GetString(6),
                    ActionName = reader.GetString(7),
                    HandlerType = reader.GetString(8),
                    IsIdempotent = reader.GetBoolean(9)
                });
            }
        }

        return new WorkflowDefinitionGraphRecord
        {
            Nodes = nodes,
            Edges = edges,
            NodeById = nodes.ToDictionary(node => node.NodeId),
            NodeActionsByNodeId = nodeActionsByNodeId,
            IncomingEdgesByTargetNodeId = edges
                .GroupBy(edge => edge.TargetNodeId)
                .ToDictionary(group => group.Key, group => group.OrderBy(edge => edge.Priority).ThenBy(edge => edge.EdgeId).ToList()),
            OutgoingEdgesBySourceNodeId = edges
                .GroupBy(edge => edge.SourceNodeId)
                .ToDictionary(group => group.Key, group => group.OrderBy(edge => edge.Priority).ThenBy(edge => edge.EdgeId).ToList())
        };
    }

    public static async Task<Dictionary<string, StoredWorkflowAnswerRecord>> LoadStoredAnswersByKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = @"
SELECT
    a.id,
    a.answer_definition_id,
    a.answer_key,
    a.input_type,
    a.value_boolean,
    a.value_text,
    a.value_number,
    a.selected_option_id,
    selected_option.option_value
FROM workflow_answers a
LEFT JOIN workflow_answer_options selected_option ON selected_option.id = a.selected_option_id
WHERE a.workflow_id = @workflowId
ORDER BY a.answer_definition_id, a.id;";

        var answers = new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase);
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var answerKey = reader.GetString(2);
                answers[answerKey] = new StoredWorkflowAnswerRecord
                {
                    WorkflowAnswerId = reader.GetInt64(0),
                    AnswerDefinitionId = reader.GetInt32(1),
                    AnswerKey = answerKey,
                    InputType = reader.GetString(3),
                    ValueBoolean = reader.IsDBNull(4) ? null : reader.GetBoolean(4),
                    ValueText = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ValueNumber = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    SelectedOptionId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    SelectedOptionValue = reader.IsDBNull(8) ? null : reader.GetString(8),
                    SelectedOptionIds = new List<int>(),
                    SelectedOptionValues = new List<string>()
                };
            }
        }

        const string multiSelectSql = @"
SELECT
    a.answer_key,
    o.id,
    o.option_value
FROM workflow_answers a
JOIN workflow_answer_selected_options aso ON aso.workflow_answer_id = a.id
JOIN workflow_answer_options o ON o.id = aso.answer_option_id
WHERE a.workflow_id = @workflowId
ORDER BY a.answer_definition_id, a.id, o.sort_order, o.id;";

        await using var multiSelectCommand = new NpgsqlCommand(multiSelectSql, connection, transaction);
        multiSelectCommand.Parameters.AddWithValue("workflowId", workflowId);
        await using var multiSelectReader = await multiSelectCommand.ExecuteReaderAsync();

        while (await multiSelectReader.ReadAsync())
        {
            var answerKey = multiSelectReader.GetString(0);
            if (!answers.TryGetValue(answerKey, out var answer))
            {
                continue;
            }

            answer.SelectedOptionIds.Add(multiSelectReader.GetInt32(1));
            answer.SelectedOptionValues.Add(multiSelectReader.GetString(2));
        }

        return answers;
    }

    public static async Task<TargetPersonRecord> LoadTargetPerson(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long targetPersonId)
    {
        const string sql = @"
SELECT
    p.id,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, latest.first_name), COALESCE(p.last_name, latest.last_name))), ''),
        u.display_name,
        linked_directory.display_name,
        'Person #' || p.id::text
    ) AS display_name,
    COALESCE(p.department_id, latest.department_id, u.department_id) AS department_id,
    d.name AS department_name,
    COALESCE(p.current_position_role_id, latest.position_role_id) AS role_id,
    r.name AS role_name,
    COALESCE(p.employee_number, latest.employee_number, linked_directory.employee_number) AS employee_number,
    COALESCE(p.badge_number, latest.badge_number) AS badge_number,
    COALESCE(p.first_name, latest.first_name) AS first_name,
    COALESCE(p.last_name, latest.last_name) AS last_name
FROM people p
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN directory_identities linked_directory ON linked_directory.id = p.directory_identity_id
LEFT JOIN LATERAL (
    SELECT
        w.department_id,
        w.position_role_id,
        w.employee_number,
        w.badge_number,
        w.first_name,
        w.last_name,
        w.created_at
    FROM workflows w
    WHERE w.target_person_id = p.id
       OR (p.employee_number IS NOT NULL AND w.employee_number = p.employee_number)
       OR (
            w.employee_number > 0
            AND TRIM(COALESCE(w.first_name, '') || ' ' || COALESCE(w.last_name, '')) =
                COALESCE(
                    NULLIF(BTRIM(CONCAT_WS(' ', p.first_name, p.last_name)), ''),
                    u.display_name,
                    'Person #' || p.id::text
                )
       )
    ORDER BY w.created_at DESC
    LIMIT 1
) latest ON TRUE
LEFT JOIN departments d ON d.id = COALESCE(p.department_id, latest.department_id, u.department_id)
LEFT JOIN app_roles r ON r.id = COALESCE(p.current_position_role_id, latest.position_role_id)
WHERE p.id = @targetPersonId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("targetPersonId", targetPersonId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Die angegebene Zielperson wurde nicht gefunden.");
        }

        return new TargetPersonRecord
        {
            PersonId = reader.GetInt64(0),
            DisplayName = reader.GetString(1),
            DepartmentId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            DepartmentName = reader.IsDBNull(3) ? null : reader.GetString(3),
            RoleId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            RoleName = reader.IsDBNull(5) ? null : reader.GetString(5),
            EmployeeNumber = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            BadgeNumber = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            FirstName = reader.IsDBNull(8) ? null : reader.GetString(8),
            LastName = reader.IsDBNull(9) ? null : reader.GetString(9)
        };
    }

    public static async Task<(long UserId, string DisplayName, string Email, string IdentityKey, string PreferredPath)?> LoadActiveUserNotificationRecipient(
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

    public static async Task<int?> LoadDepartmentLeadResponsibilityId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId)
    {
        const string sql = @"
SELECT id
FROM app_responsibilities
WHERE department_id = @departmentId
  AND responsibility_type = 'department_lead'
  AND is_active = TRUE
ORDER BY id
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentId", departmentId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (int?)scalar;
    }

    public static async Task<(long? UserId, int? ResponsibilityId)> ResolveDepartmentRequirementSelectionAssignment(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId)
    {
        const string sql = @"
SELECT
    COALESCE(requirement_user.id, lead_user.id) AS user_id,
    r.id AS responsibility_id
FROM departments d
LEFT JOIN department_settings ds ON ds.department_id = d.id
LEFT JOIN people requirement_person ON requirement_person.id = ds.requirement_approver_person_id
LEFT JOIN app_users requirement_user ON requirement_user.id = requirement_person.app_user_id AND requirement_user.is_active = TRUE
LEFT JOIN people lead_person ON lead_person.id = ds.department_lead_person_id
LEFT JOIN app_users lead_user ON lead_user.id = lead_person.app_user_id AND lead_user.is_active = TRUE
LEFT JOIN app_responsibilities r
    ON r.department_id = d.id
   AND r.responsibility_type = 'department_lead'
   AND r.is_active = TRUE
WHERE d.id = @departmentId
LIMIT 1;";

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("departmentId", departmentId);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                long? userId = reader.IsDBNull(0) ? null : reader.GetInt64(0);
                int? responsibilityId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                if (userId.HasValue || responsibilityId.HasValue)
                {
                    return (userId, responsibilityId);
                }
            }
        }

        var fallbackResponsibilityId = await LoadDepartmentLeadResponsibilityId(connection, transaction, departmentId);
        if (!fallbackResponsibilityId.HasValue)
        {
            return (null, null);
        }

        var fallbackUserId = await ResolvePrimaryAssigneeUserId(
            connection,
            transaction,
            fallbackResponsibilityId.Value,
            departmentId);

        return (fallbackUserId, fallbackResponsibilityId.Value);
    }

    public static string NormalizeAdminTaskTemplateIconKey(string? iconKey)
    {
        var normalized = iconKey?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "berechtigungen" : normalized;
    }
}

internal sealed class TargetPersonRecord
{
    public required long PersonId { get; init; }
    public required string DisplayName { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? RoleId { get; init; }
    public string? RoleName { get; init; }
    public int? EmployeeNumber { get; init; }
    public int? BadgeNumber { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}

internal enum TaskGenerationStage
{
    Initial,
    AfterSupervisor,
    Full
}

internal sealed class WorkflowTaskGenerationContext
{
    public required int WorkflowDefinitionId { get; init; }
    public required string ProcessTypeName { get; init; }
    public required bool RequiresSupervisorStep { get; init; }
    public string? ApprovalTaskTemplateKey { get; init; }
    public long? MeasureNodeId { get; init; }
}

internal sealed class WorkflowDefinitionGraphRecord
{
    public required List<WorkflowDefinitionNodeRecord> Nodes { get; init; }
    public required List<WorkflowDefinitionEdgeRecord> Edges { get; init; }
    public required Dictionary<long, WorkflowDefinitionNodeRecord> NodeById { get; init; }
    public required Dictionary<long, List<WorkflowNodeActionRecord>> NodeActionsByNodeId { get; init; }
    public required Dictionary<long, List<WorkflowDefinitionEdgeRecord>> IncomingEdgesByTargetNodeId { get; init; }
    public required Dictionary<long, List<WorkflowDefinitionEdgeRecord>> OutgoingEdgesBySourceNodeId { get; init; }
}

internal sealed class WorkflowDefinitionNodeRecord
{
    public required long NodeId { get; init; }
    public required string NodeKey { get; init; }
    public required string NodeType { get; init; }
    public string? Title { get; init; }
    public required int SortOrder { get; init; }
    public JsonElement? Config { get; init; }
}

internal sealed class WorkflowDefinitionEdgeRecord
{
    public required long EdgeId { get; init; }
    public required long SourceNodeId { get; init; }
    public required long TargetNodeId { get; init; }
    public required int Priority { get; init; }
    public string? ConditionExpression { get; init; }
}

internal sealed class WorkflowNodeActionRecord
{
    public required long Id { get; init; }
    public required long ActionDefinitionId { get; init; }
    public required int ExecutionOrder { get; init; }
    public required string OnErrorBehavior { get; init; }
    public JsonElement? InputMapping { get; init; }
    public required string ActionKey { get; init; }
    public required string ActionName { get; init; }
    public required string HandlerType { get; init; }
    public required bool IsIdempotent { get; init; }
}
