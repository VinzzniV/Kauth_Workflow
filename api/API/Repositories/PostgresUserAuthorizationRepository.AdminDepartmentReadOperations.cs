using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresUserAuthorizationRepository
{
    // P1-Hull (Z11-F1): Listenausgabe mit Limit/Offset/Search/Sort + Total.
    // Interner Detail-Loader (per departmentId) bleibt erhalten und wird von CRUD-Pfaden genutzt.
    private static async Task<AdminListPageDto<AdminDepartmentAssignmentDto>> LoadAdminDepartmentAssignmentsPage(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        AdminListQuery query,
        CancellationToken cancellationToken)
    {
        var orderBy = query.Sort?.Trim().ToLowerInvariant() switch
        {
            "name_desc" => "d.name DESC, d.id DESC",
            "id" => "d.id ASC",
            "id_desc" => "d.id DESC",
            _ => "d.name ASC, d.id ASC"
        };

        var sql = $@"
WITH managed_departments AS (
    SELECT DISTINCT u.department_id
    FROM app_users u
    JOIN directory_identities di ON di.app_user_id = u.id
    WHERE u.directory_synced = TRUE
      AND u.is_active = TRUE
      AND di.account_enabled = TRUE
      AND u.department_override_active = FALSE
      AND u.department_id IS NOT NULL
),
entra_manager_candidates AS (
    SELECT DISTINCT
        u.department_id,
        u.id AS app_user_id,
        p.id AS person_id,
        u.display_name
    FROM app_users u
    JOIN directory_identities di ON di.app_user_id = u.id
    JOIN directory_group_members dgm ON dgm.directory_identity_id = di.id
    JOIN directory_group_role_mappings dgrm ON dgrm.directory_group_id = dgm.directory_group_id
    JOIN app_roles ar ON ar.id = dgrm.app_role_id
    LEFT JOIN people p ON p.app_user_id = u.id
    WHERE u.directory_synced = TRUE
      AND u.is_active = TRUE
      AND di.account_enabled = TRUE
      AND u.department_override_active = FALSE
      AND u.department_id IS NOT NULL
      AND dgrm.is_active = TRUE
      AND ar.role_key = 'auth_manager'
      AND ar.role_kind = 'system'
),
candidate_summary AS (
    SELECT
        department_id,
        COUNT(*) AS candidate_count,
        MIN(app_user_id) AS resolved_user_id,
        MIN(person_id) AS resolved_person_id,
        MIN(display_name) AS resolved_display_name,
        STRING_AGG(display_name, ', ' ORDER BY display_name) AS candidate_names
    FROM entra_manager_candidates
    GROUP BY department_id
)
SELECT
    d.id,
    d.name,
    lead_user.id,
    lead_user.display_name,
    requirement_user.id,
    requirement_user.display_name,
    CASE
        WHEN managed.department_id IS NULL THEN 'manual'
        ELSE 'entra_managed'
    END AS assignment_source,
    CASE
        WHEN managed.department_id IS NULL THEN 'manual'
        WHEN COALESCE(candidate.candidate_count, 0) = 1 THEN 'resolved'
        WHEN COALESCE(candidate.candidate_count, 0) = 0 THEN 'missing'
        ELSE 'conflict'
    END AS sync_state,
    CASE
        WHEN managed.department_id IS NULL THEN 'Keine Entra-geführte Abteilungsleitung erkannt.'
        WHEN COALESCE(candidate.candidate_count, 0) = 1
         AND candidate.resolved_person_id IS NOT NULL
         AND candidate.resolved_person_id = ds.department_lead_person_id
         AND candidate.resolved_person_id = ds.requirement_approver_person_id
            THEN 'Entra hat genau eine aktive Abteilungsleitung für diese Abteilung aufgelöst.'
        WHEN COALESCE(candidate.candidate_count, 0) = 1
            THEN 'Entra führt diese Abteilung. Beim nächsten Sync werden Leitung und Anforderungsverantwortung auf '
                || COALESCE(candidate.resolved_display_name, 'die gefundene Person')
                || ' gesetzt.'
        WHEN COALESCE(candidate.candidate_count, 0) = 0
            THEN 'Keine aktive Entra-Abteilungsleitung für diese Abteilung gefunden.'
        ELSE 'Mehrere aktive Entra-Abteilungsleitungen gefunden: '
            || COALESCE(candidate.candidate_names, 'unbekannt')
            || '.'
    END AS sync_detail,
    ds.updated_at,
    COUNT(*) OVER() AS total_count
FROM departments d
LEFT JOIN department_settings ds ON ds.department_id = d.id
LEFT JOIN people lead_person ON lead_person.id = ds.department_lead_person_id
LEFT JOIN app_users lead_user ON lead_user.id = lead_person.app_user_id
LEFT JOIN people requirement_person ON requirement_person.id = ds.requirement_approver_person_id
LEFT JOIN app_users requirement_user ON requirement_user.id = requirement_person.app_user_id
LEFT JOIN managed_departments managed ON managed.department_id = d.id
LEFT JOIN candidate_summary candidate ON candidate.department_id = d.id
WHERE (@search = '' OR d.name ILIKE @pattern)
ORDER BY {orderBy}
LIMIT @limit OFFSET @offset;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("search", query.NormalizedSearch);
        command.Parameters.AddWithValue("pattern", query.SearchPattern);
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.AddWithValue("offset", query.Offset);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var assignments = new List<AdminDepartmentAssignmentDto>();
        var total = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            assignments.Add(new AdminDepartmentAssignmentDto
            {
                DepartmentId = reader.GetInt32(0),
                DepartmentName = reader.GetString(1),
                DepartmentLeadUserId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                DepartmentLeadDisplayName = reader.IsDBNull(3) ? null : reader.GetString(3),
                RequirementOwnerUserId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                RequirementOwnerDisplayName = reader.IsDBNull(5) ? null : reader.GetString(5),
                AssignmentSource = reader.GetString(6),
                SyncState = reader.GetString(7),
                SyncDetail = reader.IsDBNull(8) ? null : reader.GetString(8),
                UpdatedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
            });
            total = reader.GetInt32(10);
        }

        return new AdminListPageDto<AdminDepartmentAssignmentDto>
        {
            Items = assignments,
            Total = total,
            Limit = query.Limit,
            Offset = query.Offset
        };
    }

    private static async Task<List<AdminDepartmentAssignmentDto>> LoadAdminDepartmentAssignments(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int? departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
WITH managed_departments AS (
    SELECT DISTINCT u.department_id
    FROM app_users u
    JOIN directory_identities di ON di.app_user_id = u.id
    WHERE u.directory_synced = TRUE
      AND u.is_active = TRUE
      AND di.account_enabled = TRUE
      AND u.department_override_active = FALSE
      AND u.department_id IS NOT NULL
),
entra_manager_candidates AS (
    SELECT DISTINCT
        u.department_id,
        u.id AS app_user_id,
        p.id AS person_id,
        u.display_name
    FROM app_users u
    JOIN directory_identities di ON di.app_user_id = u.id
    JOIN directory_group_members dgm ON dgm.directory_identity_id = di.id
    JOIN directory_group_role_mappings dgrm ON dgrm.directory_group_id = dgm.directory_group_id
    JOIN app_roles ar ON ar.id = dgrm.app_role_id
    LEFT JOIN people p ON p.app_user_id = u.id
    WHERE u.directory_synced = TRUE
      AND u.is_active = TRUE
      AND di.account_enabled = TRUE
      AND u.department_override_active = FALSE
      AND u.department_id IS NOT NULL
      AND dgrm.is_active = TRUE
      AND ar.role_key = 'auth_manager'
      AND ar.role_kind = 'system'
),
candidate_summary AS (
    SELECT
        department_id,
        COUNT(*) AS candidate_count,
        MIN(app_user_id) AS resolved_user_id,
        MIN(person_id) AS resolved_person_id,
        MIN(display_name) AS resolved_display_name,
        STRING_AGG(display_name, ', ' ORDER BY display_name) AS candidate_names
    FROM entra_manager_candidates
    GROUP BY department_id
)
SELECT
    d.id,
    d.name,
    lead_user.id,
    lead_user.display_name,
    requirement_user.id,
    requirement_user.display_name,
    CASE
        WHEN managed.department_id IS NULL THEN 'manual'
        ELSE 'entra_managed'
    END AS assignment_source,
    CASE
        WHEN managed.department_id IS NULL THEN 'manual'
        WHEN COALESCE(candidate.candidate_count, 0) = 1 THEN 'resolved'
        WHEN COALESCE(candidate.candidate_count, 0) = 0 THEN 'missing'
        ELSE 'conflict'
    END AS sync_state,
    CASE
        WHEN managed.department_id IS NULL THEN 'Keine Entra-geführte Abteilungsleitung erkannt.'
        WHEN COALESCE(candidate.candidate_count, 0) = 1
         AND candidate.resolved_person_id IS NOT NULL
         AND candidate.resolved_person_id = ds.department_lead_person_id
         AND candidate.resolved_person_id = ds.requirement_approver_person_id
            THEN 'Entra hat genau eine aktive Abteilungsleitung für diese Abteilung aufgelöst.'
        WHEN COALESCE(candidate.candidate_count, 0) = 1
            THEN 'Entra führt diese Abteilung. Beim nächsten Sync werden Leitung und Anforderungsverantwortung auf '
                || COALESCE(candidate.resolved_display_name, 'die gefundene Person')
                || ' gesetzt.'
        WHEN COALESCE(candidate.candidate_count, 0) = 0
            THEN 'Keine aktive Entra-Abteilungsleitung für diese Abteilung gefunden.'
        ELSE 'Mehrere aktive Entra-Abteilungsleitungen gefunden: '
            || COALESCE(candidate.candidate_names, 'unbekannt')
            || '.'
    END AS sync_detail,
    ds.updated_at
FROM departments d
LEFT JOIN department_settings ds ON ds.department_id = d.id
LEFT JOIN people lead_person ON lead_person.id = ds.department_lead_person_id
LEFT JOIN app_users lead_user ON lead_user.id = lead_person.app_user_id
LEFT JOIN people requirement_person ON requirement_person.id = ds.requirement_approver_person_id
LEFT JOIN app_users requirement_user ON requirement_user.id = requirement_person.app_user_id
LEFT JOIN managed_departments managed ON managed.department_id = d.id
LEFT JOIN candidate_summary candidate ON candidate.department_id = d.id
WHERE (@departmentId IS NULL OR d.id = @departmentId)
ORDER BY d.name, d.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var departmentIdParameter = command.Parameters.Add("departmentId", NpgsqlDbType.Integer);
        departmentIdParameter.Value = (object?)departmentId ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var assignments = new List<AdminDepartmentAssignmentDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            assignments.Add(new AdminDepartmentAssignmentDto
            {
                DepartmentId = reader.GetInt32(0),
                DepartmentName = reader.GetString(1),
                DepartmentLeadUserId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                DepartmentLeadDisplayName = reader.IsDBNull(3) ? null : reader.GetString(3),
                RequirementOwnerUserId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                RequirementOwnerDisplayName = reader.IsDBNull(5) ? null : reader.GetString(5),
                AssignmentSource = reader.GetString(6),
                SyncState = reader.GetString(7),
                SyncDetail = reader.IsDBNull(8) ? null : reader.GetString(8),
                UpdatedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
            });
        }

        return assignments;
    }
}
