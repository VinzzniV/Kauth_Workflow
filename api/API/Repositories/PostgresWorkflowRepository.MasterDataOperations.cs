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

    public async Task<List<WorkflowProcessTypeDto>> GetActiveProcessTypes(bool managerOnly = false)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT key, name, description, requires_target_person
FROM process_types
WHERE is_active = TRUE
ORDER BY sort_order, name;";

        const string managerOnlySql = @"
SELECT key, name, description, requires_target_person
FROM process_types
WHERE is_active = TRUE
  AND allows_manager_creation = TRUE
ORDER BY sort_order, name;";

        var useManagerCreationColumn = managerOnly && await HasProcessTypeManagerCreationColumn(connection, null);
        await using var command = new NpgsqlCommand(useManagerCreationColumn ? managerOnlySql : sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var processTypes = new List<WorkflowProcessTypeDto>();
        while (await reader.ReadAsync())
        {
            processTypes.Add(new WorkflowProcessTypeDto
            {
                Key = reader.GetString(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                RequiresTargetPerson = reader.GetBoolean(3)
            });
        }

        if (!managerOnly || useManagerCreationColumn)
        {
            return processTypes;
        }

        return processTypes
            .Where(processType => LegacyManagerCreatableProcessTypeKeys.Contains(processType.Key, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<bool> IsManagerCreatableProcessType(string processTypeKey)
    {
        if (string.IsNullOrWhiteSpace(processTypeKey))
        {
            return false;
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var normalizedProcessTypeKey = processTypeKey.Trim().ToLowerInvariant();
        if (!await HasProcessTypeManagerCreationColumn(connection, null))
        {
            if (!LegacyManagerCreatableProcessTypeKeys.Contains(normalizedProcessTypeKey, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            const string fallbackSql = @"
SELECT EXISTS(
    SELECT 1
    FROM process_types
    WHERE key = @processTypeKey
      AND is_active = TRUE
);";

            await using var fallbackCommand = new NpgsqlCommand(fallbackSql, connection);
            fallbackCommand.Parameters.AddWithValue("processTypeKey", normalizedProcessTypeKey);
            return (bool)(await fallbackCommand.ExecuteScalarAsync() ?? false);
        }

        const string sql = @"
SELECT allows_manager_creation
FROM process_types
WHERE key = @processTypeKey
  AND is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("processTypeKey", normalizedProcessTypeKey);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is bool allowsManagerCreation && allowsManagerCreation;
    }

    public async Task<List<WorkflowTargetPersonDto>> SearchWorkflowTargetPeople(
        string? query,
        int limit = 20,
        IReadOnlyCollection<int>? observableDepartmentIds = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    p.id,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, latest.first_name), COALESCE(p.last_name, latest.last_name))), ''),
        u.display_name,
        'Person #' || p.id::text
    ) AS display_name,
    COALESCE(latest.department_id, p.department_id, u.department_id) AS department_id,
    d.name AS department_name,
    latest.position_role_id,
    r.name AS role_name,
    COALESCE(latest.employee_number, p.employee_number) AS employee_number,
    COALESCE(latest.badge_number, p.badge_number) AS badge_number,
    COALESCE(p.first_name, latest.first_name) AS first_name,
    COALESCE(p.last_name, latest.last_name) AS last_name
FROM people p
LEFT JOIN app_users u ON u.id = p.app_user_id
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
LEFT JOIN departments d ON d.id = COALESCE(latest.department_id, p.department_id, u.department_id)
LEFT JOIN app_roles r ON r.id = latest.position_role_id
WHERE (
      @departmentIds IS NULL
      OR COALESCE(latest.department_id, p.department_id, u.department_id) = ANY(@departmentIds)
  )
  AND (
      @query = ''
      OR COALESCE(
            NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, latest.first_name), COALESCE(p.last_name, latest.last_name))), ''),
            u.display_name,
            'Person #' || p.id::text
         ) ILIKE @pattern
      OR COALESCE(d.name, '') ILIKE @pattern
      OR COALESCE(r.name, '') ILIKE @pattern
      OR CAST(COALESCE(latest.employee_number, p.employee_number, 0) AS TEXT) ILIKE @pattern
  )
