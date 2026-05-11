using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresUserAuthorizationRepository
{
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
}
