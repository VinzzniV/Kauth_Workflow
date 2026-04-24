using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresRotationRepository
{
    public async Task<bool> DepartmentExists(int departmentId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = "SELECT EXISTS(SELECT 1 FROM departments WHERE id = @departmentId);";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("departmentId", departmentId);
        return await command.ExecuteScalarAsync() is true;
    }

    public async Task<bool> ResponsibilityExists(int responsibilityId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = "SELECT EXISTS(SELECT 1 FROM app_responsibilities WHERE id = @responsibilityId);";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        return await command.ExecuteScalarAsync() is true;
    }

    public async Task<List<DepartmentActionTemplateDto>> GetDepartmentActionTemplates(int? departmentId, bool? isActive = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    t.id AS template_id,
    t.department_id,
    d.name AS department_name,
    t.trigger_type,
    t.title,
    t.description,
    t.task_type,
    t.default_responsibility_id,
    r.name AS default_responsibility_name,
    t.due_offset_days,
    t.reminder_offset_days,
    t.is_automatable,
    t.automation_key,
    t.is_active,
    t.created_at,
    t.updated_at
FROM department_action_templates t
JOIN departments d ON d.id = t.department_id
LEFT JOIN app_responsibilities r ON r.id = t.default_responsibility_id
WHERE (@departmentId IS NULL OR t.department_id = @departmentId)
  AND (@isActive IS NULL OR t.is_active = @isActive)
ORDER BY d.name, t.trigger_type, t.title, t.id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add("departmentId", NpgsqlDbType.Integer).Value = (object?)departmentId ?? DBNull.Value;
        command.Parameters.Add("isActive", NpgsqlDbType.Boolean).Value = (object?)isActive ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync();
        var templates = new List<DepartmentActionTemplateDto>();
        while (await reader.ReadAsync())
        {
            templates.Add(MapDepartmentActionTemplate(reader));
        }

        return templates;
    }

    public async Task<DepartmentActionTemplateDto?> GetDepartmentActionTemplate(int templateId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    t.id AS template_id,
    t.department_id,
    d.name AS department_name,
    t.trigger_type,
    t.title,
    t.description,
    t.task_type,
    t.default_responsibility_id,
    r.name AS default_responsibility_name,
    t.due_offset_days,
    t.reminder_offset_days,
    t.is_automatable,
    t.automation_key,
    t.is_active,
    t.created_at,
    t.updated_at
FROM department_action_templates t
JOIN departments d ON d.id = t.department_id
LEFT JOIN app_responsibilities r ON r.id = t.default_responsibility_id
WHERE t.id = @templateId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("templateId", templateId);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapDepartmentActionTemplate(reader) : null;
    }

    public async Task<DepartmentActionTemplateDto> CreateDepartmentActionTemplate(DepartmentActionTemplateUpsertRequest request)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
INSERT INTO department_action_templates (
    department_id,
    trigger_type,
    title,
    description,
    task_type,
    default_responsibility_id,
    due_offset_days,
    reminder_offset_days,
    is_automatable,
    automation_key,
    is_active
)
VALUES (
    @departmentId,
    @triggerType,
    @title,
    @description,
    @taskType,
    @defaultResponsibilityId,
    @dueOffsetDays,
    @reminderOffsetDays,
    @isAutomatable,
    @automationKey,
    @isActive
)
RETURNING id;";

        int templateId;
        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("departmentId", request.DepartmentId);
            command.Parameters.AddWithValue("triggerType", request.TriggerType);
            command.Parameters.AddWithValue("title", request.Title);
            command.Parameters.AddWithValue("description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("taskType", request.TaskType);
            command.Parameters.AddWithValue("defaultResponsibilityId", (object?)request.DefaultResponsibilityId ?? DBNull.Value);
            command.Parameters.AddWithValue("dueOffsetDays", request.DueOffsetDays);
            command.Parameters.AddWithValue("reminderOffsetDays", (object?)request.ReminderOffsetDays ?? DBNull.Value);
            command.Parameters.AddWithValue("isAutomatable", request.IsAutomatable);
            command.Parameters.AddWithValue("automationKey", (object?)request.AutomationKey ?? DBNull.Value);
            command.Parameters.AddWithValue("isActive", request.IsActive);

            var scalar = await command.ExecuteScalarAsync();
            if (scalar is not int createdTemplateId)
            {
                throw new InvalidOperationException("Department action template could not be created.");
            }

            templateId = createdTemplateId;
        }

        return await GetDepartmentActionTemplate(templateId)
               ?? throw new InvalidOperationException("Created department action template could not be loaded afterwards.");
    }

    public async Task<DepartmentActionTemplateDto?> UpdateDepartmentActionTemplate(
        int templateId,
        DepartmentActionTemplateUpsertRequest request)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
