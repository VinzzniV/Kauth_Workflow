using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private static readonly HashSet<string> AllowedAdminTaskTemplateConditionOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq",
        "neq",
        "is_true",
        "is_false",
        "is_null",
        "is_not_null"
    };

    private static readonly HashSet<string> AllowedAdminTaskTemplateDependencyStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "open",
        "ready",
        "in_progress",
        "blocked",
        "done"
    };

    public async Task<List<AdminTaskTemplateDto>> GetAdminTaskTemplates(int processTypeId)
    {
        if (processTypeId <= 0)
        {
            throw new InvalidOperationException("processTypeId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        await EnsureProcessTypeExists(connection, null, processTypeId);

        const string sql = @"
SELECT
    tt.id,
    tt.process_type_id,
    tt.template_key,
    tt.title,
    tt.category,
    tt.description,
    tt.icon_key,
    tt.owning_department_id,
    tt.default_responsibility_id,
    tt.process_area_label,
    tt.is_department_phase_task,
    tt.is_required,
    tt.due_in_days,
    tt.sort_order,
    tt.is_active,
    tt.created_at,
    COALESCE(cond.condition_count, 0) AS condition_count,
    COALESCE(dep.dependency_count, 0) AS dependency_count
FROM task_templates tt
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS condition_count
    FROM task_template_conditions c
    WHERE c.task_template_id = tt.id
) cond ON TRUE
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS dependency_count
    FROM task_template_dependencies d
    WHERE d.task_template_id = tt.id
) dep ON TRUE
WHERE tt.process_type_id = @processTypeId
ORDER BY tt.sort_order, tt.title, tt.id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        await using var reader = await command.ExecuteReaderAsync();

        var templates = new List<AdminTaskTemplateDto>();
        while (await reader.ReadAsync())
        {
            templates.Add(MapAdminTaskTemplate(reader));
        }

        return templates;
    }

    public async Task<AdminDependencyGraphDto> GetAdminDependencyGraph(int processTypeId)
    {
        if (processTypeId <= 0)
        {
            throw new InvalidOperationException("processTypeId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        await EnsureProcessTypeExists(connection, null, processTypeId);

        const string nodesSql = @"
SELECT
    id,
    title,
    category
FROM task_templates
WHERE process_type_id = @processTypeId
ORDER BY sort_order, title, id;";

        var nodes = new List<AdminDependencyGraphNodeDto>();
        await using (var nodesCommand = new NpgsqlCommand(nodesSql, connection))
        {
            nodesCommand.Parameters.AddWithValue("processTypeId", processTypeId);
            await using var reader = await nodesCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                nodes.Add(new AdminDependencyGraphNodeDto
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Category = reader.GetString(2)
                });
            }
        }

        const string edgesSql = @"
SELECT
    d.id,
    d.task_template_id,
    d.depends_on_task_template_id,
    d.required_status
FROM task_template_dependencies d
JOIN task_templates t ON t.id = d.task_template_id
WHERE t.process_type_id = @processTypeId
ORDER BY d.id;";

        var edges = new List<AdminDependencyGraphEdgeDto>();
        await using (var edgesCommand = new NpgsqlCommand(edgesSql, connection))
        {
            edgesCommand.Parameters.AddWithValue("processTypeId", processTypeId);
            await using var reader = await edgesCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                edges.Add(new AdminDependencyGraphEdgeDto
                {
                    Id = reader.GetInt64(0),
                    SourceTemplateId = reader.GetInt32(1),
                    TargetTemplateId = reader.GetInt32(2),
                    RequiredStatus = reader.GetString(3)
                });
            }
        }

        return new AdminDependencyGraphDto
        {
            Nodes = nodes,
            Edges = edges
        };
    }

    public async Task<AdminTaskTemplateDto> CreateAdminTaskTemplate(AdminTaskTemplateUpsertRequest request)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        ValidateAdminTaskTemplateRequest(request);
        await EnsureProcessTypeExists(connection, null, request.ProcessTypeId);
        await EnsureTemplateKeyAvailable(connection, null, request.TemplateKey!, null);

        const string sql = @"