ORDER BY display_name, p.id
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
            people.Add(new WorkflowTargetPersonDto
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
            });
        }

        return people;
    }

    public async Task<List<CompletedOnboardingSearchResultDto>> SearchCompletedOnboardings(
        string? search,
        int limit = 20,
        IReadOnlyCollection<int>? observableDepartmentIds = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    onboarding.uid,
    p.id,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, latest.first_name, onboarding.first_name), COALESCE(p.last_name, latest.last_name, onboarding.last_name))), ''),
        u.display_name,
        'Person #' || p.id::text
    ) AS display_name,
    COALESCE(p.first_name, latest.first_name, onboarding.first_name) AS first_name,
    COALESCE(p.last_name, latest.last_name, onboarding.last_name) AS last_name,
    COALESCE(latest.department_id, onboarding.department_id, p.department_id, u.department_id) AS department_id,
    d.name AS department_name,
    COALESCE(latest.position_role_id, onboarding.position_role_id) AS position_role_id,
    r.name AS role_name,
    COALESCE(latest.employee_number, p.employee_number, onboarding.employee_number) AS employee_number,
    COALESCE(latest.badge_number, p.badge_number, onboarding.badge_number) AS badge_number,
    onboarding.completed_at,
    onboarding.archived_at
FROM people p
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN LATERAL (
    SELECT
        w.department_id,
        w.position_role_id,
        w.employee_number,
        w.badge_number,
        w.first_name,
        w.last_name
    FROM workflows w
    WHERE w.target_person_id = p.id
       OR (
            p.employee_number IS NOT NULL
            AND w.employee_number = p.employee_number
       )
       OR (
            w.employee_number IS NOT NULL
            AND w.employee_number > 0
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
JOIN LATERAL (
    SELECT
        w.uid,
        w.first_name,
        w.last_name,
        w.employee_number,
        w.badge_number,
        w.department_id,
        w.position_role_id,
        COALESCE(w.completed_at, w.created_at) AS completed_at,
        w.archived_at
    FROM workflows w
    JOIN process_types pt ON pt.id = w.process_type_id
    WHERE pt.key = 'onboarding'
      AND w.status = 'completed'
      AND (
            w.target_person_id = p.id
            OR (
                COALESCE(latest.employee_number, p.employee_number) IS NOT NULL
                AND w.employee_number = COALESCE(latest.employee_number, p.employee_number)
            )
            OR (
                w.employee_number > 0
                AND TRIM(COALESCE(w.first_name, '') || ' ' || COALESCE(w.last_name, '')) =
                    COALESCE(
                        NULLIF(BTRIM(CONCAT_WS(' ', p.first_name, p.last_name)), ''),
                        u.display_name,
                        'Person #' || p.id::text
                    )
            )
      )
    ORDER BY COALESCE(w.completed_at, w.created_at) DESC, w.id DESC
    LIMIT 1
) onboarding ON TRUE
LEFT JOIN departments d ON d.id = COALESCE(latest.department_id, onboarding.department_id, p.department_id, u.department_id)
LEFT JOIN app_roles r ON r.id = COALESCE(latest.position_role_id, onboarding.position_role_id)
WHERE (
        @departmentIds IS NULL
        OR COALESCE(latest.department_id, onboarding.department_id, p.department_id, u.department_id) = ANY(@departmentIds)
  )
  AND (
        @search = ''
        OR COALESCE(
            NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, latest.first_name, onboarding.first_name), COALESCE(p.last_name, latest.last_name, onboarding.last_name))), ''),
            u.display_name,
            'Person #' || p.id::text
        ) ILIKE @pattern
        OR TRIM(onboarding.first_name || ' ' || onboarding.last_name) ILIKE @pattern
        OR CAST(COALESCE(latest.employee_number, p.employee_number, onboarding.employee_number) AS TEXT) ILIKE @pattern
        OR COALESCE(d.name, '') ILIKE @pattern
        OR COALESCE(r.name, '') ILIKE @pattern
  )
ORDER BY onboarding.completed_at DESC, display_name
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
        var results = new List<CompletedOnboardingSearchResultDto>();
        while (await reader.ReadAsync())
        {
            results.Add(new CompletedOnboardingSearchResultDto
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
            });
        }

        return results;
    }

    // Anforderungen und Rollenempfehlungen bilden die Eingabemaske fuer neue Workflows.
    public async Task<List<RequirementDto>> GetRequirements(string? processTypeKey = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var processTypeId = await ResolveProcessTypeId(connection, null, processTypeKey);
        return await LoadRequirements(connection, null, processTypeId);
    }

    public async Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId, string? processTypeKey = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var processTypeId = await ResolveProcessTypeId(connection, null, processTypeKey);
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

    private static async Task<int> ResolveProcessTypeId(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string? processTypeKey)
    {
        const string sql = @"
SELECT id
FROM process_types
WHERE key = @processTypeKey
  AND is_active = TRUE
LIMIT 1;";

        var normalizedProcessTypeKey = string.IsNullOrWhiteSpace(processTypeKey)
            ? "onboarding"
            : processTypeKey.Trim().ToLowerInvariant();

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
