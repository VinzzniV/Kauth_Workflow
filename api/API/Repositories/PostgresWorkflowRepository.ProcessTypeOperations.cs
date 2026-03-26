using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private sealed class ProcessTypeActivationState
    {
        public required bool IsActive { get; init; }
        public required bool CanActivate { get; init; }
        public string? ActivationBlockedReason { get; init; }
    }

    public async Task<List<AdminProcessTypeDto>> GetAdminProcessTypes()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    pt.id,
    pt.key,
    pt.name,
    pt.description,
    pt.requires_supervisor_step,
    pt.approval_task_template_key,
    pt.requires_target_person,
    pt.icon_key,
    pt.is_active,
    pt.sort_order,
    COALESCE(wc.workflow_count, 0) AS workflow_count,
    COALESCE(ac.answer_count, 0) AS answer_definition_count,
    COALESCE(tc.task_count, 0) AS task_template_count,
    CASE
        WHEN COALESCE(ac.active_answer_count, 0) = 0 THEN FALSE
        WHEN COALESCE(tc.active_task_count, 0) = 0 THEN FALSE
        WHEN pt.requires_supervisor_step AND NOT COALESCE(approval.approval_task_exists, FALSE) THEN FALSE
        ELSE TRUE
    END AS can_activate,
    CASE
        WHEN COALESCE(ac.active_answer_count, 0) = 0 THEN 'Keine aktiven Anforderungen konfiguriert.'
        WHEN COALESCE(tc.active_task_count, 0) = 0 THEN 'Keine aktiven Aufgabenvorlagen konfiguriert.'
        WHEN pt.requires_supervisor_step AND NOT COALESCE(approval.approval_task_exists, FALSE) THEN 'Konfigurierter Freigabe-Task fehlt oder ist inaktiv.'
        ELSE NULL
    END AS activation_blocked_reason
FROM process_types pt
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS workflow_count
    FROM workflows w
    WHERE w.process_type_id = pt.id
) wc ON TRUE
LEFT JOIN LATERAL (
    SELECT
        COUNT(*)::int AS answer_count,
        COUNT(*) FILTER (WHERE wad.is_active = TRUE)::int AS active_answer_count
    FROM workflow_answer_definitions wad
    WHERE wad.process_type_id = pt.id
) ac ON TRUE
LEFT JOIN LATERAL (
    SELECT
        COUNT(*)::int AS task_count,
        COUNT(*) FILTER (WHERE tt.is_active = TRUE)::int AS active_task_count
    FROM task_templates tt
    WHERE tt.process_type_id = pt.id
) tc ON TRUE
LEFT JOIN LATERAL (
    SELECT EXISTS(
        SELECT 1
        FROM task_templates tt
        WHERE tt.process_type_id = pt.id
          AND tt.template_key = pt.approval_task_template_key
          AND tt.is_active = TRUE
    ) AS approval_task_exists
) approval ON TRUE
ORDER BY pt.sort_order, pt.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var processTypes = new List<AdminProcessTypeDto>();
        while (await reader.ReadAsync())
        {
            processTypes.Add(new AdminProcessTypeDto
            {
                Id = reader.GetInt32(0),
                Key = reader.GetString(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                RequiresSupervisorStep = reader.GetBoolean(4),
                ApprovalTaskTemplateKey = reader.IsDBNull(5) ? null : reader.GetString(5),
                RequiresTargetPerson = reader.GetBoolean(6),
                IconKey = reader.IsDBNull(7) ? null : reader.GetString(7),
                IsActive = reader.GetBoolean(8),
                SortOrder = reader.GetInt32(9),
                WorkflowCount = reader.GetInt32(10),
                AnswerDefinitionCount = reader.GetInt32(11),
                TaskTemplateCount = reader.GetInt32(12),
                CanActivate = reader.GetBoolean(13),
                ActivationBlockedReason = reader.IsDBNull(14) ? null : reader.GetString(14),
            });
        }

        return processTypes;
    }

    public async Task<AdminProcessTypeDto?> UpdateProcessType(int processTypeId, AdminProcessTypeUpdateRequest request)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var setClauses = new List<string>();
        var parameters = new List<NpgsqlParameter>
        {
            new("@id", processTypeId)
        };

        if (request.Name is not null)
        {
            var trimmedName = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                throw new InvalidOperationException("Der Name darf nicht leer sein.");
            }

            setClauses.Add("name = @name");
            parameters.Add(new NpgsqlParameter("@name", trimmedName));
        }

        if (request.Description is not null)
        {
            setClauses.Add("description = @description");
            parameters.Add(new NpgsqlParameter("@description", string.IsNullOrWhiteSpace(request.Description) ? DBNull.Value : request.Description.Trim()));
        }

        if (request.IconKey is not null)
        {
            setClauses.Add("icon_key = @icon_key");
            parameters.Add(new NpgsqlParameter("@icon_key", string.IsNullOrWhiteSpace(request.IconKey) ? DBNull.Value : request.IconKey.Trim()));
        }

        if (request.IsActive.HasValue)
        {
            var activationState = await LoadProcessTypeActivationState(connection, processTypeId);
            if (activationState is null)
            {
                return null;
            }

            if (request.IsActive.Value && !activationState.IsActive && !activationState.CanActivate)
            {
                throw new InvalidOperationException(
                    activationState.ActivationBlockedReason
                    ?? "Der Prozesstyp kann noch nicht aktiviert werden.");
            }

            setClauses.Add("is_active = @is_active");
            parameters.Add(new NpgsqlParameter("@is_active", request.IsActive.Value));
        }

        if (request.SortOrder.HasValue)
        {
            setClauses.Add("sort_order = @sort_order");
            parameters.Add(new NpgsqlParameter("@sort_order", request.SortOrder.Value));
        }

        if (setClauses.Count == 0)
        {
            return (await GetAdminProcessTypes()).Find(pt => pt.Id == processTypeId);
        }

        var updateSql = $@"
