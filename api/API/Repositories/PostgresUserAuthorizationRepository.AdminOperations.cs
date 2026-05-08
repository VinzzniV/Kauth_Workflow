using Npgsql;
using NpgsqlTypes;
using System.Globalization;
using System.Text;

namespace API;

internal sealed partial class PostgresUserAuthorizationRepository
{
    public async Task<AdminDepartmentAssignmentDto> CreateDepartment(
        string departmentName,
        CancellationToken cancellationToken = default)
    {
        var normalizedDepartmentName = NormalizeRequired(departmentName, "Department name is required.");

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await EnsureDepartmentNameAvailable(connection, transaction, normalizedDepartmentName, null, cancellationToken);

        const string sql = @"
INSERT INTO departments (name)
VALUES (@name)
RETURNING id;";

        int departmentId;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("name", normalizedDepartmentName);
            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            if (scalar is null)
            {
                throw new InvalidOperationException("Department could not be created.");
            }

            departmentId = (int)scalar;
        }

        await transaction.CommitAsync(cancellationToken);
        var departments = await LoadAdminDepartmentAssignments(connection, null, departmentId, cancellationToken);
        return departments.Single();
    }

    public async Task<bool> DeleteDepartment(int departmentId, CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            throw new InvalidOperationException("departmentId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await DepartmentExists(connection, transaction, departmentId, cancellationToken))
        {
            return false;
        }

        await EnsureDepartmentDeletionAllowed(connection, transaction, departmentId, cancellationToken);

        const string sql = @"
DELETE FROM departments
WHERE id = @departmentId;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentId", departmentId);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<AdminRoleDto> CreateDepartmentPosition(
        int departmentId,
        string positionName,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            throw new InvalidOperationException("departmentId must be greater than zero.");
        }

        var normalizedPositionName = NormalizeRequired(positionName, "Position name is required.");

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await DepartmentExists(connection, transaction, departmentId, cancellationToken))
        {
            throw new InvalidOperationException("Selected department is invalid.");
        }

        await EnsurePositionNameAvailable(connection, transaction, departmentId, normalizedPositionName, null, cancellationToken);
        var roleKey = await GenerateUniquePositionRoleKey(connection, transaction, departmentId, normalizedPositionName, cancellationToken);

        const string sql = """
INSERT INTO app_roles (department_id, role_key, name, role_kind, is_active)
VALUES (@departmentId, @roleKey, @name, 'position', TRUE)
RETURNING id;
""";

        int positionId;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("departmentId", departmentId);
            command.Parameters.AddWithValue("roleKey", roleKey);
            command.Parameters.AddWithValue("name", normalizedPositionName);
            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            if (scalar is null)
            {
                throw new InvalidOperationException("Position could not be created.");
            }

            positionId = (int)scalar;
        }

        await transaction.CommitAsync(cancellationToken);
        var positions = await LoadAdminPositionRoles(connection, null, positionId, cancellationToken);
        return positions.Single();
    }

    public async Task<AdminRoleDto?> UpdateDepartmentPosition(
        int positionId,
        string positionName,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (positionId <= 0)
        {
            throw new InvalidOperationException("positionId must be greater than zero.");
        }

        var normalizedPositionName = NormalizeRequired(positionName, "Position name is required.");

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var departmentId = await LoadPositionDepartmentId(connection, transaction, positionId, cancellationToken);
        if (departmentId is null)
        {
            return null;
        }

        await EnsurePositionNameAvailable(connection, transaction, departmentId.Value, normalizedPositionName, positionId, cancellationToken);

        const string sql = """
UPDATE app_roles
SET name = @name,
    is_active = @isActive
WHERE id = @positionId
  AND role_kind = 'position';
""";

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("positionId", positionId);
            command.Parameters.AddWithValue("name", normalizedPositionName);
            command.Parameters.AddWithValue("isActive", isActive);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var positions = await LoadAdminPositionRoles(connection, null, positionId, cancellationToken);
        return positions.FirstOrDefault();
    }

    public async Task<bool> DeleteDepartmentPosition(int positionId, CancellationToken cancellationToken = default)
    {
        if (positionId <= 0)
        {
            throw new InvalidOperationException("positionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (await LoadPositionDepartmentId(connection, transaction, positionId, cancellationToken) is null)
        {
            return false;
        }

        await EnsurePositionDeletionAllowed(connection, transaction, positionId, cancellationToken);

        const string sql = """
DELETE FROM app_roles
WHERE id = @positionId
  AND role_kind = 'position';
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("positionId", positionId);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<AdminResponsibilityOwnerDto> CreateResponsibility(
        string responsibilityName,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        var normalizedResponsibilityName = NormalizeRequired(
            responsibilityName,
            "Responsibility name is required.");
        var normalizedDepartmentId = NormalizeNullableDepartmentId(departmentId);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (normalizedDepartmentId.HasValue
            && !await DepartmentExists(connection, transaction, normalizedDepartmentId.Value, cancellationToken))
        {
            throw new InvalidOperationException("Selected department is invalid.");
        }

        var responsibilityKey = await GenerateUniqueResponsibilityKey(
            connection,
            transaction,
            normalizedResponsibilityName,
            cancellationToken);
        var systemKey = await GenerateUniqueSystemKey(
            connection,
            transaction,
            normalizedResponsibilityName,
            cancellationToken);

        const string sql = """
INSERT INTO app_responsibilities (
    department_id,
    responsibility_key,
    system_key,
    name,
    responsibility_type,
    description,
    is_active
)
VALUES (
    @departmentId,
    @responsibilityKey,
    @systemKey,
    @name,
    'application',
    NULL,
    TRUE
)
RETURNING id;
""";

        int responsibilityId;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            var departmentParameter = command.Parameters.Add("departmentId", NpgsqlDbType.Integer);
            departmentParameter.Value = (object?)normalizedDepartmentId ?? DBNull.Value;
            command.Parameters.AddWithValue("responsibilityKey", responsibilityKey);
            command.Parameters.AddWithValue("systemKey", systemKey);
            command.Parameters.AddWithValue("name", normalizedResponsibilityName);

            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            if (scalar is null)
            {
                throw new InvalidOperationException("Responsibility could not be created.");
            }

            responsibilityId = (int)scalar;
        }

        await transaction.CommitAsync(cancellationToken);
        var responsibilities = await LoadAdminResponsibilityOwners(connection, null, responsibilityId, cancellationToken);
        return responsibilities.Single();
    }

    public async Task<bool> DeleteResponsibility(int responsibilityId, CancellationToken cancellationToken = default)
    {
        if (responsibilityId <= 0)
        {
            throw new InvalidOperationException("responsibilityId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await ResponsibilityExists(connection, transaction, responsibilityId, cancellationToken))
        {
            return false;
        }

        await EnsureResponsibilityDeletionAllowed(connection, transaction, responsibilityId, cancellationToken);

        const string sql = """
DELETE FROM app_responsibilities
WHERE id = @responsibilityId;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

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

    public async Task<AdminGroupDto?> UpdateGroupRoles(
        int groupId,
        IReadOnlyList<int> roleIds,
        CancellationToken cancellationToken = default)
    {
        if (groupId <= 0)
        {
            throw new InvalidOperationException("groupId must be greater than zero.");
        }

        var normalizedRoleIds = roleIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await GroupExists(connection, transaction, groupId, cancellationToken))
        {
            return null;
        }

        await EnsureRoleIdsExist(connection, transaction, normalizedRoleIds, cancellationToken);

        const string deleteSql = @"
DELETE FROM app_group_roles
WHERE app_group_id = @groupId
  AND (cardinality(@roleIds) = 0 OR app_role_id <> ALL(@roleIds));";

        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("groupId", groupId);
            deleteCommand.Parameters.Add("roleIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = normalizedRoleIds;
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (normalizedRoleIds.Length > 0)
        {
            const string insertSql = @"
INSERT INTO app_group_roles (app_group_id, app_role_id)
SELECT @groupId, role_id
FROM UNNEST(@roleIds) AS role_id
ON CONFLICT (app_group_id, app_role_id) DO NOTHING;";

            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("groupId", groupId);
            insertCommand.Parameters.Add("roleIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = normalizedRoleIds;
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var groups = await LoadAdminGroups(connection, null, groupId, cancellationToken);
        return groups.FirstOrDefault();
    }

    public async Task<AdminDepartmentAssignmentDto?> UpdateDepartmentAssignment(
        int departmentId,
        long? departmentLeadUserId,
        long? requirementOwnerUserId,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            throw new InvalidOperationException("departmentId must be greater than zero.");
        }

        var normalizedDepartmentLeadUserId = NormalizeNullableUserId(departmentLeadUserId);
        var normalizedRequirementOwnerUserId = NormalizeNullableUserId(requirementOwnerUserId);
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await DepartmentExists(connection, transaction, departmentId, cancellationToken))
        {
            return null;
        }

        var selectedUserIds = new[] { normalizedDepartmentLeadUserId, normalizedRequirementOwnerUserId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        await EnsureActiveUserIdsExist(
            connection,
            transaction,
            selectedUserIds,
            cancellationToken);

        if (normalizedDepartmentLeadUserId.HasValue)
        {
            await EnsureUsersCanAccessSupervisorStep(
                connection,
                transaction,
                [normalizedDepartmentLeadUserId.Value],
                cancellationToken);
        }

        var personIdsByUserId = new Dictionary<long, long>();
        foreach (var userId in selectedUserIds)
        {
            personIdsByUserId[userId] = await UpsertPersonRecord(connection, transaction, userId, null, cancellationToken);
        }

        var normalizedDepartmentLeadPersonId = normalizedDepartmentLeadUserId.HasValue
            ? personIdsByUserId[normalizedDepartmentLeadUserId.Value]
            : (long?)null;
        var normalizedRequirementOwnerPersonId = normalizedRequirementOwnerUserId.HasValue
            ? personIdsByUserId[normalizedRequirementOwnerUserId.Value]
            : (long?)null;

        if (normalizedDepartmentLeadUserId is null && normalizedRequirementOwnerUserId is null)
        {
            const string deleteSql = @"
DELETE FROM department_settings
WHERE department_id = @departmentId;";

            await using var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction);
            deleteCommand.Parameters.AddWithValue("departmentId", departmentId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            const string upsertSql = @"
INSERT INTO department_settings (
    department_id,
    department_lead_person_id,
    requirement_approver_person_id,
    updated_at
)
VALUES (
    @departmentId,
    @departmentLeadPersonId,
    @requirementOwnerPersonId,
    NOW()
)
ON CONFLICT (department_id) DO UPDATE
SET department_lead_person_id = EXCLUDED.department_lead_person_id,
    requirement_approver_person_id = EXCLUDED.requirement_approver_person_id,
    updated_at = NOW();";

            await using var upsertCommand = new NpgsqlCommand(upsertSql, connection, transaction);
            upsertCommand.Parameters.AddWithValue("departmentId", departmentId);
            var leadParameter = upsertCommand.Parameters.Add("departmentLeadPersonId", NpgsqlDbType.Bigint);
            leadParameter.Value = (object?)normalizedDepartmentLeadPersonId ?? DBNull.Value;
            var requirementParameter = upsertCommand.Parameters.Add("requirementOwnerPersonId", NpgsqlDbType.Bigint);
            requirementParameter.Value = (object?)normalizedRequirementOwnerPersonId ?? DBNull.Value;
            await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var assignments = await LoadAdminDepartmentAssignments(connection, null, departmentId, cancellationToken);
        return assignments.FirstOrDefault();
    }

    public async Task<AdminResponsibilityOwnerDto?> UpdateResponsibilityOwner(
        int responsibilityId,
        long? appUserId,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        if (responsibilityId <= 0)
        {
            throw new InvalidOperationException("responsibilityId must be greater than zero.");
        }

        var normalizedUserId = NormalizeNullableUserId(appUserId);
        var normalizedDepartmentId = NormalizeNullableDepartmentId(departmentId);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await ResponsibilityExists(connection, transaction, responsibilityId, cancellationToken))
        {
            return null;
        }

        if (normalizedUserId.HasValue)
        {
            await EnsureActiveUserIdsExist(connection, transaction, new[] { normalizedUserId.Value }, cancellationToken);
        }

        if (normalizedDepartmentId.HasValue && !await DepartmentExists(connection, transaction, normalizedDepartmentId.Value, cancellationToken))
        {
            throw new InvalidOperationException("Selected department is invalid.");
        }

        var personId = normalizedUserId.HasValue
            ? await UpsertPersonRecord(connection, transaction, normalizedUserId.Value, null, cancellationToken)
            : (long?)null;

        var effectiveDepartmentId = normalizedDepartmentId
            ?? await LoadResponsibilityOwningDepartmentId(connection, transaction, responsibilityId, cancellationToken);

        if (!personId.HasValue && !effectiveDepartmentId.HasValue)
        {
            throw new InvalidOperationException("A responsibility must be assigned to a person or department.");
        }

        const string upsertSql = @"
INSERT INTO system_responsibilities (
    system_key,
    app_responsibility_id,
    responsible_person_id,
    responsible_department_id,
    updated_at
)
SELECT
    COALESCE(existing.system_key, r.system_key),
    r.id,
    @personId,
    @departmentId,
    NOW()
FROM app_responsibilities r
LEFT JOIN system_responsibilities existing ON existing.app_responsibility_id = r.id
        WHERE r.id = @responsibilityId
          AND COALESCE(existing.system_key, r.system_key) IS NOT NULL
ON CONFLICT (system_key) DO UPDATE
SET
    app_responsibility_id = EXCLUDED.app_responsibility_id,
    responsible_person_id = EXCLUDED.responsible_person_id,
    responsible_department_id = EXCLUDED.responsible_department_id,
    updated_at = NOW();";

        await using (var upsertCommand = new NpgsqlCommand(upsertSql, connection, transaction))
        {
            upsertCommand.Parameters.AddWithValue("responsibilityId", responsibilityId);
            var personParameter = upsertCommand.Parameters.Add("personId", NpgsqlDbType.Bigint);
            personParameter.Value = (object?)personId ?? DBNull.Value;
            var departmentParameter = upsertCommand.Parameters.Add("departmentId", NpgsqlDbType.Integer);
            departmentParameter.Value = (object?)effectiveDepartmentId ?? DBNull.Value;
            var affectedRows = await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
            if (affectedRows == 0)
            {
                throw new InvalidOperationException("Responsibility is not linked to a system key.");
            }
        }

        await transaction.CommitAsync(cancellationToken);
        var assignments = await LoadAdminResponsibilityOwners(connection, null, responsibilityId, cancellationToken);
        return assignments.FirstOrDefault();
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

    private static async Task<bool> DepartmentExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM departments WHERE id = @departmentId);";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentId", departmentId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> RoleExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int roleId,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM app_roles WHERE id = @roleId AND role_kind = 'system');";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("roleId", roleId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task EnsureDepartmentNameAvailable(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string departmentName,
        int? excludeDepartmentId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM departments
    WHERE LOWER(name) = LOWER(@departmentName)
      AND (@excludeDepartmentId IS NULL OR id <> @excludeDepartmentId)
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentName", departmentName);
        var excludeDepartmentIdParameter = command.Parameters.Add("excludeDepartmentId", NpgsqlDbType.Integer);
        excludeDepartmentIdParameter.Value = (object?)excludeDepartmentId ?? DBNull.Value;

        var exists = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        if (exists)
        {
            throw new InvalidOperationException("A department with this name already exists.");
        }
    }

    private static async Task EnsureDepartmentDeletionAllowed(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId,
        CancellationToken cancellationToken)
    {
        const string workflowSql = @"
SELECT EXISTS(
    SELECT 1
    FROM workflows
    WHERE department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            workflowSql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while workflows still reference it.",
            cancellationToken);

        const string departmentSettingsSql = @"
SELECT EXISTS(
    SELECT 1
    FROM department_settings
    WHERE department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            departmentSettingsSql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while department ownership settings still exist.",
            cancellationToken);

        const string userSql = @"
SELECT EXISTS(
    SELECT 1
    FROM app_users
    WHERE department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            userSql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while users are still assigned to it.",
            cancellationToken);

        const string peopleSql = @"
SELECT EXISTS(
    SELECT 1
    FROM people
    WHERE department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            peopleSql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while person records are still assigned to it.",
            cancellationToken);

        const string roleSql = @"
SELECT EXISTS(
    SELECT 1
    FROM app_roles
    WHERE department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            roleSql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while roles still belong to it.",
            cancellationToken);

        const string responsibilitySql = @"
SELECT EXISTS(
    SELECT 1
    FROM app_responsibilities
    WHERE department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            responsibilitySql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while responsibilities still belong to it.",
            cancellationToken);

        const string systemOwnerSql = @"
SELECT EXISTS(
    SELECT 1
    FROM system_responsibilities
    WHERE responsible_department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            systemOwnerSql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while it is assigned as an owner for one or more systems.",
            cancellationToken);

        // LA5: workflow_node_task_specs hat keine owning_department_id-Spalte mehr
        // (Inventur: 0 Eintraege belegt, Spalte gestrichen). Department-Delete kollidiert
        // also nicht mehr mit Task-Specs.
    }

    private static async Task<int?> LoadPositionDepartmentId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int positionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT department_id
FROM app_roles
WHERE id = @positionId
  AND role_kind = 'position';
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("positionId", positionId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null || scalar is DBNull ? null : Convert.ToInt32(scalar);
    }

    private static async Task EnsurePositionNameAvailable(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId,
        string positionName,
        int? excludePositionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT EXISTS(
    SELECT 1
    FROM app_roles
    WHERE department_id = @departmentId
      AND role_kind = 'position'
      AND LOWER(name) = LOWER(@positionName)
      AND (@excludePositionId IS NULL OR id <> @excludePositionId)
);
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentId", departmentId);
        command.Parameters.AddWithValue("positionName", positionName);
        var excludePositionIdParameter = command.Parameters.Add("excludePositionId", NpgsqlDbType.Integer);
        excludePositionIdParameter.Value = (object?)excludePositionId ?? DBNull.Value;

        var exists = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        if (exists)
        {
            throw new InvalidOperationException("A position with this name already exists in this department.");
        }
    }

    private static async Task EnsurePositionDeletionAllowed(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int positionId,
        CancellationToken cancellationToken)
    {
        const string workflowSql = """
SELECT EXISTS(
    SELECT 1
    FROM workflows
    WHERE position_role_id = @positionId
);
""";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            workflowSql,
            "positionId",
            NpgsqlDbType.Integer,
            positionId,
            "Position cannot be deleted while workflows still reference it. Deactivate it instead.",
            cancellationToken);
    }

    private static async Task EnsureResponsibilityDeletionAllowed(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId,
        CancellationToken cancellationToken)
    {
        const string taskAssignmentSql = """
SELECT EXISTS(
    SELECT 1
    FROM task_assignments
    WHERE assignee_responsibility_id = @responsibilityId
);
""";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            taskAssignmentSql,
            "responsibilityId",
            NpgsqlDbType.Integer,
            responsibilityId,
            "Responsibility cannot be deleted while workflow tasks still reference it.",
            cancellationToken);

        var responsibilityKey = await LoadResponsibilityKey(
            connection,
            transaction,
            responsibilityId,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(responsibilityKey))
        {
            const string workflowConfigSql = """
SELECT EXISTS(
    SELECT 1
    FROM workflow_node_configs config
    WHERE STRPOS(
        config.config_json::text,
        '"responsibilityKey": "' || @responsibilityKey || '"'
    ) > 0
);
""";

            await EnsureNoReferencedRows(
                connection,
                transaction,
                workflowConfigSql,
                "responsibilityKey",
                NpgsqlDbType.Text,
                responsibilityKey,
                "Responsibility cannot be deleted while workflow definitions still reference it.",
                cancellationToken);
        }
    }

    private static async Task EnsureNoReferencedRows(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        string parameterName,
        NpgsqlDbType parameterType,
        object parameterValue,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.Add(parameterName, parameterType).Value = parameterValue;

        var exists = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        if (exists)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private static async Task<bool> GroupExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int groupId,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM app_groups WHERE id = @groupId);";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("groupId", groupId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<bool> ResponsibilityExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM app_responsibilities
    WHERE id = @responsibilityId
      AND is_active = TRUE
      AND responsibility_type = 'application'
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
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

    private static async Task<int?> LoadResponsibilityOwningDepartmentId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT department_id
