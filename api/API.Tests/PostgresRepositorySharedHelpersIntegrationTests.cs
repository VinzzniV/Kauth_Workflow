using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class PostgresRepositorySharedHelpersIntegrationTests
{
    private const string EnvVarName = "ONBOARDING_TEST_CONNECTION_STRING";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LoadActiveUserNotificationRecipientsBulk_MapsRolesAndSkipsInactive()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVarName)
            ?? throw new InvalidOperationException("Test connection string not set.");

        var adminUserId = await DirectorySyncedTestUserHelper.EnsureUserAsync(
            connectionString, "bulk.admin@kauth.local", "Bulk Admin");
        var managerUserId = await DirectorySyncedTestUserHelper.EnsureUserAsync(
            connectionString, "bulk.manager@kauth.local", "Bulk Manager");
        var plainUserId = await DirectorySyncedTestUserHelper.EnsureUserAsync(
            connectionString, "bulk.plain@kauth.local", "Bulk Plain");
        var inactiveUserId = await DirectorySyncedTestUserHelper.EnsureUserAsync(
            connectionString, "bulk.inactive@kauth.local", "Bulk Inactive");

        await using var setup = new NpgsqlConnection(connectionString);
        await setup.OpenAsync();
        await using (var setupTx = await setup.BeginTransactionAsync())
        {
            await AssignRoleAsync(setup, setupTx, adminUserId, "auth_admin");
            await AssignRoleAsync(setup, setupTx, managerUserId, "auth_manager");
            await using (var deactivate = new NpgsqlCommand(
                "UPDATE app_users SET is_active = FALSE WHERE id = @id;", setup, setupTx))
            {
                deactivate.Parameters.AddWithValue("id", inactiveUserId);
                await deactivate.ExecuteNonQueryAsync();
            }
            await setupTx.CommitAsync();
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var unknownId = 9_999_999L;
        var input = new[] { adminUserId, managerUserId, plainUserId, inactiveUserId, unknownId };

        var result = await PostgresRepositorySharedHelpers.LoadActiveUserNotificationRecipientsBulk(
            connection, transaction, input);

        Assert.Equal(3, result.Count);
        Assert.False(result.ContainsKey(inactiveUserId));
        Assert.False(result.ContainsKey(unknownId));

        Assert.Equal("/workflows", result[adminUserId].PreferredPath);
        Assert.Equal("Bulk Admin", result[adminUserId].DisplayName);
        Assert.Equal("bulk.admin@kauth.local", result[adminUserId].Email);
        Assert.Equal("bulk.admin@kauth.local", result[adminUserId].IdentityKey);

        Assert.Equal("/supervisor", result[managerUserId].PreferredPath);
        Assert.Equal("/tasks/my", result[plainUserId].PreferredPath);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LoadActiveUserNotificationRecipientsBulk_EmptyInput_ReturnsEmpty()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVarName)
            ?? throw new InvalidOperationException("Test connection string not set.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var result = await PostgresRepositorySharedHelpers.LoadActiveUserNotificationRecipientsBulk(
            connection, transaction, Array.Empty<long>());

        Assert.Empty(result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LoadActiveUserNotificationRecipientsBulk_PrefersNotificationEmailAndExternalKey()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVarName)
            ?? throw new InvalidOperationException("Test connection string not set.");

        var userId = await DirectorySyncedTestUserHelper.EnsureUserAsync(
            connectionString, "bulk.override@kauth.local", "Bulk Override");

        await using (var setup = new NpgsqlConnection(connectionString))
        {
            await setup.OpenAsync();
            await using var update = new NpgsqlCommand(
                @"UPDATE app_users
                  SET notification_email = '  override.notif@kauth.local  ',
                      external_key = '  ext-key-override  '
                  WHERE id = @id;", setup);
            update.Parameters.AddWithValue("id", userId);
            await update.ExecuteNonQueryAsync();
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var result = await PostgresRepositorySharedHelpers.LoadActiveUserNotificationRecipientsBulk(
            connection, transaction, new[] { userId });

        Assert.True(result.TryGetValue(userId, out var record));
        Assert.Equal("override.notif@kauth.local", record.Email);
        Assert.Equal("ext-key-override", record.IdentityKey);
        Assert.Equal("/tasks/my", record.PreferredPath);
    }

    private static async Task AssignRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId,
        string roleKey)
    {
        const string sql = @"
INSERT INTO app_user_roles (app_user_id, app_role_id)
SELECT @userId, r.id FROM app_roles r WHERE r.role_key = @roleKey
ON CONFLICT DO NOTHING;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("roleKey", roleKey);
        await command.ExecuteNonQueryAsync();
    }
}