UPDATE process_types
SET {string.Join(", ", setClauses)}
WHERE id = @id
RETURNING id;";

        await using var updateCommand = new NpgsqlCommand(updateSql, connection);
        foreach (var parameter in parameters)
        {
            updateCommand.Parameters.Add(parameter);
        }

        var updatedId = await updateCommand.ExecuteScalarAsync();
        if (updatedId is null)
        {
            return null;
        }

        return (await GetAdminProcessTypes()).Find(pt => pt.Id == processTypeId);
    }

    private static async Task<ProcessTypeActivationState?> LoadProcessTypeActivationState(
        NpgsqlConnection connection,
        int processTypeId)
    {
        const string sql = @"
SELECT
    pt.is_active,
    CASE
        WHEN NOT EXISTS (
            SELECT 1
            FROM workflow_answer_definitions wad
            WHERE wad.process_type_id = pt.id
              AND wad.is_active = TRUE
        ) THEN FALSE
        WHEN NOT EXISTS (
            SELECT 1
            FROM task_templates tt
            WHERE tt.process_type_id = pt.id
              AND tt.is_active = TRUE
        ) THEN FALSE
        WHEN pt.requires_supervisor_step AND NOT EXISTS (
            SELECT 1
            FROM task_templates tt
            WHERE tt.process_type_id = pt.id
              AND tt.template_key = pt.approval_task_template_key
              AND tt.is_active = TRUE
        ) THEN FALSE
        ELSE TRUE
    END AS can_activate,
    CASE
        WHEN NOT EXISTS (
            SELECT 1
            FROM workflow_answer_definitions wad
            WHERE wad.process_type_id = pt.id
              AND wad.is_active = TRUE
        ) THEN 'Keine aktiven Anforderungen konfiguriert.'
        WHEN NOT EXISTS (
            SELECT 1
            FROM task_templates tt
            WHERE tt.process_type_id = pt.id
              AND tt.is_active = TRUE
        ) THEN 'Keine aktiven Aufgabenvorlagen konfiguriert.'
        WHEN pt.requires_supervisor_step AND NOT EXISTS (
            SELECT 1
            FROM task_templates tt
            WHERE tt.process_type_id = pt.id
              AND tt.template_key = pt.approval_task_template_key
              AND tt.is_active = TRUE
        ) THEN 'Konfigurierter Freigabe-Task fehlt oder ist inaktiv.'
        ELSE NULL
    END AS activation_blocked_reason
FROM process_types pt
WHERE pt.id = @id
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", processTypeId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new ProcessTypeActivationState
        {
            IsActive = reader.GetBoolean(0),
            CanActivate = reader.GetBoolean(1),
            ActivationBlockedReason = reader.IsDBNull(2) ? null : reader.GetString(2),
        };
    }
}
