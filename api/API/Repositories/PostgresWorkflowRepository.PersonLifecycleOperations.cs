using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    public async Task<WorkflowTargetPersonDto> CreatePerson(CreatePersonRequest request, long actorUserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var firstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            throw new InvalidOperationException("Vorname und Nachname sind erforderlich.");
        }

        if (!request.EmployeeNumber.HasValue || request.EmployeeNumber.Value <= 0)
        {
            throw new InvalidOperationException("Eine gültige Personalnummer ist erforderlich.");
        }

        if (!request.BadgeNumber.HasValue || request.BadgeNumber.Value <= 0)
        {
            throw new InvalidOperationException("Eine gültige Kartennummer ist erforderlich.");
        }

        if (!request.DepartmentId.HasValue || request.DepartmentId.Value <= 0)
        {
            throw new InvalidOperationException("Eine gültige Stamm-Abteilung ist erforderlich.");
        }

        if (!request.RoleId.HasValue || request.RoleId.Value <= 0)
        {
            throw new InvalidOperationException("Eine gültige Stelle ist erforderlich.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        if (!await DepartmentExistsInternal(connection, transaction, request.DepartmentId.Value))
        {
            throw new InvalidOperationException("Die angegebene Stamm-Abteilung wurde nicht gefunden.");
        }

        await EnsureValidPositionRole(connection, transaction, request.RoleId.Value, request.DepartmentId.Value);

        const string insertSql = """
INSERT INTO people (
    app_user_id,
    directory_identity_id,
    department_id,
    current_position_role_id,
    first_name,
    last_name,
    employee_number,
    badge_number,
    employment_status,
    entry_date,
    exit_date,
    created_at,
    updated_at
)
VALUES (
    NULL,
    NULL,
    @departmentId,
    @roleId,
    @firstName,
    @lastName,
    @employeeNumber,
    @badgeNumber,
    'planned',
    NULL,
    NULL,
    NOW(),
    NOW()
)
RETURNING id;
""";

        long personId;
        try
        {
            await using var command = new NpgsqlCommand(insertSql, connection, transaction);
            command.Parameters.AddWithValue("departmentId", request.DepartmentId.Value);
            command.Parameters.AddWithValue("roleId", request.RoleId.Value);
            command.Parameters.AddWithValue("firstName", firstName);
            command.Parameters.AddWithValue("lastName", lastName);
            command.Parameters.AddWithValue("employeeNumber", request.EmployeeNumber.Value);
            command.Parameters.AddWithValue("badgeNumber", request.BadgeNumber.Value);
            var scalar = await command.ExecuteScalarAsync();
            if (scalar is not long createdPersonId)
            {
                throw new InvalidOperationException("Der Mitarbeiter-Stammsatz konnte nicht angelegt werden.");
            }

            personId = createdPersonId;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation
                                           && string.Equals(ex.ConstraintName, "uq_people_employee_number", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Die Personalnummer ist bereits einem bestehenden Mitarbeiter zugeordnet.");
        }

        await transaction.CommitAsync();

        var createdPerson = await LoadPersonSummary(personId);
        return createdPerson ?? throw new InvalidOperationException("Der angelegte Mitarbeiter konnte nicht geladen werden.");
    }

    public async Task ApplyPersonLifecycleProjection(Guid workflowUid, long? actorUserId = null)
    {
        _ = actorUserId;

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string workflowSql = """
SELECT
    w.target_person_id,
    COALESCE(vpt.key, pt.key) AS process_type_key,
    w.department_id,
    w.position_role_id,
    w.first_name,
    w.last_name,
    w.employee_number,
    w.badge_number,
    w.status,
    COALESCE(w.completed_at, w.created_at) AS effective_completed_at
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
LEFT JOIN process_types vpt ON vpt.id = v.primary_legacy_process_type_id
WHERE w.uid = @workflowUid
LIMIT 1
FOR UPDATE OF w;
""";

        long? targetPersonId;
        string? processTypeKey;
        int? departmentId;
        int? roleId;
        string? firstName;
        string? lastName;
        int? employeeNumber;
        int? badgeNumber;
        string workflowStatus;
        DateTime effectiveCompletedAt;

        await using (var command = new NpgsqlCommand(workflowSql, connection, transaction))
        {
            command.Parameters.AddWithValue("workflowUid", workflowUid);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                await transaction.RollbackAsync();
                return;
            }

            targetPersonId = reader.IsDBNull(0) ? null : reader.GetInt64(0);
            processTypeKey = reader.IsDBNull(1) ? null : reader.GetString(1);
            departmentId = reader.IsDBNull(2) ? null : reader.GetInt32(2);
            roleId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
            firstName = reader.IsDBNull(4) ? null : reader.GetString(4);
            lastName = reader.IsDBNull(5) ? null : reader.GetString(5);
            employeeNumber = reader.IsDBNull(6) ? null : reader.GetInt32(6);
            badgeNumber = reader.IsDBNull(7) ? null : reader.GetInt32(7);
            workflowStatus = reader.GetString(8);
            effectiveCompletedAt = reader.GetDateTime(9);
        }

        if (!targetPersonId.HasValue
            || !string.Equals(workflowStatus, "completed", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(processTypeKey))
        {
            await transaction.CommitAsync();
            return;
        }

        var normalizedProcessTypeKey = processTypeKey.Trim().ToLowerInvariant();
        var projectionApplied = false;
        if (string.Equals(normalizedProcessTypeKey, "onboarding", StringComparison.Ordinal))
        {
            const string sql = """
UPDATE people
SET
    department_id = COALESCE(@departmentId, department_id),
    current_position_role_id = COALESCE(@roleId, current_position_role_id),
    first_name = COALESCE(@firstName, first_name),
    last_name = COALESCE(@lastName, last_name),
    employee_number = COALESCE(@employeeNumber, employee_number),
    badge_number = COALESCE(@badgeNumber, badge_number),
    employment_status = 'active',
    entry_date = COALESCE(entry_date, @entryDate),
    exit_date = NULL,
    updated_at = NOW()
WHERE id = @personId;
""";

            projectionApplied = await ExecuteProjectionUpdate(
                connection,
                transaction,
                sql,
                targetPersonId.Value,
                departmentId,
                roleId,
                firstName,
                lastName,
                employeeNumber,
                badgeNumber,
                effectiveCompletedAt);
        }
        else if (string.Equals(normalizedProcessTypeKey, "offboarding", StringComparison.Ordinal))
        {
            const string sql = """
UPDATE people
SET
    employment_status = 'exited',
    exit_date = COALESCE(exit_date, @entryDate),
    updated_at = NOW()
WHERE id = @personId;
""";

            projectionApplied = await ExecuteProjectionUpdate(
                connection,
                transaction,
                sql,
                targetPersonId.Value,
                departmentId: null,
                roleId: null,
                firstName: null,
                lastName: null,
                employeeNumber: null,
                badgeNumber: null,
                effectiveCompletedAt);
        }
        else if (string.Equals(normalizedProcessTypeKey, "department_change", StringComparison.Ordinal))
        {
            const string sql = """
UPDATE people
SET
    department_id = COALESCE(@departmentId, department_id),
    updated_at = NOW()
WHERE id = @personId;
""";

            projectionApplied = await ExecuteProjectionUpdate(
                connection,
                transaction,
                sql,
                targetPersonId.Value,
                departmentId,
                roleId: null,
                firstName: null,
                lastName: null,
                employeeNumber: null,
                badgeNumber: null,
                effectiveCompletedAt);
        }
        else if (string.Equals(normalizedProcessTypeKey, "name_change", StringComparison.Ordinal))
        {
            const string sql = """
UPDATE people
SET
    first_name = COALESCE(@firstName, first_name),
    last_name = COALESCE(@lastName, last_name),
    updated_at = NOW()
WHERE id = @personId;
""";

            projectionApplied = await ExecuteProjectionUpdate(
                connection,
                transaction,
                sql,
                targetPersonId.Value,
                departmentId: null,
                roleId: null,
                firstName,
                lastName,
                employeeNumber: null,
                badgeNumber: null,
                effectiveCompletedAt);
        }
        else if (string.Equals(normalizedProcessTypeKey, "position_change", StringComparison.Ordinal)
                 || string.Equals(normalizedProcessTypeKey, "role_change", StringComparison.Ordinal))
        {
            const string sql = """
UPDATE people
SET
    current_position_role_id = COALESCE(@roleId, current_position_role_id),
    updated_at = NOW()
WHERE id = @personId;
""";

            projectionApplied = await ExecuteProjectionUpdate(
                connection,
                transaction,
                sql,
                targetPersonId.Value,
                departmentId: null,
                roleId,
                firstName: null,
                lastName: null,
                employeeNumber: null,
                badgeNumber: null,
                effectiveCompletedAt);
        }

        if (!projectionApplied)
        {
            await transaction.CommitAsync();
            return;
        }

        await transaction.CommitAsync();
    }

    private static async Task<bool> ExecuteProjectionUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        long personId,
        int? departmentId,
        int? roleId,
        string? firstName,
        string? lastName,
        int? employeeNumber,
        int? badgeNumber,
        DateTime effectiveCompletedAt)
    {
        try
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("personId", personId);
            command.Parameters.Add("departmentId", NpgsqlDbType.Integer).Value = (object?)departmentId ?? DBNull.Value;
            command.Parameters.Add("roleId", NpgsqlDbType.Integer).Value = (object?)roleId ?? DBNull.Value;
            command.Parameters.Add("firstName", NpgsqlDbType.Text).Value = (object?)firstName ?? DBNull.Value;
            command.Parameters.Add("lastName", NpgsqlDbType.Text).Value = (object?)lastName ?? DBNull.Value;
            command.Parameters.Add("employeeNumber", NpgsqlDbType.Integer).Value = (object?)employeeNumber ?? DBNull.Value;
            command.Parameters.Add("badgeNumber", NpgsqlDbType.Integer).Value = (object?)badgeNumber ?? DBNull.Value;
            command.Parameters.AddWithValue("entryDate", DateOnly.FromDateTime(effectiveCompletedAt));
            return await command.ExecuteNonQueryAsync() > 0;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation
                                           && string.Equals(ex.ConstraintName, "uq_people_employee_number", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Die Personalnummer ist bereits einem anderen Mitarbeiter zugeordnet.");
        }
    }

    private async Task<WorkflowTargetPersonDto?> LoadPersonSummary(long personId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = """
WITH latest_completed_onboarding AS (
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
        JOIN process_types pt ON pt.id = w.process_type_id
        LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
        LEFT JOIN process_types vpt ON vpt.id = v.primary_legacy_process_type_id
        WHERE w.target_person_id IS NOT NULL
          AND pt.key = 'onboarding'
          AND (w.workflow_definition_version_id IS NULL OR vpt.key = 'onboarding')
          AND w.status = 'completed'
    ) resolved
    ORDER BY resolved.person_id, resolved.completed_at DESC, resolved.id DESC
)
SELECT
    p.id,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', p.first_name, p.last_name)), ''),
        u.display_name,
        linked_directory.display_name,
        'Person #' || p.id::text
    ) AS display_name,
    p.department_id,
    d.name AS department_name,
    p.current_position_role_id,
    r.name AS role_name,
    p.employee_number,
    p.badge_number,
    p.first_name,
    p.last_name,
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
LEFT JOIN departments d ON d.id = p.department_id
LEFT JOIN app_roles r ON r.id = p.current_position_role_id
LEFT JOIN latest_completed_onboarding ON latest_completed_onboarding.person_id = p.id
WHERE p.id = @personId
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("personId", personId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

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

    private static async Task<bool> DepartmentExistsInternal(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId)
    {
        const string sql = """
SELECT EXISTS(
    SELECT 1
    FROM departments
    WHERE id = @departmentId
);
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentId", departmentId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }
}
