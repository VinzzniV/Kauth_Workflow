using Npgsql;
using NpgsqlTypes;

namespace API;

// Laedt Benutzer, Rollen, Gruppen und Verantwortlichkeiten fuer Demo-Login und Admin-Bereich aus PostgreSQL.
internal sealed class PostgresUserAuthorizationRepository : IUserAuthorizationRepository
{
    private static readonly string DemoEligibleRoleFilterSql = string.Join(
        ",\n          ",
        AuthorizationRoles.ReadAllowed.Select(static roleKey => $"'{roleKey}'"));

    // Baut aus einer aufgeloesten Identitaet das vollstaendige CurrentUser-Modell fuer die Autorisierung.
    public async Task<CurrentUser?> ResolveCurrentUser(ResolvedIdentity identity, CancellationToken cancellationToken = default)
    {
        var userIdFilter = identity.UserId is > 0 ? identity.UserId : null;
        var normalizedExternalKey = Normalize(identity.ExternalKey);
        var normalizedEmail = Normalize(identity.Email);

        if (!userIdFilter.HasValue
            && string.IsNullOrWhiteSpace(normalizedExternalKey)
            && string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string userSql = @"
SELECT
    u.id,
    u.external_key,
    u.display_name,
    u.email,
    u.is_active,
    u.department_id,
    d.name
FROM app_users u
LEFT JOIN departments d ON d.id = u.department_id
WHERE (
    (@userId IS NOT NULL AND u.id = @userId)
    OR (@externalKey IS NOT NULL AND u.external_key = @externalKey)
    OR (@email IS NOT NULL AND LOWER(u.email) = LOWER(@email))
)
ORDER BY CASE
    WHEN @userId IS NOT NULL AND u.id = @userId THEN 0
    WHEN @externalKey IS NOT NULL AND u.external_key = @externalKey THEN 0
    ELSE 1
END,
u.id
LIMIT 1;";

        long userId;
        string? externalKey;
        string displayName;
        string email;
        bool isActive;
        int? departmentId;
        string? departmentName;

        await using (var userCommand = new NpgsqlCommand(userSql, connection))
        {
            var userIdParameter = userCommand.Parameters.Add("userId", NpgsqlDbType.Bigint);
            userIdParameter.Value = (object?)userIdFilter ?? DBNull.Value;

            var externalKeyParameter = userCommand.Parameters.Add("externalKey", NpgsqlDbType.Text);
            externalKeyParameter.Value = (object?)normalizedExternalKey ?? DBNull.Value;

            var emailParameter = userCommand.Parameters.Add("email", NpgsqlDbType.Text);
            emailParameter.Value = (object?)normalizedEmail ?? DBNull.Value;

            await using var reader = await userCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            userId = reader.GetInt64(0);
            externalKey = reader.IsDBNull(1) ? null : reader.GetString(1);
            displayName = reader.GetString(2);
            email = reader.GetString(3);
            isActive = reader.GetBoolean(4);
            departmentId = reader.IsDBNull(5) ? null : reader.GetInt32(5);
            departmentName = reader.IsDBNull(6) ? null : reader.GetString(6);
        }

        var directRoles = await LoadDirectRoles(connection, userId, cancellationToken);
        var groups = await LoadGroups(connection, userId, cancellationToken);
        var groupRoles = await LoadGroupRoles(connection, userId, cancellationToken);
        var effectiveRoles = BuildEffectiveRoles(directRoles, groupRoles);
        var directResponsibilities = await LoadDirectResponsibilities(connection, userId, cancellationToken);
        var groupResponsibilities = await LoadGroupResponsibilities(connection, userId, cancellationToken);
        var effectiveResponsibilities = BuildEffectiveResponsibilities(directResponsibilities, groupResponsibilities);

        return new CurrentUser
        {
            UserId = userId,
            ExternalKey = externalKey,
            DisplayName = displayName,
            Email = email,
            IsActive = isActive,
            DepartmentId = departmentId,
            DepartmentName = departmentName,
            IdentityProvider = identity.Provider,
            Groups = groups,
            DirectRoles = directRoles,
            GroupRoles = groupRoles,
            EffectiveRoles = effectiveRoles,
            DirectResponsibilities = directResponsibilities,
            GroupResponsibilities = groupResponsibilities,
            EffectiveResponsibilities = effectiveResponsibilities
        };
    }

    // Liefert die auswaehlbaren Demo-Benutzer fuer die Login-Seite.
    public async Task<List<DemoLoginUserOptionDto>> GetDemoLoginUsers(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        var sql = $@"
SELECT
    u.id,
    u.external_key,
    u.display_name,
    u.email,
    d.name
FROM app_users u
LEFT JOIN departments d ON d.id = u.department_id
WHERE u.is_active = TRUE
  AND u.external_key IS NOT NULL
  AND BTRIM(u.external_key) <> ''
  AND EXISTS (
      SELECT 1
      FROM (
          SELECT ur.app_role_id AS role_id
          FROM app_user_roles ur
          WHERE ur.app_user_id = u.id
          UNION
          SELECT gr.app_role_id AS role_id
          FROM app_user_groups ug
          JOIN app_group_roles gr ON gr.app_group_id = ug.app_group_id
          WHERE ug.app_user_id = u.id
      ) assigned_roles
      JOIN app_roles r ON r.id = assigned_roles.role_id
      WHERE r.role_key IN (
          {DemoEligibleRoleFilterSql}
      )
      AND r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
  )
ORDER BY u.display_name, u.id;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var users = new List<DemoLoginUserOptionDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new DemoLoginUserOptionDto
            {
                UserId = reader.GetInt64(0),
                Username = reader.GetString(1),
                DisplayName = reader.GetString(2),
                Email = reader.GetString(3),
                DepartmentName = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return users;
    }

    public async Task<List<AdminUserDto>> GetAdminUsers(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminUsers(connection, null, null, cancellationToken);
    }

    public async Task<List<AdminRoleDto>> GetAdminRoles(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminRoles(connection, null, cancellationToken);
    }

    public async Task<List<AdminGroupDto>> GetAdminGroups(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminGroups(connection, null, null, cancellationToken);
    }

    public async Task<List<AdminDepartmentAssignmentDto>> GetAdminDepartmentAssignments(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminDepartmentAssignments(connection, null, null, cancellationToken);
    }

    public async Task<List<AdminResponsibilityOwnerDto>> GetAdminResponsibilityOwners(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminResponsibilityOwners(connection, null, null, cancellationToken);
    }

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

    public async Task<AdminUserDto> CreateUser(
        string? externalKey,
        string displayName,
        string email,
        string? notificationEmail,
        int? departmentId,
        bool isActive,
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
    is_active
)
VALUES (
    @externalKey,
    @departmentId,
    @displayName,
    @email,
    @notificationEmail,
    @isActive
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

        if (await UserHasTaskAssignments(connection, transaction, userId, cancellationToken))
        {
            throw new InvalidOperationException("User is still assigned to workflow tasks and cannot be deleted.");
        }

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
    is_active = @isActive
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
        var userIds = new[] { normalizedDepartmentLeadUserId, normalizedRequirementOwnerUserId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await DepartmentExists(connection, transaction, departmentId, cancellationToken))
        {
            return null;
        }

        await EnsureActiveUserIdsExist(connection, transaction, userIds, cancellationToken);

        var personIdsByUserId = new Dictionary<long, long>();
        foreach (var userId in userIds)
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

    private static async Task<List<AdminRoleDto>> LoadAdminRoles(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT
    r.id,
    r.role_key,
    r.name,
    r.role_kind,
    r.department_id,
    d.name,
    r.is_active
FROM app_roles r
LEFT JOIN departments d ON d.id = r.department_id
WHERE r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
ORDER BY r.role_kind, d.name, r.name, r.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var roles = new List<AdminRoleDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(new AdminRoleDto
            {
                RoleId = reader.GetInt32(0),
                RoleKey = reader.GetString(1),
                RoleName = reader.GetString(2),
                RoleKind = reader.GetString(3),
                DepartmentId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                DepartmentName = reader.IsDBNull(5) ? null : reader.GetString(5),
                IsActive = reader.GetBoolean(6)
            });
        }

        return roles;
    }

    private static async Task<List<AdminGroupDto>> LoadAdminGroups(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int? groupId,
        CancellationToken cancellationToken)
    {
        const string baseSql = @"
SELECT
    g.id,
    g.group_key,
    g.name,
    g.description,
    g.is_active
FROM app_groups g
WHERE (@groupId IS NULL OR g.id = @groupId)
ORDER BY g.name, g.id;";

        var groups = new List<AdminGroupDto>();
        var groupById = new Dictionary<int, AdminGroupDto>();

        await using (var baseCommand = new NpgsqlCommand(baseSql, connection, transaction))
        {
            var baseGroupIdParameter = baseCommand.Parameters.Add("groupId", NpgsqlDbType.Integer);
            baseGroupIdParameter.Value = (object?)groupId ?? DBNull.Value;
            await using var baseReader = await baseCommand.ExecuteReaderAsync(cancellationToken);

            while (await baseReader.ReadAsync(cancellationToken))
            {
                var group = new AdminGroupDto
                {
                    GroupId = baseReader.GetInt32(0),
                    GroupKey = baseReader.GetString(1),
                    GroupName = baseReader.GetString(2),
                    Description = baseReader.IsDBNull(3) ? null : baseReader.GetString(3),
                    IsActive = baseReader.GetBoolean(4),
                    Roles = new List<AdminRoleDto>()
                };

                groups.Add(group);
                groupById[group.GroupId] = group;
            }
        }

        if (groupById.Count == 0)
        {
            return groups;
        }

        var rolesSql = $@"
SELECT
    gr.app_group_id,
    r.id,
    r.role_key,
    r.name,
    r.role_kind,
    r.department_id,
    d.name,
    r.is_active
FROM app_group_roles gr
JOIN app_roles r ON r.id = gr.app_role_id
LEFT JOIN departments d ON d.id = r.department_id
WHERE (@groupId IS NULL OR gr.app_group_id = @groupId)
  AND r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
ORDER BY gr.app_group_id, r.role_kind, d.name, r.name, r.id;";

        await using var rolesCommand = new NpgsqlCommand(rolesSql, connection, transaction);
        var rolesGroupIdParameter = rolesCommand.Parameters.Add("groupId", NpgsqlDbType.Integer);
        rolesGroupIdParameter.Value = (object?)groupId ?? DBNull.Value;
        await using var rolesReader = await rolesCommand.ExecuteReaderAsync(cancellationToken);

        while (await rolesReader.ReadAsync(cancellationToken))
        {
            var currentGroupId = rolesReader.GetInt32(0);
            if (!groupById.TryGetValue(currentGroupId, out var group))
            {
                continue;
            }

            group.Roles.Add(new AdminRoleDto
            {
                RoleId = rolesReader.GetInt32(1),
                RoleKey = rolesReader.GetString(2),
                RoleName = rolesReader.GetString(3),
                RoleKind = rolesReader.GetString(4),
                DepartmentId = rolesReader.IsDBNull(5) ? null : rolesReader.GetInt32(5),
                DepartmentName = rolesReader.IsDBNull(6) ? null : rolesReader.GetString(6),
                IsActive = rolesReader.GetBoolean(7)
            });
        }

        return groups;
    }

    private static async Task<List<AdminDepartmentAssignmentDto>> LoadAdminDepartmentAssignments(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int? departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    d.id,
    d.name,
    lead_user.id,
    lead_user.display_name,
    requirement_user.id,
    requirement_user.display_name,
    ds.updated_at
FROM departments d
LEFT JOIN department_settings ds ON ds.department_id = d.id
LEFT JOIN people lead_person ON lead_person.id = ds.department_lead_person_id
LEFT JOIN app_users lead_user ON lead_user.id = lead_person.app_user_id
LEFT JOIN people requirement_person ON requirement_person.id = ds.requirement_approver_person_id
LEFT JOIN app_users requirement_user ON requirement_user.id = requirement_person.app_user_id
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
                UpdatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
            });
        }

