using System.Text.Json;
using AdAutomationWorker.Core.Handlers;
using Npgsql;
using NpgsqlTypes;

namespace AdAutomationWorker.Core.Polling;

// Einzige produktive Implementierung von IWorkerJobStore. Schreibt ausschliesslich gegen die
// drei Spalten/Tabellen, die im GRANT-Konzept der Schritt-1-Sub-Architektur stehen:
//   - SELECT auf View automation_jobs_windows_worker (target_runtime='windows_worker'-Filter)
//   - UPDATE auf Basistabelle automation_jobs (Status, Claim, Heartbeat)
//   - INSERT/UPDATE auf automation_job_attempts (Attempt-Lifecycle)
//   - INSERT auf automation_job_logs (Worker-Observability)
//
// Kein DELETE, kein Zugriff auf andere Tabellen. Der Worker-DB-User hat technisch keinen Grant
// fuer anderes.
public sealed class PostgresWorkerJobStore : IWorkerJobStore
{
    private const string JobStatusPending = "pending";
    private const string JobStatusRunning = "running";
    private const string JobStatusSucceeded = "succeeded";
    private const string JobStatusFailed = "failed";
    private const string AttemptStatusRunning = "running";
    private const string AttemptStatusSucceeded = "succeeded";
    private const string AttemptStatusFailed = "failed";

    private readonly string connectionString;