INSERT INTO task_templates (
    process_type_id,
    template_key,
    title,
    category,
    description,
    icon_key,
    owning_department_id,
    default_responsibility_id,
    process_area_label,
    is_department_phase_task,
    is_required,
    due_in_days,
    sort_order,
    is_active
)
VALUES (
    @processTypeId,
    @templateKey,
    @title,
    @category,
    @description,
    @iconKey,
    @owningDepartmentId,
    @defaultResponsibilityId,
    @processAreaLabel,
    @isDepartmentPhaseTask,
    @isRequired,
    @dueInDays,
    @sortOrder,
    @isActive
)
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        BindAdminTaskTemplateParameters(command, request);

        var createdId = await command.ExecuteScalarAsync();
        if (createdId is not int templateId)
        {
            throw new InvalidOperationException("Task template could not be created.");
        }

        return await GetAdminTaskTemplateById(connection, null, templateId)
            ?? throw new InvalidOperationException("Task template could not be loaded after creation.");
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
        await EnsureProcessTypeExists(connection, null, request.ProcessTypeId);
        await EnsureTemplateKeyAvailable(connection, null, request.TemplateKey!, templateId);

        const string sql = @"
UPDATE task_templates
SET
    process_type_id = @processTypeId,
    template_key = @templateKey,
    title = @title,
    category = @category,
    description = @description,
    icon_key = @iconKey,
    owning_department_id = @owningDepartmentId,
    default_responsibility_id = @defaultResponsibilityId,
    process_area_label = @processAreaLabel,
    is_department_phase_task = @isDepartmentPhaseTask,
    is_required = @isRequired,
    due_in_days = @dueInDays,
    sort_order = @sortOrder,
    is_active = @isActive
WHERE id = @templateId
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        BindAdminTaskTemplateParameters(command, request);
        command.Parameters.AddWithValue("templateId", templateId);

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
DELETE FROM task_templates
WHERE id = @templateId;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("templateId", templateId);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<List<AdminTaskTemplateConditionDto>> GetAdminTaskTemplateConditions(int templateId)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var template = await GetAdminTaskTemplateById(connection, null, templateId);
        if (template is null)
        {
            throw new InvalidOperationException("Task template not found.");
        }

        const string sql = @"
SELECT
    id,
    task_template_id,
    condition_group,
    answer_key,
    operator,
    expected_value_text,
    expected_value_boolean,
    expected_value_number
FROM task_template_conditions
WHERE task_template_id = @templateId
ORDER BY condition_group, id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("templateId", templateId);
        await using var reader = await command.ExecuteReaderAsync();

        var conditions = new List<AdminTaskTemplateConditionDto>();
        while (await reader.ReadAsync())
        {
            conditions.Add(MapAdminTaskTemplateCondition(reader));
        }

        return conditions;
    }

    public async Task<AdminTaskTemplateConditionDto> CreateAdminTaskTemplateCondition(
        int templateId,
        AdminTaskTemplateConditionCreateRequest request)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var template = await GetAdminTaskTemplateById(connection, null, templateId);
        if (template is null)
        {
            throw new InvalidOperationException("Task template not found.");
        }

        ValidateAdminTaskTemplateConditionRequest(request);
        await EnsureAnswerKeyBelongsToProcessType(connection, null, request.AnswerKey!, template.ProcessTypeId);

        const string sql = @"
