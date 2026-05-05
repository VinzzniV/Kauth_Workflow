using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace API;

// Eigenstaendiges Runtime-Repository fuer den Workflow-Definitions-Ausfuehrungspfad.
// Bedient die HTTP-Endpunkte fuer Publish/Create/Complete (form|approval|task) sowie
// die Cross-Partial-Aufrufe aus TaskOperations + LifecycleOperations (internal static Wrapper).
internal sealed partial class PostgresWorkflowRuntimeRepository : IWorkflowDefinitionRuntimeRepository
{
    private readonly IWorkflowAuditWriteOperations _auditWrite;
    private readonly IWorkflowStatusCalculationService _statusCalculation;
    private readonly IWorkflowNotificationDispatchOperations _notificationDispatch;
    private readonly IWorkflowAutomationOperations _automation;
    private readonly IWorkflowDefinitionValidationService _workflowDefinitionValidationService;

    public PostgresWorkflowRuntimeRepository()
        : this(
            new PostgresWorkflowAuditWriteOperations(),
            new PostgresWorkflowStatusCalculationService(),
            new PostgresWorkflowNotificationDispatchOperations(),
            new PostgresWorkflowAutomationOperations(),
            new WorkflowDefinitionValidationService())
    {
    }

    internal PostgresWorkflowRuntimeRepository(
        IWorkflowAuditWriteOperations auditWrite,
        IWorkflowStatusCalculationService statusCalculation,
        IWorkflowNotificationDispatchOperations notificationDispatch,
        IWorkflowAutomationOperations automation,
        IWorkflowDefinitionValidationService workflowDefinitionValidationService)
    {
        _auditWrite = auditWrite;
        _statusCalculation = statusCalculation;
        _notificationDispatch = notificationDispatch;
        _automation = automation;
        _workflowDefinitionValidationService = workflowDefinitionValidationService;
    }

    private static string GetConnectionString()
    {
        return LifecycleRuntimeSettingsResolver.GetRequiredConnectionString();
    }

    private const string WorkflowDefinitionRetiredStatus = "retired";
    private const string RuntimeStatusRunning = "running";
    private const string RuntimeStatusWaitingOnNode = "waiting_on_node";
    private const string RuntimeStatusCompleted = "completed";
    private const string RuntimeStatusFailed = "failed";
    private const string RuntimeStatusCancelled = "cancelled";
    private const string NodeInstanceStatusActive = "active";
    internal const string NodeInstanceStatusDone = "done";
    internal const string NodeInstanceStatusFailed = "failed";
    private const string NodeInstanceStatusCancelled = "cancelled";