        return assignments;
    }

    private static async Task<List<AdminResponsibilityOwnerDto>> LoadAdminResponsibilityOwners(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int? responsibilityId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    r.id,
    r.responsibility_key,
    r.name,
    r.responsibility_type,
    r.system_key,
    COALESCE(sr.responsible_department_id, r.department_id),
    COALESCE(configured_department.name, owning_department.name),
    u.id,
    u.display_name,
    sr.updated_at
FROM app_responsibilities r
LEFT JOIN system_responsibilities sr ON sr.app_responsibility_id = r.id
LEFT JOIN departments configured_department ON configured_department.id = sr.responsible_department_id
LEFT JOIN departments owning_department ON owning_department.id = r.department_id
LEFT JOIN people p ON p.id = sr.responsible_person_id
LEFT JOIN app_users u ON u.id = p.app_user_id
WHERE r.is_active = TRUE
  AND r.system_key IS NOT NULL
  AND (@responsibilityId IS NULL OR r.id = @responsibilityId)
ORDER BY
    CASE r.responsibility_type
        WHEN 'process' THEN 0
        ELSE 1
    END,
    COALESCE(configured_department.name, owning_department.name, ''),
    r.name,
    r.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var responsibilityIdParameter = command.Parameters.Add("responsibilityId", NpgsqlDbType.Integer);
        responsibilityIdParameter.Value = (object?)responsibilityId ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var assignments = new List<AdminResponsibilityOwnerDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            assignments.Add(new AdminResponsibilityOwnerDto
            {
                ResponsibilityId = reader.GetInt32(0),
                ResponsibilityKey = reader.GetString(1),
                ResponsibilityName = reader.GetString(2),
                ResponsibilityType = reader.GetString(3),
                SystemKey = reader.IsDBNull(4) ? null : reader.GetString(4),
                DepartmentId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                DepartmentName = reader.IsDBNull(6) ? null : reader.GetString(6),
                AppUserId = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                AppUserDisplayName = reader.IsDBNull(8) ? null : reader.GetString(8),
                UpdatedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
            });
        }

        return assignments;
    }

    private static async Task<List<AdminUserDto>> LoadAdminUsers(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long? userId,
        CancellationToken cancellationToken)
    {
        const string baseSql = @"
SELECT
    u.id,
    u.external_key,
    u.display_name,
    u.email,
    u.notification_email,
    u.is_active,
    COALESCE(p.department_id, u.department_id),
    d.name
FROM app_users u
LEFT JOIN people p ON p.app_user_id = u.id
LEFT JOIN departments d ON d.id = COALESCE(p.department_id, u.department_id)
WHERE (@userId IS NULL OR u.id = @userId)
ORDER BY u.display_name, u.id;";

        var users = new List<AdminUserDto>();
        var userById = new Dictionary<long, AdminUserDto>();

        await using (var baseCommand = new NpgsqlCommand(baseSql, connection, transaction))
        {
            var baseUserIdParameter = baseCommand.Parameters.Add("userId", NpgsqlDbType.Bigint);
            baseUserIdParameter.Value = (object?)userId ?? DBNull.Value;
            await using var baseReader = await baseCommand.ExecuteReaderAsync(cancellationToken);

            while (await baseReader.ReadAsync(cancellationToken))
            {
                var user = new AdminUserDto
                {
                    UserId = baseReader.GetInt64(0),
                    ExternalKey = baseReader.IsDBNull(1) ? null : baseReader.GetString(1),
                    DisplayName = baseReader.GetString(2),
                    Email = baseReader.GetString(3),
                    NotificationEmail = baseReader.IsDBNull(4) ? null : baseReader.GetString(4),
                    IsActive = baseReader.GetBoolean(5),
                    DepartmentId = baseReader.IsDBNull(6) ? null : baseReader.GetInt32(6),
                    DepartmentName = baseReader.IsDBNull(7) ? null : baseReader.GetString(7),
                    Roles = new List<AdminRoleDto>(),
                    Groups = new List<AdminGroupRefDto>()
                };

                users.Add(user);
                userById[user.UserId] = user;
            }
        }

        if (userById.Count == 0)
        {
            return users;
        }

        var roleSql = $@"
SELECT
    ur.app_user_id,
    r.id,
    r.role_key,
    r.name,
    r.role_kind,
    r.department_id,
    d.name,
    r.is_active
FROM app_user_roles ur
JOIN app_roles r ON r.id = ur.app_role_id
LEFT JOIN departments d ON d.id = r.department_id
WHERE (@userId IS NULL OR ur.app_user_id = @userId)
  AND r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
ORDER BY ur.app_user_id, r.role_kind, d.name, r.name, r.id;";

        await using (var roleCommand = new NpgsqlCommand(roleSql, connection, transaction))
        {
            var roleUserIdParameter = roleCommand.Parameters.Add("userId", NpgsqlDbType.Bigint);
            roleUserIdParameter.Value = (object?)userId ?? DBNull.Value;
            await using var roleReader = await roleCommand.ExecuteReaderAsync(cancellationToken);

            while (await roleReader.ReadAsync(cancellationToken))
            {
                var currentUserId = roleReader.GetInt64(0);
                if (!userById.TryGetValue(currentUserId, out var user))
                {
                    continue;
                }

                user.Roles.Add(new AdminRoleDto
                {
                    RoleId = roleReader.GetInt32(1),
                    RoleKey = roleReader.GetString(2),
                    RoleName = roleReader.GetString(3),
                    RoleKind = roleReader.GetString(4),
                    DepartmentId = roleReader.IsDBNull(5) ? null : roleReader.GetInt32(5),
                    DepartmentName = roleReader.IsDBNull(6) ? null : roleReader.GetString(6),
                    IsActive = roleReader.GetBoolean(7)
                });
            }
        }

        const string groupSql = @"
SELECT
    ug.app_user_id,
    g.id,
    g.group_key,
    g.name,
    g.description,
    g.is_active
FROM app_user_groups ug
JOIN app_groups g ON g.id = ug.app_group_id
WHERE (@userId IS NULL OR ug.app_user_id = @userId)
ORDER BY ug.app_user_id, g.name, g.id;";

        await using var groupCommand = new NpgsqlCommand(groupSql, connection, transaction);
        var groupUserIdParameter = groupCommand.Parameters.Add("userId", NpgsqlDbType.Bigint);
        groupUserIdParameter.Value = (object?)userId ?? DBNull.Value;
        await using var groupReader = await groupCommand.ExecuteReaderAsync(cancellationToken);

        while (await groupReader.ReadAsync(cancellationToken))
        {
            var currentUserId = groupReader.GetInt64(0);
            if (!userById.TryGetValue(currentUserId, out var user))
            {
                continue;
            }

            user.Groups.Add(new AdminGroupRefDto
            {
                GroupId = groupReader.GetInt32(1),
                GroupKey = groupReader.GetString(2),
                GroupName = groupReader.GetString(3),
                Description = groupReader.IsDBNull(4) ? null : groupReader.GetString(4),
                IsActive = groupReader.GetBoolean(5)
            });
        }

        return users;
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
SELECT COUNT(*)
FROM workflows
WHERE department_id = @departmentId;";

        await using (var workflowCommand = new NpgsqlCommand(workflowSql, connection, transaction))
        {
            workflowCommand.Parameters.AddWithValue("departmentId", departmentId);
            var workflowCount = Convert.ToInt32(await workflowCommand.ExecuteScalarAsync(cancellationToken) ?? 0);
            if (workflowCount > 0)
            {
                throw new InvalidOperationException("Department cannot be deleted while workflows still reference it.");
            }
        }

        const string responsibilitySql = @"
SELECT COUNT(*)
FROM system_responsibilities
WHERE responsible_department_id = @departmentId
  AND responsible_person_id IS NULL;";

        await using var responsibilityCommand = new NpgsqlCommand(responsibilitySql, connection, transaction);
        responsibilityCommand.Parameters.AddWithValue("departmentId", departmentId);
        var responsibilityCount = Convert.ToInt32(await responsibilityCommand.ExecuteScalarAsync(cancellationToken) ?? 0);
        if (responsibilityCount > 0)
        {
            throw new InvalidOperationException("Department cannot be deleted while it is the only assigned owner for one or more systems.");
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

    private static async Task<List<CurrentUserRole>> LoadDirectRoles(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT
    r.id,
    r.role_key,
    r.name,
    r.role_kind
FROM app_user_roles ur
JOIN app_roles r ON r.id = ur.app_role_id
WHERE ur.app_user_id = @userId
  AND r.is_active = TRUE
  AND r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
ORDER BY r.role_kind, r.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var roles = new List<CurrentUserRole>();
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(new CurrentUserRole
            {
                RoleId = reader.GetInt32(0),
                RoleKey = reader.GetString(1),
                RoleName = reader.GetString(2),
                RoleKind = reader.GetString(3),
                AssignmentSource = "direct",
                GroupId = null,
                GroupKey = null
            });
        }

        return roles;
    }

    private static async Task<List<CurrentUserGroup>> LoadGroups(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT
    g.id,
    g.group_key,
    g.name,
    g.description
FROM app_user_groups ug
JOIN app_groups g ON g.id = ug.app_group_id
WHERE ug.app_user_id = @userId
  AND g.is_active = TRUE
ORDER BY g.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var groups = new List<CurrentUserGroup>();
        while (await reader.ReadAsync(cancellationToken))
        {
            groups.Add(new CurrentUserGroup
            {
                GroupId = reader.GetInt32(0),
                GroupKey = reader.GetString(1),
                GroupName = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        return groups;
    }

    private static async Task<List<CurrentUserRole>> LoadGroupRoles(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    g.id,
    g.group_key,
    r.id,
    r.role_key,
    r.name,
    r.role_kind
FROM app_user_groups ug
JOIN app_groups g ON g.id = ug.app_group_id
JOIN app_group_roles gr ON gr.app_group_id = g.id
JOIN app_roles r ON r.id = gr.app_role_id
WHERE ug.app_user_id = @userId
  AND g.is_active = TRUE
  AND r.is_active = TRUE
  AND r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
ORDER BY g.name, r.role_kind, r.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var groupRoles = new List<CurrentUserRole>();
        while (await reader.ReadAsync(cancellationToken))
        {
            groupRoles.Add(new CurrentUserRole
            {
                GroupId = reader.GetInt32(0),
                GroupKey = reader.GetString(1),
                RoleId = reader.GetInt32(2),
                RoleKey = reader.GetString(3),
                RoleName = reader.GetString(4),
                RoleKind = reader.GetString(5),
                AssignmentSource = "group"
            });
        }

        return groupRoles;
    }

    private static List<CurrentUserRole> BuildEffectiveRoles(
        IReadOnlyList<CurrentUserRole> directRoles,
        IReadOnlyList<CurrentUserRole> groupRoles)
    {
        var effectiveByRoleId = new Dictionary<int, CurrentUserRole>();

        foreach (var role in directRoles)
        {
            effectiveByRoleId[role.RoleId] = role;
        }

        foreach (var role in groupRoles)
        {
            if (!effectiveByRoleId.ContainsKey(role.RoleId))
            {
                effectiveByRoleId[role.RoleId] = role;
            }
        }

        return effectiveByRoleId
            .Values
            .OrderBy(role => role.RoleKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.RoleName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.RoleKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<CurrentUserResponsibility>> LoadDirectResponsibilities(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    r.id,
    r.responsibility_key,
    r.name,
    r.responsibility_type,
    r.department_id,
    d.name
FROM app_user_responsibilities ur
JOIN app_responsibilities r ON r.id = ur.app_responsibility_id
LEFT JOIN departments d ON d.id = r.department_id
WHERE ur.app_user_id = @userId
  AND r.is_active = TRUE
ORDER BY r.responsibility_type, r.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var responsibilities = new List<CurrentUserResponsibility>();
        while (await reader.ReadAsync(cancellationToken))
        {
            responsibilities.Add(new CurrentUserResponsibility
            {
                ResponsibilityId = reader.GetInt32(0),
                ResponsibilityKey = reader.GetString(1),
                ResponsibilityName = reader.GetString(2),
                ResponsibilityType = reader.GetString(3),
                DepartmentId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                DepartmentName = reader.IsDBNull(5) ? null : reader.GetString(5),
                AssignmentSource = "direct",
                GroupId = null,
                GroupKey = null
            });
        }

        return responsibilities;
    }

    private static async Task<List<CurrentUserResponsibility>> LoadGroupResponsibilities(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    g.id,
    g.group_key,
    r.id,
    r.responsibility_key,
    r.name,
    r.responsibility_type,
    r.department_id,
    d.name
FROM app_user_groups ug
JOIN app_groups g ON g.id = ug.app_group_id
JOIN app_group_responsibilities gr ON gr.app_group_id = g.id
JOIN app_responsibilities r ON r.id = gr.app_responsibility_id
LEFT JOIN departments d ON d.id = r.department_id
WHERE ug.app_user_id = @userId
  AND g.is_active = TRUE
  AND r.is_active = TRUE
ORDER BY g.name, r.responsibility_type, r.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var responsibilities = new List<CurrentUserResponsibility>();
        while (await reader.ReadAsync(cancellationToken))
        {
            responsibilities.Add(new CurrentUserResponsibility
            {
                GroupId = reader.GetInt32(0),
                GroupKey = reader.GetString(1),
                ResponsibilityId = reader.GetInt32(2),
                ResponsibilityKey = reader.GetString(3),
                ResponsibilityName = reader.GetString(4),
                ResponsibilityType = reader.GetString(5),
                DepartmentId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                DepartmentName = reader.IsDBNull(7) ? null : reader.GetString(7),
                AssignmentSource = "group"
            });
        }

        return responsibilities;
    }

    private static List<CurrentUserResponsibility> BuildEffectiveResponsibilities(
        IReadOnlyList<CurrentUserResponsibility> directResponsibilities,
        IReadOnlyList<CurrentUserResponsibility> groupResponsibilities)
    {
        var effectiveByResponsibilityId = new Dictionary<int, CurrentUserResponsibility>();

        foreach (var responsibility in directResponsibilities)
        {
            effectiveByResponsibilityId[responsibility.ResponsibilityId] = responsibility;
        }

        foreach (var responsibility in groupResponsibilities)
        {
            if (!effectiveByResponsibilityId.ContainsKey(responsibility.ResponsibilityId))
            {
                effectiveByResponsibilityId[responsibility.ResponsibilityId] = responsibility;
            }
        }

        return effectiveByResponsibilityId
            .Values
            .OrderBy(responsibility => responsibility.ResponsibilityType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(responsibility => responsibility.ResponsibilityName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(responsibility => responsibility.ResponsibilityKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("CONNECTION_STRING is not configured.");
        }

        return connectionString;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return normalized;
    }

    private static long? NormalizeNullableUserId(long? value)
    {
        return value is > 0 ? value : null;
    }

    private static int? NormalizeNullableDepartmentId(int? value)
    {
        return value is > 0 ? value : null;
    }
}
