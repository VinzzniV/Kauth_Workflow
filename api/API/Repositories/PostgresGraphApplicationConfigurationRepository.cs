using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed class PostgresGraphApplicationConfigurationRepository : IGraphApplicationConfigurationRepository
{
    public async Task<StoredGraphApplicationSettings?> GetSettings(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT tenant_id, client_id, client_secret, updated_at
FROM graph_application_settings
WHERE id = 1
LIMIT 1;";

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new StoredGraphApplicationSettings
            {
                TenantId = reader.IsDBNull(0) ? null : reader.GetString(0),
                ClientId = reader.IsDBNull(1) ? null : reader.GetString(1),
                ClientSecret = reader.IsDBNull(2) ? null : reader.GetString(2),
                UpdatedAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3)
            };
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return null;
        }
    }

    public async Task<StoredGraphApplicationSettings> UpsertSettings(
        GraphApplicationSettingsUpsertModel settings,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
INSERT INTO graph_application_settings (
    id,
    tenant_id,
    client_id,
    client_secret,
    updated_at
)
VALUES (
    1,
    @tenantId,
    @clientId,
    @clientSecret,
    NOW()
)
ON CONFLICT (id) DO UPDATE
SET
    tenant_id = EXCLUDED.tenant_id,
    client_id = EXCLUDED.client_id,
    client_secret = EXCLUDED.client_secret,
    updated_at = NOW()
RETURNING tenant_id, client_id, client_secret, updated_at;";

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            AddNullableText(command, "tenantId", settings.TenantId);
            AddNullableText(command, "clientId", settings.ClientId);
            AddNullableText(command, "clientSecret", settings.ClientSecret);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            return new StoredGraphApplicationSettings
            {
                TenantId = reader.IsDBNull(0) ? null : reader.GetString(0),
                ClientId = reader.IsDBNull(1) ? null : reader.GetString(1),
                ClientSecret = reader.IsDBNull(2) ? null : reader.GetString(2),
                UpdatedAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3)
            };
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            throw new InvalidOperationException(
                "Graph application settings table is missing. Apply db/39_graph_application_settings.sql first.",
                ex);
        }
    }

    private static void AddNullableText(NpgsqlCommand command, string parameterName, string? value)
    {
        var parameter = command.Parameters.Add(parameterName, NpgsqlDbType.Text);
        parameter.Value = (object?)value ?? DBNull.Value;
    }

    private static string GetConnectionString()
    {
        return LifecycleRuntimeSettingsResolver.GetRequiredConnectionString();
    }
}