    public PostgresWorkerJobStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
    }

    public async Task<WorkerJobClaim?> ClaimNextPendingJobAsync(string workerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string claimSql = """
SELECT
    j.id,
    w.uid,
    j.workflow_node_instance_id,
    ad.action_key,
    j.payload_json::text
FROM automation_jobs_windows_worker j
INNER JOIN workflows w ON w.id = j.workflow_id
INNER JOIN action_definitions ad ON ad.id = j.action_definition_id
WHERE j.status = @pendingStatus
  AND j.available_at <= NOW()
ORDER BY j.available_at, j.created_at, j.id
FOR UPDATE SKIP LOCKED
LIMIT 1;
""";

        long jobId;
        Guid workflowUid;
        long workflowNodeInstanceId;
        string actionKey;
        JsonElement payload;

        await using (var command = new NpgsqlCommand(claimSql, connection, transaction))
        {
            command.Parameters.AddWithValue("pendingStatus", JobStatusPending);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            jobId = reader.GetInt64(0);
            workflowUid = reader.GetGuid(1);
            workflowNodeInstanceId = reader.GetInt64(2);
            actionKey = reader.GetString(3);
            payload = reader.IsDBNull(4)
                ? JsonDocument.Parse("{}").RootElement
                : JsonDocument.Parse(reader.GetString(4)).RootElement;
        }

        var nextAttemptNumber = await LoadNextAttemptNumberAsync(connection, transaction, jobId, cancellationToken);

        const string insertAttemptSql = """
INSERT INTO automation_job_attempts (
    automation_job_id,
    attempt_number,
    status,
    started_at
)
VALUES (
    @jobId,
    @attemptNumber,
    @attemptStatus,
    NOW()
);
""";
        await using (var insertAttempt = new NpgsqlCommand(insertAttemptSql, connection, transaction))
        {
            insertAttempt.Parameters.AddWithValue("jobId", jobId);
            insertAttempt.Parameters.AddWithValue("attemptNumber", nextAttemptNumber);
            insertAttempt.Parameters.AddWithValue("attemptStatus", AttemptStatusRunning);
            await insertAttempt.ExecuteNonQueryAsync(cancellationToken);
        }

        const string claimUpdateSql = """
UPDATE automation_jobs
SET
    status = @runningStatus,
    started_at = COALESCE(started_at, NOW()),
    claimed_at = NOW(),
    claimed_by = @workerId,
    heartbeat_at = NOW()
WHERE id = @jobId;
""";
        await using (var updateJob = new NpgsqlCommand(claimUpdateSql, connection, transaction))
        {
            updateJob.Parameters.AddWithValue("runningStatus", JobStatusRunning);
            updateJob.Parameters.AddWithValue("workerId", workerId);
            updateJob.Parameters.AddWithValue("jobId", jobId);
            await updateJob.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new WorkerJobClaim
        {
            JobId = jobId,
            WorkflowUid = workflowUid,
            WorkflowNodeInstanceId = workflowNodeInstanceId,
            ActionKey = actionKey,
            Payload = payload,
            AttemptNumber = nextAttemptNumber
        };
    }

    public async Task<int> ReleaseStaleClaimsAsync(TimeSpan staleTimeout, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
UPDATE automation_jobs
SET
    status = @pendingStatus,
    claimed_at = NULL,
    claimed_by = NULL,
    heartbeat_at = NULL
WHERE target_runtime = 'windows_worker'
  AND status = @runningStatus
  AND (heartbeat_at IS NULL OR heartbeat_at < NOW() - make_interval(secs => @staleSeconds));
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("pendingStatus", JobStatusPending);
        command.Parameters.AddWithValue("runningStatus", JobStatusRunning);
        command.Parameters.AddWithValue("staleSeconds", staleTimeout.TotalSeconds);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> UpdateHeartbeatAsync(long jobId, string workerId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // claimed_by-Filter: Lease-Schutz. Wenn der StaleReleaser einer anderen API-Instanz
        // den Job inzwischen zurueckgesetzt hat, soll dieser Heartbeat NICHT durchgehen.
        const string sql = """
UPDATE automation_jobs
SET heartbeat_at = NOW()
WHERE id = @jobId
  AND claimed_by = @workerId
  AND status = @runningStatus;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("jobId", jobId);
        command.Parameters.AddWithValue("workerId", workerId);
        command.Parameters.AddWithValue("runningStatus", JobStatusRunning);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task MarkJobSucceededAsync(
        long jobId,
        int attemptNumber,
        JsonElement? output,
        IReadOnlyList<WorkerLogEntry> logs,
        CancellationToken cancellationToken)
        => MarkJobCompletionAsync(jobId, attemptNumber, JobStatusSucceeded, AttemptStatusSucceeded, output, errorMessage: null, logs, cancellationToken);

    public Task MarkJobFailedAsync(
        long jobId,
        int attemptNumber,
        string errorMessage,
        IReadOnlyList<WorkerLogEntry> logs,
        CancellationToken cancellationToken)
        => MarkJobCompletionAsync(jobId, attemptNumber, JobStatusFailed, AttemptStatusFailed, output: null, errorMessage, logs, cancellationToken);

    private async Task MarkJobCompletionAsync(
        long jobId,
        int attemptNumber,
        string jobStatus,
        string attemptStatus,
        JsonElement? output,
        string? errorMessage,
        IReadOnlyList<WorkerLogEntry> logs,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateJobSql = """
UPDATE automation_jobs
SET
    status = @jobStatus,
    completed_at = NOW()
WHERE id = @jobId;
""";
        await using (var updateJob = new NpgsqlCommand(updateJobSql, connection, transaction))
        {
            updateJob.Parameters.AddWithValue("jobStatus", jobStatus);
            updateJob.Parameters.AddWithValue("jobId", jobId);
            await updateJob.ExecuteNonQueryAsync(cancellationToken);
        }

        const string updateAttemptSql = """
UPDATE automation_job_attempts
SET
    status = @attemptStatus,
    error_message = @errorMessage,
    output_json = @outputJson,
    completed_at = NOW()
WHERE automation_job_id = @jobId
  AND attempt_number = @attemptNumber;
""";
        await using (var updateAttempt = new NpgsqlCommand(updateAttemptSql, connection, transaction))
        {
            updateAttempt.Parameters.AddWithValue("jobId", jobId);
            updateAttempt.Parameters.AddWithValue("attemptNumber", attemptNumber);
            updateAttempt.Parameters.AddWithValue("attemptStatus", attemptStatus);
            updateAttempt.Parameters.AddWithValue("errorMessage", (object?)errorMessage ?? DBNull.Value);
            updateAttempt.Parameters.Add(new NpgsqlParameter("outputJson", NpgsqlDbType.Jsonb)
            {
                Value = output.HasValue ? JsonSerializer.Serialize(output.Value) : DBNull.Value
            });
            await updateAttempt.ExecuteNonQueryAsync(cancellationToken);
        }

        if (logs.Count > 0)
        {
            const string insertLogSql = """
INSERT INTO automation_job_logs (
    automation_job_id,
    level,
    message,
    details_json
)
VALUES (
    @jobId,
    @level,
    @message,
    @detailsJson
);
""";
            foreach (var log in logs)
            {
                await using var insertLog = new NpgsqlCommand(insertLogSql, connection, transaction);
                insertLog.Parameters.AddWithValue("jobId", jobId);
                insertLog.Parameters.AddWithValue("level", NormalizeLogLevel(log.Level));
                insertLog.Parameters.AddWithValue("message", log.Message.Trim());
                insertLog.Parameters.Add(new NpgsqlParameter("detailsJson", NpgsqlDbType.Jsonb)
                {
                    Value = log.Details.HasValue ? JsonSerializer.Serialize(log.Details.Value) : DBNull.Value
                });
                await insertLog.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<int> LoadNextAttemptNumberAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long jobId,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT COALESCE(MAX(attempt_number), 0) + 1
FROM automation_job_attempts
WHERE automation_job_id = @jobId;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("jobId", jobId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is int v ? v : 1;
    }

    private static string NormalizeLogLevel(string level)
    {
        var normalized = level.Trim().ToLowerInvariant();
        return normalized is "debug" or "info" or "warning" or "error" ? normalized : "info";
    }
}