UPDATE department_action_templates
SET
    department_id = @departmentId,
    trigger_type = @triggerType,
    title = @title,
    description = @description,
    task_type = @taskType,
    default_responsibility_id = @defaultResponsibilityId,
    due_offset_days = @dueOffsetDays,
    reminder_offset_days = @reminderOffsetDays,
    is_automatable = @isAutomatable,
    automation_key = @automationKey,
    is_active = @isActive,
    updated_at = NOW()
WHERE id = @templateId
RETURNING id;";

        var updated = false;
        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("templateId", templateId);
            command.Parameters.AddWithValue("departmentId", request.DepartmentId);
            command.Parameters.AddWithValue("triggerType", request.TriggerType);
            command.Parameters.AddWithValue("title", request.Title);
            command.Parameters.AddWithValue("description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("taskType", request.TaskType);
            command.Parameters.AddWithValue("defaultResponsibilityId", (object?)request.DefaultResponsibilityId ?? DBNull.Value);
            command.Parameters.AddWithValue("dueOffsetDays", request.DueOffsetDays);
            command.Parameters.AddWithValue("reminderOffsetDays", (object?)request.ReminderOffsetDays ?? DBNull.Value);
            command.Parameters.AddWithValue("isAutomatable", request.IsAutomatable);
            command.Parameters.AddWithValue("automationKey", (object?)request.AutomationKey ?? DBNull.Value);
            command.Parameters.AddWithValue("isActive", request.IsActive);

            updated = await command.ExecuteScalarAsync() is int;
        }

        return updated ? await GetDepartmentActionTemplate(templateId) : null;
    }

    public async Task<bool> DeleteDepartmentActionTemplate(int templateId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
UPDATE department_action_templates
SET is_active = FALSE,
    updated_at = NOW()
WHERE id = @templateId
  AND is_active = TRUE;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("templateId", templateId);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static DepartmentActionTemplateDto MapDepartmentActionTemplate(NpgsqlDataReader reader)
    {
        var templateId = reader.GetOrdinal("template_id");
        var departmentId = reader.GetOrdinal("department_id");
        var departmentName = reader.GetOrdinal("department_name");
        var triggerType = reader.GetOrdinal("trigger_type");
        var title = reader.GetOrdinal("title");
        var description = reader.GetOrdinal("description");
        var taskType = reader.GetOrdinal("task_type");
        var defaultResponsibilityId = reader.GetOrdinal("default_responsibility_id");
        var defaultResponsibilityName = reader.GetOrdinal("default_responsibility_name");
        var dueOffsetDays = reader.GetOrdinal("due_offset_days");
        var reminderOffsetDays = reader.GetOrdinal("reminder_offset_days");
        var isAutomatable = reader.GetOrdinal("is_automatable");
        var automationKey = reader.GetOrdinal("automation_key");
        var isActive = reader.GetOrdinal("is_active");
        var createdAt = reader.GetOrdinal("created_at");
        var updatedAt = reader.GetOrdinal("updated_at");

        return new DepartmentActionTemplateDto
        {
            Id = reader.GetInt32(templateId),
            DepartmentId = reader.GetInt32(departmentId),
            DepartmentName = reader.IsDBNull(departmentName) ? null : reader.GetString(departmentName),
            TriggerType = reader.GetString(triggerType),
            Title = reader.GetString(title),
            Description = reader.IsDBNull(description) ? null : reader.GetString(description),
            TaskType = reader.GetString(taskType),
            DefaultResponsibilityId = reader.IsDBNull(defaultResponsibilityId) ? null : reader.GetInt32(defaultResponsibilityId),
            DefaultResponsibilityName = reader.IsDBNull(defaultResponsibilityName) ? null : reader.GetString(defaultResponsibilityName),
            DueOffsetDays = reader.GetInt32(dueOffsetDays),
            ReminderOffsetDays = reader.IsDBNull(reminderOffsetDays) ? null : reader.GetInt32(reminderOffsetDays),
            IsAutomatable = reader.GetBoolean(isAutomatable),
            AutomationKey = reader.IsDBNull(automationKey) ? null : reader.GetString(automationKey),
            IsActive = reader.GetBoolean(isActive),
            CreatedAt = reader.GetDateTime(createdAt),
            UpdatedAt = reader.GetDateTime(updatedAt)
        };
    }
}
