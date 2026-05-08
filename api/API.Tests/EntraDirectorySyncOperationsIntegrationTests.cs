using API.Services.Directory;
using Npgsql;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class EntraDirectorySyncOperationsIntegrationTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=26432;Database=appdb;Username=app;Password=app_pw";

    private static string GetTestConnectionString() =>
        Environment.GetEnvironmentVariable("ONBOARDING_TEST_CONNECTION_STRING") ?? DefaultTestConnectionString;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpsertDirectoryIdentitiesBatchAsync_InsertsNewIdentitiesAndReturnsMapping()
    {
        var connectionString = GetTestConnectionString();
        var operations = new EntraDirectorySyncOperations();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var entraIdA = Guid.NewGuid();
        var entraIdB = Guid.NewGuid();
        var input = new List<(Guid EntraObjectId, EntraDirectoryUser User)>
        {
            (entraIdA, new EntraDirectoryUser(entraIdA.ToString(), $"upsert.a.{entraIdA:N}@dir.local", $"a.{entraIdA:N}@mail.local", "User A", true, "Sync-Dept-Alpha", "12345", null)),
            (entraIdB, new EntraDirectoryUser(entraIdB.ToString(), $"upsert.b.{entraIdB:N}@dir.local", null, "User B", false, null, null, null)),
        };

        try
        {
            var mapping = await operations.UpsertDirectoryIdentitiesBatchAsync(connection, input, CancellationToken.None);

            Assert.Equal(2, mapping.Count);
            Assert.True(mapping.ContainsKey(entraIdA));
            Assert.True(mapping.ContainsKey(entraIdB));

            await using var verify = new NpgsqlCommand(
                "SELECT display_name, mail, account_enabled, department_name, employee_number FROM directory_identities WHERE entra_object_id = @id;",
                connection);
            verify.Parameters.AddWithValue("id", entraIdA);
            await using var reader = await verify.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("User A", reader.GetString(0));
            Assert.False(reader.IsDBNull(1));
            Assert.True(reader.GetBoolean(2));
            Assert.Equal("Sync-Dept-Alpha", reader.GetString(3));
            Assert.Equal(12345, reader.GetInt32(4));
        }
        finally
        {
            await DeleteIdentitiesAsync(connection, [entraIdA, entraIdB]);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpsertDirectoryIdentitiesBatchAsync_UpdatesExistingAndDedupsLastWins()
    {
        var connectionString = GetTestConnectionString();
        var operations = new EntraDirectorySyncOperations();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var entraId = Guid.NewGuid();
        var input1 = new List<(Guid EntraObjectId, EntraDirectoryUser User)>
        {
            (entraId, new EntraDirectoryUser(entraId.ToString(), "first@dir.local", "first@mail.local", "First Name", true, "Initial-Dept", "1", null)),
        };

        try
        {
            var first = await operations.UpsertDirectoryIdentitiesBatchAsync(connection, input1, CancellationToken.None);
            var firstId = first[entraId];

            // Second batch updates the existing identity AND contains a duplicate of the same Entra id.
            // Last entry must win (dedup-last-wins semantic of the dictionary in the implementation).
            var input2 = new List<(Guid EntraObjectId, EntraDirectoryUser User)>
            {
                (entraId, new EntraDirectoryUser(entraId.ToString(), "ignored@dir.local", "ignored@mail.local", "Ignored", true, "Ignored-Dept", "9", null)),
                (entraId, new EntraDirectoryUser(entraId.ToString(), "winning@dir.local", "winning@mail.local", "Winning Name", false, "Winning-Dept", "42", null)),
            };

            var second = await operations.UpsertDirectoryIdentitiesBatchAsync(connection, input2, CancellationToken.None);

            Assert.Single(second);
            Assert.Equal(firstId, second[entraId]);

            await using var verify = new NpgsqlCommand(
                "SELECT display_name, mail, account_enabled, department_name, employee_number, user_principal_name FROM directory_identities WHERE entra_object_id = @id;",
                connection);
            verify.Parameters.AddWithValue("id", entraId);
            await using var reader = await verify.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Winning Name", reader.GetString(0));
            Assert.Equal("winning@mail.local", reader.GetString(1));
            Assert.False(reader.GetBoolean(2));
            Assert.Equal("Winning-Dept", reader.GetString(3));
            Assert.Equal(42, reader.GetInt32(4));
            Assert.Equal("winning@dir.local", reader.GetString(5));
        }
        finally
        {
            await DeleteIdentitiesAsync(connection, [entraId]);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task InsertGroupMembershipsBatchAsync_InsertsAndIsIdempotent()
    {
        var connectionString = GetTestConnectionString();
        var operations = new EntraDirectorySyncOperations();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var entraIdA = Guid.NewGuid();
        var entraIdB = Guid.NewGuid();
        var input = new List<(Guid EntraObjectId, EntraDirectoryUser User)>
        {
            (entraIdA, new EntraDirectoryUser(entraIdA.ToString(), $"m.a.{entraIdA:N}@dir.local", null, "Member A", true, null, null, null)),
            (entraIdB, new EntraDirectoryUser(entraIdB.ToString(), $"m.b.{entraIdB:N}@dir.local", null, "Member B", true, null, null, null)),
        };

        var directoryGroupId = await CreateDirectoryGroupAsync(connection);

        try
        {
            var mapping = await operations.UpsertDirectoryIdentitiesBatchAsync(connection, input, CancellationToken.None);
            var identityIds = new[] { mapping[entraIdA], mapping[entraIdB] };

            await operations.InsertGroupMembershipsBatchAsync(connection, directoryGroupId, identityIds, CancellationToken.None);
            await operations.InsertGroupMembershipsBatchAsync(connection, directoryGroupId, identityIds, CancellationToken.None);

            await using var countCmd = new NpgsqlCommand(
                "SELECT COUNT(*)::int FROM directory_group_members WHERE directory_group_id = @gid;",
                connection);
            countCmd.Parameters.AddWithValue("gid", directoryGroupId);
            var count = (int)(await countCmd.ExecuteScalarAsync() ?? 0);
            Assert.Equal(2, count);

            await operations.InsertGroupMembershipsBatchAsync(connection, directoryGroupId, [], CancellationToken.None);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand(
                "DELETE FROM directory_group_members WHERE directory_group_id = @gid; DELETE FROM directory_groups WHERE id = @gid;",
                connection);
            cleanup.Parameters.AddWithValue("gid", directoryGroupId);
            await cleanup.ExecuteNonQueryAsync();
            await DeleteIdentitiesAsync(connection, [entraIdA, entraIdB]);
        }
    }

    private static async Task<int> CreateDirectoryGroupAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO directory_groups (external_group_id, display_name, description)
            VALUES (@externalGroupId, @displayName, 'Z9-3 integration test')
            RETURNING id;
            """,
            connection);
        command.Parameters.AddWithValue("externalGroupId", Guid.NewGuid());
        command.Parameters.AddWithValue("displayName", $"Z9-3 Membership Group {Guid.NewGuid():N}");
        return (int)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Failed to create directory group."));
    }

    private static async Task DeleteIdentitiesAsync(NpgsqlConnection connection, IReadOnlyList<Guid> entraObjectIds)
    {
        await using var command = new NpgsqlCommand(
            "DELETE FROM directory_identities WHERE entra_object_id = ANY(@ids);",
            connection);
        command.Parameters.Add("ids", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Uuid).Value = entraObjectIds.ToArray();
        await command.ExecuteNonQueryAsync();
    }
}