    public async Task<WorkflowDefinitionVersionDetailDto?> PublishWorkflowDefinitionVersion(long versionId)
    {
        if (versionId <= 0)
        {
            throw new InvalidOperationException("versionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var versionRecord = await PostgresWorkflowRepository.GetWorkflowDefinitionVersionRecord(connection, transaction, versionId);
        if (versionRecord is null)
        {
            return null;
        }

        var detail = await PostgresWorkflowRepository.GetAdminWorkflowDefinitionVersionDetailById(connection, transaction, versionId, _automation, _workflowDefinitionValidationService);
        if (detail is null)
        {
            return null;
        }

        if (string.Equals(versionRecord.Status, PostgresWorkflowRepository.WorkflowDefinitionPublishedStatus, StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync();
            return detail;
        }

        if (!string.Equals(versionRecord.Status, PostgresWorkflowRepository.WorkflowDefinitionDraftStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Workflow definition version '{versionId}' is not publishable because it is in status '{versionRecord.Status}'.");
        }

        if (!detail.CanPublish)
        {
            throw new InvalidOperationException(
                string.Join(" ", detail.ValidationIssues.Select(issue => issue.Message)));
        }

        const string retireExistingSql = """
UPDATE workflow_definition_versions
SET
    status = @retiredStatus,
    updated_at = NOW()
WHERE workflow_definition_id = @workflowDefinitionId
  AND id <> @versionId
  AND status = @publishedStatus;
""";

        await using (var retireCommand = new NpgsqlCommand(retireExistingSql, connection, transaction))
        {
            retireCommand.Parameters.AddWithValue("workflowDefinitionId", versionRecord.WorkflowDefinitionId);
            retireCommand.Parameters.AddWithValue("versionId", versionId);
            retireCommand.Parameters.AddWithValue("publishedStatus", PostgresWorkflowRepository.WorkflowDefinitionPublishedStatus);
            retireCommand.Parameters.AddWithValue("retiredStatus", WorkflowDefinitionRetiredStatus);
            await retireCommand.ExecuteNonQueryAsync();
        }

        const string publishSql = """
UPDATE workflow_definition_versions
SET
    status = @publishedStatus,
    published_at = NOW(),
    updated_at = NOW()
WHERE id = @versionId;
""";

        await using (var publishCommand = new NpgsqlCommand(publishSql, connection, transaction))
        {
            publishCommand.Parameters.AddWithValue("versionId", versionId);
            publishCommand.Parameters.AddWithValue("publishedStatus", PostgresWorkflowRepository.WorkflowDefinitionPublishedStatus);
            await publishCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return await PostgresWorkflowRepository.GetAdminWorkflowDefinitionVersionDetailById(connection, null, versionId, _automation, _workflowDefinitionValidationService);
    }


    public async Task<WorkflowDefinitionRuntimeDetailDto?> GetWorkflowDefinitionRuntimeDetail(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        return await GetWorkflowDefinitionRuntimeDetailInternal(connection, null, workflowUid);
    }

    public async Task<List<WorkflowRuntimeEventDto>> GetWorkflowDefinitionRuntimeEvents(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = """
SELECT
    e.id,
    e.workflow_node_instance_id,
    e.event_type,
    e.payload_json::text,
    e.created_at
FROM workflows w
JOIN workflow_runtime_events e ON e.workflow_id = w.id
WHERE w.uid = @workflowUid
  AND w.workflow_definition_version_id IS NOT NULL
ORDER BY e.created_at, e.id;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workflowUid", workflowUid);
        await using var reader = await command.ExecuteReaderAsync();

        var events = new List<WorkflowRuntimeEventDto>();
        while (await reader.ReadAsync())
        {
            events.Add(new WorkflowRuntimeEventDto
            {
                Id = reader.GetInt64(0),
                WorkflowNodeInstanceId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
                EventType = reader.GetString(2),
                Payload = reader.IsDBNull(3) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(3)),
                CreatedAt = reader.GetDateTime(4)
            });
        }

        return events;
    }

    internal static async Task CompleteRuntimeSupervisorGatekeeperStep(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid workflowUid,
        IReadOnlyList<RequirementSelectionInputDto> selections,
        long actorUserId)
    {
        var workflow = await LoadRuntimeWorkflowHeader(connection, transaction, workflowUid)
            ?? throw new InvalidOperationException("Workflow runtime instance was not found.");
        var graph = await PostgresRepositorySharedHelpers.LoadWorkflowDefinitionGraph(connection, transaction, workflow.WorkflowDefinitionVersionId);
        var gatekeeperNodeExecution = await LoadActiveSupervisorGatekeeperNodeExecution(
            connection,
            transaction,
            workflow.WorkflowId,
            graph,
            workflow.PrimaryLegacyProcessTypeKey,
            workflow.RequiresSupervisorStep);
        if (gatekeeperNodeExecution is null)
        {
            throw new InvalidOperationException("Für diesen Workflow ist kein aktiver Supervisor-Gatekeeper vorhanden.");
        }

        await CompleteRuntimeFormNodeInternal(
            connection,
            transaction,
            workflow,
            graph,
            gatekeeperNodeExecution,
            selections,
            actorUserId);
    }

    internal static async Task CompleteRuntimeFormNodeInternal(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RuntimeWorkflowHeaderRecord workflow,
        WorkflowDefinitionGraphRecord graph,
        WorkflowNodeExecutionRecord activeNodeExecution,
        IReadOnlyList<RequirementSelectionInputDto> selections,
        long actorUserId)
    {
        var legacyProcessTypeKey = WorkflowRuntimeEngine.GetRequiredNodeConfigString(activeNodeExecution.Node, "legacyProcessTypeKey");
        var legacyProcessTypeId = await PostgresWorkflowRepository.ResolveWorkflowDefinitionLegacyProcessTypeId(
            connection,
            transaction,
            legacyProcessTypeKey,
            requireActive: true);
        if (!legacyProcessTypeId.HasValue)
        {
            throw new InvalidOperationException(
                $"Node '{activeNodeExecution.Node.NodeKey}' references unknown or inactive legacyProcessTypeKey '{legacyProcessTypeKey}'.");
        }

        await DeleteWorkflowAnswersForProcessType(connection, transaction, workflow.WorkflowId, legacyProcessTypeId.Value);

        var answerDefinitions = await PostgresWorkflowRepository.LoadAnswerDefinitionRecords(connection, transaction, legacyProcessTypeId.Value);
        var roleDefaults = await PostgresWorkflowRepository.LoadRoleDefaultRecords(connection, transaction, workflow.RoleId, legacyProcessTypeId.Value);
        var persistedAnswers = await PostgresWorkflowRepository.PersistWorkflowAnswers(
            connection,
            transaction,
            workflow.WorkflowId,
            selections,
            answerDefinitions,
            roleDefaults);
        var answersByKey = persistedAnswers
            .GroupBy(answer => answer.AnswerKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        PostgresWorkflowRepository.ValidateSupervisorSelections(answerDefinitions, answersByKey);

        await UpdateNodeInstanceStatus(
            connection,
            transaction,
            activeNodeExecution.NodeInstanceId,
            NodeInstanceStatusDone,
            CreateJsonbPayload(new
            {
                selectionCount = selections.Count,
                legacyProcessTypeKey
            }));

        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflow.WorkflowId,
            activeNodeExecution.NodeInstanceId,
            "form_completed",
            CreateJsonbPayload(new
            {
                nodeKey = activeNodeExecution.Node.NodeKey,
                selectionCount = selections.Count
            }));

        await PostgresRepositorySharedHelpers.InsertAuditEntry(
            connection,
            transaction,
            workflow.WorkflowId,
            null,
            actorUserId,
            "runtime_form_completed",
            null,
            NodeInstanceStatusDone,
            activeNodeExecution.Node.NodeKey);

        await AdvanceRuntimeUntilWaitOrTerminal(
            connection,
            transaction,
            workflow.WorkflowId,
            graph,
            activeNodeExecution.Node,
            await PostgresRepositorySharedHelpers.LoadStoredAnswersByKey(connection, transaction, workflow.WorkflowId),
            actorUserId);
    }

    internal static async Task<WorkflowDefinitionRuntimeDetailDto?> GetWorkflowDefinitionRuntimeDetailInternal(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid workflowUid)
    {
        const string headerSql = """
SELECT
    w.id,
    w.uid,
    d.definition_key,
    d.name,
    v.id,
    v.version_number,
    COALESCE(w.current_runtime_status, @defaultRuntimeStatus),
    w.department_id,
    w.position_role_id,
    w.first_name,
    w.last_name,
    w.employee_number,
    w.badge_number,
    w.target_person_id,
    w.deadline_date,
    w.created_at,
    w.started_at,
    w.completed_at
FROM workflows w
JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
JOIN workflow_definitions d ON d.id = v.workflow_definition_id
WHERE w.uid = @workflowUid
  AND w.workflow_definition_version_id IS NOT NULL
LIMIT 1;
""";

        WorkflowDefinitionRuntimeDetailDto? detail;
        await using (var headerCommand = new NpgsqlCommand(headerSql, connection, transaction))
        {
            headerCommand.Parameters.AddWithValue("workflowUid", workflowUid);
            headerCommand.Parameters.AddWithValue("defaultRuntimeStatus", RuntimeStatusRunning);
            await using var reader = await headerCommand.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            detail = new WorkflowDefinitionRuntimeDetailDto
            {
                WorkflowId = reader.GetInt64(0),
                WorkflowUid = reader.GetGuid(1),
                WorkflowDefinitionKey = reader.GetString(2),
                WorkflowDefinitionName = reader.GetString(3),
                WorkflowDefinitionVersionId = reader.GetInt64(4),
                WorkflowDefinitionVersionNumber = reader.GetInt32(5),
                CurrentRuntimeStatus = reader.GetString(6),
                DepartmentId = reader.GetInt32(7),
                RoleId = reader.GetInt32(8),
                FirstName = reader.IsDBNull(9) ? null : reader.GetString(9),
                LastName = reader.IsDBNull(10) ? null : reader.GetString(10),
                EmployeeNumber = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                BadgeNumber = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                TargetPersonId = reader.IsDBNull(13) ? null : reader.GetInt64(13),
                DeadlineDate = reader.IsDBNull(14) ? null : reader.GetFieldValue<DateOnly>(14),
                CreatedAt = reader.GetDateTime(15),
                StartedAt = reader.IsDBNull(16) ? null : reader.GetDateTime(16),
                CompletedAt = reader.IsDBNull(17) ? null : reader.GetDateTime(17),
                NodeInstances = new List<WorkflowNodeInstanceDto>()
            };
        }

        const string nodeSql = """
SELECT
    ni.id,
    n.node_key,
    n.node_type,
    n.title,
    ni.status,
    ni.started_at,
    ni.completed_at,
    ni.result_json::text
FROM workflows w
JOIN workflow_node_instances ni ON ni.workflow_id = w.id
JOIN workflow_nodes n ON n.id = ni.workflow_node_id
WHERE w.uid = @workflowUid
  AND w.workflow_definition_version_id IS NOT NULL
ORDER BY n.sort_order, n.node_key, ni.id;
""";

        await using (var nodeCommand = new NpgsqlCommand(nodeSql, connection, transaction))
        {
            nodeCommand.Parameters.AddWithValue("workflowUid", workflowUid);
            await using var reader = await nodeCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                detail.NodeInstances.Add(new WorkflowNodeInstanceDto
                {
                    Id = reader.GetInt64(0),
                    NodeKey = reader.GetString(1),
                    NodeType = reader.GetString(2),
                    Title = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Status = reader.GetString(4),
                    StartedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    CompletedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    Result = reader.IsDBNull(7) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(7))
                });
            }
        }

        return detail;
    }

    internal static async Task<PublishedWorkflowDefinitionVersionRecord?> LoadPublishedWorkflowDefinitionVersion(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string workflowDefinitionKey)
    {
        const string sql = """
SELECT
    d.id,
    d.definition_key,
    d.name,
    v.id,
    v.version_number,
    d.id,
    d.definition_key
FROM workflow_definitions d
JOIN workflow_definition_versions v ON v.workflow_definition_id = d.id
WHERE d.definition_key = @definitionKey
  AND v.status = @publishedStatus
ORDER BY v.version_number DESC, v.id DESC
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("definitionKey", workflowDefinitionKey.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("publishedStatus", PostgresWorkflowRepository.WorkflowDefinitionPublishedStatus);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new PublishedWorkflowDefinitionVersionRecord
        {
            WorkflowDefinitionId = reader.GetInt32(0),
            WorkflowDefinitionKey = reader.GetString(1),
            WorkflowDefinitionName = reader.GetString(2),
            VersionId = reader.GetInt64(3),
            VersionNumber = reader.GetInt32(4),
            PrimaryLegacyProcessTypeId = reader.GetInt32(5),
            PrimaryLegacyProcessTypeKey = reader.GetString(6)
        };
    }

    internal static async Task<long> CreateWorkflowNodeInstance(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        WorkflowDefinitionNodeRecord node,
        string status,
        string? resultJson)
    {
        const string existingSql = """
SELECT id
FROM workflow_node_instances
WHERE workflow_id = @workflowId
  AND workflow_node_id = @workflowNodeId
LIMIT 1;
""";

        await using (var existingCommand = new NpgsqlCommand(existingSql, connection, transaction))
        {
            existingCommand.Parameters.AddWithValue("workflowId", workflowId);
            existingCommand.Parameters.AddWithValue("workflowNodeId", node.NodeId);
            var existing = await existingCommand.ExecuteScalarAsync();
            if (existing is long)
            {
                throw new InvalidOperationException(
                    $"Workflow node '{node.NodeKey}' would be re-entered, but node re-entry is not supported in T4.");
            }
        }

        const string insertSql = """
INSERT INTO workflow_node_instances (
    workflow_id,
    workflow_node_id,
    status,
    started_at,
    completed_at,
    result_json
)
VALUES (
    @workflowId,
    @workflowNodeId,
    @status,
    NOW(),
    CASE WHEN @isTerminalStatus THEN NOW() ELSE NULL END,
    @resultJson
)
RETURNING id;
""";

        await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("workflowId", workflowId);
        insertCommand.Parameters.AddWithValue("workflowNodeId", node.NodeId);
        insertCommand.Parameters.AddWithValue("status", status);
        insertCommand.Parameters.AddWithValue(
            "isTerminalStatus",
            string.Equals(status, NodeInstanceStatusDone, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, NodeInstanceStatusFailed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, NodeInstanceStatusCancelled, StringComparison.OrdinalIgnoreCase));
        insertCommand.Parameters.Add(
            new NpgsqlParameter("resultJson", NpgsqlDbType.Jsonb)
            {
                Value = (object?)resultJson ?? DBNull.Value
            });

        var createdId = await insertCommand.ExecuteScalarAsync();
        if (createdId is not long nodeInstanceId)
        {
            throw new InvalidOperationException($"Workflow node instance for '{node.NodeKey}' could not be created.");
        }

        return nodeInstanceId;
    }

    internal static async Task UpdateNodeInstanceStatus(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long nodeInstanceId,
        string status,
        string? resultJson)
    {
        const string sql = """
UPDATE workflow_node_instances
SET
    status = @status,
    completed_at = CASE
        WHEN @isTerminalStatus THEN COALESCE(completed_at, NOW())
        ELSE completed_at
    END,
    result_json = @resultJson
WHERE id = @nodeInstanceId;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("nodeInstanceId", nodeInstanceId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue(
            "isTerminalStatus",
            string.Equals(status, NodeInstanceStatusDone, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, NodeInstanceStatusFailed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, NodeInstanceStatusCancelled, StringComparison.OrdinalIgnoreCase));
        command.Parameters.Add(
            new NpgsqlParameter("resultJson", NpgsqlDbType.Jsonb)
            {
                Value = (object?)resultJson ?? DBNull.Value
            });
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task<long> InsertWorkflowRuntimeEvent(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        long? workflowNodeInstanceId,
        string eventType,
        string? payloadJson)
    {
        const string sql = """
INSERT INTO workflow_runtime_events (
    workflow_id,
    workflow_node_instance_id,
    event_type,
    payload_json
)
VALUES (
    @workflowId,
    @workflowNodeInstanceId,
    @eventType,
    @payloadJson
)
RETURNING id;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.Add("workflowNodeInstanceId", NpgsqlDbType.Bigint).Value =
            (object?)workflowNodeInstanceId ?? DBNull.Value;
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.Add(
            new NpgsqlParameter("payloadJson", NpgsqlDbType.Jsonb)
            {
                Value = (object?)payloadJson ?? DBNull.Value
            });

        var createdId = await command.ExecuteScalarAsync();
        return createdId is long id
            ? id
            : throw new InvalidOperationException($"Runtime event '{eventType}' could not be created.");
    }

    // Schritt 7 Slice 1.6 — Loop auf Engine + Adapter umgestellt.
    //
    // Der ehemalige ~340-Zeilen-Loop ist jetzt eine queue-basierte
    // Drainage: pro completedNode wird ein frischer Snapshot geladen,
    // die pure Engine plant, der Adapter wendet den Plan an, und alle
    // sofort auto-completed Measure-Nodes wandern zurueck in die Queue
    // fuer einen Re-Plan (Q6 Apply-seitige Iteration).
    //
    // Verhalten ist 1:1 zur Vorgaenger-Implementierung:
    // - SetWorkflowRuntimeState("running") passiert in ApplyRuntimePlan.
    // - Failure → ApplyOutcome ruft FailRuntimeWorkflow + Loop bricht ab.
    // - Wait/Completion-Outcome wird vom Adapter geschrieben.
    // - Rekursiver Setup-Auto-Complete-Pfad wird ueber ApplyResult-Liste
    //   getrieben statt als rekursiver Methodenaufruf.
    internal static async Task AdvanceRuntimeUntilWaitOrTerminal(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        WorkflowDefinitionGraphRecord graph,
        WorkflowDefinitionNodeRecord completedNode,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        long? actorUserId)
    {
        var pending = new Queue<long>();
        pending.Enqueue(completedNode.NodeId);

        while (pending.Count > 0)
        {
            var currentNodeId = pending.Dequeue();
            if (!graph.NodeById.TryGetValue(currentNodeId, out var currentNode))
            {
                continue;
            }

            var snapshot = await LoadRuntimeSnapshot(connection, transaction, workflowId, graph, answersByKey);
            var plan = WorkflowRuntimeEngine.Plan(snapshot, currentNode);
            var result = await ApplyRuntimePlan(connection, transaction, snapshot, plan, actorUserId);

            // Failure beendet den Loop — ApplyOutcome hat bereits
            // FailRuntimeWorkflow geschrieben.
            if (plan.Outcome is WorkflowFailureOutcome)
            {
                return;
            }

            foreach (var measureId in result.ImmediatelyCompletedMeasureNodeIds)
            {
                pending.Enqueue(measureId);
            }
        }
    }

    private static async Task<RuntimeWorkflowStatusContextRecord> LoadRuntimeWorkflowStatusContext(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = """
SELECT
    pt.definition_key,
    pt.requires_supervisor_step
FROM workflows w
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
WHERE w.id = @workflowId
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Workflow runtime status context could not be loaded.");
        }

        return new RuntimeWorkflowStatusContextRecord
        {
            PrimaryLegacyProcessTypeKey = reader.GetString(0),
            RequiresSupervisorStep = reader.GetBoolean(1)
        };
    }

    private static async Task<WorkflowNodeExecutionRecord?> LoadActiveSupervisorGatekeeperNodeExecution(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        WorkflowDefinitionGraphRecord graph,
        string? primaryLegacyProcessTypeKey,
        bool requiresSupervisorStep)
    {
        var evaluation = WorkflowRuntimeEngine.EvaluateSupervisorGatekeeper(graph, primaryLegacyProcessTypeKey, requiresSupervisorStep);
        if (!evaluation.IsSatisfied || string.IsNullOrWhiteSpace(evaluation.GatekeeperNodeKey))
        {
            return null;
        }

        const string sql = """
SELECT
    ni.id,
    ni.status,
    n.id,
    n.node_key,
    n.node_type,
    n.title,
    n.sort_order,
    nc.config_json::text
FROM workflow_node_instances ni
JOIN workflow_nodes n ON n.id = ni.workflow_node_id
LEFT JOIN workflow_node_configs nc ON nc.workflow_node_id = n.id
WHERE ni.workflow_id = @workflowId
  AND ni.status = @status
  AND n.node_key = @nodeKey
LIMIT 1
FOR UPDATE OF ni;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("status", NodeInstanceStatusActive);
        command.Parameters.AddWithValue("nodeKey", evaluation.GatekeeperNodeKey);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new WorkflowNodeExecutionRecord
        {
            NodeInstanceId = reader.GetInt64(0),
            Status = reader.GetString(1),
            Node = new WorkflowDefinitionNodeRecord
            {
                NodeId = reader.GetInt64(2),
                NodeKey = reader.GetString(3),
                NodeType = reader.GetString(4),
                Title = reader.IsDBNull(5) ? null : reader.GetString(5),
                SortOrder = reader.GetInt32(6),
                Config = reader.IsDBNull(7) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(7))
            }
        };
    }

    private static async Task<long> CreateRuntimeWorkflowTask(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        long nodeInstanceId,
        WorkflowDefinitionNodeRecord node)
    {
        const string existingSql = """
SELECT id
FROM workflow_tasks
WHERE node_instance_id = @nodeInstanceId
LIMIT 1;
""";

        await using (var existingCommand = new NpgsqlCommand(existingSql, connection, transaction))
        {
            existingCommand.Parameters.AddWithValue("nodeInstanceId", nodeInstanceId);
            var existing = await existingCommand.ExecuteScalarAsync();
            if (existing is long existingTaskId)
            {
                return existingTaskId;
            }
        }

        // LA5: Per-Node-Spec wird ueber die node.NodeId aufgeloest — kein String-Bridge mehr.
        var template = await LoadTaskSpecForNode(connection, transaction, node.NodeId);
        if (template is null)
        {
            throw new InvalidOperationException(
                $"Node '{node.NodeKey}' has no workflow_node_task_specs entry. Definition publishing should ensure one spec per task/approval node.");
        }

        var workflowContext = await LoadWorkflowRuntimeTaskContext(connection, transaction, workflowId);
        var workflowDueAt = await PostgresWorkflowTaskGenerationService.LoadWorkflowDueAtAsync(connection, transaction, workflowId);

        const string insertTaskSql = """
INSERT INTO workflow_tasks (
    workflow_id,
    node_instance_id,
    workflow_node_task_spec_id,
    task_key,
    title,
    category,
    description,
    icon_key,
    process_area_label,
    is_department_phase_task,
    status,
    is_required,
    due_in_days,
    due_at,
    sort_order,
    ready_at
)
VALUES (
    @workflowId,
    @nodeInstanceId,
    @workflowNodeTaskSpecId,
    @taskKey,
    @title,
    @category,
    @description,
    @iconKey,
    @processAreaLabel,
    @isDepartmentPhaseTask,
    'ready',
    @isRequired,
    @dueInDays,
    CASE
        WHEN @workflowDueAt IS NOT NULL AND @dueInDays IS NOT NULL
            THEN LEAST(@workflowDueAt, NOW() + (@dueInDays * INTERVAL '1 day'))
        WHEN @workflowDueAt IS NOT NULL THEN @workflowDueAt
        WHEN @dueInDays IS NOT NULL THEN NOW() + (@dueInDays * INTERVAL '1 day')
        ELSE NULL
    END,
    @sortOrder,
    NOW()
)
RETURNING id;
""";

        long workflowTaskId;
        await using (var insertTaskCommand = new NpgsqlCommand(insertTaskSql, connection, transaction))
        {
            insertTaskCommand.Parameters.AddWithValue("workflowId", workflowId);
            insertTaskCommand.Parameters.AddWithValue("nodeInstanceId", nodeInstanceId);
            insertTaskCommand.Parameters.AddWithValue("workflowNodeTaskSpecId", (long)template.Id);
            insertTaskCommand.Parameters.AddWithValue("taskKey", template.TemplateKey);
            insertTaskCommand.Parameters.AddWithValue("title", template.Title);
            insertTaskCommand.Parameters.AddWithValue("category", template.Category);
            insertTaskCommand.Parameters.AddWithValue("description", template.Description);
            insertTaskCommand.Parameters.AddWithValue("iconKey", template.IconKey);
            insertTaskCommand.Parameters.Add("processAreaLabel", NpgsqlDbType.Varchar).Value =
                (object?)template.ProcessAreaLabel ?? DBNull.Value;
            insertTaskCommand.Parameters.AddWithValue("isDepartmentPhaseTask", template.IsDepartmentPhaseTask);
            insertTaskCommand.Parameters.AddWithValue("isRequired", template.IsRequired);
            insertTaskCommand.Parameters.Add("dueInDays", NpgsqlDbType.Integer).Value =
                (object?)template.DueInDays ?? DBNull.Value;
            insertTaskCommand.Parameters.Add("workflowDueAt", NpgsqlDbType.TimestampTz).Value =
                (object?)workflowDueAt ?? DBNull.Value;
            insertTaskCommand.Parameters.AddWithValue("sortOrder", template.SortOrder);

            var scalar = await insertTaskCommand.ExecuteScalarAsync();
            if (scalar is not long createdTaskId)
            {
                throw new InvalidOperationException("Workflow task could not be created for runtime node.");
            }

            workflowTaskId = createdTaskId;
        }

        long? assigneeUserId;
        int? assigneeResponsibilityId;
        if (string.Equals(node.NodeType, "approval", StringComparison.OrdinalIgnoreCase))
        {
            var supervisorAssignment = await PostgresRepositorySharedHelpers.ResolveDepartmentRequirementSelectionAssignment(
                connection,
                transaction,
                workflowContext.DepartmentId);
            assigneeUserId = supervisorAssignment.UserId;
            assigneeResponsibilityId = assigneeUserId.HasValue ? null : supervisorAssignment.ResponsibilityId;
        }
        else
        {
            assigneeResponsibilityId = template.DefaultResponsibilityId;
            assigneeUserId = assigneeResponsibilityId.HasValue
                ? await PostgresRepositorySharedHelpers.ResolvePrimaryAssigneeUserId(
                    connection,
                    transaction,
                    assigneeResponsibilityId.Value,
                    workflowContext.DepartmentId)
                : null;
        }

        if (!assigneeUserId.HasValue && !assigneeResponsibilityId.HasValue)
        {
            throw new InvalidOperationException(
                $"Node '{node.NodeKey}' references task template '{template.TemplateKey}' without a resolvable assignment.");
        }

        var assignmentType = assigneeUserId.HasValue ? "user" : "responsibility";
        var storedAssigneeResponsibilityId = assignmentType == "responsibility"
            ? assigneeResponsibilityId
            : null;

        const string insertAssignmentSql = """
INSERT INTO task_assignments (
    workflow_task_id,
    assignee_user_id,
    assignee_responsibility_id,
    assignment_type,
    is_primary
)
VALUES (
    @workflowTaskId,
    @assigneeUserId,
    @assigneeResponsibilityId,
    @assignmentType,
    TRUE
);
""";

        await using var insertAssignmentCommand = new NpgsqlCommand(insertAssignmentSql, connection, transaction);
        insertAssignmentCommand.Parameters.AddWithValue("workflowTaskId", workflowTaskId);
        insertAssignmentCommand.Parameters.AddWithValue("assigneeUserId", (object?)assigneeUserId ?? DBNull.Value);
        insertAssignmentCommand.Parameters.AddWithValue(
            "assigneeResponsibilityId",
            (object?)storedAssigneeResponsibilityId ?? DBNull.Value);
        insertAssignmentCommand.Parameters.AddWithValue(
            "assignmentType",
            assignmentType);
        await insertAssignmentCommand.ExecuteNonQueryAsync();

        return workflowTaskId;
    }

    private static async Task EnsureRuntimeSetupTasksGenerated(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        WorkflowDefinitionNodeRecord node,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        long? actorUserId)
    {
        if (await WorkflowHasGeneratedSetupTasks(connection, transaction, workflowId))
        {
            return;
        }

        var workflowContext = await LoadWorkflowRuntimeTaskContext(connection, transaction, workflowId);
        var generatedTaskCount = await PostgresWorkflowTaskGenerationService.GenerateWorkflowTasksAsync(
            connection,
            transaction,
            workflowId,
            workflowContext.DepartmentId,
            answersByKey,
            TaskGenerationStage.Full);

        if (generatedTaskCount > 0)
        {
            await InsertWorkflowRuntimeEvent(
                connection,
                transaction,
                workflowId,
                null,
                "setup_tasks_generated",
                CreateJsonbPayload(new
                {
                    nodeKey = node.NodeKey,
                    generatedTaskCount
                }));

            await PostgresRepositorySharedHelpers.InsertAuditEntry(
                connection,
                transaction,
                workflowId,
                null,
                actorUserId,
                "tasks_generated",
                null,
                null,
                $"{generatedTaskCount} Aufgabe(n) in Setup-Phase erstellt");
        }

        await PostgresWorkflowStatusCalculationService.RecalculateWorkflowTaskAvailabilityAsync(connection, transaction, workflowId);
        await PostgresWorkflowStatusCalculationService.RecalculateAndPersistWorkflowStatusAsync(connection, transaction, workflowId, actorUserId);
    }

    private static async Task<bool> WorkflowHasGeneratedSetupTasks(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = """
SELECT EXISTS(
    SELECT 1
    FROM workflow_tasks
    WHERE workflow_id = @workflowId
      AND node_instance_id IS NULL
);
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    internal static async Task<bool> TryCompleteRuntimeSetupNodeIfReady(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        WorkflowDefinitionGraphRecord graph,
        WorkflowNodeExecutionRecord activeNodeExecution,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        long? actorUserId)
    {
        if (!await AreRuntimeSetupTasksCompleted(connection, transaction, workflowId))
        {
            return false;
        }

        await UpdateNodeInstanceStatus(
            connection,
            transaction,
            activeNodeExecution.NodeInstanceId,
            NodeInstanceStatusDone,
            CreateJsonbPayload(new { completedBy = "task_completion" }));

        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflowId,
            activeNodeExecution.NodeInstanceId,
            "setup_completed",
            CreateJsonbPayload(new { nodeKey = activeNodeExecution.Node.NodeKey }));

        await PostgresRepositorySharedHelpers.InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            null,
            actorUserId,
            "runtime_setup_completed",
            null,
            NodeInstanceStatusDone,
            activeNodeExecution.Node.NodeKey);

        await AdvanceRuntimeUntilWaitOrTerminal(
            connection,
            transaction,
            workflowId,
            graph,
            activeNodeExecution.Node,
            answersByKey,
            actorUserId);
        return true;
    }

    private static async Task<bool> AreRuntimeSetupTasksCompleted(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = """
SELECT status, is_required
FROM workflow_tasks
WHERE workflow_id = @workflowId
  AND node_instance_id IS NULL;
""";

        var taskStates = new List<(string Status, bool IsRequired)>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                taskStates.Add((reader.GetString(0), reader.GetBoolean(1)));
            }
        }

        if (taskStates.Count == 0)
        {
            return true;
        }

        var relevantTaskStates = taskStates
            .Where(task => task.IsRequired)
            .Select(task => task.Status)
            .ToList();
        if (relevantTaskStates.Count == 0)
        {
            relevantTaskStates = taskStates
                .Select(task => task.Status)
                .ToList();
        }

        return relevantTaskStates.All(status =>
            string.Equals(status, "done", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<WorkflowNodeExecutionRecord?> LoadActiveRuntimeNodeExecutionByType(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        string nodeType)
    {
        const string sql = """
SELECT
    ni.id,
    ni.status,
    n.id,
    n.node_key,
    n.node_type,
    n.title,
    n.sort_order,
    nc.config_json::text
FROM workflow_node_instances ni
JOIN workflow_nodes n ON n.id = ni.workflow_node_id
LEFT JOIN workflow_node_configs nc ON nc.workflow_node_id = n.id
WHERE ni.workflow_id = @workflowId
  AND ni.status = @status
  AND n.node_type = @nodeType
LIMIT 1
FOR UPDATE OF ni;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("status", NodeInstanceStatusActive);
        command.Parameters.AddWithValue("nodeType", nodeType);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new WorkflowNodeExecutionRecord
        {
            NodeInstanceId = reader.GetInt64(0),
            Status = reader.GetString(1),
            Node = new WorkflowDefinitionNodeRecord
            {
                NodeId = reader.GetInt64(2),
                NodeKey = reader.GetString(3),
                NodeType = reader.GetString(4),
                Title = reader.IsDBNull(5) ? null : reader.GetString(5),
                SortOrder = reader.GetInt32(6),
                Config = reader.IsDBNull(7) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(7))
            }
        };
    }

    internal static async Task<WorkflowNodeExecutionRecord?> LoadActiveRuntimeMeasureNodeExecution(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = """
SELECT
    ni.id,
    ni.status,
    n.id,
    n.node_key,
    n.node_type,
    n.title,
    n.sort_order,
    nc.config_json::text
FROM workflow_node_instances ni
JOIN workflow_nodes n ON n.id = ni.workflow_node_id
LEFT JOIN workflow_node_configs nc ON nc.workflow_node_id = n.id
WHERE ni.workflow_id = @workflowId
  AND ni.status = @status
  AND n.node_type = ANY(@nodeTypes)
LIMIT 1
FOR UPDATE OF ni;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("status", NodeInstanceStatusActive);
        command.Parameters.AddWithValue("nodeTypes", MeasureGenerationNodeTypes);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new WorkflowNodeExecutionRecord
        {
            NodeInstanceId = reader.GetInt64(0),
            Status = reader.GetString(1),
            Node = new WorkflowDefinitionNodeRecord
            {
                NodeId = reader.GetInt64(2),
                NodeKey = reader.GetString(3),
                NodeType = reader.GetString(4),
                Title = reader.IsDBNull(5) ? null : reader.GetString(5),
                SortOrder = reader.GetInt32(6),
                Config = reader.IsDBNull(7) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(7))
            }
        };
    }

    internal static async Task TryAdvanceRuntimeSetupFromTaskStatusUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        Guid workflowUid,
        long actorUserId)
    {
        var workflow = await LoadRuntimeWorkflowHeader(connection, transaction, workflowUid);
        if (workflow is null)
        {
            return;
        }

        var activeSetupNode = await LoadActiveRuntimeMeasureNodeExecution(
            connection,
            transaction,
            workflowId);
        if (activeSetupNode is null)
        {
            return;
        }

        var graph = await PostgresRepositorySharedHelpers.LoadWorkflowDefinitionGraph(connection, transaction, workflow.WorkflowDefinitionVersionId);
        await TryCompleteRuntimeSetupNodeIfReady(
            connection,
            transaction,
            workflowId,
            graph,
            activeSetupNode,
            await PostgresRepositorySharedHelpers.LoadStoredAnswersByKey(connection, transaction, workflowId),
            actorUserId);
    }

    private static readonly string[] MeasureGenerationNodeTypes =
    [
        "measure_provision",
        "measure_deprovision",
        "measure_change",
        "measure_rename"
    ];

    private static bool IsMeasureGenerationNodeType(string? nodeType)
    {
        return !string.IsNullOrWhiteSpace(nodeType)
               && MeasureGenerationNodeTypes.Contains(nodeType.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    internal static async Task<RuntimeWorkflowHeaderRecord?> LoadRuntimeWorkflowHeader(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid workflowUid)
    {
        const string sql = """
SELECT
    w.id,
    w.uid,
    w.workflow_definition_version_id,
    w.position_role_id,
    pt.definition_key,
    pt.requires_supervisor_step,
    COALESCE(w.current_runtime_status, @defaultRuntimeStatus)
FROM workflows w
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
WHERE w.uid = @workflowUid
  AND w.workflow_definition_version_id IS NOT NULL
LIMIT 1
FOR UPDATE OF w;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowUid", workflowUid);
        command.Parameters.AddWithValue("defaultRuntimeStatus", RuntimeStatusRunning);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new RuntimeWorkflowHeaderRecord
        {
            WorkflowId = reader.GetInt64(0),
            WorkflowUid = reader.GetGuid(1),
            WorkflowDefinitionVersionId = reader.GetInt64(2),
            RoleId = reader.GetInt32(3),
            PrimaryLegacyProcessTypeKey = reader.GetString(4),
            RequiresSupervisorStep = reader.GetBoolean(5),
            CurrentRuntimeStatus = reader.GetString(6)
        };
    }

    private static async Task<RuntimeWorkflowHeaderRecord> LoadRuntimeWorkflowHeaderByWorkflowId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = """
SELECT
    w.id,
    w.uid,
    w.workflow_definition_version_id,
    w.position_role_id,
    pt.definition_key,
    pt.requires_supervisor_step,
    COALESCE(w.current_runtime_status, @defaultRuntimeStatus)
FROM workflows w
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
WHERE w.id = @workflowId
  AND w.workflow_definition_version_id IS NOT NULL
LIMIT 1
FOR UPDATE OF w;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("defaultRuntimeStatus", RuntimeStatusRunning);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Workflow runtime instance was not found.");
        }

        return new RuntimeWorkflowHeaderRecord
        {
            WorkflowId = reader.GetInt64(0),
            WorkflowUid = reader.GetGuid(1),
            WorkflowDefinitionVersionId = reader.GetInt64(2),
            RoleId = reader.GetInt32(3),
            PrimaryLegacyProcessTypeKey = reader.GetString(4),
            RequiresSupervisorStep = reader.GetBoolean(5),
            CurrentRuntimeStatus = reader.GetString(6)
        };
    }

    internal static async Task<long?> LoadWorkflowTaskIdByNodeInstanceId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long nodeInstanceId)
    {
        const string sql = """
SELECT id
FROM workflow_tasks
WHERE node_instance_id = @nodeInstanceId
LIMIT 1
FOR UPDATE;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("nodeInstanceId", nodeInstanceId);
        var scalar = await command.ExecuteScalarAsync();
        return scalar is long taskId ? taskId : null;
    }

    // Fuehrt die Runtime-Seite eines Task-Node-Abschlusses aus (Status setzen, Event, Audit, Engine-Loop).
    // Wird vom Lifecycle-Service ueber `IWorkflowLifecycleScopedRepository.CompleteRuntimeTaskNodeInScope` aufgerufen.
    internal static async Task CompleteTaskNodeRuntimeSide(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        Guid workflowUid,
        long nodeInstanceId,
        long actorUserId,
        string? comment = null)
    {
        var workflow = await LoadRuntimeWorkflowHeader(connection, transaction, workflowUid)
            ?? throw new InvalidOperationException("Workflow runtime instance was not found.");
        var graph = await PostgresRepositorySharedHelpers.LoadWorkflowDefinitionGraph(connection, transaction, workflow.WorkflowDefinitionVersionId);
        var nodeExecution = await LoadNodeExecutionForUpdate(connection, transaction, workflowId, nodeInstanceId);
        EnsureActiveRuntimeNode(nodeExecution, "task");
        var activeNodeExecution = nodeExecution!;

        await UpdateNodeInstanceStatus(
            connection,
            transaction,
            activeNodeExecution.NodeInstanceId,
            NodeInstanceStatusDone,
            CreateJsonbPayload(new { comment = NormalizeRuntimeOptionalText(comment) }));

        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflow.WorkflowId,
            activeNodeExecution.NodeInstanceId,
            "task_completed",
            CreateJsonbPayload(new
            {
                nodeKey = activeNodeExecution.Node.NodeKey,
                comment = NormalizeRuntimeOptionalText(comment)
            }));

        await PostgresRepositorySharedHelpers.InsertAuditEntry(
            connection,
            transaction,
            workflow.WorkflowId,
            null,
            actorUserId,
            "runtime_task_completed",
            null,
            NodeInstanceStatusDone,
            activeNodeExecution.Node.NodeKey);

        await AdvanceRuntimeUntilWaitOrTerminal(
            connection,
            transaction,
            workflow.WorkflowId,
            graph,
            activeNodeExecution.Node,
            await PostgresRepositorySharedHelpers.LoadStoredAnswersByKey(connection, transaction, workflow.WorkflowId),
            actorUserId);
    }

    // Prueft, ob der aktive Setup-Node nach einem Task-Status-Wechsel abgeschlossen werden kann.
    internal static async Task TryAdvanceSetupNodeIfReady(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        Guid workflowUid,
        long actorUserId)
    {
        var workflow = await LoadRuntimeWorkflowHeader(connection, transaction, workflowUid);
        if (workflow is null)
        {
            return;
        }

        var activeSetupNode = await LoadActiveRuntimeMeasureNodeExecution(
            connection,
            transaction,
            workflowId);
        if (activeSetupNode is null)
        {
            return;
        }

        var graph = await PostgresRepositorySharedHelpers.LoadWorkflowDefinitionGraph(connection, transaction, workflow.WorkflowDefinitionVersionId);
        await TryCompleteRuntimeSetupNodeIfReady(
            connection,
            transaction,
            workflowId,
            graph,
            activeSetupNode,
            await PostgresRepositorySharedHelpers.LoadStoredAnswersByKey(connection, transaction, workflowId),
            actorUserId);
    }

    // Setzt die Approval-Entscheidung am Runtime-Approval-Node durch (rejected = Workflow cancelled; approved = Engine-Loop).
    internal static async Task ApplyApprovalNodeDecision(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        Guid workflowUid,
        long nodeInstanceId,
        bool approved,
        long actorUserId)
    {
        var workflow = await LoadRuntimeWorkflowHeader(connection, transaction, workflowUid)
            ?? throw new InvalidOperationException("Workflow runtime instance was not found.");
        var graph = await PostgresRepositorySharedHelpers.LoadWorkflowDefinitionGraph(connection, transaction, workflow.WorkflowDefinitionVersionId);
        var nodeExecution = await LoadNodeExecutionForUpdate(connection, transaction, workflowId, nodeInstanceId);
        EnsureActiveRuntimeNode(nodeExecution, "approval");
        var activeNodeExecution = nodeExecution!;

        if (!approved)
        {
            await UpdateNodeInstanceStatus(
                connection,
                transaction,
                activeNodeExecution.NodeInstanceId,
                NodeInstanceStatusCancelled,
                CreateJsonbPayload(new { approved = false }));

            await InsertWorkflowRuntimeEvent(
                connection,
                transaction,
                workflow.WorkflowId,
                activeNodeExecution.NodeInstanceId,
                "approval_rejected",
                CreateJsonbPayload(new { nodeKey = activeNodeExecution.Node.NodeKey }));

            await SetWorkflowRuntimeState(
                connection,
                transaction,
                workflow.WorkflowId,
                RuntimeStatusCancelled,
                "completed",
                completedAt: DateTime.UtcNow);

            await PostgresRepositorySharedHelpers.InsertAuditEntry(
                connection,
                transaction,
                workflow.WorkflowId,
                null,
                actorUserId,
                "workflow_definition_runtime_cancelled",
                null,
                RuntimeStatusCancelled,
                activeNodeExecution.Node.NodeKey);
            return;
        }

        await UpdateNodeInstanceStatus(
            connection,
            transaction,
            activeNodeExecution.NodeInstanceId,
            NodeInstanceStatusDone,
            CreateJsonbPayload(new { approved = true }));

        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflow.WorkflowId,
            activeNodeExecution.NodeInstanceId,
            "approval_completed",
            CreateJsonbPayload(new { nodeKey = activeNodeExecution.Node.NodeKey, approved = true }));

        await PostgresRepositorySharedHelpers.InsertAuditEntry(
            connection,
            transaction,
            workflow.WorkflowId,
            null,
            actorUserId,
            "runtime_approval_completed",
            null,
            NodeInstanceStatusDone,
            activeNodeExecution.Node.NodeKey);

        await AdvanceRuntimeUntilWaitOrTerminal(
            connection,
            transaction,
            workflow.WorkflowId,
            graph,
            activeNodeExecution.Node,
            await PostgresRepositorySharedHelpers.LoadStoredAnswersByKey(connection, transaction, workflow.WorkflowId),
            actorUserId);
    }

    private static async Task<TaskTemplateRecord?> LoadTaskSpecForNode(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowNodeId)
    {
        // LA5: Per-Node Task-Spezifikation. Fuer task/approval-Nodes existiert maximal 1 Spec.
        const string sql = """
SELECT
    id,
    spec_key,
    title,
    description,
    category,
    icon_key,
    default_responsibility_id,
    process_area_label,
    is_department_phase_task,
    is_required,
    due_in_days,
    sort_order
FROM workflow_node_task_specs
WHERE workflow_node_id = @workflowNodeId
ORDER BY sort_order, id
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowNodeId", workflowNodeId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new TaskTemplateRecord
        {
            Id = checked((int)reader.GetInt64(0)),
            TemplateKey = reader.GetString(1),
            Title = reader.GetString(2),
            Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Category = reader.GetString(4),
            IconKey = PostgresRepositorySharedHelpers.NormalizeAdminTaskTemplateIconKey(reader.IsDBNull(5) ? null : reader.GetString(5)),
            DefaultResponsibilityId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            ProcessAreaLabel = reader.IsDBNull(7) ? null : reader.GetString(7),
            IsDepartmentPhaseTask = reader.GetBoolean(8),
            IsRequired = reader.GetBoolean(9),
            DueInDays = reader.IsDBNull(10) ? null : reader.GetInt32(10),
            SortOrder = reader.GetInt32(11)
        };
    }

    private static async Task<WorkflowRuntimeTaskContextRecord> LoadWorkflowRuntimeTaskContext(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = """
SELECT department_id
FROM workflows
WHERE id = @workflowId
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Workflow runtime task context could not be loaded.");
        }

        return new WorkflowRuntimeTaskContextRecord
        {
            DepartmentId = reader.GetInt32(0)
        };
    }

