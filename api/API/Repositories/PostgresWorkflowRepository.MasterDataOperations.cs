using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    // Stammdaten fuer die HR-Erfassung.
    public async Task<List<DepartmentDto>> GetDepartments()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT id, name
FROM departments
ORDER BY name;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var departments = new List<DepartmentDto>();
        while (await reader.ReadAsync())
        {
            departments.Add(new DepartmentDto
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return departments;
    }

    public async Task<List<RoleDto>> GetRoles()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT r.id, r.department_id, d.name, r.name, r.is_active
FROM app_roles r
JOIN departments d ON d.id = r.department_id
WHERE r.role_kind = 'position'
  AND r.is_active = TRUE
ORDER BY d.name, r.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var roles = new List<RoleDto>();
        while (await reader.ReadAsync())
        {
            roles.Add(new RoleDto
            {
                Id = reader.GetInt32(0),
                DepartmentId = reader.GetInt32(1),
                DepartmentName = reader.GetString(2),
                Name = reader.GetString(3),
                IsActive = reader.GetBoolean(4)
            });
        }

        return roles;
    }

    public async Task<bool> IsManagerCreatableDefinition(string workflowDefinitionKey)
    {
        if (string.IsNullOrWhiteSpace(workflowDefinitionKey))
        {
            return false;
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT allows_manager_creation
FROM workflow_definitions
WHERE definition_key = @workflowDefinitionKey
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workflowDefinitionKey", workflowDefinitionKey.Trim().ToLowerInvariant());

        var scalar = await command.ExecuteScalarAsync();
        return scalar is bool allowsManagerCreation && allowsManagerCreation;
    }

    public async Task<List<WorkflowTargetPersonDto>> SearchWorkflowTargetPeople(
        string? query,
        int limit = 20,
        IReadOnlyCollection<int>? observableDepartmentIds = null)
    {
        return await SearchPeopleInternal(query, limit, observableDepartmentIds, requireCompletedOnboarding: false);
    }

    public async Task<List<WorkflowTargetPersonDto>> SearchRotationEligiblePeople(
        string? query,
        int limit = 20,
        IReadOnlyCollection<int>? observableDepartmentIds = null)
    {
        return await SearchPeopleInternal(query, limit, observableDepartmentIds, requireCompletedOnboarding: true);
    }

    private async Task<List<WorkflowTargetPersonDto>> SearchPeopleInternal(
        string? query,
        int limit,
        IReadOnlyCollection<int>? observableDepartmentIds,
        bool requireCompletedOnboarding)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var sql = $@"
WITH latest_workflow AS (
    SELECT DISTINCT ON (resolved.person_id)
        resolved.person_id,
        resolved.department_id,
        resolved.position_role_id,
        resolved.employee_number,
        resolved.badge_number,
        resolved.first_name,
        resolved.last_name
    FROM (
        SELECT
            w.target_person_id AS person_id,
            w.department_id,
            w.position_role_id,
            w.employee_number,
            w.badge_number,
            w.first_name,
            w.last_name,
            w.created_at,
            w.id
        FROM workflows w
        WHERE w.target_person_id IS NOT NULL

        UNION ALL

        SELECT
            p.id AS person_id,
            w.department_id,
            w.position_role_id,
            w.employee_number,
            w.badge_number,
            w.first_name,
            w.last_name,
            w.created_at,
            w.id
        FROM people p
        JOIN workflows w ON p.employee_number IS NOT NULL AND w.employee_number = p.employee_number
    ) resolved
    ORDER BY resolved.person_id, resolved.created_at DESC, resolved.id DESC
),
latest_completed_onboarding AS (
    SELECT DISTINCT ON (resolved.person_id)
        resolved.person_id,
        resolved.workflow_uid,
        resolved.completed_at
    FROM (
        SELECT
            w.target_person_id AS person_id,
            w.uid AS workflow_uid,
            COALESCE(w.completed_at, w.created_at) AS completed_at,
            w.id
        FROM workflows w
        JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
        LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
        LEFT JOIN workflow_definitions vpt ON vpt.id = v.workflow_definition_id
        WHERE w.target_person_id IS NOT NULL
          AND pt.definition_key = 'onboarding'
          AND (w.workflow_definition_version_id IS NULL OR vpt.definition_key = 'onboarding')
          AND w.status = 'completed'

        UNION ALL

        SELECT
            p.id AS person_id,
            w.uid AS workflow_uid,
            COALESCE(w.completed_at, w.created_at) AS completed_at,
            w.id
        FROM people p
        JOIN workflows w ON p.employee_number IS NOT NULL AND w.employee_number = p.employee_number
        JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
        LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
        LEFT JOIN workflow_definitions vpt ON vpt.id = v.workflow_definition_id
        WHERE pt.definition_key = 'onboarding'
          AND (w.workflow_definition_version_id IS NULL OR vpt.definition_key = 'onboarding')
          AND w.status = 'completed'
    ) resolved
    ORDER BY resolved.person_id, resolved.completed_at DESC, resolved.id DESC
)
SELECT
    p.id,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, latest_workflow.first_name), COALESCE(p.last_name, latest_workflow.last_name))), ''),
        u.display_name,
        linked_directory.display_name,
        'Person #' || p.id::text
    ) AS display_name,
    COALESCE(p.department_id, latest_workflow.department_id, u.department_id) AS department_id,
    d.name AS department_name,
    COALESCE(p.current_position_role_id, latest_workflow.position_role_id) AS role_id,
    r.name AS role_name,
    COALESCE(p.employee_number, latest_workflow.employee_number, linked_directory.employee_number) AS employee_number,
    COALESCE(p.badge_number, latest_workflow.badge_number) AS badge_number,
    COALESCE(p.first_name, latest_workflow.first_name) AS first_name,
    COALESCE(p.last_name, latest_workflow.last_name) AS last_name,
    COALESCE(
        NULLIF(BTRIM(p.employment_status), ''),
        CASE
            WHEN p.exit_date IS NOT NULL THEN 'exited'
            WHEN p.app_user_id IS NOT NULL THEN 'active'
            ELSE 'planned'
        END
    ) AS employment_status,
    p.app_user_id,
    p.directory_identity_id,
    CASE
        WHEN p.directory_identity_id IS NOT NULL THEN 'linked'
        WHEN p.app_user_id IS NOT NULL THEN 'user_only'
        ELSE 'unlinked'
    END AS directory_link_status,
    linked_directory.display_name AS directory_display_name,
    linked_directory.user_principal_name,
    linked_directory.mail,
    linked_directory.employee_number AS directory_employee_number,
    latest_completed_onboarding.workflow_uid,
    latest_completed_onboarding.completed_at
