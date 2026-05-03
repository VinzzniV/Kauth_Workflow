using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    public async Task<List<AdminTaskTemplateDto>> GetAdminTaskTemplates(int workflowDefinitionId)
    {
        if (workflowDefinitionId <= 0)
        {
            throw new InvalidOperationException("workflowDefinitionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        await EnsureProcessTypeExists(connection, null, workflowDefinitionId);
        var measureNodeId = await ResolveMeasureNodeIdForDefinition(connection, null, workflowDefinitionId);
        if (measureNodeId is null)
        {
            return new List<AdminTaskTemplateDto>();
        }

        const string sql = @"
SELECT
    tt.id,
    tt.spec_key,
    tt.title,
    tt.category,
    tt.description,
    tt.icon_key,
    tt.default_responsibility_id,
    tt.process_area_label,
    tt.is_department_phase_task,
    tt.is_required,
    tt.due_in_days,
    tt.sort_order,
    tt.created_at,
    COALESCE(cond.condition_count, 0) AS condition_count,
    COALESCE(dep.dependency_count, 0) AS dependency_count
FROM workflow_node_task_specs tt
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS condition_count
    FROM workflow_node_task_spec_conditions c
    WHERE c.workflow_node_task_spec_id = tt.id
) cond ON TRUE
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS dependency_count
    FROM workflow_node_task_spec_dependencies d
    WHERE d.workflow_node_task_spec_id = tt.id
) dep ON TRUE
WHERE tt.workflow_node_id = @measureNodeId
ORDER BY tt.sort_order, tt.title, tt.id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("measureNodeId", measureNodeId.Value);
        await using var reader = await command.ExecuteReaderAsync();

        var templates = new List<AdminTaskTemplateDto>();
        while (await reader.ReadAsync())
        {
            templates.Add(MapAdminTaskTemplate(reader, workflowDefinitionId));
        }

        return templates;
    }

    public async Task<AdminTaskTemplateDto> CreateAdminTaskTemplate(AdminTaskTemplateUpsertRequest request)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        ValidateAdminTaskTemplateRequest(request);
        await EnsureProcessTypeExists(connection, null, request.WorkflowDefinitionId);
        var measureNodeId = await ResolveMeasureNodeIdForDefinition(connection, null, request.WorkflowDefinitionId)
            ?? throw new InvalidOperationException("Die Workflow-Definition hat keinen Massnahmen-Node in der published Version.");
        await EnsureTemplateKeyAvailable(connection, null, measureNodeId, request.TemplateKey!, null);

        const string sql = @"
INSERT INTO workflow_node_task_specs (
    workflow_node_id,
    spec_key,
    title,
    category,
    description,
    icon_key,
    default_responsibility_id,
    process_area_label,
    is_department_phase_task,
    is_required,
    due_in_days,
    sort_order
)
VALUES (
    @measureNodeId,
    @templateKey,
    @title,
    @category,
    @description,
    @iconKey,
    @defaultResponsibilityId,
    @processAreaLabel,
    @isDepartmentPhaseTask,
    @isRequired,
    @dueInDays,
    @sortOrder
)
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        BindAdminTaskTemplateParameters(command, request, measureNodeId);

        var createdId = await command.ExecuteScalarAsync();
        if (createdId is not long templateId)
        {
            throw new InvalidOperationException("Task spec could not be created.");
        }

        return await GetAdminTaskTemplateById(connection, null, checked((int)templateId))
            ?? throw new InvalidOperationException("Task spec could not be loaded after creation.");
    }

    public async Task<AdminTaskTemplateDto?> UpdateAdminTaskTemplate(int templateId, AdminTaskTemplateUpsertRequest request)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        ValidateAdminTaskTemplateRequest(request);
        await EnsureProcessTypeExists(connection, null, request.WorkflowDefinitionId);
        var measureNodeId = await ResolveMeasureNodeIdForDefinition(connection, null, request.WorkflowDefinitionId)
            ?? throw new InvalidOperationException("Die Workflow-Definition hat keinen Massnahmen-Node in der published Version.");
        await EnsureTemplateKeyAvailable(connection, null, measureNodeId, request.TemplateKey!, templateId);

        // LA5: workflow_node_id (= Massnahmen-Node der Definition) wird nicht mehr beweglich
        // pro Spec — Update setzt es zwar, behaelt aber den Spec auf demselben Node, sofern
        // request.WorkflowDefinitionId zur urspruenglichen Definition passt.
        const string sql = @"
UPDATE workflow_node_task_specs
SET
    workflow_node_id = @measureNodeId,
    spec_key = @templateKey,
    title = @title,
    category = @category,
    description = @description,
    icon_key = @iconKey,
    default_responsibility_id = @defaultResponsibilityId,
    process_area_label = @processAreaLabel,
    is_department_phase_task = @isDepartmentPhaseTask,
    is_required = @isRequired,
    due_in_days = @dueInDays,
    sort_order = @sortOrder
