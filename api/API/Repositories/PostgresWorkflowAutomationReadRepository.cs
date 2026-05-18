using Npgsql;

namespace API;

internal sealed class PostgresWorkflowAutomationReadRepository : IWorkflowAutomationReadRepository
{
    public async Task<AdminListPageDto<ActionDefinitionDto>> GetAdminActionDefinitions(AdminListQuery query, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(LifecycleRuntimeSettingsResolver.GetRequiredConnectionString());
        await connection.OpenAsync(cancellationToken);

        // P1-Hull (Z11-F3): Suche ueber action_key/name, optionale Kategorie-Filterung via ?category
        // (semantisch handler_type), Sort-Whitelist mit Default name asc.
        var orderBy = query.Sort?.Trim().ToLowerInvariant() switch
        {
            "name_desc" => "name DESC, action_key, id",
            "category" => "handler_type ASC, name ASC, id",
            "category_desc" => "handler_type DESC, name ASC, id",
            "id" => "id ASC",
            "id_desc" => "id DESC",
            _ => "name ASC, action_key, id"
        };

        var sql = $@"
SELECT
    id,
    action_key,
    name,
    description,
    handler_type,
    parameter_schema_json::text,
    is_active,
    requires_approval,
    is_idempotent,
    created_at,
    updated_at,
    COUNT(*) OVER() AS total_count
FROM action_definitions
WHERE (@search = '' OR action_key ILIKE @pattern OR name ILIKE @pattern)
ORDER BY {orderBy}
LIMIT @limit OFFSET @offset;";

        var results = new List<ActionDefinitionDto>();
        var total = 0;

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("search", query.NormalizedSearch);
            command.Parameters.AddWithValue("pattern", query.SearchPattern);
            command.Parameters.AddWithValue("limit", query.Limit);
            command.Parameters.AddWithValue("offset", query.Offset);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var handlerType = reader.GetString(4);
                results.Add(new ActionDefinitionDto
                {
                    Id = reader.GetInt64(0),
                    Key = reader.GetString(1),
                    Name = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    HandlerType = handlerType,
                    IsSimulated = handlerType.StartsWith("simulated", StringComparison.OrdinalIgnoreCase),
                    ParameterSchema = reader.IsDBNull(5) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(5)),
                    IsActive = reader.GetBoolean(6),
                    RequiresApproval = reader.GetBoolean(7),
                    IsIdempotent = reader.GetBoolean(8),
                    CreatedAt = reader.GetDateTime(9),
                    UpdatedAt = reader.GetDateTime(10)
                });
                total = reader.GetInt32(11);
            }
        }
        catch (PostgresException ex) when (PostgresWorkflowAutomationOperations.IsMissingWorkflowBuilderAutomationSchema(ex))
        {
            return new AdminListPageDto<ActionDefinitionDto>
            {
                Items = [],
                Total = 0,
                Limit = query.Limit,
                Offset = query.Offset
            };
        }

        return new AdminListPageDto<ActionDefinitionDto>
        {
            Items = results,
            Total = total,
            Limit = query.Limit,
            Offset = query.Offset
        };
    }

    public async Task<IReadOnlyList<AutomationJobDetailDto>> GetAutomationJobs(
        Guid workflowUid,
        CancellationToken cancellationToken = default)
    {
        var page = await GetAutomationJobs(
            workflowUid,
            new CursorPageQuery { Limit = CursorPageQuery.MaxLimit },
            cancellationToken);
        return page.Items;
    }

    public async Task<CursorPageDto<AutomationJobDetailDto>> GetAutomationJobs(
        Guid workflowUid,
        CursorPageQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(LifecycleRuntimeSettingsResolver.GetRequiredConnectionString());
        await connection.OpenAsync(cancellationToken);

        var decoded = query.DecodedCursor;
        var hasCursor = decoded.HasValue;
        var cursorClause = hasCursor
            ? "AND (j.created_at > @cursorTs OR (j.created_at = @cursorTs AND j.id > @cursorId))"
            : "";

        var jobSql = $"""
SELECT
    j.id,
    j.workflow_id,
    j.workflow_node_instance_id,
    n.node_key,
    ad.action_key,
    ad.name,
    wna.execution_order,
    wna.on_error_behavior,
    j.status,
    j.payload_json::text,
    j.available_at,
    j.created_at,
    j.started_at,
    j.completed_at
FROM automation_jobs j
INNER JOIN workflows w ON w.id = j.workflow_id
INNER JOIN workflow_node_actions wna ON wna.id = j.workflow_node_action_id
INNER JOIN action_definitions ad ON ad.id = j.action_definition_id
INNER JOIN workflow_node_instances ni ON ni.id = j.workflow_node_instance_id
INNER JOIN workflow_nodes n ON n.id = ni.workflow_node_id
WHERE w.uid = @workflowUid
{cursorClause}
ORDER BY j.created_at, j.id
LIMIT @limit;
""";

        var jobs = new List<AutomationJobDetailDto>();
        var jobIds = new List<long>();
        await using (var command = new NpgsqlCommand(jobSql, connection))
        {
            command.Parameters.AddWithValue("workflowUid", workflowUid);
            command.Parameters.AddWithValue("limit", query.Limit + 1);
            if (hasCursor)
            {
                command.Parameters.Add("cursorTs", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = decoded!.Value.CreatedAt;
                command.Parameters.AddWithValue("cursorId", decoded!.Value.Id);
            }
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var jobId = reader.GetInt64(0);
                jobIds.Add(jobId);
                jobs.Add(new AutomationJobDetailDto
                {
                    Id = jobId,
                    WorkflowId = reader.GetInt64(1),
                    WorkflowNodeInstanceId = reader.GetInt64(2),
                    NodeKey = reader.GetString(3),
                    ActionKey = reader.GetString(4),
                    ActionName = reader.GetString(5),
                    ExecutionOrder = reader.GetInt32(6),
                    OnErrorBehavior = reader.GetString(7),
                    Status = reader.GetString(8),
                    Payload = reader.IsDBNull(9) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(9)),
                    AvailableAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                    CreatedAt = reader.GetDateTime(11),
                    StartedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                    CompletedAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                    Attempts = new List<AutomationJobAttemptDto>(),
                    Logs = new List<AutomationJobLogDto>()
                });
            }
        }

        if (jobIds.Count == 0)
        {
            return new CursorPageDto<AutomationJobDetailDto>
            {
                Items = jobs,
                HasMore = false,
                NextCursor = null
            };
        }

        var hasMore = jobs.Count > query.Limit;
        if (hasMore)
        {
            var overflowJob = jobs[^1];
            jobs.RemoveAt(jobs.Count - 1);
            jobIds.Remove(overflowJob.Id);
        }

        var jobById = jobs.ToDictionary(job => job.Id);

        // Slice 7: failure_kind mit in den SELECT, damit der Approval-Status-Service
        // Permanent- vs Transient-Failures unterscheiden kann ohne zweiten SQL-Pfad.
        const string attemptSql = """
SELECT
    automation_job_id,
    id,
    attempt_number,
    status,
    error_message,
    started_at,
    completed_at,
    failure_kind
FROM automation_job_attempts
WHERE automation_job_id = ANY(@jobIds)
ORDER BY automation_job_id, attempt_number, id;
""";

        await using (var command = new NpgsqlCommand(attemptSql, connection))
        {
            command.Parameters.AddWithValue("jobIds", jobIds.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!jobById.TryGetValue(reader.GetInt64(0), out var job))
                {
                    continue;
                }

                job.Attempts.Add(new AutomationJobAttemptDto
                {
                    Id = reader.GetInt64(1),
                    AttemptNumber = reader.GetInt32(2),
                    Status = reader.GetString(3),
                    ErrorMessage = reader.IsDBNull(4) ? null : reader.GetString(4),
                    StartedAt = reader.GetDateTime(5),
                    CompletedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    FailureKind = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }
        }

        const string logSql = """
SELECT
    automation_job_id,
    id,
    level,
    message,
    details_json::text,
    created_at
FROM automation_job_logs
WHERE automation_job_id = ANY(@jobIds)
ORDER BY automation_job_id, created_at, id;
""";

        await using (var command = new NpgsqlCommand(logSql, connection))
        {
            command.Parameters.AddWithValue("jobIds", jobIds.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!jobById.TryGetValue(reader.GetInt64(0), out var job))
                {
                    continue;
                }

                job.Logs.Add(new AutomationJobLogDto
                {
                    Id = reader.GetInt64(1),
                    Level = reader.GetString(2),
                    Message = reader.GetString(3),
                    Details = reader.IsDBNull(4) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(4)),
                    CreatedAt = reader.GetDateTime(5)
                });
            }
        }

        var lastJob = jobs.Count > 0 ? jobs[^1] : null;
        return new CursorPageDto<AutomationJobDetailDto>
        {
            Items = jobs,
            HasMore = hasMore,
            NextCursor = hasMore && lastJob is not null
                ? CursorPageQuery.EncodeCursor(lastJob.CreatedAt, lastJob.Id)
                : null
        };
    }

    // Slice 7: Live-Status-Aggregation pro Approval. Filter auf workflow_node_instance_id;
    // kein Paging — ein Bundle hat selten mehr als eine Handvoll Actions, und der
    // Approval-Dialog will alle auf einmal sehen.
    public async Task<IReadOnlyList<AutomationJobDetailDto>> GetAutomationJobsForNodeInstance(
        long workflowNodeInstanceId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(LifecycleRuntimeSettingsResolver.GetRequiredConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string jobSql = """
SELECT
    j.id,
    j.workflow_id,
    j.workflow_node_instance_id,
    n.node_key,
    ad.action_key,
    ad.name,
    wna.execution_order,
    wna.on_error_behavior,
    j.status,
    j.payload_json::text,
    j.available_at,
    j.created_at,
    j.started_at,
    j.completed_at
FROM automation_jobs j
INNER JOIN workflow_node_actions wna ON wna.id = j.workflow_node_action_id
INNER JOIN action_definitions ad ON ad.id = j.action_definition_id
INNER JOIN workflow_node_instances ni ON ni.id = j.workflow_node_instance_id
INNER JOIN workflow_nodes n ON n.id = ni.workflow_node_id
WHERE j.workflow_node_instance_id = @nodeInstanceId
ORDER BY wna.execution_order, j.created_at, j.id;
""";

        var jobs = new List<AutomationJobDetailDto>();
        var jobIds = new List<long>();
        await using (var command = new NpgsqlCommand(jobSql, connection))
        {
            command.Parameters.AddWithValue("nodeInstanceId", workflowNodeInstanceId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var jobId = reader.GetInt64(0);
                jobIds.Add(jobId);
                jobs.Add(new AutomationJobDetailDto
                {
                    Id = jobId,
                    WorkflowId = reader.GetInt64(1),
                    WorkflowNodeInstanceId = reader.GetInt64(2),
                    NodeKey = reader.GetString(3),
                    ActionKey = reader.GetString(4),
                    ActionName = reader.GetString(5),
                    ExecutionOrder = reader.GetInt32(6),
                    OnErrorBehavior = reader.GetString(7),
                    Status = reader.GetString(8),
                    Payload = reader.IsDBNull(9) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(9)),
                    AvailableAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                    CreatedAt = reader.GetDateTime(11),
                    StartedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                    CompletedAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                    Attempts = new List<AutomationJobAttemptDto>(),
                    Logs = new List<AutomationJobLogDto>()
                });
            }
        }

        if (jobIds.Count == 0)
        {
            return jobs;
        }

        var jobById = jobs.ToDictionary(job => job.Id);

        const string attemptSql = """
SELECT
    automation_job_id,
    id,
    attempt_number,
    status,
    error_message,
    started_at,
    completed_at,
    failure_kind
FROM automation_job_attempts
WHERE automation_job_id = ANY(@jobIds)
ORDER BY automation_job_id, attempt_number, id;
""";

        await using (var command = new NpgsqlCommand(attemptSql, connection))
        {
            command.Parameters.AddWithValue("jobIds", jobIds.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!jobById.TryGetValue(reader.GetInt64(0), out var job))
                {
                    continue;
                }

                job.Attempts.Add(new AutomationJobAttemptDto
                {
                    Id = reader.GetInt64(1),
                    AttemptNumber = reader.GetInt32(2),
                    Status = reader.GetString(3),
                    ErrorMessage = reader.IsDBNull(4) ? null : reader.GetString(4),
                    StartedAt = reader.GetDateTime(5),
                    CompletedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    FailureKind = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }
        }

        const string logSql = """
SELECT
    automation_job_id,
    id,
    level,
    message,
    details_json::text,
    created_at
FROM automation_job_logs
WHERE automation_job_id = ANY(@jobIds)
ORDER BY automation_job_id, created_at, id;
""";

        await using (var command = new NpgsqlCommand(logSql, connection))
        {
            command.Parameters.AddWithValue("jobIds", jobIds.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!jobById.TryGetValue(reader.GetInt64(0), out var job))
                {
                    continue;
                }

                job.Logs.Add(new AutomationJobLogDto
                {
                    Id = reader.GetInt64(1),
                    Level = reader.GetString(2),
                    Message = reader.GetString(3),
                    Details = reader.IsDBNull(4) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(4)),
                    CreatedAt = reader.GetDateTime(5)
                });
            }
        }

        return jobs;
    }
}
