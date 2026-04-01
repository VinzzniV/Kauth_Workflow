using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace API.Tests;

internal static class DirectorySyncedTestUserHelper
{
    public static async Task<long> EnsureUserAsync(
        string connectionString,
        string email,
        string displayName)
    {
        var entraObjectId = CreateStableGuid($"entra:{email}");
        var externalKey = email;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var userId = await UpsertAppUserAsync(connection, transaction, email, displayName, externalKey, entraObjectId);
        await UpsertDirectoryIdentityAsync(connection, transaction, email, displayName, entraObjectId, userId);

        await transaction.CommitAsync();
        return userId;
    }

    private static async Task<long> UpsertAppUserAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string email,
        string displayName,
        string externalKey,
        Guid entraObjectId)
    {
        const string sql = """
            INSERT INTO app_users (
                external_key,
                display_name,
                email,
                notification_email,
                is_active,
                entra_object_id,
                directory_synced,
                last_directory_synced_at,
                department_source
            )
            VALUES (
                @externalKey,
                @displayName,
                @email,
                @email,
                TRUE,
                @entraObjectId,
                TRUE,
                NOW(),
                'directory'
            )
            ON CONFLICT (email) DO UPDATE
            SET
                external_key = EXCLUDED.external_key,
                display_name = EXCLUDED.display_name,
                notification_email = EXCLUDED.notification_email,
                is_active = TRUE,
                entra_object_id = EXCLUDED.entra_object_id,
                directory_synced = TRUE,
                last_directory_synced_at = NOW(),
                department_source = 'directory'
            RETURNING id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("externalKey", externalKey);
        command.Parameters.AddWithValue("displayName", displayName);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("entraObjectId", entraObjectId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is long longId
            ? longId
            : scalar is int intId
                ? intId
                : throw new InvalidOperationException($"App user '{email}' could not be upserted.");
    }

    private static async Task UpsertDirectoryIdentityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string email,
        string displayName,
        Guid entraObjectId,
        long appUserId)
    {
        const string sql = """
            INSERT INTO directory_identities (
                entra_object_id,
                user_principal_name,
                mail,
                display_name,
                account_enabled,
                source_system,
                is_managed_externally,
                app_user_id,
                last_synced_at
            )
            VALUES (
                @entraObjectId,
                @email,
                @email,
                @displayName,
                TRUE,
                'entra',
                TRUE,
                @appUserId,
                NOW()
            )
            ON CONFLICT (entra_object_id) DO UPDATE
            SET
                user_principal_name = EXCLUDED.user_principal_name,
                mail = EXCLUDED.mail,
                display_name = EXCLUDED.display_name,
                account_enabled = TRUE,
                app_user_id = EXCLUDED.app_user_id,
                last_synced_at = NOW();
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("entraObjectId", entraObjectId);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("displayName", displayName);
        command.Parameters.AddWithValue("appUserId", appUserId);
        await command.ExecuteNonQueryAsync();
    }

    private static Guid CreateStableGuid(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = MD5.HashData(bytes);
        return new Guid(hash);
    }
}
