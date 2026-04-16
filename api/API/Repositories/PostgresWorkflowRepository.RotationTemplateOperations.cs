using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
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
    t.id,
    t.department_id,
    d.name,
    t.trigger_type,
    t.title,
    t.description,
    t.task_type,
    t.default_responsibility_id,
    r.name,
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
    t.id,
    t.department_id,
    d.name,
    t.trigger_type,
    t.title,
    t.description,
    t.task_type,
    t.default_responsibility_id,
    r.name,
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
        return new DepartmentActionTemplateDto
        {
            Id = reader.GetInt32(0),
            DepartmentId = reader.GetInt32(1),
            DepartmentName = reader.IsDBNull(2) ? null : reader.GetString(2),
            TriggerType = reader.GetString(3),
            Title = reader.GetString(4),
            Description = reader.IsDBNull(5) ? null : reader.GetString(5),
            TaskType = reader.GetString(6),
            DefaultResponsibilityId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            DefaultResponsibilityName = reader.IsDBNull(8) ? null : reader.GetString(8),
            DueOffsetDays = reader.GetInt32(9),
            ReminderOffsetDays = reader.IsDBNull(10) ? null : reader.GetInt32(10),
            IsAutomatable = reader.GetBoolean(11),
            AutomationKey = reader.IsDBNull(12) ? null : reader.GetString(12),
            IsActive = reader.GetBoolean(13),
            CreatedAt = reader.GetDateTime(14),
            UpdatedAt = reader.GetDateTime(15)
        };
    }
}
