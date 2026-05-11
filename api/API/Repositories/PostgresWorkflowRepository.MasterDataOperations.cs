using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    // Stammdaten fuer die HR-Erfassung. P1-Hull (Z11-F1): einheitliche Listen mit Limit/Offset/Search/Sort + Total.
    public async Task<AdminListPageDto<DepartmentDto>> GetDepartments(AdminListQuery query)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        // Sort-Whitelist: name (default), id (Tie-Breaker via ORDER BY angehaengt).
        var orderBy = query.Sort?.Trim().ToLowerInvariant() switch
        {
            "name_desc" => "name DESC, id DESC",
            "id" => "id ASC",
            "id_desc" => "id DESC",
            _ => "name ASC, id ASC"
        };

        var sql = $@"
SELECT id, name, COUNT(*) OVER() AS total_count
FROM departments
WHERE (@search = '' OR name ILIKE @pattern)
ORDER BY {orderBy}
LIMIT @limit OFFSET @offset;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("search", query.NormalizedSearch);
        command.Parameters.AddWithValue("pattern", query.SearchPattern);
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.AddWithValue("offset", query.Offset);

        await using var reader = await command.ExecuteReaderAsync();

        var departments = new List<DepartmentDto>();
        var total = 0;
        while (await reader.ReadAsync())
        {
            departments.Add(new DepartmentDto
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
            total = reader.GetInt32(2);
        }

        return new AdminListPageDto<DepartmentDto>
        {
            Items = departments,
            Total = total,
            Limit = query.Limit,
            Offset = query.Offset
        };
    }

    public async Task<AdminListPageDto<RoleDto>> GetRoles(AdminListQuery query)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var orderBy = query.Sort?.Trim().ToLowerInvariant() switch
        {
            "name" => "r.name ASC, r.id ASC",
            "name_desc" => "r.name DESC, r.id DESC",
            "department" => "d.name ASC, r.name ASC, r.id ASC",
            "department_desc" => "d.name DESC, r.name DESC, r.id DESC",
            "id" => "r.id ASC",
            "id_desc" => "r.id DESC",
            _ => "d.name ASC, r.name ASC, r.id ASC"
        };

        var sql = $@"
SELECT r.id, r.department_id, d.name, r.name, r.is_active, COUNT(*) OVER() AS total_count
FROM app_roles r
JOIN departments d ON d.id = r.department_id
WHERE r.role_kind = 'position'
  AND r.is_active = TRUE
  AND (@search = '' OR r.name ILIKE @pattern OR d.name ILIKE @pattern)
