using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private const string WorkflowDefinitionRetiredStatus = "retired";
    private const string RuntimeStatusRunning = "running";
    private const string RuntimeStatusWaitingOnNode = "waiting_on_node";
    private const string RuntimeStatusCompleted = "completed";
    private const string RuntimeStatusFailed = "failed";
    private const string RuntimeStatusCancelled = "cancelled";
    private const string NodeInstanceStatusActive = "active";
    private const string NodeInstanceStatusDone = "done";
    private const string NodeInstanceStatusFailed = "failed";
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

        var versionRecord = await GetWorkflowDefinitionVersionRecord(connection, transaction, versionId);
        if (versionRecord is null)
        {
            return null;
        }

        var detail = await GetAdminWorkflowDefinitionVersionDetailById(connection, transaction, versionId);
        if (detail is null)
        {
            return null;
        }

        if (string.Equals(versionRecord.Status, WorkflowDefinitionPublishedStatus, StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync();
            return detail;
        }

        if (!string.Equals(versionRecord.Status, WorkflowDefinitionDraftStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Workflow definition version '{versionId}' is not publishable because it is in status '{versionRecord.Status}'.");
        }

        if (!detail.CanPublish)
        {
            throw new InvalidOperationException(
                string.Join(" ", detail.ValidationIssues.Select(issue => issue.Message)));
        }

        var primaryLegacyProcessTypeId = await ResolveWorkflowDefinitionLegacyProcessTypeId(
            connection,
            transaction,
            detail.PrimaryLegacyProcessTypeKey,
            requireActive: true);

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
            retireCommand.Parameters.AddWithValue("publishedStatus", WorkflowDefinitionPublishedStatus);
            retireCommand.Parameters.AddWithValue("retiredStatus", WorkflowDefinitionRetiredStatus);
            await retireCommand.ExecuteNonQueryAsync();
        }

        const string publishSql = """
UPDATE workflow_definition_versions
SET
    status = @publishedStatus,
    primary_legacy_process_type_id = @primaryLegacyProcessTypeId,
    published_at = NOW(),
    updated_at = NOW()
WHERE id = @versionId;
""";

        await using (var publishCommand = new NpgsqlCommand(publishSql, connection, transaction))
        {
            publishCommand.Parameters.AddWithValue("versionId", versionId);
            publishCommand.Parameters.AddWithValue("publishedStatus", WorkflowDefinitionPublishedStatus);
            publishCommand.Parameters.AddWithValue("primaryLegacyProcessTypeId", primaryLegacyProcessTypeId.Value);
            await publishCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return await GetAdminWorkflowDefinitionVersionDetailById(connection, null, versionId);
    }

    public async Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowDefinitionInstance(
        CreateWorkflowDefinitionInstanceRequest request,
        long createdByUserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.WorkflowDefinitionKey))
        {
            throw new InvalidOperationException("WorkflowDefinitionKey is required.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var publishedVersion = await LoadPublishedWorkflowDefinitionVersion(
            connection,
            transaction,
            request.WorkflowDefinitionKey);
        if (publishedVersion is null)
        {
            throw new InvalidOperationException(
                $"No published workflow definition exists for key '{request.WorkflowDefinitionKey.Trim().ToLowerInvariant()}'.");
        }

        var processType = await LoadProcessTypeForCreate(connection, transaction, publishedVersion.PrimaryLegacyProcessTypeKey);
        var graph = await LoadWorkflowDefinitionGraph(connection, transaction, publishedVersion.VersionId);

        var gatekeeperEvaluation = EvaluateSupervisorGatekeeper(
            graph,
            publishedVersion.PrimaryLegacyProcessTypeKey,
            processType.RequiresSupervisorStep);
        if (!gatekeeperEvaluation.IsSatisfied)
        {
            throw new InvalidOperationException(
                gatekeeperEvaluation.FailureMessage
                ?? "Die publizierte Workflow-Definition besitzt keinen gültigen Supervisor-Gatekeeper.");
        }

        if (request.DeadlineDate.HasValue && request.DeadlineDate.Value < DateOnly.FromDateTime(DateTime.Today))
        {
            throw new InvalidOperationException("Die Deadline darf nicht in der Vergangenheit liegen.");
        }

        var targetPerson = request.TargetPersonId.HasValue
            ? await LoadTargetPerson(connection, transaction, request.TargetPersonId.Value)
            : null;

        if (processType.RequiresTargetPerson && targetPerson is null)
        {
            throw new InvalidOperationException(
                $"Die publizierte Workflow-Definition '{publishedVersion.WorkflowDefinitionKey}' erfordert eine bestehende Zielperson.");
        }

        if (!processType.RequiresTargetPerson && request.TargetPersonId.HasValue)
        {
            throw new InvalidOperationException(
                $"Die publizierte Workflow-Definition '{publishedVersion.WorkflowDefinitionKey}' darf keine bestehende Zielperson referenzieren.");
        }

        var effectiveDepartmentId = request.DepartmentId;
        int? effectiveRoleId = request.RoleId;

        if (request.RoleId.HasValue)
        {
            if (!request.DepartmentId.HasValue)
            {
                throw new InvalidOperationException("Die Abteilung ist erforderlich, wenn eine Zielrolle direkt angegeben wird.");
            }

            await EnsureValidPositionRole(connection, transaction, request.RoleId.Value, request.DepartmentId.Value);
        }
        else if (targetPerson is not null)
        {
            if (!targetPerson.DepartmentId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Fuer die Zielperson '{targetPerson.DisplayName}' ist keine aktuelle Abteilung ableitbar.");
            }

            if (!targetPerson.RoleId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Fuer die Zielperson '{targetPerson.DisplayName}' ist keine aktuelle Stelle ableitbar.");
            }

            effectiveDepartmentId = targetPerson.DepartmentId.Value;
            effectiveRoleId = targetPerson.RoleId.Value;
        }

        if (!effectiveDepartmentId.HasValue)
        {
            throw new InvalidOperationException("Die Abteilung ist fuer diese Workflow-Definition erforderlich.");
        }

        if (!effectiveRoleId.HasValue)
        {
            throw new InvalidOperationException("Die Zielrolle ist fuer diese Workflow-Definition erforderlich.");
        }

        var departmentId = effectiveDepartmentId.Value;
        var roleId = effectiveRoleId.Value;

        var firstName = NormalizeRuntimeOptionalText(request.FirstName);
        var lastName = NormalizeRuntimeOptionalText(request.LastName);
        if (!processType.RequiresTargetPerson && targetPerson is not null)
        {
            var derivedFirstName = targetPerson.FirstName;
            var derivedLastName = targetPerson.LastName;
            if (string.IsNullOrWhiteSpace(derivedFirstName) || string.IsNullOrWhiteSpace(derivedLastName))
            {
                (derivedFirstName, derivedLastName) = SplitDisplayName(targetPerson.DisplayName);
            }

            firstName ??= derivedFirstName;
            lastName ??= derivedLastName;
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new InvalidOperationException("Der Vorname ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new InvalidOperationException("Der Nachname ist erforderlich.");
        }

        var employeeNumber = request.EmployeeNumber ?? targetPerson?.EmployeeNumber;
        if (!employeeNumber.HasValue || employeeNumber.Value <= 0)
        {
            throw new InvalidOperationException("Die Personalnummer ist erforderlich.");
        }

        var badgeNumber = request.BadgeNumber ?? targetPerson?.BadgeNumber;
        if (!badgeNumber.HasValue || badgeNumber.Value <= 0)
        {
            throw new InvalidOperationException("Die Ausweisnummer ist erforderlich.");
        }

        const string insertWorkflowSql = """
INSERT INTO workflows (
    process_type_id,
    workflow_definition_version_id,
    department_id,
    position_role_id,
    created_by_user_id,
    target_person_id,
    first_name,
    last_name,
    employee_number,
    badge_number,
    deadline_date,
    current_runtime_status,
    status,
    started_at
)
VALUES (
    @processTypeId,
    @workflowDefinitionVersionId,
    @departmentId,
    @roleId,
    @createdByUserId,
    @targetPersonId,
    @firstName,
    @lastName,
    @employeeNumber,
    @badgeNumber,
    @deadlineDate,
    @currentRuntimeStatus,
    @legacyStatus,
    NOW()
)
RETURNING id, uid;
""";

        long workflowId;
        Guid workflowUid;
        await using (var insertWorkflowCommand = new NpgsqlCommand(insertWorkflowSql, connection, transaction))
        {
            insertWorkflowCommand.Parameters.AddWithValue("processTypeId", publishedVersion.PrimaryLegacyProcessTypeId);
            insertWorkflowCommand.Parameters.AddWithValue("workflowDefinitionVersionId", publishedVersion.VersionId);
            insertWorkflowCommand.Parameters.AddWithValue("departmentId", departmentId);
            insertWorkflowCommand.Parameters.AddWithValue("roleId", roleId);
            insertWorkflowCommand.Parameters.AddWithValue("createdByUserId", createdByUserId);
            insertWorkflowCommand.Parameters.Add("targetPersonId", NpgsqlDbType.Bigint).Value =
                (object?)request.TargetPersonId ?? DBNull.Value;
            insertWorkflowCommand.Parameters.AddWithValue("firstName", firstName);
            insertWorkflowCommand.Parameters.AddWithValue("lastName", lastName);
            insertWorkflowCommand.Parameters.AddWithValue("employeeNumber", employeeNumber.Value);
            insertWorkflowCommand.Parameters.AddWithValue("badgeNumber", badgeNumber.Value);
            insertWorkflowCommand.Parameters.Add("deadlineDate", NpgsqlDbType.Date).Value =
                (object?)request.DeadlineDate ?? DBNull.Value;
            insertWorkflowCommand.Parameters.AddWithValue("currentRuntimeStatus", RuntimeStatusRunning);
            insertWorkflowCommand.Parameters.AddWithValue("legacyStatus", "draft");

            await using var reader = await insertWorkflowCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Workflow runtime instance could not be created.");
            }

            workflowId = reader.GetInt64(0);
            workflowUid = reader.GetGuid(1);
        }

        await InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            null,
            createdByUserId,
            "workflow_definition_runtime_created",
            null,
            RuntimeStatusRunning,
            publishedVersion.WorkflowDefinitionKey);

        var startNode = graph.Nodes.Single(node => string.Equals(node.NodeType, "start", StringComparison.OrdinalIgnoreCase));

        var startNodeInstanceId = await CreateWorkflowNodeInstance(
            connection,
            transaction,
            workflowId,
            startNode,
            NodeInstanceStatusDone,
            CreateJsonbPayload(new { auto = true, reason = "runtime_start" }));

        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflowId,
            null,
            "workflow_started",
            CreateJsonbPayload(new
            {
                workflowDefinitionKey = publishedVersion.WorkflowDefinitionKey,
                workflowDefinitionVersionId = publishedVersion.VersionId
            }));

        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflowId,
            startNodeInstanceId,
            "node_completed",
            CreateJsonbPayload(new { nodeKey = startNode.NodeKey, nodeType = startNode.NodeType, auto = true }));

        await AdvanceRuntimeUntilWaitOrTerminal(
            connection,
            transaction,
            workflowId,
            graph,
            startNode,
            await LoadStoredAnswersByKey(connection, transaction, workflowId),
            createdByUserId);

        await CreateWorkflowNotifications(
            connection,
            transaction,
            workflowId,
            departmentId,
            processType.RequiresSupervisorStep,
            publishedVersion.WorkflowDefinitionKey,
            publishedVersion.WorkflowDefinitionName);

        await transaction.CommitAsync();

        return await GetWorkflowDefinitionRuntimeDetailInternal(connection, null, workflowUid)
            ?? throw new InvalidOperationException("Workflow runtime instance could not be loaded after creation.");
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
                Payload = reader.IsDBNull(3) ? null : ParseJsonElement(reader.GetString(3)),
                CreatedAt = reader.GetDateTime(4)
            });
        }

        return events;
    }

    public async Task<WorkflowDefinitionRuntimeDetailDto?> CompleteRuntimeFormNode(
        Guid workflowUid,
        long nodeInstanceId,
        CompleteRuntimeFormNodeRequest request,
        long actorUserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var workflow = await LoadRuntimeWorkflowHeader(connection, transaction, workflowUid);
        if (workflow is null)
        {
            return null;
        }

        var graph = await LoadWorkflowDefinitionGraph(connection, transaction, workflow.WorkflowDefinitionVersionId);
        var nodeExecution = await LoadNodeExecutionForUpdate(connection, transaction, workflow.WorkflowId, nodeInstanceId);
        EnsureActiveRuntimeNode(nodeExecution, "form");
        await CompleteRuntimeFormNodeInternal(
            connection,
            transaction,
            workflow,
            graph,
            nodeExecution!,
            request.RequirementSelections ?? [],
            actorUserId);

        await transaction.CommitAsync();
        return await GetWorkflowDefinitionRuntimeDetailInternal(connection, null, workflowUid);
    }

    public async Task<WorkflowDefinitionRuntimeDetailDto?> CompleteRuntimeApprovalNode(
        Guid workflowUid,
        long nodeInstanceId,
        CompleteRuntimeApprovalNodeRequest request,
        long actorUserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var workflow = await LoadRuntimeWorkflowHeader(connection, transaction, workflowUid);
        if (workflow is null)
        {
            return null;
        }

        var nodeExecution = await LoadNodeExecutionForUpdate(connection, transaction, workflow.WorkflowId, nodeInstanceId);
        EnsureActiveRuntimeNode(nodeExecution, "approval");
        var linkedTaskId = await LoadWorkflowTaskIdByNodeInstanceId(connection, transaction, nodeInstanceId);
        if (linkedTaskId.HasValue)
        {
            var taskRecord = await LoadTaskStateForUpdate(connection, transaction, linkedTaskId.Value);
            if (!taskRecord.HasValue)
            {
                throw new InvalidOperationException("Approval task could not be loaded.");
            }

            var (_, _, taskStatus, _, _, _, taskTitle, _, _) = taskRecord.Value;
            if (!TaskStatusRules.TerminalTaskStatuses.Contains(taskStatus))
            {
                await PersistTaskStatus(connection, transaction, linkedTaskId.Value, "done");
                await InsertAuditEntry(
                    connection,
                    transaction,
                    workflow.WorkflowId,
                    linkedTaskId.Value,
                    actorUserId,
                    "task_status_changed",
                    taskStatus,
                    "done",
                    BuildTaskStatusAuditDetail(taskTitle));
                await SyncPrimaryAssignmentCompletion(connection, transaction, linkedTaskId.Value, "done");
            }
        }

        await ApplyRuntimeApprovalDecisionFromWorkflowTask(
            connection,
            transaction,
            workflow.WorkflowId,
            workflowUid,
            nodeInstanceId,
            request.Approved,
            actorUserId);

        await transaction.CommitAsync();
        return await GetWorkflowDefinitionRuntimeDetailInternal(connection, null, workflowUid);
    }

    public async Task<WorkflowDefinitionRuntimeDetailDto?> CompleteRuntimeTaskNode(
        Guid workflowUid,
        long nodeInstanceId,
        CompleteRuntimeTaskNodeRequest request,
        long actorUserId)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var workflow = await LoadRuntimeWorkflowHeader(connection, transaction, workflowUid);
        if (workflow is null)
        {
            return null;
        }

        var nodeExecution = await LoadNodeExecutionForUpdate(connection, transaction, workflow.WorkflowId, nodeInstanceId);
        EnsureActiveRuntimeNode(nodeExecution, "task");
        var linkedTaskId = await LoadWorkflowTaskIdByNodeInstanceId(connection, transaction, nodeInstanceId);
        if (linkedTaskId.HasValue)
        {
            var taskRecord = await LoadTaskStateForUpdate(connection, transaction, linkedTaskId.Value);
            if (!taskRecord.HasValue)
            {
                throw new InvalidOperationException("Runtime task could not be loaded.");
            }

            var (_, _, taskStatus, _, _, _, taskTitle, _, _) = taskRecord.Value;
            if (!TaskStatusRules.TerminalTaskStatuses.Contains(taskStatus))
            {
                await PersistTaskStatus(connection, transaction, linkedTaskId.Value, "done");
                await InsertAuditEntry(
                    connection,
                    transaction,
                    workflow.WorkflowId,
                    linkedTaskId.Value,
                    actorUserId,
                    "task_status_changed",
                    taskStatus,
                    "done",
                    BuildTaskStatusAuditDetail(taskTitle));
                await SyncPrimaryAssignmentCompletion(connection, transaction, linkedTaskId.Value, "done");
            }
        }

        await CompleteRuntimeTaskNodeFromTaskStatusUpdate(
            connection,
            transaction,
            workflow.WorkflowId,
            workflowUid,
            nodeInstanceId,
            actorUserId,
            request.Comment);

        await transaction.CommitAsync();
        return await GetWorkflowDefinitionRuntimeDetailInternal(connection, null, workflowUid);
    }

    private async Task CompleteRuntimeSupervisorGatekeeperStep(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid workflowUid,
        IReadOnlyList<RequirementSelectionInputDto> selections,
        long actorUserId)
    {
        var workflow = await LoadRuntimeWorkflowHeader(connection, transaction, workflowUid)
            ?? throw new InvalidOperationException("Workflow runtime instance was not found.");
        var graph = await LoadWorkflowDefinitionGraph(connection, transaction, workflow.WorkflowDefinitionVersionId);
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

    private static async Task CompleteRuntimeFormNodeInternal(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RuntimeWorkflowHeaderRecord workflow,
        WorkflowDefinitionGraphRecord graph,
        WorkflowNodeExecutionRecord activeNodeExecution,
        IReadOnlyList<RequirementSelectionInputDto> selections,
        long actorUserId)
    {
        var legacyProcessTypeKey = GetRequiredNodeConfigString(activeNodeExecution.Node, "legacyProcessTypeKey");
        var legacyProcessTypeId = await ResolveWorkflowDefinitionLegacyProcessTypeId(
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

        var answerDefinitions = await LoadAnswerDefinitionRecords(connection, transaction, legacyProcessTypeId.Value);
        var roleDefaults = await LoadRoleDefaultRecords(connection, transaction, workflow.RoleId, legacyProcessTypeId.Value);
        var persistedAnswers = await PersistWorkflowAnswers(
            connection,
            transaction,
            workflow.WorkflowId,
            selections,
            answerDefinitions,
            roleDefaults);
        var answersByKey = persistedAnswers
            .GroupBy(answer => answer.AnswerKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        ValidateSupervisorSelections(answerDefinitions, answersByKey);

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

        await InsertAuditEntry(
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
            await LoadStoredAnswersByKey(connection, transaction, workflow.WorkflowId),
            actorUserId);
    }

    private static async Task<WorkflowDefinitionRuntimeDetailDto?> GetWorkflowDefinitionRuntimeDetailInternal(
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
    w.status,
    pt.key,
    pt.name,
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
JOIN process_types pt ON pt.id = w.process_type_id
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
                LegacyWorkflowStatus = reader.GetString(7),
                PrimaryLegacyProcessTypeKey = reader.GetString(8),
                PrimaryLegacyProcessTypeName = reader.GetString(9),
                DepartmentId = reader.GetInt32(10),
                RoleId = reader.GetInt32(11),
                FirstName = reader.IsDBNull(12) ? null : reader.GetString(12),
                LastName = reader.IsDBNull(13) ? null : reader.GetString(13),
                EmployeeNumber = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                BadgeNumber = reader.IsDBNull(15) ? null : reader.GetInt32(15),
                TargetPersonId = reader.IsDBNull(16) ? null : reader.GetInt64(16),
                DeadlineDate = reader.IsDBNull(17) ? null : reader.GetFieldValue<DateOnly>(17),
                CreatedAt = reader.GetDateTime(18),
                StartedAt = reader.IsDBNull(19) ? null : reader.GetDateTime(19),
                CompletedAt = reader.IsDBNull(20) ? null : reader.GetDateTime(20),
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
                    Result = reader.IsDBNull(7) ? null : ParseJsonElement(reader.GetString(7))
                });
            }
        }

        return detail;
    }

    private static async Task<PublishedWorkflowDefinitionVersionRecord?> LoadPublishedWorkflowDefinitionVersion(
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
    v.primary_legacy_process_type_id,
    pt.key
FROM workflow_definitions d
JOIN workflow_definition_versions v ON v.workflow_definition_id = d.id
JOIN process_types pt ON pt.id = v.primary_legacy_process_type_id
WHERE d.definition_key = @definitionKey
  AND v.status = @publishedStatus
  AND pt.is_active = TRUE
ORDER BY v.version_number DESC, v.id DESC
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("definitionKey", workflowDefinitionKey.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("publishedStatus", WorkflowDefinitionPublishedStatus);
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

    private static async Task<WorkflowDefinitionGraphRecord> LoadWorkflowDefinitionGraph(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long versionId)
    {
        const string nodeSql = """
SELECT
    n.id,
    n.node_key,
    n.node_type,
    n.title,
    n.sort_order,
    nc.config_json::text
FROM workflow_nodes n
LEFT JOIN workflow_node_configs nc ON nc.workflow_node_id = n.id
WHERE n.workflow_definition_version_id = @versionId
ORDER BY n.sort_order, n.node_key, n.id;
""";

        var nodes = new List<WorkflowDefinitionNodeRecord>();
        await using (var nodeCommand = new NpgsqlCommand(nodeSql, connection, transaction))
        {
            nodeCommand.Parameters.AddWithValue("versionId", versionId);
            await using var reader = await nodeCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                nodes.Add(new WorkflowDefinitionNodeRecord
                {
                    NodeId = reader.GetInt64(0),
                    NodeKey = reader.GetString(1),
                    NodeType = reader.GetString(2),
                    Title = reader.IsDBNull(3) ? null : reader.GetString(3),
                    SortOrder = reader.GetInt32(4),
                    Config = reader.IsDBNull(5) ? null : ParseJsonElement(reader.GetString(5))
                });
            }
        }

        const string edgeSql = """
SELECT
    e.id,
    e.source_workflow_node_id,
    e.target_workflow_node_id,
    e.priority,
    e.condition_expression
FROM workflow_edges e
WHERE e.workflow_definition_version_id = @versionId
ORDER BY e.source_workflow_node_id, e.priority, e.id;
""";

        var edges = new List<WorkflowDefinitionEdgeRecord>();
        await using (var edgeCommand = new NpgsqlCommand(edgeSql, connection, transaction))
        {
            edgeCommand.Parameters.AddWithValue("versionId", versionId);
            await using var reader = await edgeCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                edges.Add(new WorkflowDefinitionEdgeRecord
                {
                    EdgeId = reader.GetInt64(0),
                    SourceNodeId = reader.GetInt64(1),
                    TargetNodeId = reader.GetInt64(2),
                    Priority = reader.GetInt32(3),
                    ConditionExpression = reader.IsDBNull(4) ? null : reader.GetString(4)
                });
            }
        }

        const string actionSql = """
SELECT
    wna.workflow_node_id,
    wna.id,
    wna.action_definition_id,
    wna.execution_order,
    wna.on_error_behavior,
    wna.input_mapping_json::text,
    ad.action_key,
    ad.name,
    ad.handler_type,
    ad.is_idempotent
FROM workflow_node_actions wna
INNER JOIN action_definitions ad ON ad.id = wna.action_definition_id
INNER JOIN workflow_nodes n ON n.id = wna.workflow_node_id
WHERE n.workflow_definition_version_id = @versionId
ORDER BY wna.workflow_node_id, wna.execution_order, wna.id;
""";

        var nodeActionsByNodeId = new Dictionary<long, List<WorkflowNodeActionRecord>>();
        await using (var actionCommand = new NpgsqlCommand(actionSql, connection, transaction))
        {
            actionCommand.Parameters.AddWithValue("versionId", versionId);
            await using var reader = await actionCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var workflowNodeId = reader.GetInt64(0);
                if (!nodeActionsByNodeId.TryGetValue(workflowNodeId, out var actions))
                {
                    actions = new List<WorkflowNodeActionRecord>();
                    nodeActionsByNodeId.Add(workflowNodeId, actions);
                }

                actions.Add(new WorkflowNodeActionRecord
                {
                    Id = reader.GetInt64(1),
                    ActionDefinitionId = reader.GetInt64(2),
                    ExecutionOrder = reader.GetInt32(3),
                    OnErrorBehavior = reader.GetString(4),
                    InputMapping = reader.IsDBNull(5) ? null : ParseJsonElement(reader.GetString(5)),
                    ActionKey = reader.GetString(6),
                    ActionName = reader.GetString(7),
                    HandlerType = reader.GetString(8),
                    IsIdempotent = reader.GetBoolean(9)
                });
            }
        }

        return new WorkflowDefinitionGraphRecord
        {
            Nodes = nodes,
            Edges = edges,
            NodeById = nodes.ToDictionary(node => node.NodeId),
            NodeActionsByNodeId = nodeActionsByNodeId,
            IncomingEdgesByTargetNodeId = edges
                .GroupBy(edge => edge.TargetNodeId)
                .ToDictionary(group => group.Key, group => group.OrderBy(edge => edge.Priority).ThenBy(edge => edge.EdgeId).ToList()),
            OutgoingEdgesBySourceNodeId = edges
                .GroupBy(edge => edge.SourceNodeId)
                .ToDictionary(group => group.Key, group => group.OrderBy(edge => edge.Priority).ThenBy(edge => edge.EdgeId).ToList())
        };
    }

    private static async Task<long> CreateWorkflowNodeInstance(
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

    private static async Task UpdateNodeInstanceStatus(
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

    private static async Task<long> InsertWorkflowRuntimeEvent(
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

    private static async Task AdvanceRuntimeUntilWaitOrTerminal(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        WorkflowDefinitionGraphRecord graph,
        WorkflowDefinitionNodeRecord completedNode,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        long? actorUserId)
    {
        await SetWorkflowRuntimeState(connection, transaction, workflowId, RuntimeStatusRunning, "in_progress", null);

        var pendingNodes = new Queue<WorkflowDefinitionNodeRecord>();
        var scheduledNodeIds = new HashSet<long>();

        try
        {
            foreach (var nextNode in ResolveNextNodes(graph, completedNode, answersByKey))
            {
                EnqueueIfNeeded(pendingNodes, scheduledNodeIds, nextNode);
            }
        }
        catch (InvalidOperationException ex)
        {
            await FailRuntimeWorkflow(connection, transaction, workflowId, actorUserId, ex.Message);
            return;
        }

        while (pendingNodes.Count > 0)
        {
            var nextNode = pendingNodes.Dequeue();
            _ = scheduledNodeIds.Remove(nextNode.NodeId);

            try
            {
                if (await LoadWorkflowNodeInstanceByWorkflowNodeId(connection, transaction, workflowId, nextNode.NodeId) is not null)
                {
                    continue;
                }

                if (!await CanActivateRuntimeNode(connection, transaction, workflowId, graph, nextNode))
                {
                    continue;
                }

                if (await ShouldAutoCompleteSupervisorApprovalBridge(
                        connection,
                        transaction,
                        workflowId,
                        graph,
                        completedNode,
                        nextNode))
                {
                    var skippedApprovalNodeInstanceId = await CreateWorkflowNodeInstance(
                        connection,
                        transaction,
                        workflowId,
                        nextNode,
                        NodeInstanceStatusDone,
                        CreateJsonbPayload(new
                        {
                            auto = true,
                            reason = "supervisor_gatekeeper_form_already_completed"
                        }));

                    await InsertWorkflowRuntimeEvent(
                        connection,
                        transaction,
                        workflowId,
                        skippedApprovalNodeInstanceId,
                        "node_completed",
                        CreateJsonbPayload(new
                        {
                            nodeKey = nextNode.NodeKey,
                            nodeType = nextNode.NodeType,
                            auto = true,
                            reason = "supervisor_gatekeeper_form_already_completed"
                        }));

                    await InsertAuditEntry(
                        connection,
                        transaction,
                        workflowId,
                        null,
                        actorUserId,
                        "runtime_node_auto_completed",
                        null,
                        nextNode.NodeType,
                        nextNode.NodeKey);

                    foreach (var resolvedNode in ResolveNextNodes(graph, nextNode, answersByKey))
                    {
                        EnqueueIfNeeded(pendingNodes, scheduledNodeIds, resolvedNode);
                    }

                    continue;
                }

                switch (nextNode.NodeType)
                {
                    case "start":
                    case "decision":
                    case "parallel_split":
                    case "parallel_join":
                    case "end":
                    {
                        var autoNodeInstanceId = await CreateWorkflowNodeInstance(
                            connection,
                            transaction,
                            workflowId,
                            nextNode,
                            NodeInstanceStatusDone,
                            CreateJsonbPayload(new { auto = true }));

                        await InsertWorkflowRuntimeEvent(
                            connection,
                            transaction,
                            workflowId,
                            autoNodeInstanceId,
                            "node_completed",
                            CreateJsonbPayload(new { nodeKey = nextNode.NodeKey, nodeType = nextNode.NodeType, auto = true }));

                        if (string.Equals(nextNode.NodeType, "decision", StringComparison.OrdinalIgnoreCase))
                        {
                            var decisionTarget = ResolveDecisionTarget(graph, nextNode, answersByKey, out var selectedEdge);
                            if (decisionTarget is null || selectedEdge is null)
                            {
                                throw new InvalidOperationException(
                                    $"Decision node '{nextNode.NodeKey}' has no matching outgoing edge.");
                            }

                            await UpdateNodeInstanceStatus(
                                connection,
                                transaction,
                                autoNodeInstanceId,
                                NodeInstanceStatusDone,
                                CreateJsonbPayload(new
                                {
                                    auto = true,
                                    selectedTargetNodeKey = decisionTarget.NodeKey,
                                    selectedEdgeId = selectedEdge.EdgeId
                                }));

                            await InsertWorkflowRuntimeEvent(
                                connection,
                                transaction,
                                workflowId,
                                autoNodeInstanceId,
                                "decision_branch_selected",
                                CreateJsonbPayload(new
                                {
                                    nodeKey = nextNode.NodeKey,
                                    targetNodeKey = decisionTarget.NodeKey,
                                    edgePriority = selectedEdge.Priority
                                }));
                        }

                        foreach (var resolvedNode in ResolveNextNodes(graph, nextNode, answersByKey))
                        {
                            EnqueueIfNeeded(pendingNodes, scheduledNodeIds, resolvedNode);
                        }

                        break;
                    }
                    case "setup":
                    case "measure_provision":
                    case "measure_deprovision":
                    case "measure_change":
                    case "measure_rename":
                    {
                        var activeNodeInstanceId = await CreateWorkflowNodeInstance(
                            connection,
                            transaction,
                            workflowId,
                            nextNode,
                            NodeInstanceStatusActive,
                            null);

                        await InsertWorkflowRuntimeEvent(
                            connection,
                            transaction,
                            workflowId,
                            activeNodeInstanceId,
                            "node_activated",
                            CreateJsonbPayload(new { nodeKey = nextNode.NodeKey, nodeType = nextNode.NodeType }));

                        await EnsureRuntimeSetupTasksGenerated(
                            connection,
                            transaction,
                            workflowId,
                            nextNode,
                            answersByKey,
                            actorUserId);

                        await InsertAuditEntry(
                            connection,
                            transaction,
                            workflowId,
                            null,
                            actorUserId,
                            "runtime_node_waiting",
                            null,
                            nextNode.NodeType,
                            nextNode.NodeKey);

                        await TryCompleteRuntimeSetupNodeIfReady(
                            connection,
                            transaction,
                            workflowId,
                            graph,
                            new WorkflowNodeExecutionRecord
                            {
                                NodeInstanceId = activeNodeInstanceId,
                                Status = NodeInstanceStatusActive,
                                Node = nextNode
                            },
                            answersByKey,
                            actorUserId);
                        break;
                    }
                    case "form":
                    case "approval":
                    case "task":
                    case "automation":
                    {
                        var activeNodeInstanceId = await CreateWorkflowNodeInstance(
                            connection,
                            transaction,
                            workflowId,
                            nextNode,
                            NodeInstanceStatusActive,
                            null);

                        await InsertWorkflowRuntimeEvent(
                            connection,
                            transaction,
                            workflowId,
                            activeNodeInstanceId,
                            "node_activated",
                            CreateJsonbPayload(new { nodeKey = nextNode.NodeKey, nodeType = nextNode.NodeType }));

                        if (string.Equals(nextNode.NodeType, "task", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(nextNode.NodeType, "approval", StringComparison.OrdinalIgnoreCase))
                        {
                            await CreateRuntimeWorkflowTask(
                                connection,
                                transaction,
                                workflowId,
                                activeNodeInstanceId,
                                nextNode);
                        }
                        else if (string.Equals(nextNode.NodeType, "automation", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!graph.NodeActionsByNodeId.TryGetValue(nextNode.NodeId, out var actions)
                                || actions.Count == 0)
                            {
                                throw new InvalidOperationException(
                                    $"Automation node '{nextNode.NodeKey}' has no configured actions.");
                            }

                            var firstAction = actions
                                .OrderBy(action => action.ExecutionOrder)
                                .ThenBy(action => action.Id)
                                .First();

                            var payload = await BuildAutomationJobPayload(
                                connection,
                                transaction,
                                workflowId,
                                firstAction.InputMapping,
                                answersByKey,
                                CancellationToken.None);
                            await CreateAutomationJob(
                                connection,
                                transaction,
                                workflowId,
                                activeNodeInstanceId,
                                firstAction.Id,
                                firstAction.ActionDefinitionId,
                                payload,
                                CancellationToken.None);
                        }

                        await InsertAuditEntry(
                            connection,
                            transaction,
                            workflowId,
                            null,
                            actorUserId,
                            "runtime_node_waiting",
                            null,
                            nextNode.NodeType,
                            nextNode.NodeKey);
                        break;
                    }
                    default:
                        throw new InvalidOperationException(
                            $"Node type '{nextNode.NodeType}' is not supported by the runtime.");
                }
            }
            catch (InvalidOperationException ex)
            {
                await FailRuntimeWorkflow(connection, transaction, workflowId, actorUserId, ex.Message);
                return;
            }
        }

        var activeNodes = await LoadActiveRuntimeNodes(connection, transaction, workflowId);
        if (activeNodes.Count > 0)
        {
            var legacyStatus = default(string);
            if (activeNodes.Any(node => IsMeasureGenerationNodeType(node.NodeType)))
            {
                await RecalculateAndPersistWorkflowStatus(connection, transaction, workflowId, actorUserId);
                legacyStatus = await LoadWorkflowStatusForUpdate(connection, transaction, workflowId) ?? "in_progress";
            }
            else
            {
                var workflowStatusContext = await LoadRuntimeWorkflowStatusContext(connection, transaction, workflowId);
                legacyStatus = MapLegacyStatusForActiveNodes(
                    graph,
                    activeNodes,
                    workflowStatusContext.PrimaryLegacyProcessTypeKey,
                    workflowStatusContext.RequiresSupervisorStep);
            }

            await SetWorkflowRuntimeState(
                connection,
                transaction,
                workflowId,
                RuntimeStatusWaitingOnNode,
                legacyStatus,
                null);
            return;
        }

        await CompleteRuntimeWorkflow(connection, transaction, workflowId, actorUserId, RuntimeStatusCompleted, "workflow_completed");
    }

    private static void EnqueueIfNeeded(
        Queue<WorkflowDefinitionNodeRecord> queue,
        ISet<long> scheduledNodeIds,
        WorkflowDefinitionNodeRecord node)
    {
        if (scheduledNodeIds.Add(node.NodeId))
        {
            queue.Enqueue(node);
        }
    }

    private static IReadOnlyList<WorkflowDefinitionNodeRecord> ResolveNextNodes(
        WorkflowDefinitionGraphRecord graph,
        WorkflowDefinitionNodeRecord currentNode,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        if (!graph.OutgoingEdgesBySourceNodeId.TryGetValue(currentNode.NodeId, out var outgoingEdges)
            || outgoingEdges.Count == 0)
        {
            return [];
        }

        if (string.Equals(currentNode.NodeType, "decision", StringComparison.OrdinalIgnoreCase))
        {
            var decisionTarget = ResolveDecisionTarget(graph, currentNode, answersByKey, out _);
            return decisionTarget is null ? [] : [decisionTarget];
        }

        if (string.Equals(currentNode.NodeType, "parallel_split", StringComparison.OrdinalIgnoreCase))
        {
            return outgoingEdges
                .Select(edge => graph.NodeById.GetValueOrDefault(edge.TargetNodeId))
                .Where(node => node is not null)
                .Cast<WorkflowDefinitionNodeRecord>()
                .ToList();
        }

        if (outgoingEdges.Count != 1)
        {
            throw new InvalidOperationException(
                $"Node '{currentNode.NodeKey}' uses {outgoingEdges.Count} outgoing edges, but only decision and parallel_split nodes may branch.");
        }

        var nextNode = graph.NodeById.GetValueOrDefault(outgoingEdges[0].TargetNodeId);
        return nextNode is null ? [] : [nextNode];
    }

    private static async Task<bool> CanActivateRuntimeNode(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        WorkflowDefinitionGraphRecord graph,
        WorkflowDefinitionNodeRecord node)
    {
        if (!string.Equals(node.NodeType, "parallel_join", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!graph.IncomingEdgesByTargetNodeId.TryGetValue(node.NodeId, out var incomingEdges)
            || incomingEdges.Count == 0)
        {
            return true;
        }

        var sourceNodeIds = incomingEdges
            .Select(edge => edge.SourceNodeId)
            .Distinct()
            .ToArray();
        var nodeInstanceStates = await LoadWorkflowNodeInstanceStates(connection, transaction, workflowId, sourceNodeIds);

        return sourceNodeIds.All(sourceNodeId =>
            nodeInstanceStates.TryGetValue(sourceNodeId, out var status)
            && string.Equals(status, NodeInstanceStatusDone, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<Dictionary<long, string>> LoadWorkflowNodeInstanceStates(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        IReadOnlyCollection<long> workflowNodeIds)
    {
        if (workflowNodeIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        const string sql = """
SELECT workflow_node_id, status
FROM workflow_node_instances
WHERE workflow_id = @workflowId
  AND workflow_node_id = ANY(@workflowNodeIds);
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("workflowNodeIds", workflowNodeIds.ToArray());
        await using var reader = await command.ExecuteReaderAsync();

        var result = new Dictionary<long, string>();
        while (await reader.ReadAsync())
        {
            result[reader.GetInt64(0)] = reader.GetString(1);
        }

        return result;
    }

    private static async Task<WorkflowNodeInstanceRecord?> LoadWorkflowNodeInstanceByWorkflowNodeId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        long workflowNodeId)
    {
        const string sql = """
SELECT id, status
FROM workflow_node_instances
WHERE workflow_id = @workflowId
  AND workflow_node_id = @workflowNodeId
LIMIT 1
FOR UPDATE;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("workflowNodeId", workflowNodeId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new WorkflowNodeInstanceRecord
        {
            Id = reader.GetInt64(0),
            Status = reader.GetString(1)
        };
    }

    private static async Task<List<ActiveRuntimeNodeRecord>> LoadActiveRuntimeNodes(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = """
SELECT
    ni.id,
    n.node_key,
    n.node_type
FROM workflow_node_instances ni
JOIN workflow_nodes n ON n.id = ni.workflow_node_id
WHERE ni.workflow_id = @workflowId
  AND ni.status = @status;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("status", NodeInstanceStatusActive);
        await using var reader = await command.ExecuteReaderAsync();

        var activeNodes = new List<ActiveRuntimeNodeRecord>();
        while (await reader.ReadAsync())
        {
            activeNodes.Add(new ActiveRuntimeNodeRecord
            {
                NodeInstanceId = reader.GetInt64(0),
                NodeKey = reader.GetString(1),
                NodeType = reader.GetString(2)
            });
        }

        return activeNodes;
    }

    private static async Task<RuntimeWorkflowStatusContextRecord> LoadRuntimeWorkflowStatusContext(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = """
SELECT
    pt.key,
    pt.requires_supervisor_step
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
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

    private static WorkflowDefinitionSupervisorGatekeeperEvaluation EvaluateSupervisorGatekeeper(
        WorkflowDefinitionGraphRecord graph,
        string? primaryLegacyProcessTypeKey,
        bool requiresSupervisorStep)
    {
        return WorkflowDefinitionSupervisorGatekeeperRules.Evaluate(
            graph.Nodes.Select(node => new WorkflowDefinitionSupervisorGatekeeperNode
            {
                NodeKey = node.NodeKey,
                NodeType = node.NodeType,
                LegacyProcessTypeKey = TryGetNodeConfigString(node, "legacyProcessTypeKey")
            }).ToList(),
            graph.Edges.Select(edge => new WorkflowDefinitionSupervisorGatekeeperEdge
            {
                SourceNodeKey = graph.NodeById[edge.SourceNodeId].NodeKey,
                TargetNodeKey = graph.NodeById[edge.TargetNodeId].NodeKey,
                Priority = edge.Priority
            }).ToList(),
            primaryLegacyProcessTypeKey,
            requiresSupervisorStep);
    }

    private static bool IsSupervisorGatekeeperNode(
        WorkflowDefinitionGraphRecord graph,
        string nodeKey,
        string? primaryLegacyProcessTypeKey,
        bool requiresSupervisorStep)
    {
        var evaluation = EvaluateSupervisorGatekeeper(graph, primaryLegacyProcessTypeKey, requiresSupervisorStep);
        return evaluation.IsSatisfied
            && string.Equals(evaluation.GatekeeperNodeKey, nodeKey, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<WorkflowNodeExecutionRecord?> LoadActiveSupervisorGatekeeperNodeExecution(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        WorkflowDefinitionGraphRecord graph,
        string? primaryLegacyProcessTypeKey,
        bool requiresSupervisorStep)
    {
        var evaluation = EvaluateSupervisorGatekeeper(graph, primaryLegacyProcessTypeKey, requiresSupervisorStep);
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
                Config = reader.IsDBNull(7) ? null : ParseJsonElement(reader.GetString(7))
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

        var legacyTemplateKey = GetRequiredNodeConfigString(node, "legacyTemplateKey");
        var template = await LoadActiveTaskTemplateByKey(connection, transaction, legacyTemplateKey);
        if (template is null)
        {
            throw new InvalidOperationException(
                $"Node '{node.NodeKey}' references unknown or inactive legacyTemplateKey '{legacyTemplateKey}'.");
        }

        var workflowContext = await LoadWorkflowRuntimeTaskContext(connection, transaction, workflowId);
        var workflowDueAt = await LoadWorkflowDueAt(connection, transaction, workflowId);

        const string insertTaskSql = """
INSERT INTO workflow_tasks (
    workflow_id,
    node_instance_id,
    task_template_id,
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
    @taskTemplateId,
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
            insertTaskCommand.Parameters.AddWithValue("taskTemplateId", template.Id);
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
            var supervisorAssignment = await ResolveDepartmentRequirementSelectionAssignment(
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
                ? await ResolvePrimaryAssigneeUserId(
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
            (object?)assigneeResponsibilityId ?? DBNull.Value);
        insertAssignmentCommand.Parameters.AddWithValue(
            "assignmentType",
            assigneeUserId.HasValue ? "user" : "responsibility");
        await insertAssignmentCommand.ExecuteNonQueryAsync();

        return workflowTaskId;
    }

    private static async Task<bool> ShouldAutoCompleteSupervisorApprovalBridge(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        WorkflowDefinitionGraphRecord graph,
        WorkflowDefinitionNodeRecord completedNode,
        WorkflowDefinitionNodeRecord candidateNode)
    {
        if (!string.Equals(candidateNode.NodeType, "approval", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var workflowContext = await LoadWorkflowTaskGenerationContext(connection, transaction, workflowId);
        if (!workflowContext.RequiresSupervisorStep
            || string.IsNullOrWhiteSpace(workflowContext.ApprovalTaskTemplateKey))
        {
            return false;
        }

        var gatekeeperProcessTypeKey = TryGetNodeConfigString(completedNode, "legacyProcessTypeKey");
        if (!IsSupervisorGatekeeperNode(
                graph,
                completedNode.NodeKey,
                gatekeeperProcessTypeKey,
                workflowContext.RequiresSupervisorStep))
        {
            return false;
        }

        var approvalTemplateKey = TryGetNodeConfigString(candidateNode, "legacyTemplateKey");
        return string.Equals(
            approvalTemplateKey,
            workflowContext.ApprovalTaskTemplateKey,
            StringComparison.OrdinalIgnoreCase);
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
        var generatedTaskCount = await GenerateWorkflowTasks(
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

            await InsertAuditEntry(
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

        await RecalculateWorkflowTaskAvailability(connection, transaction, workflowId);
        await RecalculateAndPersistWorkflowStatus(connection, transaction, workflowId, actorUserId);
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

    private static async Task<bool> TryCompleteRuntimeSetupNodeIfReady(
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

        await InsertAuditEntry(
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
                Config = reader.IsDBNull(7) ? null : ParseJsonElement(reader.GetString(7))
            }
        };
    }

    private static async Task<WorkflowNodeExecutionRecord?> LoadActiveRuntimeMeasureNodeExecution(
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
                Config = reader.IsDBNull(7) ? null : ParseJsonElement(reader.GetString(7))
            }
        };
    }

    private static async Task TryAdvanceRuntimeSetupFromTaskStatusUpdate(
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

        var graph = await LoadWorkflowDefinitionGraph(connection, transaction, workflow.WorkflowDefinitionVersionId);
        await TryCompleteRuntimeSetupNodeIfReady(
            connection,
            transaction,
            workflowId,
            graph,
            activeSetupNode,
            await LoadStoredAnswersByKey(connection, transaction, workflowId),
            actorUserId);
    }

    private static readonly string[] MeasureGenerationNodeTypes =
    [
        "setup",
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

    private static WorkflowDefinitionNodeRecord? ResolveDecisionTarget(
        WorkflowDefinitionGraphRecord graph,
        WorkflowDefinitionNodeRecord decisionNode,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        out WorkflowDefinitionEdgeRecord? selectedEdge)
    {
        selectedEdge = null;
        if (!graph.OutgoingEdgesBySourceNodeId.TryGetValue(decisionNode.NodeId, out var outgoingEdges)
            || outgoingEdges.Count == 0)
        {
            return null;
        }

        WorkflowDefinitionEdgeRecord? fallbackEdge = null;
        foreach (var edge in outgoingEdges.OrderBy(edge => edge.Priority).ThenBy(edge => edge.EdgeId))
        {
            if (string.IsNullOrWhiteSpace(edge.ConditionExpression))
            {
                fallbackEdge ??= edge;
                continue;
            }

            var condition = ParseDecisionCondition(edge.ConditionExpression);
            if (TaskConditionEvaluator.EvaluateCondition(condition, answersByKey))
            {
                selectedEdge = edge;
                return graph.NodeById.GetValueOrDefault(edge.TargetNodeId);
            }
        }

        if (fallbackEdge is not null)
        {
            selectedEdge = fallbackEdge;
            return graph.NodeById.GetValueOrDefault(fallbackEdge.TargetNodeId);
        }

        return null;
    }

    private static TaskTemplateConditionRecord ParseDecisionCondition(string conditionExpression)
    {
        try
        {
            using var document = JsonDocument.Parse(conditionExpression);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Decision condition must be a JSON object.");
            }

            if (!root.TryGetProperty("answerKey", out var answerKeyProperty)
                || answerKeyProperty.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(answerKeyProperty.GetString()))
            {
                throw new InvalidOperationException("Decision condition requires answerKey.");
            }

            if (!root.TryGetProperty("operator", out var operatorProperty)
                || operatorProperty.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(operatorProperty.GetString()))
            {
                throw new InvalidOperationException("Decision condition requires operator.");
            }

            return new TaskTemplateConditionRecord
            {
                TaskTemplateId = 0,
                ConditionGroup = 0,
                AnswerKey = answerKeyProperty.GetString()!.Trim(),
                Operator = operatorProperty.GetString()!.Trim().ToLowerInvariant(),
                ExpectedValueText = root.TryGetProperty("expectedValueText", out var expectedTextProperty)
                    && expectedTextProperty.ValueKind == JsonValueKind.String
                    ? expectedTextProperty.GetString()
                    : null,
                ExpectedValueBoolean = root.TryGetProperty("expectedValueBoolean", out var expectedBooleanProperty)
                    && expectedBooleanProperty.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? expectedBooleanProperty.GetBoolean()
                    : null,
                ExpectedValueNumber = root.TryGetProperty("expectedValueNumber", out var expectedNumberProperty)
                    && expectedNumberProperty.ValueKind == JsonValueKind.Number
                    && expectedNumberProperty.TryGetDecimal(out var expectedNumber)
                        ? expectedNumber
                        : null
            };
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Decision condition is not valid JSON: {ex.Message}", ex);
        }
    }

    private static async Task<RuntimeWorkflowHeaderRecord?> LoadRuntimeWorkflowHeader(
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
    pt.key,
    pt.requires_supervisor_step,
    COALESCE(w.current_runtime_status, @defaultRuntimeStatus)
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
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
    pt.key,
    pt.requires_supervisor_step,
    COALESCE(w.current_runtime_status, @defaultRuntimeStatus)
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
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

    private static async Task<long?> LoadWorkflowTaskIdByNodeInstanceId(
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

    private static async Task CompleteRuntimeTaskNodeFromTaskStatusUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        Guid workflowUid,
        long nodeInstanceId,
        long actorUserId,
        string? comment)
    {
        var workflow = await LoadRuntimeWorkflowHeader(connection, transaction, workflowUid)
            ?? throw new InvalidOperationException("Workflow runtime instance was not found.");
        var graph = await LoadWorkflowDefinitionGraph(connection, transaction, workflow.WorkflowDefinitionVersionId);
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

        await InsertAuditEntry(
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
            await LoadStoredAnswersByKey(connection, transaction, workflow.WorkflowId),
            actorUserId);
    }

    private static async Task ApplyRuntimeApprovalDecisionFromWorkflowTask(
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
        var graph = await LoadWorkflowDefinitionGraph(connection, transaction, workflow.WorkflowDefinitionVersionId);
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

            await InsertAuditEntry(
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

        await InsertAuditEntry(
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
            await LoadStoredAnswersByKey(connection, transaction, workflow.WorkflowId),
            actorUserId);
    }

    private static async Task<TaskTemplateRecord?> LoadActiveTaskTemplateByKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string legacyTemplateKey)
    {
        const string sql = """
SELECT
    id,
    template_key,
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
FROM task_templates
WHERE template_key = @templateKey
  AND is_active = TRUE
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("templateKey", legacyTemplateKey.Trim());
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new TaskTemplateRecord
        {
            Id = reader.GetInt32(0),
            TemplateKey = reader.GetString(1),
            Title = reader.GetString(2),
            Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Category = reader.GetString(4),
            IconKey = NormalizeAdminTaskTemplateIconKey(reader.IsDBNull(5) ? null : reader.GetString(5)),
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

    private static async Task<WorkflowNodeExecutionRecord?> LoadNodeExecutionForUpdate(
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
                Config = reader.IsDBNull(7) ? null : ParseJsonElement(reader.GetString(7))
            }
        };
    }

    private static void EnsureActiveRuntimeNode(WorkflowNodeExecutionRecord? nodeExecution, string expectedNodeType)
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

    private static string GetRequiredNodeConfigString(WorkflowDefinitionNodeRecord node, string propertyName)
    {
        if (!node.Config.HasValue || node.Config.Value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Node '{node.NodeKey}' requires config property '{propertyName}'.");
        }

        if (!node.Config.Value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidOperationException(
                $"Node '{node.NodeKey}' requires config property '{propertyName}' as non-empty string.");
        }

        return property.GetString()!.Trim();
    }

    private static string? TryGetNodeConfigString(WorkflowDefinitionNodeRecord node, string propertyName)
    {
        if (!node.Config.HasValue || node.Config.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!node.Config.Value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            return null;
        }

        return property.GetString()!.Trim();
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
      WHERE process_type_id = @processTypeId
  );
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetWorkflowRuntimeState(
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
        await InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            null,
            actorUserId,
            "workflow_definition_runtime_completed",
            null,
            runtimeStatus);
    }

    private static async Task FailRuntimeWorkflow(
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
        await InsertAuditEntry(
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

    private static string MapLegacyStatusForActiveNodes(
        WorkflowDefinitionGraphRecord graph,
        IReadOnlyCollection<ActiveRuntimeNodeRecord> activeNodes,
        string? primaryLegacyProcessTypeKey,
        bool requiresSupervisorStep)
    {
        if (activeNodes.Any(node =>
                IsSupervisorGatekeeperNode(
                    graph,
                    node.NodeKey,
                    primaryLegacyProcessTypeKey,
                    requiresSupervisorStep)))
        {
            return "waiting_for_supervisor";
        }

        if (activeNodes.Any(node => string.Equals(node.NodeType, "approval", StringComparison.OrdinalIgnoreCase)))
        {
            return "waiting_for_supervisor";
        }

        if (activeNodes.Any(node => string.Equals(node.NodeType, "task", StringComparison.OrdinalIgnoreCase)))
        {
            return "waiting_for_department";
        }

        return "in_progress";
    }

    private static string? CreateJsonbPayload(object? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value);
    }

    private static string? NormalizeRuntimeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class PublishedWorkflowDefinitionVersionRecord
    {
        public required int WorkflowDefinitionId { get; init; }
        public required string WorkflowDefinitionKey { get; init; }
        public required string WorkflowDefinitionName { get; init; }
        public required long VersionId { get; init; }
        public required int VersionNumber { get; init; }
        public required int PrimaryLegacyProcessTypeId { get; init; }
        public required string PrimaryLegacyProcessTypeKey { get; init; }
    }

    private sealed class WorkflowDefinitionGraphRecord
    {
        public required List<WorkflowDefinitionNodeRecord> Nodes { get; init; }
        public required List<WorkflowDefinitionEdgeRecord> Edges { get; init; }
        public required Dictionary<long, WorkflowDefinitionNodeRecord> NodeById { get; init; }
        public required Dictionary<long, List<WorkflowNodeActionRecord>> NodeActionsByNodeId { get; init; }
        public required Dictionary<long, List<WorkflowDefinitionEdgeRecord>> IncomingEdgesByTargetNodeId { get; init; }
        public required Dictionary<long, List<WorkflowDefinitionEdgeRecord>> OutgoingEdgesBySourceNodeId { get; init; }
    }

    private sealed class WorkflowDefinitionNodeRecord
    {
        public required long NodeId { get; init; }
        public required string NodeKey { get; init; }
        public required string NodeType { get; init; }
        public string? Title { get; init; }
        public required int SortOrder { get; init; }
        public JsonElement? Config { get; init; }
    }

    private sealed class WorkflowDefinitionEdgeRecord
    {
        public required long EdgeId { get; init; }
        public required long SourceNodeId { get; init; }
        public required long TargetNodeId { get; init; }
        public required int Priority { get; init; }
        public string? ConditionExpression { get; init; }
    }

    private sealed class RuntimeWorkflowHeaderRecord
    {
        public required long WorkflowId { get; init; }
        public required Guid WorkflowUid { get; init; }
        public required long WorkflowDefinitionVersionId { get; init; }
        public required int RoleId { get; init; }
        public required string PrimaryLegacyProcessTypeKey { get; init; }
        public required bool RequiresSupervisorStep { get; init; }
        public required string CurrentRuntimeStatus { get; init; }
    }

    private sealed class WorkflowNodeExecutionRecord
    {
        public required long NodeInstanceId { get; init; }
        public required string Status { get; init; }
        public required WorkflowDefinitionNodeRecord Node { get; init; }
    }

    private sealed class WorkflowNodeInstanceRecord
    {
        public required long Id { get; init; }
        public required string Status { get; init; }
    }

    private sealed class ActiveRuntimeNodeRecord
    {
        public required long NodeInstanceId { get; init; }
        public required string NodeKey { get; init; }
        public required string NodeType { get; init; }
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