FROM people p
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN directory_identities linked_directory ON linked_directory.id = p.directory_identity_id
LEFT JOIN latest_workflow ON latest_workflow.person_id = p.id
LEFT JOIN latest_completed_onboarding ON latest_completed_onboarding.person_id = p.id
LEFT JOIN departments d ON d.id = COALESCE(p.department_id, latest_workflow.department_id, u.department_id)
LEFT JOIN app_roles r ON r.id = COALESCE(p.current_position_role_id, latest_workflow.position_role_id)
WHERE (
      @departmentIds IS NULL
      OR COALESCE(p.department_id, latest_workflow.department_id, u.department_id) = ANY(@departmentIds)
  )
  {(requireCompletedOnboarding ? "AND latest_completed_onboarding.workflow_uid IS NOT NULL" : string.Empty)}
  AND (
      @query = ''
      OR COALESCE(
            NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, latest_workflow.first_name), COALESCE(p.last_name, latest_workflow.last_name))), ''),
            u.display_name,
            linked_directory.display_name,
            'Person #' || p.id::text
         ) ILIKE @pattern
      OR COALESCE(d.name, '') ILIKE @pattern
      OR COALESCE(r.name, '') ILIKE @pattern
      OR CAST(COALESCE(p.employee_number, latest_workflow.employee_number, linked_directory.employee_number, 0) AS TEXT) ILIKE @pattern
      OR COALESCE(linked_directory.user_principal_name, '') ILIKE @pattern
      OR COALESCE(linked_directory.mail, '') ILIKE @pattern
  )