    internal static async Task<WorkflowNodeExecutionRecord?> LoadNodeExecutionForUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        long nodeInstanceId)
    {
        const string sql = """
SELECT
    ni.id,
    ni.status,
    n.id,
    n.node_key,
    n.node_type,
    n.title,
    n.sort_order,
    nc.config_json::text
FROM workflow_node_instances ni
JOIN workflow_nodes n ON n.id = ni.workflow_node_id
LEFT JOIN workflow_node_configs nc ON nc.workflow_node_id = n.id
WHERE ni.id = @nodeInstanceId
  AND ni.workflow_id = @workflowId
LIMIT 1
FOR UPDATE OF ni;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("nodeInstanceId", nodeInstanceId);
        command.Parameters.AddWithValue("workflowId", workflowId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new WorkflowNodeExecutionRecord
        {
            NodeInstanceId = reader.GetInt64(0),
            Status = reader.GetString(1),
            Node = new WorkflowDefinitionNodeRecord
            {
                NodeId = reader.GetInt64(2),
                NodeKey = reader.GetString(3),
                NodeType = reader.GetString(4),
                Title = reader.IsDBNull(5) ? null : reader.GetString(5),
                SortOrder = reader.GetInt32(6),
                Config = reader.IsDBNull(7) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(7))
            }
        };
    }

    internal static void EnsureActiveRuntimeNode(WorkflowNodeExecutionRecord? nodeExecution, string expectedNodeType)
    {
        if (nodeExecution is null)
        {
            throw new InvalidOperationException("Workflow node instance was not found.");
        }

        if (!string.Equals(nodeExecution.Status, NodeInstanceStatusActive, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Workflow node instance is not active.");
        }

        if (!string.Equals(nodeExecution.Node.NodeType, expectedNodeType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Workflow node instance '{nodeExecution.Node.NodeKey}' is not of expected type '{expectedNodeType}'.");
        }
    }


    private static async Task DeleteWorkflowAnswersForProcessType(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        int processTypeId)
    {
        const string sql = """
DELETE FROM workflow_answers
WHERE workflow_id = @workflowId
  AND answer_definition_id IN (
      SELECT id
      FROM workflow_answer_definitions
      WHERE workflow_definition_id = @processTypeId
  );
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task SetWorkflowRuntimeState(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        string runtimeStatus,
        string legacyStatus,
        DateTime? completedAt)
    {
        const string sql = """
UPDATE workflows
SET
    current_runtime_status = @runtimeStatus,
    status = @legacyStatus,
    completed_at = @completedAt
WHERE id = @workflowId;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("runtimeStatus", runtimeStatus);
        command.Parameters.AddWithValue("legacyStatus", legacyStatus);
        command.Parameters.Add("completedAt", NpgsqlDbType.TimestampTz).Value =
            (object?)completedAt ?? DBNull.Value;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CompleteRuntimeWorkflow(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        long? actorUserId,
        string runtimeStatus,
        string eventType)
    {
        await SetWorkflowRuntimeState(connection, transaction, workflowId, runtimeStatus, "completed", DateTime.UtcNow);
        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflowId,
            null,
            eventType,
            CreateJsonbPayload(new { runtimeStatus }));
        await PostgresRepositorySharedHelpers.InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            null,
            actorUserId,
            "workflow_definition_runtime_completed",
            null,
            runtimeStatus);
    }

    internal static async Task FailRuntimeWorkflow(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        long? actorUserId,
        string reason)
    {
        await SetWorkflowRuntimeState(connection, transaction, workflowId, RuntimeStatusFailed, "completed", DateTime.UtcNow);
        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflowId,
            null,
            "workflow_failed",
            CreateJsonbPayload(new { reason }));
        await PostgresRepositorySharedHelpers.InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            null,
            actorUserId,
            "workflow_definition_runtime_failed",
            null,
            RuntimeStatusFailed,
            reason);
    }

    internal static string? CreateJsonbPayload(object? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value);
    }

    internal static string? NormalizeRuntimeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal sealed class PublishedWorkflowDefinitionVersionRecord
    {
        public required int WorkflowDefinitionId { get; init; }
        public required string WorkflowDefinitionKey { get; init; }
        public required string WorkflowDefinitionName { get; init; }
        public required long VersionId { get; init; }
        public required int VersionNumber { get; init; }
        public required int PrimaryLegacyProcessTypeId { get; init; }
        public required string PrimaryLegacyProcessTypeKey { get; init; }
    }

    internal sealed class RuntimeWorkflowHeaderRecord
    {
        public required long WorkflowId { get; init; }
        public required Guid WorkflowUid { get; init; }
        public required long WorkflowDefinitionVersionId { get; init; }
        public required int RoleId { get; init; }
        public required string PrimaryLegacyProcessTypeKey { get; init; }
        public required bool RequiresSupervisorStep { get; init; }
        public required string CurrentRuntimeStatus { get; init; }
    }

    internal sealed class WorkflowNodeExecutionRecord
    {
        public required long NodeInstanceId { get; init; }
        public required string Status { get; init; }
        public required WorkflowDefinitionNodeRecord Node { get; init; }
    }

    private sealed class RuntimeWorkflowStatusContextRecord
    {
        public required string PrimaryLegacyProcessTypeKey { get; init; }
        public required bool RequiresSupervisorStep { get; init; }
    }

    private sealed class WorkflowRuntimeTaskContextRecord
    {
        public required int DepartmentId { get; init; }
    }
}
