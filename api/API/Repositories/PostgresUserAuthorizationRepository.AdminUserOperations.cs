using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresUserAuthorizationRepository
{
    public async Task<AdminUserDto> CreateUser(
        string? externalKey,
        string displayName,
        string email,
        string? notificationEmail,
        int? departmentId,
        bool isActive,
        long? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedExternalKey = Normalize(externalKey);
        var normalizedDisplayName = NormalizeRequired(displayName, "Display name is required.");
        var normalizedEmail = NormalizeRequired(email, "Email is required.");
        var normalizedNotificationEmail = Normalize(notificationEmail);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (departmentId.HasValue && !await DepartmentExists(connection, transaction, departmentId.Value, cancellationToken))
        {
            throw new InvalidOperationException("Selected department is invalid.");
        }

        await EnsureUserIdentityAvailable(
            connection,
            transaction,
            normalizedEmail,
            normalizedExternalKey,
            null,
            cancellationToken);

        const string sql = @"
INSERT INTO app_users (
    external_key,
    department_id,
    display_name,
    email,
    notification_email,
    is_active,
    department_source,
    department_override_active
)
VALUES (
    @externalKey,
    @departmentId,
    @displayName,
    @email,
    @notificationEmail,
    @isActive,
    CASE WHEN @departmentId IS NULL THEN 'unassigned' ELSE 'local' END,
    FALSE
)
RETURNING id;";

        long userId;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            var externalKeyParameter = command.Parameters.Add("externalKey", NpgsqlDbType.Text);
            externalKeyParameter.Value = (object?)normalizedExternalKey ?? DBNull.Value;
            var departmentIdParameter = command.Parameters.Add("departmentId", NpgsqlDbType.Integer);
            departmentIdParameter.Value = (object?)departmentId ?? DBNull.Value;
            command.Parameters.AddWithValue("displayName", normalizedDisplayName);
            command.Parameters.AddWithValue("email", normalizedEmail);
            var notificationEmailParameter = command.Parameters.Add("notificationEmail", NpgsqlDbType.Text);
            notificationEmailParameter.Value = (object?)normalizedNotificationEmail ?? DBNull.Value;
            command.Parameters.AddWithValue("isActive", isActive);

            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            if (scalar is null)
            {
                throw new InvalidOperationException("User could not be created.");
            }

            userId = (long)scalar;
        }

        await UpsertPersonRecord(connection, transaction, userId, departmentId, cancellationToken);
        await LogPermissionAuditAsync(
            connection,
            transaction,
            actorUserId,
            "created",
            "user",
            $"User {normalizedDisplayName} created.",
            null,
            new { userId, normalizedDisplayName, normalizedEmail, departmentId, isActive },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        var users = await LoadAdminUsers(connection, null, userId, cancellationToken);
        return users.Single();
    }

    public async Task<bool> DeleteUser(long userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new InvalidOperationException("userId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await UserExists(connection, transaction, userId, cancellationToken))
        {
            return false;
        }

        await EnsureUserDeletionAllowed(connection, transaction, userId, cancellationToken);

        const string sql = @"
DELETE FROM app_users
WHERE id = @userId;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<AdminUserDto?> UpdateUserMasterData(
        long userId,
        string? externalKey,
        string displayName,
        string email,
        string? notificationEmail,
        int? departmentId,
        bool isActive,
        long? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new InvalidOperationException("userId must be greater than zero.");
        }

        var normalizedExternalKey = Normalize(externalKey);
        var normalizedDisplayName = NormalizeRequired(displayName, "Display name is required.");
        var normalizedEmail = NormalizeRequired(email, "Email is required.");
        var normalizedNotificationEmail = Normalize(notificationEmail);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await UserExists(connection, transaction, userId, cancellationToken))
        {
            return null;
        }

        var previousUser = (await LoadAdminUsers(connection, transaction, userId, cancellationToken)).FirstOrDefault();

        if (departmentId.HasValue && !await DepartmentExists(connection, transaction, departmentId.Value, cancellationToken))
        {
            throw new InvalidOperationException("Selected department is invalid.");
        }

        await EnsureUserIdentityAvailable(
            connection,
            transaction,
            normalizedEmail,
            normalizedExternalKey,
            userId,
            cancellationToken);

        const string sql = @"
UPDATE app_users
SET external_key = @externalKey,
    display_name = @displayName,
    email = @email,
    notification_email = @notificationEmail,
    department_id = @departmentId,
    is_active = @isActive,
    department_source = CASE
        WHEN directory_synced = TRUE AND @departmentId IS NOT NULL THEN 'override'
        WHEN directory_synced = TRUE AND @departmentId IS NULL THEN 'unassigned'
        WHEN @departmentId IS NULL THEN 'unassigned'
        ELSE 'local'
    END,
    department_override_active = CASE
        WHEN directory_synced = TRUE AND (
            department_id IS DISTINCT FROM @departmentId
            OR department_override_active = TRUE
        ) THEN @departmentId IS NOT NULL
        ELSE FALSE
    END
WHERE id = @userId;";

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            var externalKeyParameter = command.Parameters.Add("externalKey", NpgsqlDbType.Text);
            externalKeyParameter.Value = (object?)normalizedExternalKey ?? DBNull.Value;
            command.Parameters.AddWithValue("displayName", normalizedDisplayName);
            command.Parameters.AddWithValue("email", normalizedEmail);
            var notificationEmailParameter = command.Parameters.Add("notificationEmail", NpgsqlDbType.Text);
            notificationEmailParameter.Value = (object?)normalizedNotificationEmail ?? DBNull.Value;
            var departmentIdParameter = command.Parameters.Add("departmentId", NpgsqlDbType.Integer);
            departmentIdParameter.Value = (object?)departmentId ?? DBNull.Value;
            command.Parameters.AddWithValue("isActive", isActive);
            command.Parameters.AddWithValue("userId", userId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await UpsertPersonRecord(connection, transaction, userId, departmentId, cancellationToken);
        var updatedUserBeforeCommit = (await LoadAdminUsers(connection, transaction, userId, cancellationToken)).FirstOrDefault();
        await LogPermissionAuditAsync(
            connection,
            transaction,
            actorUserId,
            "updated",
            "user",
            $"User {userId} master data updated.",
            previousUser,
            updatedUserBeforeCommit,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        var users = await LoadAdminUsers(connection, null, userId, cancellationToken);
        return users.FirstOrDefault();
    }

    public async Task<AdminUserDto?> UpdateUserRoles(
        long userId,
        IReadOnlyList<int> roleIds,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new InvalidOperationException("userId must be greater than zero.");
        }

        var normalizedRoleIds = roleIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await UserExists(connection, transaction, userId, cancellationToken))
        {
            return null;
        }

        await EnsureRoleIdsExist(connection, transaction, normalizedRoleIds, cancellationToken);

        const string deleteSql = @"
DELETE FROM app_user_roles
WHERE app_user_id = @userId
  AND (cardinality(@roleIds) = 0 OR app_role_id <> ALL(@roleIds));";

        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("userId", userId);
            deleteCommand.Parameters.Add("roleIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = normalizedRoleIds;
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (normalizedRoleIds.Length > 0)
        {
            const string insertSql = @"
INSERT INTO app_user_roles (app_user_id, app_role_id)
SELECT @userId, role_id
FROM UNNEST(@roleIds) AS role_id
ON CONFLICT (app_user_id, app_role_id) DO NOTHING;";

            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("userId", userId);
            insertCommand.Parameters.Add("roleIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = normalizedRoleIds;
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var users = await LoadAdminUsers(connection, null, userId, cancellationToken);
        return users.FirstOrDefault();
    }

    public async Task<AdminUserDto?> UpdateUserGroups(
        long userId,
        IReadOnlyList<int> groupIds,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new InvalidOperationException("userId must be greater than zero.");
        }

        var normalizedGroupIds = groupIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await UserExists(connection, transaction, userId, cancellationToken))
        {
            return null;
        }

        await EnsureGroupIdsExist(connection, transaction, normalizedGroupIds, cancellationToken);

        const string deleteSql = @"
DELETE FROM app_user_groups
WHERE app_user_id = @userId
  AND (cardinality(@groupIds) = 0 OR app_group_id <> ALL(@groupIds));";

        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("userId", userId);
            deleteCommand.Parameters.Add("groupIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = normalizedGroupIds;
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (normalizedGroupIds.Length > 0)
        {
            const string insertSql = @"
INSERT INTO app_user_groups (app_user_id, app_group_id)
SELECT @userId, group_id
FROM UNNEST(@groupIds) AS group_id
ON CONFLICT (app_user_id, app_group_id) DO NOTHING;";

            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("userId", userId);
            insertCommand.Parameters.Add("groupIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = normalizedGroupIds;
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var users = await LoadAdminUsers(connection, null, userId, cancellationToken);
        return users.FirstOrDefault();
    }

    private static async Task<bool> UserExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM app_users WHERE id = @userId);";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> UserHasTaskAssignments(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM task_assignments
    WHERE assignee_user_id = @userId
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task EnsureUserDeletionAllowed(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId,
        CancellationToken cancellationToken)
    {
        if (await UserHasTaskAssignments(connection, transaction, userId, cancellationToken))
        {
            throw new InvalidOperationException("User is still assigned to workflow tasks and cannot be deleted.");
        }

        const string departmentLeadSql = @"
SELECT EXISTS(
    SELECT 1
    FROM department_settings ds
    JOIN people p ON p.id = ds.department_lead_person_id
    WHERE p.app_user_id = @userId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            departmentLeadSql,
            "userId",
            NpgsqlDbType.Bigint,
            userId,
            "User is configured as department lead for one or more departments and cannot be deleted.",
            cancellationToken);

        const string requirementOwnerSql = @"
SELECT EXISTS(
    SELECT 1
    FROM department_settings ds
    JOIN people p ON p.id = ds.requirement_approver_person_id
    WHERE p.app_user_id = @userId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            requirementOwnerSql,
            "userId",
            NpgsqlDbType.Bigint,
            userId,
            "User is configured as requirement owner for one or more departments and cannot be deleted.",
            cancellationToken);

        const string systemOwnerSql = @"
SELECT EXISTS(
    SELECT 1
    FROM system_responsibilities sr
    JOIN people p ON p.id = sr.responsible_person_id
    WHERE p.app_user_id = @userId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            systemOwnerSql,
            "userId",
            NpgsqlDbType.Bigint,
            userId,
            "User is configured as the responsible owner for one or more systems and cannot be deleted.",
            cancellationToken);
    }

    private static async Task EnsureActiveUserIdsExist(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long[] userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Length == 0)
        {
            return;
        }

        const string sql = @"
SELECT COUNT(*)
FROM app_users
WHERE id = ANY(@userIds)
  AND is_active = TRUE;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.Add("userIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = userIds;
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken) ?? 0);

        if (count != userIds.Length)
        {
            throw new InvalidOperationException("One or more users are invalid or inactive.");
        }
    }

    private static async Task EnsureUsersCanAccessSupervisorStep(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long[] userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Length == 0)
        {
            return;
        }

        foreach (var userId in userIds)
        {
            var user = (await LoadAdminUsers(connection, transaction, userId, cancellationToken)).SingleOrDefault();
            if (user is null || !AdminUserEligibility.CanAccessSupervisorStep(user))
            {
                throw new InvalidOperationException(
                    "Department lead and requirement owner must be active users with supervisor access.");
            }
        }
    }

    private static async Task EnsureUserIdentityAvailable(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string email,
        string? externalKey,
        long? excludeUserId,
        CancellationToken cancellationToken)
    {
        const string emailSql = @"
SELECT EXISTS(
    SELECT 1
    FROM app_users
    WHERE LOWER(email) = LOWER(@email)
      AND (@excludeUserId IS NULL OR id <> @excludeUserId)
);";

        await using (var emailCommand = new NpgsqlCommand(emailSql, connection, transaction))
        {
            emailCommand.Parameters.AddWithValue("email", email);
            var excludeUserIdParameter = emailCommand.Parameters.Add("excludeUserId", NpgsqlDbType.Bigint);
            excludeUserIdParameter.Value = (object?)excludeUserId ?? DBNull.Value;

            var emailExists = (bool)(await emailCommand.ExecuteScalarAsync(cancellationToken) ?? false);
            if (emailExists)
            {
                throw new InvalidOperationException("A user with this email already exists.");
            }
        }

        if (string.IsNullOrWhiteSpace(externalKey))
        {
            return;
        }

        const string externalKeySql = @"
SELECT EXISTS(
    SELECT 1
    FROM app_users
    WHERE LOWER(external_key) = LOWER(@externalKey)
      AND (@excludeUserId IS NULL OR id <> @excludeUserId)
);";

        await using var externalKeyCommand = new NpgsqlCommand(externalKeySql, connection, transaction);
        externalKeyCommand.Parameters.AddWithValue("externalKey", externalKey);
        var externalKeyExcludeUserIdParameter = externalKeyCommand.Parameters.Add("excludeUserId", NpgsqlDbType.Bigint);
        externalKeyExcludeUserIdParameter.Value = (object?)excludeUserId ?? DBNull.Value;

        var externalKeyExists = (bool)(await externalKeyCommand.ExecuteScalarAsync(cancellationToken) ?? false);
        if (externalKeyExists)
        {
            throw new InvalidOperationException("A user with this login name already exists.");
        }
    }

    private static async Task<long> UpsertPersonRecord(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId,
        int? departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO people (app_user_id, department_id, updated_at)
SELECT
    u.id,
    COALESCE(@departmentId, u.department_id),
    NOW()
FROM app_users u
WHERE u.id = @userId
ON CONFLICT (app_user_id) DO UPDATE
SET
    department_id = EXCLUDED.department_id,
    updated_at = NOW()
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);
        var departmentIdParameter = command.Parameters.Add("departmentId", NpgsqlDbType.Integer);
        departmentIdParameter.Value = (object?)departmentId ?? DBNull.Value;

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        if (scalar is null)
        {
            throw new InvalidOperationException("Person record could not be synchronized.");
        }

        return (long)scalar;
    }
}