ORDER BY
    latest_completed_onboarding.completed_at DESC NULLS LAST,
    display_name,
    p.id
LIMIT @limit;";

        await using var command = new NpgsqlCommand(sql, connection);
        var normalizedQuery = query?.Trim() ?? string.Empty;
        command.Parameters.AddWithValue("query", normalizedQuery);
        command.Parameters.AddWithValue("pattern", $"%{normalizedQuery}%");
        command.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, 50));
        command.Parameters.Add("departmentIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value =
            observableDepartmentIds is null
                ? DBNull.Value
                : observableDepartmentIds.ToArray();
        await using var reader = await command.ExecuteReaderAsync();

        var people = new List<WorkflowTargetPersonDto>();
        while (await reader.ReadAsync())
        {
            people.Add(MapWorkflowTargetPerson(reader));
        }

        return people;
    }

    public async Task<List<WorkflowTargetPersonSourceDto>> SearchWorkflowTargetPersonSources(
        string? search,
        int limit = 20,
        IReadOnlyCollection<int>? observableDepartmentIds = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    source_workflow.uid,
    matched_person.person_id,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(matched_person.first_name, source_workflow.first_name), COALESCE(matched_person.last_name, source_workflow.last_name))), ''),
        matched_person.display_name,
        'Person #' || matched_person.person_id::text
    ) AS display_name,
    COALESCE(matched_person.first_name, source_workflow.first_name) AS first_name,
    COALESCE(matched_person.last_name, source_workflow.last_name) AS last_name,
    COALESCE(source_workflow.department_id, matched_person.department_id, matched_person.user_department_id) AS department_id,
    d.name AS department_name,
    source_workflow.position_role_id,
    r.name AS role_name,
    COALESCE(matched_person.employee_number, source_workflow.employee_number) AS employee_number,
    COALESCE(matched_person.badge_number, source_workflow.badge_number) AS badge_number,
    source_workflow.completed_at,
    source_workflow.archived_at
FROM (
    SELECT
        w.id,
        w.uid,
        w.target_person_id,
        w.first_name,
        w.last_name,
        w.employee_number,
        w.badge_number,
        w.department_id,
        w.position_role_id,
        COALESCE(w.completed_at, w.created_at) AS completed_at,
        w.archived_at
    FROM workflows w
    JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
    LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
    LEFT JOIN workflow_definitions vpt ON vpt.id = v.workflow_definition_id
    WHERE pt.definition_key = 'onboarding'
      AND (w.workflow_definition_version_id IS NULL OR vpt.definition_key = 'onboarding')
      AND w.status = 'completed'
) source_workflow
JOIN LATERAL (
    SELECT
        p.id AS person_id,
        p.first_name,
        p.last_name,
        p.employee_number,
        p.badge_number,
        p.department_id,
        u.display_name,
        u.department_id AS user_department_id
    FROM people p
    LEFT JOIN app_users u ON u.id = p.app_user_id
    WHERE source_workflow.target_person_id = p.id
       OR (p.employee_number IS NOT NULL AND source_workflow.employee_number = p.employee_number)
       OR (
            source_workflow.employee_number IS NOT NULL
            AND source_workflow.employee_number > 0
            AND TRIM(COALESCE(source_workflow.first_name, '') || ' ' || COALESCE(source_workflow.last_name, '')) =
                COALESCE(
                    NULLIF(BTRIM(CONCAT_WS(' ', p.first_name, p.last_name)), ''),
                    u.display_name,
                    'Person #' || p.id::text
                )
       )
    ORDER BY
        CASE
            WHEN source_workflow.target_person_id = p.id THEN 0
            WHEN p.employee_number IS NOT NULL AND source_workflow.employee_number = p.employee_number THEN 1
            ELSE 2
        END,
        p.id
    LIMIT 1
) matched_person ON TRUE
LEFT JOIN departments d ON d.id = COALESCE(source_workflow.department_id, matched_person.department_id, matched_person.user_department_id)
LEFT JOIN app_roles r ON r.id = source_workflow.position_role_id
WHERE (
        @departmentIds IS NULL
        OR COALESCE(source_workflow.department_id, matched_person.department_id, matched_person.user_department_id) = ANY(@departmentIds)
  )
  AND (
        @search = ''
        OR COALESCE(
            NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(matched_person.first_name, source_workflow.first_name), COALESCE(matched_person.last_name, source_workflow.last_name))), ''),
            matched_person.display_name,
            'Person #' || matched_person.person_id::text
        ) ILIKE @pattern
        OR TRIM(COALESCE(source_workflow.first_name, '') || ' ' || COALESCE(source_workflow.last_name, '')) ILIKE @pattern
        OR CAST(COALESCE(matched_person.employee_number, source_workflow.employee_number, 0) AS TEXT) ILIKE @pattern
        OR COALESCE(d.name, '') ILIKE @pattern
        OR COALESCE(r.name, '') ILIKE @pattern
  )
