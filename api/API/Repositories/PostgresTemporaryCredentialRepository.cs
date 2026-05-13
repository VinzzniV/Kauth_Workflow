using Npgsql;
using NpgsqlTypes;

namespace API;

// Lese-Pfad (Late-Decrypt) auf `temporary_credentials`. Zwei Statements pro Read:
//   1. SELECT mit pgp_sym_decrypt fuer die eigentliche Wertaufloesung
//   2. UPDATE der Audit-Spalten (first_read_at + read_count) -- bewusst KEIN Read-Block,
//      sondern reine Audit-Sicht.
// Bei Read-Failures wird der Audit-Pfad NICHT angelaufen (kein Zaehler-Skew bei nicht
// gefundenen UUIDs oder abgelaufenen Eintraegen).
//
// Migrationspfad-Etappe 9a Schritt 6 Sub-A.
internal sealed class PostgresTemporaryCredentialRepository : ITemporaryCredentialRepository
{
    private const string AdInitialPasswordType = "ad_initial_password";

    private readonly IVaultKeyProvider keyProvider;

    public PostgresTemporaryCredentialRepository(IVaultKeyProvider keyProvider)
    {
        this.keyProvider = keyProvider;
    }

    public async Task<string> ReadAdInitialPasswordByVaultIdAsync(
        Guid credentialVaultId,
        CancellationToken cancellationToken = default)
    {
        var key = keyProvider.GetSymmetricKey();

        await using var connection = new NpgsqlConnection(LifecycleRuntimeSettingsResolver.GetRequiredConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string selectSql = @"
SELECT
    expires_at,
    pgp_sym_decrypt(encrypted_value, @key)::text AS plain
FROM temporary_credentials
WHERE id = @id
  AND credential_type = @type
LIMIT 1;";

        string plain;
        DateTime expiresAtUtc;
        try
        {
            await using var selectCommand = new NpgsqlCommand(selectSql, connection);
            selectCommand.Parameters.Add("id", NpgsqlDbType.Uuid).Value = credentialVaultId;
            selectCommand.Parameters.Add("type", NpgsqlDbType.Varchar).Value = AdInitialPasswordType;
            selectCommand.Parameters.Add("key", NpgsqlDbType.Text).Value = key;

            await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    $"No vault entry found for credentialVaultId '{credentialVaultId}' " +
                    "(credential_type='ad_initial_password'). Either the predecessor CreateAdUserLdaps " +
                    "did not produce a Vault row, the UUID is invalid, or the row was cleaned up.");
            }

            expiresAtUtc = reader.GetDateTime(0);
            plain = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        }
        catch (PostgresException ex)
        {
            throw new InvalidOperationException(
                $"Vault decrypt failed for credentialVaultId '{credentialVaultId}'. " +
                "Likely cause: Vault key in '" + EnvVaultKeyProvider.EnvironmentVariableName +
                "' does not match the key the row was encrypted with on the Worker side. " +
                "Inner: " + ex.MessageText,
                ex);
        }

        if (expiresAtUtc <= DateTime.UtcNow)
        {
            throw new InvalidOperationException(
                $"Vault entry for credentialVaultId '{credentialVaultId}' expired at {expiresAtUtc:O} " +
                "and is no longer readable. The Welcome-Mail cannot be sent; trigger a fresh onboarding workflow.");
        }

        if (string.IsNullOrEmpty(plain))
        {
            throw new InvalidOperationException(
                $"Vault entry for credentialVaultId '{credentialVaultId}' decrypted to an empty value. " +
                "Likely a Worker-side write bug.");
        }

        const string auditSql = @"
UPDATE temporary_credentials
SET first_read_at = COALESCE(first_read_at, NOW()),
    read_count = read_count + 1
WHERE id = @id
  AND credential_type = @type;";

        await using var auditCommand = new NpgsqlCommand(auditSql, connection);
        auditCommand.Parameters.Add("id", NpgsqlDbType.Uuid).Value = credentialVaultId;
        auditCommand.Parameters.Add("type", NpgsqlDbType.Varchar).Value = AdInitialPasswordType;
        await auditCommand.ExecuteNonQueryAsync(cancellationToken);

        return plain;
    }
}