WHERE id = @templateId
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        BindAdminTaskTemplateParameters(command, request, measureNodeId);
        command.Parameters.AddWithValue("templateId", (long)templateId);

        var updatedId = await command.ExecuteScalarAsync();
        if (updatedId is null)
        {
            return null;
        }

        return await GetAdminTaskTemplateById(connection, null, templateId);
    }

    public async Task<bool> DeleteAdminTaskTemplate(int templateId)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        if (await TemplateHasWorkflowTaskReferences(connection, null, templateId))
        {
            throw new InvalidOperationException(
                "Die Vorlage wird bereits von Workflow-Aufgaben referenziert und kann nicht gelöscht werden.");
        }

        const string sql = @"
DELETE FROM workflow_node_task_specs
WHERE id = @templateId;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("templateId", (long)templateId);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static AdminTaskTemplateDto MapAdminTaskTemplate(NpgsqlDataReader reader, int workflowDefinitionId)
    {
        // LA5: is_active und owning_department_id sind nicht mehr im Schema.
        // DTO-Form bleibt fuer Frontend-Kompat unveraendert; dropped fields werden hardcoded.
        return new AdminTaskTemplateDto
        {
            Id = checked((int)reader.GetInt64(0)),
            WorkflowDefinitionId = workflowDefinitionId,
            TemplateKey = reader.GetString(1),
            Title = reader.GetString(2),
            Category = reader.GetString(3),
            Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            IconKey = reader.IsDBNull(5) ? null : reader.GetString(5),
            OwningDepartmentId = null,
            DefaultResponsibilityId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            ProcessAreaLabel = reader.IsDBNull(7) ? null : reader.GetString(7),
            IsDepartmentPhaseTask = reader.GetBoolean(8),
            IsRequired = reader.GetBoolean(9),
            DueInDays = reader.IsDBNull(10) ? null : reader.GetInt32(10),
            SortOrder = reader.GetInt32(11),
            IsActive = true,
            CreatedAt = reader.GetDateTime(12),
            ConditionCount = reader.GetInt32(13),
            DependencyCount = reader.GetInt32(14)
        };
    }

    private static void ValidateAdminTaskTemplateRequest(AdminTaskTemplateUpsertRequest request)
    {
        if (request.WorkflowDefinitionId <= 0)
        {
            throw new InvalidOperationException("workflowDefinitionId must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.TemplateKey))
        {
            throw new InvalidOperationException("Der Template-Key ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("Der Titel ist erforderlich.");
        }

        if (request.DueInDays.HasValue && request.DueInDays.Value < 0)
        {
            throw new InvalidOperationException("dueInDays darf nicht negativ sein.");
        }
    }

    private static void BindAdminTaskTemplateParameters(NpgsqlCommand command, AdminTaskTemplateUpsertRequest request, long measureNodeId)
    {
        command.Parameters.AddWithValue("measureNodeId", measureNodeId);
        command.Parameters.AddWithValue("templateKey", request.TemplateKey!.Trim());
        command.Parameters.AddWithValue("title", request.Title!.Trim());
        command.Parameters.AddWithValue("category", NormalizeAdminTaskTemplateCategory(request.Category));
        command.Parameters.AddWithValue("description", request.Description?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("iconKey", PostgresRepositorySharedHelpers.NormalizeAdminTaskTemplateIconKey(request.IconKey));
        command.Parameters.AddWithValue("defaultResponsibilityId", (object?)request.DefaultResponsibilityId ?? DBNull.Value);
        command.Parameters.AddWithValue("processAreaLabel", (object?)NormalizeNullableText(request.ProcessAreaLabel) ?? DBNull.Value);
        command.Parameters.AddWithValue("isDepartmentPhaseTask", request.IsDepartmentPhaseTask);
        command.Parameters.AddWithValue("isRequired", request.IsRequired);
        command.Parameters.AddWithValue("dueInDays", (object?)request.DueInDays ?? DBNull.Value);
        command.Parameters.AddWithValue("sortOrder", request.SortOrder);
    }

    private static string NormalizeAdminTaskTemplateCategory(string? category)
    {
        var normalized = category?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "general" : normalized;
    }

    // Shared helper — also referenced by TaskTemplateConditionOperations.cs (BindAdminTaskTemplateConditionParameters).
    private static string? NormalizeNullableText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    // Shared helper — also referenced by TaskTemplateDependencyOperations.cs (GetAdminDependencyGraph)
    // and indirectly by callers in WorkflowDefinitionAdminOperations that resolve a workflow_definition_id.
    private static async Task<int> EnsureProcessTypeExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int workflowDefinitionId)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM workflow_definitions
    WHERE id = @id
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", workflowDefinitionId);
        var exists = (bool)(await command.ExecuteScalarAsync() ?? false);
        if (!exists)
        {
            throw new InvalidOperationException("Der ausgewählte Prozesstyp ist ungültig.");
        }
        return workflowDefinitionId;
    }

    // LA5: loest workflow_definition_id zum Maßnahmen-Node der published Version auf.
    // Cross-File-Helper, genutzt von TaskTemplate{Admin,Condition,Dependency}Operations.
    private static async Task<long?> ResolveMeasureNodeIdForDefinition(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int workflowDefinitionId)
    {
        const string sql = @"
SELECT n.id
FROM workflow_definition_versions v
JOIN workflow_nodes n ON n.workflow_definition_version_id = v.id
WHERE v.workflow_definition_id = @workflowDefinitionId
  AND v.published_at IS NOT NULL
  AND n.node_type LIKE 'measure_%'
ORDER BY v.published_at DESC, n.id
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowDefinitionId", workflowDefinitionId);
        var scalar = await command.ExecuteScalarAsync();
        return scalar is long nodeId ? nodeId : null;
    }

    private static async Task EnsureTemplateKeyAvailable(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long measureNodeId,
        string templateKey,
        int? excludedTemplateId)
    {
        const string sql = @"
SELECT id
FROM workflow_node_task_specs
WHERE workflow_node_id = @measureNodeId
  AND spec_key = @templateKey
  AND (@excludedTemplateId IS NULL OR id <> @excludedTemplateId)
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("measureNodeId", measureNodeId);
        command.Parameters.AddWithValue("templateKey", templateKey.Trim());
        command.Parameters.Add("excludedTemplateId", NpgsqlDbType.Bigint).Value =
            excludedTemplateId.HasValue ? (object)(long)excludedTemplateId.Value : DBNull.Value;

        if (await command.ExecuteScalarAsync() is not null)
        {
            throw new InvalidOperationException("Eine Task-Vorlage mit diesem Key existiert bereits.");
        }
    }

    private static async Task<bool> TemplateHasWorkflowTaskReferences(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int templateId)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM workflow_tasks
    WHERE workflow_node_task_spec_id = @templateId
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("templateId", (long)templateId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    // Shared helper — also referenced by TaskTemplateConditionOperations and TaskTemplateDependencyOperations.
    private static async Task<AdminTaskTemplateDto?> GetAdminTaskTemplateById(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int templateId)
    {
        // workflow_definition_id wird via Node->Version->Definition hergeleitet.
        const string sql = @"
SELECT
    tt.id,
    wd.id AS workflow_definition_id,
    tt.spec_key,
    tt.title,
    tt.category,
    tt.description,
    tt.icon_key,
    tt.default_responsibility_id,
    tt.process_area_label,
    tt.is_department_phase_task,
    tt.is_required,
    tt.due_in_days,
    tt.sort_order,
    tt.created_at,
    COALESCE(cond.condition_count, 0) AS condition_count,
    COALESCE(dep.dependency_count, 0) AS dependency_count
FROM workflow_node_task_specs tt
JOIN workflow_nodes n ON n.id = tt.workflow_node_id
JOIN workflow_definition_versions v ON v.id = n.workflow_definition_version_id
JOIN workflow_definitions wd ON wd.id = v.workflow_definition_id
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS condition_count
    FROM workflow_node_task_spec_conditions c
    WHERE c.workflow_node_task_spec_id = tt.id
) cond ON TRUE
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS dependency_count
    FROM workflow_node_task_spec_dependencies d
    WHERE d.workflow_node_task_spec_id = tt.id
) dep ON TRUE
WHERE tt.id = @templateId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("templateId", (long)templateId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        var workflowDefinitionId = reader.GetInt32(1);
        return new AdminTaskTemplateDto
        {
            Id = checked((int)reader.GetInt64(0)),
            WorkflowDefinitionId = workflowDefinitionId,
            TemplateKey = reader.GetString(2),
            Title = reader.GetString(3),
            Category = reader.GetString(4),
            Description = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            IconKey = reader.IsDBNull(6) ? null : reader.GetString(6),
            OwningDepartmentId = null,
            DefaultResponsibilityId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            ProcessAreaLabel = reader.IsDBNull(8) ? null : reader.GetString(8),
            IsDepartmentPhaseTask = reader.GetBoolean(9),
            IsRequired = reader.GetBoolean(10),
            DueInDays = reader.IsDBNull(11) ? null : reader.GetInt32(11),
            SortOrder = reader.GetInt32(12),
            IsActive = true,
            CreatedAt = reader.GetDateTime(13),
            ConditionCount = reader.GetInt32(14),
            DependencyCount = reader.GetInt32(15)
        };
    }
}