INSERT INTO task_template_conditions (
    task_template_id,
    condition_group,
    answer_key,
    operator,
    expected_value_text,
    expected_value_boolean,
    expected_value_number
)
VALUES (
    @templateId,
    @conditionGroup,
    @answerKey,
    @operator,
    @expectedValueText,
    @expectedValueBoolean,
    @expectedValueNumber
)
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        BindAdminTaskTemplateConditionParameters(command, templateId, request);

        var createdId = await command.ExecuteScalarAsync();
        if (createdId is not long conditionId)
        {
            throw new InvalidOperationException("Task template condition could not be created.");
        }

        return await GetAdminTaskTemplateConditionById(connection, null, templateId, conditionId)
            ?? throw new InvalidOperationException("Task template condition could not be loaded after creation.");
    }

    public async Task<bool> DeleteAdminTaskTemplateCondition(int templateId, long conditionId)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        if (conditionId <= 0)
        {
            throw new InvalidOperationException("conditionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
DELETE FROM task_template_conditions
WHERE id = @conditionId
  AND task_template_id = @templateId;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("conditionId", conditionId);
        command.Parameters.AddWithValue("templateId", templateId);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<List<AdminTaskTemplateDependencyDto>> GetAdminTaskTemplateDependencies(int templateId)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var template = await GetAdminTaskTemplateById(connection, null, templateId);
        if (template is null)
        {
            throw new InvalidOperationException("Task template not found.");
        }

        const string sql = @"
SELECT
    d.id,
    d.task_template_id,
    d.depends_on_task_template_id,
    dep.title,
    d.required_status
FROM task_template_dependencies d
JOIN task_templates dep ON dep.id = d.depends_on_task_template_id
WHERE d.task_template_id = @templateId
ORDER BY dep.sort_order, dep.title, d.id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("templateId", templateId);
        await using var reader = await command.ExecuteReaderAsync();

        var dependencies = new List<AdminTaskTemplateDependencyDto>();
        while (await reader.ReadAsync())
        {
            dependencies.Add(MapAdminTaskTemplateDependency(reader));
        }

        return dependencies;
    }

    public async Task<AdminTaskTemplateDependencyDto> CreateAdminTaskTemplateDependency(
        int templateId,
        AdminTaskTemplateDependencyCreateRequest request)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var template = await GetAdminTaskTemplateById(connection, null, templateId);
        if (template is null)
        {
            throw new InvalidOperationException("Task template not found.");
        }

        ValidateAdminTaskTemplateDependencyRequest(request);

        var dependsOnTemplate = await GetAdminTaskTemplateById(connection, null, request.DependsOnTaskTemplateId);
        if (dependsOnTemplate is null)
        {
            throw new InvalidOperationException("Die referenzierte Abhängigkeit existiert nicht.");
        }

        if (dependsOnTemplate.ProcessTypeId != template.ProcessTypeId)
        {
            throw new InvalidOperationException("Abhängigkeiten dürfen nur innerhalb desselben Prozesstyps angelegt werden.");
        }

        if (request.DependsOnTaskTemplateId == templateId)
        {
            throw new InvalidOperationException("Ein Task-Template darf nicht von sich selbst abhängen.");
        }

        if (await AdminTaskTemplateDependencyExists(connection, null, templateId, request.DependsOnTaskTemplateId))
        {
            throw new InvalidOperationException("Diese Abhängigkeit ist bereits vorhanden.");
        }

        var existingDependencies = await LoadAdminTaskTemplateDependencyGraph(connection, null, template.ProcessTypeId);
        if (WouldCreateDependencyCycle(templateId, request.DependsOnTaskTemplateId, existingDependencies))
        {
            throw new InvalidOperationException("Die Abhängigkeit würde einen Kreis erzeugen.");
        }

        const string sql = @"
INSERT INTO task_template_dependencies (
    task_template_id,
    depends_on_task_template_id,
    required_status
)
VALUES (
    @templateId,
    @dependsOnTaskTemplateId,
    @requiredStatus
)
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        BindAdminTaskTemplateDependencyParameters(command, templateId, request);

        var createdId = await command.ExecuteScalarAsync();
        if (createdId is not long dependencyId)
        {
            throw new InvalidOperationException("Task template dependency could not be created.");
        }

        return await GetAdminTaskTemplateDependencyById(connection, null, templateId, dependencyId)
            ?? throw new InvalidOperationException("Task template dependency could not be loaded after creation.");
    }

    public async Task<bool> DeleteAdminTaskTemplateDependency(int templateId, long dependencyId)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        if (dependencyId <= 0)
        {
            throw new InvalidOperationException("dependencyId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
DELETE FROM task_template_dependencies
WHERE id = @dependencyId
  AND task_template_id = @templateId;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("dependencyId", dependencyId);
        command.Parameters.AddWithValue("templateId", templateId);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static AdminTaskTemplateDto MapAdminTaskTemplate(NpgsqlDataReader reader)
    {
        return new AdminTaskTemplateDto
        {
            Id = reader.GetInt32(0),
            ProcessTypeId = reader.GetInt32(1),
            TemplateKey = reader.GetString(2),
            Title = reader.GetString(3),
            Category = reader.GetString(4),
            Description = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            IconKey = reader.IsDBNull(6) ? null : reader.GetString(6),
            OwningDepartmentId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            DefaultResponsibilityId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            ProcessAreaLabel = reader.IsDBNull(9) ? null : reader.GetString(9),
            IsDepartmentPhaseTask = reader.GetBoolean(10),
            IsRequired = reader.GetBoolean(11),
            DueInDays = reader.IsDBNull(12) ? null : reader.GetInt32(12),
            SortOrder = reader.GetInt32(13),
            IsActive = reader.GetBoolean(14),
            CreatedAt = reader.GetDateTime(15),
            ConditionCount = reader.GetInt32(16),
            DependencyCount = reader.GetInt32(17)
        };
    }

    private static AdminTaskTemplateConditionDto MapAdminTaskTemplateCondition(NpgsqlDataReader reader)
    {
        return new AdminTaskTemplateConditionDto
        {
            Id = reader.GetInt64(0),
            TaskTemplateId = reader.GetInt32(1),
            ConditionGroup = reader.GetInt32(2),
            AnswerKey = reader.GetString(3),
            Operator = reader.GetString(4),
            ExpectedValueText = reader.IsDBNull(5) ? null : reader.GetString(5),
            ExpectedValueBoolean = reader.IsDBNull(6) ? null : reader.GetBoolean(6),
            ExpectedValueNumber = reader.IsDBNull(7) ? null : reader.GetDecimal(7)
        };
    }

    private static AdminTaskTemplateDependencyDto MapAdminTaskTemplateDependency(NpgsqlDataReader reader)
    {
        return new AdminTaskTemplateDependencyDto
        {
            Id = reader.GetInt64(0),
            TaskTemplateId = reader.GetInt32(1),
            DependsOnTaskTemplateId = reader.GetInt32(2),
            DependsOnTemplateTitle = reader.GetString(3),
            RequiredStatus = reader.GetString(4)
        };
    }

    private static void ValidateAdminTaskTemplateRequest(AdminTaskTemplateUpsertRequest request)
    {
        if (request.ProcessTypeId <= 0)
        {
            throw new InvalidOperationException("processTypeId must be greater than zero.");
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

    private static void ValidateAdminTaskTemplateConditionRequest(AdminTaskTemplateConditionCreateRequest request)
    {
        if (request.ConditionGroup <= 0)
        {
            throw new InvalidOperationException("conditionGroup must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.AnswerKey))
        {
            throw new InvalidOperationException("Der Answer-Key ist erforderlich.");
        }

        var normalizedOperator = NormalizeAdminTaskTemplateConditionOperator(request.Operator);
        if (!AllowedAdminTaskTemplateConditionOperators.Contains(normalizedOperator))
        {
            throw new InvalidOperationException("Der Operator ist ungültig.");
        }
    }

    private static void ValidateAdminTaskTemplateDependencyRequest(AdminTaskTemplateDependencyCreateRequest request)
    {
        if (request.DependsOnTaskTemplateId <= 0)
        {
            throw new InvalidOperationException("dependsOnTaskTemplateId must be greater than zero.");
        }

        var normalizedRequiredStatus = NormalizeAdminTaskTemplateDependencyStatus(request.RequiredStatus);
        if (!AllowedAdminTaskTemplateDependencyStatuses.Contains(normalizedRequiredStatus))
        {
            throw new InvalidOperationException("Der required_status ist ungültig.");
        }
    }

    private static void BindAdminTaskTemplateParameters(NpgsqlCommand command, AdminTaskTemplateUpsertRequest request)
    {
        command.Parameters.AddWithValue("processTypeId", request.ProcessTypeId);
        command.Parameters.AddWithValue("templateKey", request.TemplateKey!.Trim());
        command.Parameters.AddWithValue("title", request.Title!.Trim());
        command.Parameters.AddWithValue("category", NormalizeAdminTaskTemplateCategory(request.Category));
        command.Parameters.AddWithValue("description", request.Description?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("iconKey", NormalizeAdminTaskTemplateIconKey(request.IconKey));
        command.Parameters.AddWithValue("owningDepartmentId", (object?)request.OwningDepartmentId ?? DBNull.Value);
        command.Parameters.AddWithValue("defaultResponsibilityId", (object?)request.DefaultResponsibilityId ?? DBNull.Value);
        command.Parameters.AddWithValue("processAreaLabel", (object?)NormalizeNullableText(request.ProcessAreaLabel) ?? DBNull.Value);
        command.Parameters.AddWithValue("isDepartmentPhaseTask", request.IsDepartmentPhaseTask);
        command.Parameters.AddWithValue("isRequired", request.IsRequired);
        command.Parameters.AddWithValue("dueInDays", (object?)request.DueInDays ?? DBNull.Value);
        command.Parameters.AddWithValue("sortOrder", request.SortOrder);
        command.Parameters.AddWithValue("isActive", request.IsActive);
    }

    private static void BindAdminTaskTemplateConditionParameters(
        NpgsqlCommand command,
        int templateId,
        AdminTaskTemplateConditionCreateRequest request)
    {
        command.Parameters.AddWithValue("templateId", templateId);
        command.Parameters.AddWithValue("conditionGroup", request.ConditionGroup);
        command.Parameters.AddWithValue("answerKey", request.AnswerKey!.Trim());
        command.Parameters.AddWithValue("operator", NormalizeAdminTaskTemplateConditionOperator(request.Operator));
        command.Parameters.AddWithValue("expectedValueText", (object?)NormalizeNullableText(request.ExpectedValueText) ?? DBNull.Value);
        command.Parameters.AddWithValue("expectedValueBoolean", (object?)request.ExpectedValueBoolean ?? DBNull.Value);
        command.Parameters.AddWithValue("expectedValueNumber", (object?)request.ExpectedValueNumber ?? DBNull.Value);
    }

    private static void BindAdminTaskTemplateDependencyParameters(
        NpgsqlCommand command,
        int templateId,
        AdminTaskTemplateDependencyCreateRequest request)
    {
        command.Parameters.AddWithValue("templateId", templateId);
        command.Parameters.AddWithValue("dependsOnTaskTemplateId", request.DependsOnTaskTemplateId);
        command.Parameters.AddWithValue("requiredStatus", NormalizeAdminTaskTemplateDependencyStatus(request.RequiredStatus));
    }

    private static string NormalizeAdminTaskTemplateCategory(string? category)
    {
        var normalized = category?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "general" : normalized;
    }

    private static string NormalizeAdminTaskTemplateIconKey(string? iconKey)
    {
        var normalized = iconKey?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "berechtigungen" : normalized;
    }

    private static string NormalizeAdminTaskTemplateConditionOperator(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeAdminTaskTemplateDependencyStatus(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeNullableText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static async Task EnsureProcessTypeExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int processTypeId)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM process_types
    WHERE id = @processTypeId
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        var exists = (bool)(await command.ExecuteScalarAsync() ?? false);
        if (!exists)
        {
            throw new InvalidOperationException("Der ausgewählte Prozesstyp ist ungültig.");
        }
    }

    private static async Task EnsureAnswerKeyBelongsToProcessType(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string answerKey,
        int processTypeId)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM workflow_answer_definitions
    WHERE answer_key = @answerKey
      AND process_type_id = @processTypeId
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("answerKey", answerKey.Trim());
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        var exists = (bool)(await command.ExecuteScalarAsync() ?? false);
        if (!exists)
        {
            throw new InvalidOperationException("Der Answer-Key ist für diesen Prozesstyp ungültig.");
        }
    }

    private static async Task EnsureTemplateKeyAvailable(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string templateKey,
        int? excludedTemplateId)
    {
        const string sql = @"
SELECT id
FROM task_templates
WHERE template_key = @templateKey
  AND (@excludedTemplateId IS NULL OR id <> @excludedTemplateId)
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("templateKey", templateKey.Trim());
        command.Parameters.Add("excludedTemplateId", NpgsqlDbType.Integer).Value =
            (object?)excludedTemplateId ?? DBNull.Value;

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
    WHERE task_template_id = @templateId
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("templateId", templateId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<AdminTaskTemplateDto?> GetAdminTaskTemplateById(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int templateId)
    {
        const string sql = @"
SELECT
    tt.id,
    tt.process_type_id,
    tt.template_key,
    tt.title,
    tt.category,
    tt.description,
    tt.icon_key,
    tt.owning_department_id,
    tt.default_responsibility_id,
    tt.process_area_label,
    tt.is_department_phase_task,
    tt.is_required,
    tt.due_in_days,
    tt.sort_order,
    tt.is_active,
    tt.created_at,
    COALESCE(cond.condition_count, 0) AS condition_count,
    COALESCE(dep.dependency_count, 0) AS dependency_count
FROM task_templates tt
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS condition_count
    FROM task_template_conditions c
    WHERE c.task_template_id = tt.id
) cond ON TRUE
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS dependency_count
    FROM task_template_dependencies d
    WHERE d.task_template_id = tt.id
) dep ON TRUE
WHERE tt.id = @templateId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("templateId", templateId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapAdminTaskTemplate(reader);
    }

    private static async Task<AdminTaskTemplateConditionDto?> GetAdminTaskTemplateConditionById(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int templateId,
        long conditionId)
    {
        const string sql = @"
SELECT
    id,
    task_template_id,
    condition_group,
    answer_key,
    operator,
    expected_value_text,
    expected_value_boolean,
    expected_value_number
FROM task_template_conditions
WHERE id = @conditionId
  AND task_template_id = @templateId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("conditionId", conditionId);
        command.Parameters.AddWithValue("templateId", templateId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapAdminTaskTemplateCondition(reader);
    }

    private static async Task<Dictionary<int, List<int>>> LoadAdminTaskTemplateDependencyGraph(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int processTypeId)
    {
        const string sql = @"
SELECT d.task_template_id, d.depends_on_task_template_id
FROM task_template_dependencies d
JOIN task_templates t ON t.id = d.task_template_id
WHERE t.process_type_id = @processTypeId;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        await using var reader = await command.ExecuteReaderAsync();

        var graph = new Dictionary<int, List<int>>();
        while (await reader.ReadAsync())
        {
            var taskTemplateId = reader.GetInt32(0);
            var dependsOnTaskTemplateId = reader.GetInt32(1);

            if (!graph.TryGetValue(taskTemplateId, out var dependencies))
            {
                dependencies = new List<int>();
                graph[taskTemplateId] = dependencies;
            }

            dependencies.Add(dependsOnTaskTemplateId);
        }

        return graph;
    }

    private static async Task<bool> AdminTaskTemplateDependencyExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int templateId,
        int dependsOnTaskTemplateId)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM task_template_dependencies
    WHERE task_template_id = @templateId
      AND depends_on_task_template_id = @dependsOnTaskTemplateId
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("templateId", templateId);
        command.Parameters.AddWithValue("dependsOnTaskTemplateId", dependsOnTaskTemplateId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static bool WouldCreateDependencyCycle(
        int taskTemplateId,
        int dependsOnTaskTemplateId,
        IReadOnlyDictionary<int, List<int>> graph)
    {
        var stack = new Stack<int>();
        var visited = new HashSet<int>();
        stack.Push(dependsOnTaskTemplateId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (current == taskTemplateId)
            {
                return true;
            }

            if (!graph.TryGetValue(current, out var nextItems))
            {
                continue;
            }

            foreach (var next in nextItems)
            {
                stack.Push(next);
            }
        }

        return false;
    }

    private static async Task<AdminTaskTemplateDependencyDto?> GetAdminTaskTemplateDependencyById(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int templateId,
        long dependencyId)
    {
        const string sql = @"
SELECT
    d.id,
    d.task_template_id,
    d.depends_on_task_template_id,
    dep.title,
    d.required_status
FROM task_template_dependencies d
JOIN task_templates dep ON dep.id = d.depends_on_task_template_id
WHERE d.id = @dependencyId
  AND d.task_template_id = @templateId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("dependencyId", dependencyId);
        command.Parameters.AddWithValue("templateId", templateId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapAdminTaskTemplateDependency(reader);
    }
}