ORDER BY {orderBy}
LIMIT @limit OFFSET @offset;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("search", query.NormalizedSearch);
        command.Parameters.AddWithValue("pattern", query.SearchPattern);
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.AddWithValue("offset", query.Offset);

        await using var reader = await command.ExecuteReaderAsync();

        var roles = new List<RoleDto>();
        var total = 0;
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
            total = reader.GetInt32(5);
        }

        return new AdminListPageDto<RoleDto>
        {
            Items = roles,
            Total = total,
            Limit = query.Limit,
            Offset = query.Offset
        };
    }

    public async Task<AdminListPageDto<PersonDirectoryItemDto>> GetPeopleDirectory(AdminListQuery query, IReadOnlyCollection<int>? observableDepartmentIds = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var orderBy = query.Sort?.Trim().ToLowerInvariant() switch
        {
            "name_desc" => "display_name DESC",
            "department" => "department_name ASC NULLS LAST, display_name ASC",
            "department_desc" => "department_name DESC NULLS FIRST, display_name DESC",
            "status" => "employment_status ASC, display_name ASC",
            "status_desc" => "employment_status DESC, display_name DESC",
            _ => "display_name ASC"
        };

        // C: UNION ALL ergaenzt directory_identities ohne people-Record (account_enabled = true).
        // directory_only-Eintraege haben PersonId = NULL und DirectoryIdentityId gesetzt.
        // observableDepartmentIds = null bedeutet keine Einschraenkung (HR/Admin).
        var sql = $@"
SELECT
    person_id,
    directory_identity_id,
    display_name,
    department_id,
    department_name,
    role_id,
    role_name,
    employee_number,
    badge_number,
    employment_status,
    entry_date,
    exit_date,
    directory_link_status,
    job_title,
    COUNT(*) OVER() AS total_count
FROM (
    SELECT
        p.id::bigint AS person_id,
        NULL::bigint AS directory_identity_id,
        COALESCE(
            NULLIF(BTRIM(CONCAT_WS(' ', p.first_name, p.last_name)), ''),
            u.display_name,
            di.display_name,
            'Person #' || p.id::text
        ) AS display_name,
        p.department_id,
        d.name AS department_name,
        p.current_position_role_id AS role_id,
        r.name AS role_name,
        p.employee_number,
        p.badge_number,
        COALESCE(
            NULLIF(BTRIM(p.employment_status), ''),
            CASE
                WHEN p.exit_date IS NOT NULL THEN 'exited'
                WHEN p.app_user_id IS NOT NULL THEN 'active'
                ELSE 'planned'
            END
        ) AS employment_status,
        p.entry_date,
        p.exit_date,
        CASE
            WHEN p.directory_identity_id IS NOT NULL THEN 'linked'
            WHEN p.app_user_id IS NOT NULL THEN 'user_only'
            ELSE 'unlinked'
        END AS directory_link_status,
        di.job_title
    FROM people p
    LEFT JOIN app_users u ON u.id = p.app_user_id
    LEFT JOIN directory_identities di ON di.id = p.directory_identity_id
    LEFT JOIN departments d ON d.id = p.department_id
    LEFT JOIN app_roles r ON r.id = p.current_position_role_id
    WHERE (
        @search = ''
        OR COALESCE(NULLIF(BTRIM(CONCAT_WS(' ', p.first_name, p.last_name)), ''), u.display_name, di.display_name, 'Person #' || p.id::text) ILIKE @pattern
        OR COALESCE(d.name, '') ILIKE @pattern
        OR COALESCE(r.name, '') ILIKE @pattern
        OR CAST(COALESCE(p.employee_number, 0) AS TEXT) ILIKE @pattern
    )
    AND (
        @observableDepartmentIds IS NULL
        OR p.department_id = ANY(@observableDepartmentIds)
    )

    UNION ALL

    SELECT
        NULL::bigint AS person_id,
        dir.id::bigint AS directory_identity_id,
        dir.display_name,
        dep.id AS department_id,
        dep.name AS department_name,
        NULL::integer AS role_id,
        NULL::text AS role_name,
        NULL::integer AS employee_number,
        NULL::integer AS badge_number,
        'directory_only' AS employment_status,
        NULL::date AS entry_date,
        NULL::date AS exit_date,
        'directory_only' AS directory_link_status,
        dir.job_title
    FROM directory_identities dir
    LEFT JOIN departments dep ON dep.name ILIKE dir.department_name
    WHERE dir.account_enabled = true
        AND NOT EXISTS (SELECT 1 FROM people p2 WHERE p2.directory_identity_id = dir.id)
        AND (
            @search = ''
            OR dir.display_name ILIKE @pattern
            OR COALESCE(dep.name, '') ILIKE @pattern
            OR COALESCE(dir.department_name, '') ILIKE @pattern
        )
        AND (
            @observableDepartmentIds IS NULL
            OR dep.id = ANY(@observableDepartmentIds)
        )
) combined
ORDER BY {orderBy}
LIMIT @limit OFFSET @offset;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("search", query.NormalizedSearch);
        command.Parameters.AddWithValue("pattern", query.SearchPattern);
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.AddWithValue("offset", query.Offset);
        command.Parameters.Add("observableDepartmentIds", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer).Value =
            observableDepartmentIds is null ? DBNull.Value : observableDepartmentIds.ToArray();

        await using var reader = await command.ExecuteReaderAsync();

        var people = new List<PersonDirectoryItemDto>();
        var total = 0;
        while (await reader.ReadAsync())
        {
            people.Add(new PersonDirectoryItemDto
            {
                PersonId = reader.IsDBNull(0) ? null : reader.GetInt64(0),
                DirectoryIdentityId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
                DisplayName = reader.GetString(2),
                DepartmentId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                DepartmentName = reader.IsDBNull(4) ? null : reader.GetString(4),
                RoleId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                RoleName = reader.IsDBNull(6) ? null : reader.GetString(6),
                EmployeeNumber = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                BadgeNumber = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                EmploymentStatus = reader.IsDBNull(9) ? null : reader.GetString(9),
                EntryDate = reader.IsDBNull(10) ? null : DateOnly.FromDateTime(reader.GetDateTime(10)),
                ExitDate = reader.IsDBNull(11) ? null : DateOnly.FromDateTime(reader.GetDateTime(11)),
                DirectoryLinkStatus = reader.IsDBNull(12) ? null : reader.GetString(12),
                JobTitle = reader.IsDBNull(13) ? null : reader.GetString(13)
            });
            total = reader.GetInt32(14);
        }

        return new AdminListPageDto<PersonDirectoryItemDto>
        {
            Items = people,
            Total = total,
            Limit = query.Limit,
            Offset = query.Offset
        };
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

    public async Task<IReadOnlySet<string>> GetManagerCreatableDefinitionKeys()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT definition_key
FROM workflow_definitions
WHERE allows_manager_creation = TRUE;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            keys.Add(reader.GetString(0));
        }

        return keys;
    }

    public async Task<List<WorkflowTargetPersonDto>> SearchWorkflowTargetPeople(
        string? query,
        int limit = 20,
        IReadOnlyCollection<int>? observableDepartmentIds = null)
    {
        return await SearchPeopleInternal(query, limit, observableDepartmentIds, requireSourceWorkflow: false);
    }

    public async Task<List<WorkflowTargetPersonDto>> SearchRotationEligiblePeople(
        string? query,
        int limit = 20,
        IReadOnlyCollection<int>? observableDepartmentIds = null)
    {
        return await SearchPeopleInternal(query, limit, observableDepartmentIds, requireSourceWorkflow: true);
    }

    private async Task<List<WorkflowTargetPersonDto>> SearchPeopleInternal(
        string? query,
        int limit,
        IReadOnlyCollection<int>? observableDepartmentIds,
        bool requireSourceWorkflow)
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
latest_source_workflow AS (
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
    latest_source_workflow.workflow_uid,
    latest_source_workflow.completed_at
FROM people p
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN directory_identities linked_directory ON linked_directory.id = p.directory_identity_id
LEFT JOIN latest_workflow ON latest_workflow.person_id = p.id
LEFT JOIN latest_source_workflow ON latest_source_workflow.person_id = p.id
LEFT JOIN departments d ON d.id = COALESCE(p.department_id, latest_workflow.department_id, u.department_id)
LEFT JOIN app_roles r ON r.id = COALESCE(p.current_position_role_id, latest_workflow.position_role_id)
WHERE (
      @departmentIds IS NULL
      OR COALESCE(p.department_id, latest_workflow.department_id, u.department_id) = ANY(@departmentIds)
  )
  {(requireSourceWorkflow ? "AND latest_source_workflow.workflow_uid IS NOT NULL" : string.Empty)}
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
    latest_source_workflow.completed_at DESC NULLS LAST,
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

    // A1: Entra-Identitaeten die noch keinen people-Record haben.
    // "Unlinked" bedeutet: weder via directory_identity_id noch via employee_number mit people verknuepft.
    public async Task<AdminListPageDto<UnlinkedDirectoryIdentityDto>> GetUnlinkedDirectoryIdentities(
        string? departmentFilter,
        bool? onlyEnabled,
        int limit,
        int offset)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var normalizedDept = departmentFilter?.Trim() ?? string.Empty;

        const string sql = @"
SELECT
    di.id              AS directory_identity_id,
    di.entra_object_id,
    di.display_name,
    di.mail,
    di.user_principal_name,
    di.department_name,
    dept.id            AS preview_department_id,
    di.employee_number,
    di.job_title,
    di.account_enabled,
    di.app_user_id,
    (di.app_user_id IS NOT NULL) AS has_linked_app_user,
    COUNT(*) OVER ()   AS total_count
FROM directory_identities di
LEFT JOIN departments dept
    ON LOWER(dept.name) = LOWER(BTRIM(COALESCE(di.department_name, '')))
WHERE NOT EXISTS (
    SELECT 1
    FROM people p
    WHERE p.directory_identity_id = di.id
       OR (di.employee_number IS NOT NULL AND p.employee_number = di.employee_number)
)
  AND (@onlyEnabled IS NULL OR di.account_enabled = @onlyEnabled)
  AND (@deptFilter = '' OR LOWER(COALESCE(di.department_name, '')) = LOWER(@deptFilter))
ORDER BY di.department_name ASC NULLS LAST, di.display_name ASC, di.id ASC
LIMIT @limit OFFSET @offset;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add("onlyEnabled", NpgsqlDbType.Boolean).Value =
            onlyEnabled.HasValue ? (object)onlyEnabled.Value : DBNull.Value;
        command.Parameters.AddWithValue("deptFilter", normalizedDept);
        command.Parameters.AddWithValue("limit", limit);
        command.Parameters.AddWithValue("offset", offset);

        await using var reader = await command.ExecuteReaderAsync();

        var items = new List<UnlinkedDirectoryIdentityDto>();
        var total = 0;
        while (await reader.ReadAsync())
        {
            items.Add(new UnlinkedDirectoryIdentityDto
            {
                DirectoryIdentityId = reader.GetInt64(0),
                EntraObjectId = reader.GetGuid(1),
                DisplayName = reader.GetString(2),
                Mail = reader.IsDBNull(3) ? null : reader.GetString(3),
                UserPrincipalName = reader.IsDBNull(4) ? null : reader.GetString(4),
                DepartmentName = reader.IsDBNull(5) ? null : reader.GetString(5),
                PreviewDepartmentId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                EmployeeNumber = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                JobTitle = reader.IsDBNull(8) ? null : reader.GetString(8),
                AccountEnabled = reader.GetBoolean(9),
                AppUserId = reader.IsDBNull(10) ? null : reader.GetInt64(10),
                HasLinkedAppUser = reader.GetBoolean(11)
            });
            total = reader.GetInt32(12);
        }

        return new AdminListPageDto<UnlinkedDirectoryIdentityDto>
        {
            Items = items,
            Total = total,
            Limit = limit,
            Offset = offset
        };
    }

    // A1: Erstellt people-Records fuer die uebergebenen directory_identity_ids.
    // Erstellt KEINEN app_user. Auto-Linkt zu bestehendem app_user wenn entra_object_id matcht.
    // Dedupliziert via directory_identity_id und employee_number.
    public async Task<ImportPeopleFromDirectoryResultDto> ImportPeopleFromDirectory(
        List<long> directoryIdentityIds,
        long? actorUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var results = new List<ImportPeopleResultItemDto>(directoryIdentityIds.Count);

        foreach (var directoryIdentityId in directoryIdentityIds)
        {
            try
            {
                var item = await ImportSinglePersonFromDirectory(connection, directoryIdentityId, actorUserId);
                results.Add(item);
            }
            catch (Exception ex)
            {
                results.Add(new ImportPeopleResultItemDto
                {
                    DirectoryIdentityId = directoryIdentityId,
                    DisplayName = "?",
                    Outcome = "skipped",
                    SkipReason = ex.Message
                });
            }
        }

        return new ImportPeopleFromDirectoryResultDto
        {
            CreatedCount = results.Count(r => r.Outcome == "created"),
            LinkedCount = results.Count(r => r.Outcome == "linked"),
            SkippedCount = results.Count(r => r.Outcome == "skipped"),
            Results = results
        };
    }

    private static async Task<ImportPeopleResultItemDto> ImportSinglePersonFromDirectory(
        NpgsqlConnection connection,
        long directoryIdentityId,
        long? actorUserId)
    {
        // Step 1: Load identity info + find app_user via entra_object_id (auto-link candidate)
        const string loadSql = @"
SELECT
    di.display_name,
    di.employee_number,
    dept.id AS department_id,
    u.id    AS matched_app_user_id
FROM directory_identities di
LEFT JOIN departments dept
    ON LOWER(dept.name) = LOWER(BTRIM(COALESCE(di.department_name, '')))
LEFT JOIN app_users u ON u.entra_object_id = di.entra_object_id
WHERE di.id = @directoryIdentityId;";

        string displayName;
        int? employeeNumber;
        int? departmentId;
        long? matchedAppUserId;

        await using (var loadCmd = new NpgsqlCommand(loadSql, connection))
        {
            loadCmd.Parameters.AddWithValue("directoryIdentityId", directoryIdentityId);
            await using var reader = await loadCmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return new ImportPeopleResultItemDto
                {
                    DirectoryIdentityId = directoryIdentityId,
                    DisplayName = "?",
                    Outcome = "skipped",
                    SkipReason = "Identity not found."
                };
            }
            displayName = reader.GetString(0);
            employeeNumber = reader.IsDBNull(1) ? null : reader.GetInt32(1);
            departmentId = reader.IsDBNull(2) ? null : reader.GetInt32(2);
            matchedAppUserId = reader.IsDBNull(3) ? null : reader.GetInt64(3);
        }

        // Step 2: Dedup — suche existierenden people-Record via directory_identity_id, employee_number oder app_user_id.
        // Prioritaet: directory_identity_id > employee_number > app_user_id
        const string checkSql = @"