FROM app_responsibilities
WHERE id = @responsibilityId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null || scalar is DBNull ? null : (int?)scalar;
    }

    private static async Task<string?> LoadResponsibilityKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT responsibility_key
FROM app_responsibilities
WHERE id = @responsibilityId
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null || scalar is DBNull ? null : (string?)scalar;
    }

    private static async Task<string> GenerateUniqueResponsibilityKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string responsibilityName,
        CancellationToken cancellationToken)
    {
        return await GenerateUniqueResponsibilityIdentifier(
            connection,
            transaction,
            responsibilityName,
            "admin",
            "responsibility_key",
            120,
            cancellationToken);
    }

    private static async Task<string> GenerateUniqueSystemKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string responsibilityName,
        CancellationToken cancellationToken)
    {
        return await GenerateUniqueResponsibilityIdentifier(
            connection,
            transaction,
            responsibilityName,
            "system",
            "system_key",
            64,
            cancellationToken);
    }

    private static async Task<string> GenerateUniquePositionRoleKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId,
        string positionName,
        CancellationToken cancellationToken)
    {
        var departmentSlug = await LoadDepartmentSlug(connection, transaction, departmentId, cancellationToken);
        var positionSlug = BuildAdminSlug(positionName, "position");

        for (var attempt = 0; attempt < 5; attempt += 1)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var candidate = $"position_{departmentSlug}_{positionSlug}_{suffix}";
            if (candidate.Length > 120)
            {
                candidate = candidate[..120].TrimEnd('_');
            }

            if (!await PositionRoleKeyExists(connection, transaction, candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Position key could not be generated.");
    }

    private static async Task<string> LoadDepartmentSlug(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT name FROM departments WHERE id = @departmentId;";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentId", departmentId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return BuildAdminSlug(Convert.ToString(scalar, CultureInfo.InvariantCulture) ?? "", "department");
    }

    private static async Task<bool> PositionRoleKeyExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string roleKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT EXISTS(
    SELECT 1
    FROM app_roles
    WHERE role_key = @roleKey
);
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("roleKey", roleKey);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<string> GenerateUniqueResponsibilityIdentifier(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sourceName,
        string prefix,
        string columnName,
        int maxLength,
        CancellationToken cancellationToken)
    {
        var slug = BuildResponsibilitySlug(sourceName);

        for (var attempt = 0; attempt < 5; attempt += 1)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var candidate = $"{prefix}_{slug}_{suffix}";
            if (candidate.Length > maxLength)
            {
                candidate = candidate[..maxLength];
            }

            if (!await ResponsibilityIdentifierExists(
                    connection,
                    transaction,
                    columnName,
                    candidate,
                    cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Responsibility key could not be generated.");
    }

    private static async Task<bool> ResponsibilityIdentifierExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string columnName,
        string value,
        CancellationToken cancellationToken)
    {
        var sql = $"""
SELECT EXISTS(
    SELECT 1
    FROM app_responsibilities
    WHERE {columnName} = @value
);
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("value", value);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static string BuildResponsibilitySlug(string sourceName)
        => BuildAdminSlug(sourceName, "responsibility");

    private static string BuildAdminSlug(string sourceName, string fallback)
    {
        var normalized = sourceName.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character <= sbyte.MaxValue && char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (builder.Length == 0 || previousWasSeparator)
            {
                continue;
            }

            builder.Append('_');
            previousWasSeparator = true;
        }

        var slug = builder.ToString().Trim('_');
        if (string.IsNullOrWhiteSpace(slug))
        {
            return fallback;
        }

        return slug.Length <= 40 ? slug : slug[..40].TrimEnd('_');
    }

    private static async Task EnsureRoleIdsExist(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int[] roleIds,
        CancellationToken cancellationToken)
    {
        if (roleIds.Length == 0)
        {
            return;
        }

        var sql = $@"
SELECT COUNT(*)
FROM app_roles
WHERE id = ANY(@roleIds)
  AND is_active = TRUE
  AND role_kind = '{AuthorizationRoles.SystemRoleKind}';";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.Add("roleIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = roleIds;
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken) ?? 0);

        if (count != roleIds.Length)
        {
            throw new InvalidOperationException("One or more roles are invalid or inactive.");
        }
    }

    private static async Task EnsureGroupIdsExist(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int[] groupIds,
        CancellationToken cancellationToken)
    {
        if (groupIds.Length == 0)
        {
            return;
        }

        const string sql = @"
SELECT COUNT(*)
FROM app_groups
WHERE id = ANY(@groupIds)
  AND is_active = TRUE;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.Add("groupIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = groupIds;
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken) ?? 0);

        if (count != groupIds.Length)
        {
            throw new InvalidOperationException("One or more groups are invalid or inactive.");
        }
    }

    public async Task<List<EntraJobTitleDto>> GetDepartmentEntraJobTitles(
        int departmentId,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            return [];
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = """
SELECT DISTINCT di.job_title
FROM directory_identities di
JOIN departments d ON d.name ILIKE di.department_name
WHERE d.id = @departmentId
  AND di.job_title IS NOT NULL
  AND di.job_title <> ''
ORDER BY di.job_title;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("departmentId", departmentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<EntraJobTitleDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new EntraJobTitleDto { JobTitle = reader.GetString(0) });
        }

        return result;
    }

    public async Task<ImportPositionsFromEntraResult> ImportDepartmentPositionsFromEntra(
        int departmentId,
        IReadOnlyList<string> jobTitles,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            throw new InvalidOperationException("departmentId must be greater than zero.");
        }

        var normalized = jobTitles
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            return new ImportPositionsFromEntraResult { Created = 0, Skipped = 0 };
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await DepartmentExists(connection, transaction, departmentId, cancellationToken))
        {
            throw new InvalidOperationException("Selected department is invalid.");
        }

        const string existingSql = """
SELECT name FROM app_roles
WHERE department_id = @departmentId
  AND role_kind = 'position';
""";

        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = new NpgsqlCommand(existingSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("departmentId", departmentId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                existingNames.Add(reader.GetString(0));
            }
        }

        var created = 0;
        var skipped = 0;

        foreach (var title in normalized)
        {
            if (existingNames.Contains(title))
            {
                skipped++;
                continue;
            }

            var roleKey = await GenerateUniquePositionRoleKey(connection, transaction, departmentId, title, cancellationToken);

            const string insertSql = """
INSERT INTO app_roles (department_id, role_key, name, role_kind, is_active)
VALUES (@departmentId, @roleKey, @name, 'position', TRUE);
""";
            await using var insertCmd = new NpgsqlCommand(insertSql, connection, transaction);
            insertCmd.Parameters.AddWithValue("departmentId", departmentId);
            insertCmd.Parameters.AddWithValue("roleKey", roleKey);
            insertCmd.Parameters.AddWithValue("name", title);
            await insertCmd.ExecuteNonQueryAsync(cancellationToken);

            existingNames.Add(title);
            created++;
        }

        await transaction.CommitAsync(cancellationToken);
        return new ImportPositionsFromEntraResult { Created = created, Skipped = skipped };
    }
}