ORDER BY source_workflow.completed_at DESC, display_name, matched_person.person_id
LIMIT @limit;";

        await using var command = new NpgsqlCommand(sql, connection);
        var normalizedSearch = search?.Trim() ?? string.Empty;
        command.Parameters.AddWithValue("search", normalizedSearch);
        command.Parameters.AddWithValue("pattern", $"%{normalizedSearch}%");
        command.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, 50));
        command.Parameters.Add("departmentIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value =
            observableDepartmentIds is null
                ? DBNull.Value
                : observableDepartmentIds.ToArray();

        await using var reader = await command.ExecuteReaderAsync();
        var results = new List<WorkflowTargetPersonSourceDto>();
        while (await reader.ReadAsync())
        {
            results.Add(PostgresRepositorySharedHelpers.MapWorkflowTargetPersonSource(reader));
        }

        return results;
    }

    // Anforderungen und Rollenempfehlungen bilden die Eingabemaske fuer neue Workflows.
    public async Task<List<RequirementDto>> GetRequirements(string legacyProcessTypeKey)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var processTypeId = await ResolveProcessTypeId(connection, null, legacyProcessTypeKey);
        return await LoadRequirements(connection, null, processTypeId);
    }

    public async Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId, string legacyProcessTypeKey)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var processTypeId = await ResolveProcessTypeId(connection, null, legacyProcessTypeKey);
        if (roleId.HasValue && !await RoleExists(connection, null, roleId.Value))
        {
            return null;
        }

        var requirements = await LoadRequirements(connection, null, processTypeId);
        var roleRecommendations = roleId.HasValue
            ? await LoadRoleRecommendations(connection, null, roleId.Value, processTypeId)
            : await LoadAllRoleRecommendations(connection, null, processTypeId);

        return new WorkflowConfigDto
        {
            Requirements = requirements,
            RoleRecommendations = roleRecommendations
        };
    }

    private static WorkflowTargetPersonDto MapWorkflowTargetPerson(NpgsqlDataReader reader)
    {
        return new WorkflowTargetPersonDto
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
            LastName = reader.IsDBNull(9) ? null : reader.GetString(9),
            EmploymentStatus = reader.IsDBNull(10) ? null : reader.GetString(10),
            AppUserId = reader.IsDBNull(11) ? null : reader.GetInt64(11),
            DirectoryIdentityId = reader.IsDBNull(12) ? null : reader.GetInt64(12),
            DirectoryLinkStatus = reader.IsDBNull(13) ? null : reader.GetString(13),
            DirectoryDisplayName = reader.IsDBNull(14) ? null : reader.GetString(14),
            DirectoryUserPrincipalName = reader.IsDBNull(15) ? null : reader.GetString(15),
            DirectoryMail = reader.IsDBNull(16) ? null : reader.GetString(16),
            DirectoryEmployeeNumber = reader.IsDBNull(17) ? null : reader.GetInt32(17),
            LatestCompletedOnboardingWorkflowUid = reader.IsDBNull(18) ? null : reader.GetGuid(18),
            LatestCompletedOnboardingAt = reader.IsDBNull(19) ? null : reader.GetDateTime(19)
        };
    }

    private static async Task<RoleRecommendationsDto> LoadAllRoleRecommendations(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int processTypeId)
    {
        const string sql = @"
SELECT id
FROM app_roles
WHERE role_kind = 'position'
  AND is_active = TRUE
ORDER BY id;";

        var roleIds = new List<int>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                roleIds.Add(reader.GetInt32(0));
            }
        }

        var recommendedRequirementIds = new SortedSet<int>();
        var defaultValues = new List<RoleRecommendationDefaultValueDto>();
        var defaultValueKeys = new HashSet<(int RequirementId, bool? ValueBoolean, string? ValueText, decimal? ValueNumber)>();
        var defaultSelectedOptions = new List<RoleRecommendationSelectedOptionsDto>();
        var defaultSelectedOptionKeys = new HashSet<(int RequirementId, int? SelectedOptionId, string SelectedOptionIdsKey)>();

        foreach (var currentRoleId in roleIds)
        {
            var roleRecommendations = await LoadRoleRecommendations(connection, transaction, currentRoleId, processTypeId);

            foreach (var requirementId in roleRecommendations.RecommendedRequirementIds)
            {
                recommendedRequirementIds.Add(requirementId);
            }

            foreach (var defaultValue in roleRecommendations.DefaultValues)
            {
                var key = (
                    defaultValue.RequirementId,
                    defaultValue.ValueBoolean,
                    defaultValue.ValueText,
                    defaultValue.ValueNumber);

                if (!defaultValueKeys.Add(key))
                {
                    continue;
                }

                defaultValues.Add(defaultValue);
            }

            foreach (var selectedOptions in roleRecommendations.DefaultSelectedOptions)
            {
                var key = (
                    selectedOptions.RequirementId,
                    selectedOptions.SelectedOptionId,
                    string.Join(",", selectedOptions.SelectedOptionIds.OrderBy(id => id)));

                if (!defaultSelectedOptionKeys.Add(key))
                {
                    continue;
                }

                defaultSelectedOptions.Add(selectedOptions);
            }
        }

        return new RoleRecommendationsDto
        {
            RecommendedRequirementIds = recommendedRequirementIds.ToList(),
            DefaultValues = defaultValues
                .OrderBy(item => item.RequirementId)
                .ToList(),
            DefaultSelectedOptions = defaultSelectedOptions
                .OrderBy(item => item.RequirementId)
                .ThenBy(item => item.SelectedOptionId)
                .ToList()
        };
    }

    // Naming-Hinweis: Methode heißt aus Legacy-Gründen weiter ResolveProcessTypeId,
    // liefert seit Slice 6.3d-ii aber die `workflow_definitions.id` zurück. Die
    // Stammdaten-Tabellen (workflow_answer_definitions, task_templates,
    // app_role_answer_defaults, workflow_answer_derivation_rules) verweisen seit
    // 6.3d-ii via workflow_definition_id-FK auf workflow_definitions.
    private static async Task<int> ResolveProcessTypeId(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string? processTypeKey)
    {
        const string sql = @"
SELECT id
FROM workflow_definitions
WHERE definition_key = @processTypeKey
LIMIT 1;";

        if (string.IsNullOrWhiteSpace(processTypeKey))
        {
            throw new InvalidOperationException("processTypeKey is required.");
        }

        var normalizedProcessTypeKey = processTypeKey.Trim().ToLowerInvariant();

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("processTypeKey", normalizedProcessTypeKey);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is int processTypeId)
        {
            return processTypeId;
        }

        throw new InvalidOperationException($"Unbekannter oder inaktiver Prozesstyp '{normalizedProcessTypeKey}'.");
    }

}