SELECT p.id, p.directory_identity_id
FROM people p
WHERE p.directory_identity_id = @directoryIdentityId
   OR (@employeeNumber IS NOT NULL AND p.employee_number = @employeeNumber)
   OR (@matchedAppUserId IS NOT NULL AND p.app_user_id = @matchedAppUserId)
ORDER BY
    CASE
        WHEN p.directory_identity_id = @directoryIdentityId THEN 0
        WHEN @employeeNumber IS NOT NULL AND p.employee_number = @employeeNumber THEN 1
        ELSE 2
    END
LIMIT 1;";

        long? existingPersonId;
        long? existingDirId;

        await using (var checkCmd = new NpgsqlCommand(checkSql, connection))
        {
            checkCmd.Parameters.AddWithValue("directoryIdentityId", directoryIdentityId);
            checkCmd.Parameters.Add("employeeNumber", NpgsqlDbType.Integer).Value =
                employeeNumber.HasValue ? (object)employeeNumber.Value : DBNull.Value;
            checkCmd.Parameters.Add("matchedAppUserId", NpgsqlDbType.Bigint).Value =
                matchedAppUserId.HasValue ? (object)matchedAppUserId.Value : DBNull.Value;

            await using var reader = await checkCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                existingPersonId = reader.GetInt64(0);
                existingDirId = reader.IsDBNull(1) ? null : reader.GetInt64(1);
            }
            else
            {
                existingPersonId = null;
                existingDirId = null;
            }
        }

        if (existingPersonId is not null && existingDirId == directoryIdentityId)
        {
            // Bereits vollstaendig verknuepft — kein weiterer Handlungsbedarf.
            return new ImportPeopleResultItemDto
            {
                DirectoryIdentityId = directoryIdentityId,
                DisplayName = displayName,
                Outcome = "skipped",
                PersonId = existingPersonId,
                SkipReason = "Already linked to a people record."
            };
        }

        if (existingPersonId is not null)
        {
            // People-Record existiert, aber noch nicht mit dieser directory_identity verknuepft — linken.
            const string linkSql = @"
UPDATE people
SET
    directory_identity_id = @directoryIdentityId,
    app_user_id = COALESCE(app_user_id, @matchedAppUserId),
    updated_at = NOW()
WHERE id = @personId
  AND (directory_identity_id IS NULL OR directory_identity_id = @directoryIdentityId);";

            await using (var linkCmd = new NpgsqlCommand(linkSql, connection))
            {
                linkCmd.Parameters.AddWithValue("directoryIdentityId", directoryIdentityId);
                linkCmd.Parameters.Add("matchedAppUserId", NpgsqlDbType.Bigint).Value =
                    matchedAppUserId.HasValue ? (object)matchedAppUserId.Value : DBNull.Value;
                linkCmd.Parameters.AddWithValue("personId", existingPersonId);
                await linkCmd.ExecuteNonQueryAsync();
            }

            await InsertPersonImportAuditAsync(
                connection, existingPersonId.Value, matchedAppUserId, directoryIdentityId,
                employeeNumber, "directory_import_link");

            return new ImportPeopleResultItemDto
            {
                DirectoryIdentityId = directoryIdentityId,
                DisplayName = displayName,
                Outcome = "linked",
                PersonId = existingPersonId
            };
        }

        // Step 3: Neuen people-Record anlegen (kein app_user!).
        const string insertSql = @"
