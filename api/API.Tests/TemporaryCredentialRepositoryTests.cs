using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class TemporaryCredentialRepositoryTests
{
    private const string EnvVarName = "ONBOARDING_TEST_CONNECTION_STRING";
    private const string SymmetricKey = "test-symmetric-key-with-enough-entropy-XYZ-2026";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReadAdInitialPasswordByVaultIdAsync_Roundtrip_ReturnsOriginalPlainText()
    {
        var (connectionString, nodeInstanceId) = await PrepareScenarioAsync();
        var plainPassword = "P@ssw0rd-Initial-9384";
        var vaultId = await InsertVaultRowAsync(connectionString, nodeInstanceId, plainPassword,
            expiresInSeconds: 3600);

        using var connectionEnv = new ConnectionStringScope(connectionString);
        var repository = new PostgresTemporaryCredentialRepository(new EnvVaultKeyProvider(SymmetricKey));

        var result = await repository.ReadAdInitialPasswordByVaultIdAsync(vaultId);

        Assert.Equal(plainPassword, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReadAdInitialPasswordByVaultIdAsync_Expired_Throws()
    {
        var (connectionString, nodeInstanceId) = await PrepareScenarioAsync();
        var vaultId = await InsertVaultRowAsync(connectionString, nodeInstanceId, "expired-secret",
            expiresInSeconds: -60);

        using var connectionEnv = new ConnectionStringScope(connectionString);
        var repository = new PostgresTemporaryCredentialRepository(new EnvVaultKeyProvider(SymmetricKey));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ReadAdInitialPasswordByVaultIdAsync(vaultId));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReadAdInitialPasswordByVaultIdAsync_UnknownUuid_Throws()
    {
        var (connectionString, _) = await PrepareScenarioAsync();

        using var connectionEnv = new ConnectionStringScope(connectionString);
        var repository = new PostgresTemporaryCredentialRepository(new EnvVaultKeyProvider(SymmetricKey));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ReadAdInitialPasswordByVaultIdAsync(Guid.NewGuid()));
        Assert.Contains("No vault entry", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReadAdInitialPasswordByVaultIdAsync_WrongKey_Throws()
    {
        var (connectionString, nodeInstanceId) = await PrepareScenarioAsync();
        var vaultId = await InsertVaultRowAsync(connectionString, nodeInstanceId, "secret-encrypted-with-key-A",
            expiresInSeconds: 3600, overrideKey: SymmetricKey);

        using var connectionEnv = new ConnectionStringScope(connectionString);
        // Andere Key-Bytewelt im Reader → pgp_sym_decrypt wirft.
        var wrongKey = "different-symmetric-key-with-enough-entropy-1234";
        var repository = new PostgresTemporaryCredentialRepository(new EnvVaultKeyProvider(wrongKey));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ReadAdInitialPasswordByVaultIdAsync(vaultId));
        Assert.Contains("Vault decrypt failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReadAdInitialPasswordByVaultIdAsync_AuditCountersIncrement()
    {
        var (connectionString, nodeInstanceId) = await PrepareScenarioAsync();
        var vaultId = await InsertVaultRowAsync(connectionString, nodeInstanceId, "audit-secret",
            expiresInSeconds: 3600);

        using var connectionEnv = new ConnectionStringScope(connectionString);
        var repository = new PostgresTemporaryCredentialRepository(new EnvVaultKeyProvider(SymmetricKey));

        await repository.ReadAdInitialPasswordByVaultIdAsync(vaultId);
        var (firstRead1, readCount1) = await ReadAuditColumnsAsync(connectionString, vaultId);
        Assert.NotNull(firstRead1);
        Assert.Equal(1, readCount1);

        await repository.ReadAdInitialPasswordByVaultIdAsync(vaultId);
        var (firstRead2, readCount2) = await ReadAuditColumnsAsync(connectionString, vaultId);
        Assert.Equal(firstRead1, firstRead2); // first_read_at unveraendert
        Assert.Equal(2, readCount2);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReadAdInitialPasswordByVaultIdAsync_WrongCredentialType_NoMatch()
    {
        var (connectionString, nodeInstanceId) = await PrepareScenarioAsync();
        var vaultId = Guid.NewGuid();

        // Direkt mit credential_type='other_type' anlegen — Check-Constraint wuerde das blockieren,
        // also droppen wir den Check temporaer. Damit testen wir die Repository-WHERE-Klausel.
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var dropCheck = new NpgsqlCommand(
                "ALTER TABLE public.temporary_credentials DROP CONSTRAINT temporary_credentials_type;", connection);
            await dropCheck.ExecuteNonQueryAsync();

            try
            {
                await using var insert = new NpgsqlCommand(@"
INSERT INTO public.temporary_credentials (id, workflow_node_instance_id, credential_type, encrypted_value, expires_at)
VALUES (@id, @wniid, 'other_type', pgp_sym_encrypt(@plain, @key), NOW() + interval '1 hour');", connection);
                insert.Parameters.Add("id", NpgsqlDbType.Uuid).Value = vaultId;
                insert.Parameters.Add("wniid", NpgsqlDbType.Bigint).Value = nodeInstanceId;
                insert.Parameters.Add("plain", NpgsqlDbType.Text).Value = "other-type-secret";
                insert.Parameters.Add("key", NpgsqlDbType.Text).Value = SymmetricKey;
                await insert.ExecuteNonQueryAsync();
            }
            finally
            {
                await using var restoreCheck = new NpgsqlCommand(@"
ALTER TABLE public.temporary_credentials
    ADD CONSTRAINT temporary_credentials_type CHECK (credential_type IN ('ad_initial_password'));", connection);
                await restoreCheck.ExecuteNonQueryAsync();
            }
        }

        using var connectionEnv = new ConnectionStringScope(connectionString);
        var repository = new PostgresTemporaryCredentialRepository(new EnvVaultKeyProvider(SymmetricKey));

        // Repo filtert auf credential_type='ad_initial_password' → trifft nicht
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.ReadAdInitialPasswordByVaultIdAsync(vaultId));
        Assert.Contains("No vault entry", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(string ConnectionString, long NodeInstanceId)> PrepareScenarioAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVarName)
            ?? throw new InvalidOperationException("Test connection string not set.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = @"
WITH inserted_definition AS (
    INSERT INTO public.workflow_definitions (definition_key, name)
    VALUES ('vault_test_' || gen_random_uuid()::text, 'Vault Test Definition')
    RETURNING id
), inserted_version AS (
    INSERT INTO public.workflow_definition_versions (workflow_definition_id, version_number, name, status)
    SELECT id, 1, 'v1', 'draft' FROM inserted_definition
    RETURNING id
), inserted_node AS (
    INSERT INTO public.workflow_nodes (workflow_definition_version_id, node_key, node_type, title, sort_order)
    SELECT id, 'vault-node-' || gen_random_uuid()::text, 'automation', 'Vault Test Node', 10 FROM inserted_version
    RETURNING id, workflow_definition_version_id
), inserted_workflow AS (
    INSERT INTO public.workflows (
        workflow_definition_id, workflow_definition_version_id, department_id, position_role_id,
        first_name, last_name, employee_number, badge_number, status
    )
    SELECT
        d.id, n.workflow_definition_version_id,
        (SELECT id FROM public.departments WHERE name = 'IT' LIMIT 1),
        (SELECT id FROM public.app_roles WHERE role_key = 'position_developer' LIMIT 1),
        'Vault', 'Tester', 100000 + floor(random() * 800000)::int, 100000 + floor(random() * 800000)::int,
        'in_progress'
    FROM inserted_definition d, inserted_node n
    RETURNING id
)
INSERT INTO public.workflow_node_instances (workflow_id, workflow_node_id, status)
SELECT w.id, n.id, 'active'
FROM inserted_workflow w, inserted_node n
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        var nodeInstanceId = (long)(await command.ExecuteScalarAsync())!;
        return (connectionString, nodeInstanceId);
    }

    private static async Task<Guid> InsertVaultRowAsync(
        string connectionString,
        long workflowNodeInstanceId,
        string plainSecret,
        int expiresInSeconds,
        string? overrideKey = null)
    {
        var key = overrideKey ?? SymmetricKey;
        var vaultId = Guid.NewGuid();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(@"
INSERT INTO public.temporary_credentials (id, workflow_node_instance_id, credential_type, encrypted_value, expires_at)
VALUES (@id, @wniid, 'ad_initial_password', pgp_sym_encrypt(@plain, @key), NOW() + (@seconds || ' seconds')::interval);", connection);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = vaultId;
        command.Parameters.Add("wniid", NpgsqlDbType.Bigint).Value = workflowNodeInstanceId;
        command.Parameters.Add("plain", NpgsqlDbType.Text).Value = plainSecret;
        command.Parameters.Add("key", NpgsqlDbType.Text).Value = key;
        command.Parameters.Add("seconds", NpgsqlDbType.Text).Value = expiresInSeconds.ToString();
        await command.ExecuteNonQueryAsync();

        return vaultId;
    }

    private static async Task<(DateTime? FirstReadAt, int ReadCount)> ReadAuditColumnsAsync(
        string connectionString, Guid vaultId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT first_read_at, read_count FROM public.temporary_credentials WHERE id = @id;", connection);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = vaultId;
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var firstReadAt = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0);
        var readCount = reader.GetInt32(1);
        return (firstReadAt, readCount);
    }

    private sealed class ConnectionStringScope : IDisposable
    {
        private readonly string? previousValue;

        public ConnectionStringScope(string connectionString)
        {
            previousValue = Environment.GetEnvironmentVariable("CONNECTION_STRING");
            Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousValue);
        }
    }
}
