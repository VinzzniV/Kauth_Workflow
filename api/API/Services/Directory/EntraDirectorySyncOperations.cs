using Npgsql;
using NpgsqlTypes;

namespace API.Services.Directory;

internal sealed class EntraDirectorySyncOperations : IEntraDirectorySyncOperations
{
    public async Task EnsureDirectoryProjectionUserColumnsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = @"
ALTER TABLE app_users
    ADD COLUMN IF NOT EXISTS directory_synced BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS last_directory_synced_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS department_source VARCHAR(32) NOT NULL DEFAULT 'local',
    ADD COLUMN IF NOT EXISTS department_override_active BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE app_users
    DROP CONSTRAINT IF EXISTS chk_app_users_department_source;

ALTER TABLE app_users
    ADD CONSTRAINT chk_app_users_department_source
    CHECK (department_source IN ('local', 'directory', 'override', 'unassigned'));";

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, long>> UpsertDirectoryIdentitiesBatchAsync(
        NpgsqlConnection connection,
        IReadOnlyList<(Guid EntraObjectId, EntraDirectoryUser User)> validUsers,
        CancellationToken cancellationToken)
    {
        var dedup = new Dictionary<Guid, EntraDirectoryUser>(validUsers.Count);
        foreach (var entry in validUsers)
        {
            dedup[entry.EntraObjectId] = entry.User;
        }

        var count = dedup.Count;
        var entraObjectIds = new Guid[count];
        var upns = new string[count];
        var mails = new string?[count];
        var displayNames = new string[count];
        var accountEnableds = new bool[count];
        var departmentNames = new string?[count];
        var employeeNumbers = new int?[count];

        var i = 0;
        foreach (var (entraId, user) in dedup)
        {
            entraObjectIds[i] = entraId;
            upns[i] = user.UserPrincipalName ?? user.Id ?? entraId.ToString();
            mails[i] = user.Mail;
            displayNames[i] = user.DisplayName ?? user.UserPrincipalName ?? entraId.ToString();
            accountEnableds[i] = user.AccountEnabled ?? true;
            departmentNames[i] = Normalize(user.Department);
            employeeNumbers[i] = ParseDirectoryEmployeeNumber(user.EmployeeId);
            i++;
        }

        const string sql = @"
INSERT INTO directory_identities (entra_object_id, user_principal_name, mail, display_name, account_enabled, department_name, employee_number, last_synced_at)
SELECT t.entra_object_id, t.user_principal_name, t.mail, t.display_name, t.account_enabled, t.department_name, t.employee_number, NOW()
FROM unnest(@entraObjectIds::uuid[], @upns::text[], @mails::text[], @displayNames::text[], @accountEnableds::bool[], @departmentNames::text[], @employeeNumbers::int[])
     AS t(entra_object_id, user_principal_name, mail, display_name, account_enabled, department_name, employee_number)
ON CONFLICT (entra_object_id) DO UPDATE SET
    user_principal_name = EXCLUDED.user_principal_name,
    mail = EXCLUDED.mail,
    display_name = EXCLUDED.display_name,
    account_enabled = EXCLUDED.account_enabled,
    department_name = EXCLUDED.department_name,
    employee_number = EXCLUDED.employee_number,
    last_synced_at = NOW()
RETURNING id, entra_object_id;";

        var result = new Dictionary<Guid, long>(count);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add("entraObjectIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = entraObjectIds;
        cmd.Parameters.Add("upns", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = upns;
        cmd.Parameters.Add("mails", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = mails;
        cmd.Parameters.Add("displayNames", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = displayNames;
        cmd.Parameters.Add("accountEnableds", NpgsqlDbType.Array | NpgsqlDbType.Boolean).Value = accountEnableds;
        cmd.Parameters.Add("departmentNames", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = departmentNames;
        cmd.Parameters.Add("employeeNumbers", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = employeeNumbers;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt64(0);
            var entraId = reader.GetGuid(1);
            result[entraId] = id;
        }
        return result;
    }

    public async Task InsertGroupMembershipsBatchAsync(
        NpgsqlConnection connection,
        int directoryGroupId,
        long[] directoryIdentityIds,
        CancellationToken cancellationToken)
    {
        if (directoryIdentityIds.Length == 0)
        {
            return;
        }

        const string sql = @"
INSERT INTO directory_group_members (directory_group_id, directory_identity_id, synced_at)
SELECT @directoryGroupId, t.identity_id, NOW()
FROM unnest(@identityIds::bigint[]) AS t(identity_id)
ON CONFLICT DO NOTHING;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("directoryGroupId", directoryGroupId);
        cmd.Parameters.Add("identityIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = directoryIdentityIds;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AutoLinkIdentitiesToAppUsersAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE directory_identities di
SET app_user_id = u.id
FROM app_users u
WHERE di.app_user_id IS NULL
  AND u.external_key IS NOT NULL
  AND u.external_key = di.entra_object_id::text;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DirectoryDepartmentSyncResult> EnsureDirectoryDepartmentsExistAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = @"
WITH source_departments AS (
    SELECT DISTINCT BTRIM(di.department_name) AS name
    FROM directory_identities di
    WHERE di.department_name IS NOT NULL
      AND BTRIM(di.department_name) <> ''
),
inserted AS (
    INSERT INTO departments (name)
    SELECT name
    FROM source_departments
    ON CONFLICT (name) DO NOTHING
    RETURNING name
)
SELECT
    (SELECT COUNT(*)::int FROM source_departments),
    COALESCE((SELECT ARRAY_AGG(name ORDER BY name) FROM source_departments), ARRAY[]::text[]),
    (SELECT COUNT(*)::int FROM inserted),
    COALESCE((SELECT ARRAY_AGG(name ORDER BY name) FROM inserted), ARRAY[]::text[]);";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new DirectoryDepartmentSyncResult(
                reader.GetInt32(0),
                reader.GetFieldValue<string[]>(1),
                reader.GetInt32(2),
                reader.GetFieldValue<string[]>(3));
        }

        return new DirectoryDepartmentSyncResult(0, [], 0, []);
    }

    public async Task<DirectoryUserProjectionResult> UpdateExistingAppUsersFromDirectoryAsync(
        NpgsqlConnection connection,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        await EnsureDirectoryProjectionUserColumnsAsync(connection, cancellationToken);

        // Only updates existing app_users. New identities from Entra are NOT automatically
        // promoted to app_users — they remain in directory_identities until an admin imports them.
        const string sql = @"
UPDATE app_users u
SET
    external_key = di.entra_object_id::text,
    entra_object_id = di.entra_object_id,
    display_name = di.display_name,
    email = COALESCE(di.mail, di.user_principal_name, u.email),
    directory_synced = TRUE,
    last_directory_synced_at = NOW(),
    department_id = CASE
        WHEN u.department_override_active = TRUE THEN u.department_id
        ELSE department.id
    END,
    department_source = CASE
        WHEN u.department_override_active = TRUE THEN 'override'
        WHEN department.id IS NULL THEN 'unassigned'
        ELSE 'directory'
    END
FROM directory_identities di
LEFT JOIN departments department ON LOWER(department.name) = LOWER(di.department_name)
WHERE u.entra_object_id = di.entra_object_id;";

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await AutoLinkIdentitiesToAppUsersAsync(connection, cancellationToken);

        const string linkSql = @"
UPDATE directory_identities di
SET app_user_id = u.id
FROM app_users u
WHERE u.entra_object_id = di.entra_object_id
  AND di.app_user_id IS DISTINCT FROM u.id;";

        await using var linkCommand = new NpgsqlCommand(linkSql, connection);
        await linkCommand.ExecuteNonQueryAsync(cancellationToken);

        const string linkPeopleByEmployeeNumberSql = @"
WITH linkable_identities AS (
    SELECT
        di.id AS directory_identity_id,
        di.app_user_id,
        di.employee_number
    FROM directory_identities di
    WHERE di.app_user_id IS NOT NULL
      AND di.employee_number IS NOT NULL
),
matched AS (
    UPDATE people p
    SET
        app_user_id = COALESCE(p.app_user_id, linkable_identities.app_user_id),
        directory_identity_id = linkable_identities.directory_identity_id,
        updated_at = NOW()
    FROM linkable_identities
    WHERE p.employee_number = linkable_identities.employee_number
      AND (
          p.app_user_id IS NULL
          OR p.app_user_id = linkable_identities.app_user_id
      )
      AND (
          p.directory_identity_id IS NULL
          OR p.directory_identity_id = linkable_identities.directory_identity_id
      )
    RETURNING
        p.id AS matched_person_id,
        linkable_identities.app_user_id,
        linkable_identities.directory_identity_id,
        linkable_identities.employee_number
)
INSERT INTO person_match_audit_log (
    matched_person_id, app_user_id, directory_identity_id, employee_number,
    match_strategy, match_score, fallback_used, source, detail
)
SELECT
    matched.matched_person_id,
    matched.app_user_id,
    matched.directory_identity_id,
    matched.employee_number,
    'employee_number',
    1.00,
    false,
    'directory_sync_bulk',
    NULL
FROM matched;";

        await using (var linkPeopleCommand = new NpgsqlCommand(linkPeopleByEmployeeNumberSql, connection))
        {
            await linkPeopleCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string personSql = @"
INSERT INTO people (app_user_id, department_id, directory_identity_id, updated_at)
SELECT
    u.id,
    u.department_id,
    di.id,
    NOW()
FROM app_users u
JOIN directory_identities di ON di.app_user_id = u.id
WHERE NOT EXISTS (
    SELECT 1
    FROM people existing
    WHERE existing.app_user_id = u.id
       OR existing.directory_identity_id = di.id
       OR (
            di.employee_number IS NOT NULL
            AND existing.employee_number = di.employee_number
       )
)
ON CONFLICT (app_user_id) DO UPDATE
SET
    department_id = EXCLUDED.department_id,
    directory_identity_id = EXCLUDED.directory_identity_id,
    updated_at = NOW();";

        await using var personCommand = new NpgsqlCommand(personSql, connection);
        await personCommand.ExecuteNonQueryAsync(cancellationToken);

        var touchedUserCount = 0;
        var directoryAssignedUserCount = 0;
        var overrideUserCount = 0;
        var unassignedUserCount = 0;
        var linkedIdentityCount = 0;
        var sampleUsers = new List<DirectoryUserProjectionSample>();

        const string summarySql = @"
WITH touched_users AS (
    SELECT
        u.id,
        u.display_name,
        u.email,
        u.department_source,
        u.department_override_active
    FROM app_users u
    WHERE u.directory_synced = TRUE
      AND u.last_directory_synced_at >= @startedAt
)
SELECT
    COUNT(*)::int,
    COUNT(*) FILTER (WHERE department_source = 'directory')::int,
    COUNT(*) FILTER (WHERE department_source = 'override')::int,
    COUNT(*) FILTER (WHERE department_source = 'unassigned')::int,
    (
        SELECT COUNT(*)::int
        FROM directory_identities di
        WHERE di.app_user_id IS NOT NULL
          AND di.last_synced_at >= @startedAt
    )
FROM touched_users;";

        await using (var summaryCommand = new NpgsqlCommand(summarySql, connection))
        {
            summaryCommand.Parameters.AddWithValue("startedAt", startedAt);
            await using var reader = await summaryCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                touchedUserCount = reader.GetInt32(0);
                directoryAssignedUserCount = reader.GetInt32(1);
                overrideUserCount = reader.GetInt32(2);
                unassignedUserCount = reader.GetInt32(3);
                linkedIdentityCount = reader.GetInt32(4);
            }
        }

        const string sampleSql = @"
SELECT
    u.id,
    u.display_name,
    u.email,
    u.department_source,
    u.department_override_active,
    department.name AS department_name,
    di.department_name AS directory_department_name,
    di.user_principal_name
FROM app_users u
LEFT JOIN departments department ON department.id = u.department_id
LEFT JOIN LATERAL (
    SELECT
        latest.department_name,
        latest.user_principal_name
    FROM directory_identities latest
    WHERE latest.app_user_id = u.id
    ORDER BY latest.last_synced_at DESC NULLS LAST, latest.id DESC
    LIMIT 1
) di ON TRUE
WHERE u.directory_synced = TRUE
  AND u.last_directory_synced_at >= @startedAt
ORDER BY u.display_name, u.id
LIMIT 12;";

        await using (var sampleCommand = new NpgsqlCommand(sampleSql, connection))
        {
            sampleCommand.Parameters.AddWithValue("startedAt", startedAt);
            await using var reader = await sampleCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                sampleUsers.Add(new DirectoryUserProjectionSample(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetBoolean(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7)));
            }
        }

        return new DirectoryUserProjectionResult(
            touchedUserCount,
            directoryAssignedUserCount,
            overrideUserCount,
            unassignedUserCount,
            linkedIdentityCount,
            sampleUsers);
    }

    public async Task EnsureDevelopmentDefaultGroupMappingsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = @"
WITH mapping_seed(group_name, role_key, scope) AS (
    VALUES
        ('Onboarding-App-Admins', 'auth_admin', 'global'),
        ('Onboarding-App-HR', 'auth_hr', 'global'),
        ('Onboarding-App-Managers', 'auth_manager', 'global'),
        ('Onboarding-App-Access', 'auth_reader', 'global')
)
INSERT INTO directory_group_role_mappings (
    directory_group_id,
    app_role_id,
    scope,
    scope_department_id,
    is_active
)
SELECT
    dg.id,
    ar.id,
    seed.scope,
    NULL,
    TRUE
FROM mapping_seed seed
JOIN directory_groups dg ON dg.display_name = seed.group_name
JOIN app_roles ar ON ar.role_key = seed.role_key
ON CONFLICT (directory_group_id, app_role_id, scope, COALESCE(scope_department_id, -1))
DO UPDATE SET is_active = TRUE;";

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? ParseDirectoryEmployeeNumber(string? employeeId)
    {
        var normalized = Normalize(employeeId);
        return int.TryParse(normalized, out var employeeNumber) && employeeNumber > 0
            ? employeeNumber
            : null;
    }
}