INSERT INTO people (directory_identity_id, app_user_id, department_id, updated_at)
VALUES (@directoryIdentityId, @matchedAppUserId, @departmentId, NOW())
RETURNING id;";

        long newPersonId;
        await using (var insertCmd = new NpgsqlCommand(insertSql, connection))
        {
            insertCmd.Parameters.AddWithValue("directoryIdentityId", directoryIdentityId);
            insertCmd.Parameters.Add("matchedAppUserId", NpgsqlDbType.Bigint).Value =
                matchedAppUserId.HasValue ? (object)matchedAppUserId.Value : DBNull.Value;
            insertCmd.Parameters.Add("departmentId", NpgsqlDbType.Integer).Value =
                departmentId.HasValue ? (object)departmentId.Value : DBNull.Value;
            var scalar = await insertCmd.ExecuteScalarAsync();
            newPersonId = (long)scalar!;
        }

        await InsertPersonImportAuditAsync(
            connection, newPersonId, matchedAppUserId, directoryIdentityId,
            employeeNumber, "directory_import_people");

        return new ImportPeopleResultItemDto
        {
            DirectoryIdentityId = directoryIdentityId,
            DisplayName = displayName,
            Outcome = "created",
            PersonId = newPersonId
        };
    }

    private static async Task InsertPersonImportAuditAsync(
        NpgsqlConnection connection,
        long personId,
        long? appUserId,
        long directoryIdentityId,
        int? employeeNumber,
        string matchStrategy)
    {
        const string sql = @"
INSERT INTO person_match_audit_log (
    matched_person_id, app_user_id, directory_identity_id, employee_number,
    match_strategy, match_score, fallback_used, source, detail)
VALUES (
    @personId, @appUserId, @directoryIdentityId, @employeeNumber,
    @matchStrategy, 1.00, false, 'directory_import_people', NULL);";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("personId", personId);
        cmd.Parameters.Add("appUserId", NpgsqlDbType.Bigint).Value =
            appUserId.HasValue ? (object)appUserId.Value : DBNull.Value;
        cmd.Parameters.AddWithValue("directoryIdentityId", directoryIdentityId);
        cmd.Parameters.Add("employeeNumber", NpgsqlDbType.Integer).Value =
            employeeNumber.HasValue ? (object)employeeNumber.Value : DBNull.Value;
        cmd.Parameters.AddWithValue("matchStrategy", matchStrategy);
        await cmd.ExecuteNonQueryAsync();
    }

    // Anforderungen und Rollenempfehlungen bilden die Eingabemaske fuer neue Workflows.
    public async Task<List<RequirementDto>> GetRequirements(string workflowDefinitionKey)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var workflowDefinitionId = await ResolveWorkflowDefinitionId(connection, null, workflowDefinitionKey);
        return await LoadRequirements(connection, null, workflowDefinitionId);
    }

    public async Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId, string workflowDefinitionKey)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var workflowDefinitionId = await ResolveWorkflowDefinitionId(connection, null, workflowDefinitionKey);
        if (roleId.HasValue && !await RoleExists(connection, null, roleId.Value))
        {
            return null;
        }

        var requirements = await LoadRequirements(connection, null, workflowDefinitionId);
        var roleRecommendations = roleId.HasValue
            ? await LoadRoleRecommendations(connection, null, roleId.Value, workflowDefinitionId)
            : await LoadAllRoleRecommendations(connection, null, workflowDefinitionId);

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
            LatestSourceWorkflowUid = reader.IsDBNull(18) ? null : reader.GetGuid(18),
            LatestSourceWorkflowCompletedAt = reader.IsDBNull(19) ? null : reader.GetDateTime(19)
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

    // Liefert die `workflow_definitions.id` fuer einen kanonischen Definition-Key. Die
    // Stammdaten-Tabellen (workflow_answer_definitions, task_templates,
    // app_role_answer_defaults, workflow_answer_derivation_rules) verweisen seit
    // 6.3d-ii via workflow_definition_id-FK auf workflow_definitions.
    private static async Task<int> ResolveWorkflowDefinitionId(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string? workflowDefinitionKey)
    {
        const string sql = @"
SELECT id
FROM workflow_definitions
WHERE definition_key = @workflowDefinitionKey
LIMIT 1;";

        if (string.IsNullOrWhiteSpace(workflowDefinitionKey))
        {
            throw new InvalidOperationException("workflowDefinitionKey is required.");
        }

        var normalizedWorkflowDefinitionKey = workflowDefinitionKey.Trim().ToLowerInvariant();

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowDefinitionKey", normalizedWorkflowDefinitionKey);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is int workflowDefinitionId)
        {
            return workflowDefinitionId;
        }

        throw new InvalidOperationException($"Unbekannte oder inaktive Workflow-Definition '{normalizedWorkflowDefinitionKey}'.");
    }

    // A3: Aktualisiert Eintrittsdatum und Ausweisnummer auf dem people-Record.
    public async Task<bool> UpdatePersonCoreFields(long personId, DateOnly? entryDate, int? badgeNumber)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
UPDATE people
SET entry_date = @entryDate, badge_number = @badgeNumber, updated_at = NOW()
WHERE id = @personId";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("personId", personId);
        command.Parameters.Add(new NpgsqlParameter<DateOnly?>("entryDate", NpgsqlDbType.Date) { TypedValue = entryDate });
        command.Parameters.Add(new NpgsqlParameter<int?>("badgeNumber", NpgsqlDbType.Integer) { TypedValue = badgeNumber });

        var affected = await command.ExecuteNonQueryAsync();
        return affected > 0;
    }

}
