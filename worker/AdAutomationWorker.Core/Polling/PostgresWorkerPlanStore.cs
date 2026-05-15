using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;

namespace AdAutomationWorker.Core.Polling;

// Produktive IWorkerPlanStore-Implementierung. Analog PostgresWorkerJobStore, aber fuer
// automation_plan_requests. SKIP LOCKED verhindert, dass zwei Worker denselben Request claimen.
// Stale-Cleanup setzt running-Requests nach 30 s zurueck auf pending (kein Heartbeat-Loop
// noetig: Plan-Calls sind read-only LDAP und typischerweise in Sekunden fertig).
public sealed class PostgresWorkerPlanStore : IWorkerPlanStore
{
    private const string StatusPending = "pending";
    private const string StatusRunning = "running";
    private const string StatusCompleted = "completed";
    private const string StatusFailed = "failed";

    private readonly string connectionString;

    public PostgresWorkerPlanStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
    }

    public async Task<WorkerPlanClaim?> ClaimNextPlanRequestAsync(
        string workerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        const string selectSql = """
            SELECT id, workflow_instance_uid, node_key, action_key, payload_json
            FROM public.automation_plan_requests
            WHERE status = 'pending'
            ORDER BY created_at
            LIMIT 1
            FOR UPDATE SKIP LOCKED
            """;

        long requestId;
        string workflowInstanceUid, nodeKey, actionKey;
        JsonElement payloadJson;

        await using (var cmd = new NpgsqlCommand(selectSql, connection, tx))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                await tx.RollbackAsync(cancellationToken);
                return null;
            }

            requestId = reader.GetInt64(0);
            workflowInstanceUid = reader.GetString(1);
            nodeKey = reader.GetString(2);
            actionKey = reader.GetString(3);
            var payloadRaw = reader.GetString(4);
            payloadJson = JsonSerializer.Deserialize<JsonElement>(payloadRaw);
        }

        const string updateSql = """
            UPDATE public.automation_plan_requests
            SET status = 'running',
                claimed_by = @workerId,
                claimed_at = NOW()
            WHERE id = @id
            """;

        await using (var updateCmd = new NpgsqlCommand(updateSql, connection, tx))
        {
            updateCmd.Parameters.AddWithValue("workerId", workerId);
            updateCmd.Parameters.AddWithValue("id", requestId);
            await updateCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        return new WorkerPlanClaim
        {
            RequestId = requestId,
            WorkflowInstanceUid = workflowInstanceUid,
            NodeKey = nodeKey,
            ActionKey = actionKey,
            PayloadJson = payloadJson,
        };
    }

    public async Task MarkPlanRequestCompletedAsync(
        long requestId, JsonNode planJson, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE public.automation_plan_requests
            SET status = 'completed',
                plan_json = @planJson::jsonb,
                completed_at = NOW()
            WHERE id = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("planJson", planJson.ToJsonString());
        cmd.Parameters.AddWithValue("id", requestId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkPlanRequestFailedAsync(
        long requestId, string errorMessage, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE public.automation_plan_requests
            SET status = 'failed',
                error_message = @errorMessage,
                completed_at = NOW()
            WHERE id = @id
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("errorMessage", errorMessage);
        cmd.Parameters.AddWithValue("id", requestId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> ReleaseStaleClaimsAsync(
        TimeSpan staleTimeout, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE public.automation_plan_requests
            SET status = 'pending',
                claimed_by = NULL,
                claimed_at = NULL
            WHERE status = 'running'
              AND claimed_at < NOW() - @staleTimeout
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("staleTimeout", staleTimeout);
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
