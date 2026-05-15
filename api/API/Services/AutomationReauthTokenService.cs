using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;

namespace API;

// Slice 3 (Admin-Gated-Automation): Re-Auth-Token-Issue + atomare Consume-Operation.
// Klartext-Token wird ausschliesslich an den Caller zurueckgegeben; nur der SHA-256-
// Hash landet in der DB. Replay-Schutz via used_at, 60s TTL.
internal sealed class AutomationReauthTokenService
{
    public const string PurposeAutomationApproval = "automation_approval";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromSeconds(60);
    private const int TokenByteLength = 32;

    private readonly LifecycleRuntimeSettings runtimeSettings;

    public AutomationReauthTokenService(LifecycleRuntimeSettings runtimeSettings)
    {
        this.runtimeSettings = runtimeSettings;
    }

    public async Task<ReauthTokenIssueResult> IssueAsync(
        long userId,
        string purpose,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            purpose = PurposeAutomationApproval;
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        var token = Convert.ToHexString(tokenBytes).ToLowerInvariant();
        var tokenHash = HashToken(token);
        var expiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime);

        const string sql = """
            INSERT INTO public.automation_reauth_tokens (token_hash, user_id, purpose, expires_at)
            VALUES (@tokenHash, @userId, @purpose, @expiresAt)
            RETURNING id
            """;

        await using var connection = new NpgsqlConnection(runtimeSettings.ConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("tokenHash", tokenHash);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("purpose", purpose);
        cmd.Parameters.Add(new NpgsqlParameter("expiresAt", NpgsqlDbType.TimestampTz) { Value = expiresAt });

        var tokenId = (long)(await cmd.ExecuteScalarAsync(ct))!;
        return new ReauthTokenIssueResult(tokenId, token, expiresAt);
    }

    // Atomar in derselben Tx wie die Approval-Insertion: UPDATE … WHERE used_at IS NULL.
    // Returns Token-ID bei Success, null bei jedem Fehlergrund (kein/abgelaufen/used/wrong-user).
    public async Task<long?> ValidateAndConsumeAsync(
        string token,
        long userId,
        string purpose,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = HashToken(token);

        const string sql = """
            UPDATE public.automation_reauth_tokens
            SET used_at = now()
            WHERE token_hash = @tokenHash
              AND user_id = @userId
              AND purpose = @purpose
              AND used_at IS NULL
              AND expires_at > now()
            RETURNING id
            """;

        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("tokenHash", tokenHash);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("purpose", purpose);

        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar as long?;
    }

    private static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

internal sealed record ReauthTokenIssueResult(long Id, string Token, DateTimeOffset ExpiresAt);
