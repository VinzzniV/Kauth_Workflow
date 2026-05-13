using Npgsql;

namespace API;

internal sealed class WorkflowLifecycleService(
    IWorkflowRepository workflowRepository,
    IWorkflowLifecycleScopedRepository scopedRepository,
    IWorkflowAuditWriteOperations auditWrite,
    IWorkflowStatusCalculationService statusCalculation,
    IWorkflowNotificationDispatchOperations notificationDispatch,
    IWorkflowAutomationRepository automationRepository,
    WorkflowAutomationRetrySettings retrySettings) : IWorkflowLifecycleService
{
    public async Task<TaskWithWorkflowDto?> UpdateTaskStatusAsync(long taskId, string status, long actorUserId, CancellationToken cancellationToken = default)
    {
        var normalizedStatus = TaskStatusRules.NormalizeTaskStatus(status);
        await using var connection = new NpgsqlConnection(LifecycleRuntimeSettingsResolver.GetRequiredConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var result = await scopedRepository.UpdateTaskStatusInScope(connection, transaction, taskId, normalizedStatus, actorUserId, cancellationToken);
        if (result is null)
            return null;
        if (result.ShouldCompleteRuntimeTaskNode && result.RuntimeNodeInstanceId.HasValue)
            await scopedRepository.CompleteRuntimeTaskNodeInScope(connection, transaction, result.WorkflowId, result.WorkflowUid, result.RuntimeNodeInstanceId.Value, actorUserId, cancellationToken: cancellationToken);
        else if (result.ShouldTryAdvanceRuntimeSetup)
            await scopedRepository.TryAdvanceRuntimeSetupInScope(connection, transaction, result.WorkflowId, result.WorkflowUid, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await workflowRepository.GetTaskById(taskId);
    }

    public Task<TaskWithWorkflowDto?> UpdateTaskStatusByRefAsync(string taskRef, string status, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (WorkflowTaskRef.TryParse(taskRef, out var workflowTaskId))
            return UpdateTaskStatusAsync(workflowTaskId, status, actorUserId, cancellationToken);

        // Rotation-Tasks bleiben ausserhalb der Lifecycle-Engine; Repo routet auf Rotation-Repository.
        return workflowRepository.UpdateTaskStatusByRef(taskRef, status, actorUserId);
    }

    public async Task<TaskWithWorkflowDto?> DecideTaskApprovalAsync(long taskId, TaskApprovalDecisionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var connection = new NpgsqlConnection(LifecycleRuntimeSettingsResolver.GetRequiredConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var result = await scopedRepository.DecideTaskApprovalInScope(connection, transaction, taskId, request, actorUserId, cancellationToken);
        if (result is null)
            return null;
        await scopedRepository.ApplyApprovalNodeDecisionInScope(connection, transaction, result.WorkflowId, result.WorkflowUid, result.NodeInstanceId, request.Approved, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await workflowRepository.GetTaskById(taskId);
    }

    public Task<TaskWithWorkflowDto?> DecideTaskApprovalByRefAsync(string taskRef, TaskApprovalDecisionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (WorkflowTaskRef.TryParse(taskRef, out var workflowTaskId))
            return DecideTaskApprovalAsync(workflowTaskId, request, actorUserId, cancellationToken);

        // Rotation-Tasks bleiben ausserhalb der Lifecycle-Engine.
        return workflowRepository.DecideTaskApprovalByRef(taskRef, request, actorUserId);
    }

    public async Task OnAutomationJobCompletedAsync(ClaimedAutomationJobRecord job, WorkflowAutomationHandlerResult result, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(LifecycleRuntimeSettingsResolver.GetRequiredConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await scopedRepository.CompleteAutomationJobSuccessInScope(connection, transaction, job, result, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // Etappe 9a Schritt 2: Externer Worker hat Attempt + Logs + Status bereits geschrieben.
    // Wir machen nur den Workflow-Fortschritt + setzen completion_processed_at.
    public Task OnExternalAutomationJobSucceededAsync(long jobId, CancellationToken cancellationToken)
    {
        return automationRepository.ApplyExternalCompletionSuccessAsync(jobId, cancellationToken);
    }

    public async Task OnExternalAutomationJobFailedAsync(long jobId, CancellationToken cancellationToken)
    {
        var context = await automationRepository.LoadExternalCompletionContextAsync(jobId, cancellationToken)
            ?? throw new InvalidOperationException($"External completion context for job {jobId} could not be loaded.");

        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            retrySettings,
            context.AttemptNumber,
            context.IsIdempotent,
            context.FailureKind,
            context.MaxAttemptsOverride,
            context.SubsequentRetryDelaySecondsOverride is { } seconds && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : null);

        await automationRepository.ApplyExternalCompletionFailureAsync(jobId, outcome, cancellationToken);
    }

    public Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowInstanceAsync(CreateWorkflowDefinitionInstanceRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.WorkflowDefinitionKey))
        {
            throw new InvalidOperationException("WorkflowDefinitionKey is required.");
        }

        return ExecuteDefinitionRuntimeMutationAsync(async (connection, transaction) =>
        {
            var publishedVersion = await PostgresWorkflowRuntimeRepository.LoadPublishedWorkflowDefinitionVersion(
                connection,
                transaction,
                request.WorkflowDefinitionKey);
            if (publishedVersion is null)
            {
                throw new InvalidOperationException(
                    $"No published workflow definition exists for key '{request.WorkflowDefinitionKey.Trim().ToLowerInvariant()}'.");
            }

            var workflowDefinition = await PostgresWorkflowRepository.LoadWorkflowDefinitionForCreate(connection, transaction, publishedVersion.WorkflowDefinitionKey);
            var graph = await PostgresRepositorySharedHelpers.LoadWorkflowDefinitionGraph(connection, transaction, publishedVersion.VersionId);

            var gatekeeperEvaluation = WorkflowRuntimeEngine.EvaluateSupervisorGatekeeper(
                graph,
                publishedVersion.WorkflowDefinitionKey,
                workflowDefinition.RequiresSupervisorStep);
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

            if (!request.TargetPersonId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Die publizierte Workflow-Definition '{publishedVersion.WorkflowDefinitionKey}' erfordert eine bestehende Zielperson.");
            }

            var targetPerson = await PostgresRepositorySharedHelpers.LoadTargetPerson(connection, transaction, request.TargetPersonId.Value);

            var effectiveDepartmentId = request.DepartmentId ?? targetPerson.DepartmentId;
            int? effectiveRoleId = request.RoleId ?? targetPerson.RoleId;

            if (request.RoleId.HasValue)
            {
                if (!effectiveDepartmentId.HasValue)
                {
                    throw new InvalidOperationException("Die Abteilung ist erforderlich, wenn eine Zielrolle direkt angegeben wird.");
                }

                await PostgresRepositorySharedHelpers.EnsureValidPositionRole(connection, transaction, request.RoleId.Value, effectiveDepartmentId.Value);
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
            if (request.RoleId.HasValue)
            {
                await PostgresRepositorySharedHelpers.EnsureValidPositionRole(connection, transaction, roleId, departmentId);
            }

            var firstName = PostgresWorkflowRuntimeRepository.NormalizeRuntimeOptionalText(request.FirstName);
            var lastName = PostgresWorkflowRuntimeRepository.NormalizeRuntimeOptionalText(request.LastName);
            if (!workflowDefinition.RequiresTargetPerson)
            {
                var derivedFirstName = targetPerson.FirstName;
                var derivedLastName = targetPerson.LastName;
                if (string.IsNullOrWhiteSpace(derivedFirstName) || string.IsNullOrWhiteSpace(derivedLastName))
                {
                    (derivedFirstName, derivedLastName) = PostgresWorkflowRepository.SplitDisplayName(targetPerson.DisplayName);
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
    workflow_definition_id,
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
    @workflowStatus,
    NOW()
)
RETURNING id, uid;
""";

            long workflowId;
            Guid workflowUid;
            await using (var insertWorkflowCommand = new NpgsqlCommand(insertWorkflowSql, connection, transaction))
            {
                insertWorkflowCommand.Parameters.AddWithValue("processTypeId", publishedVersion.WorkflowDefinitionId);
                insertWorkflowCommand.Parameters.AddWithValue("workflowDefinitionVersionId", publishedVersion.VersionId);
                insertWorkflowCommand.Parameters.AddWithValue("departmentId", departmentId);
                insertWorkflowCommand.Parameters.AddWithValue("roleId", roleId);
                insertWorkflowCommand.Parameters.AddWithValue("createdByUserId", actorUserId);
                insertWorkflowCommand.Parameters.AddWithValue("targetPersonId", request.TargetPersonId.Value);
                insertWorkflowCommand.Parameters.AddWithValue("firstName", firstName);
                insertWorkflowCommand.Parameters.AddWithValue("lastName", lastName);
                insertWorkflowCommand.Parameters.AddWithValue("employeeNumber", employeeNumber.Value);
                insertWorkflowCommand.Parameters.AddWithValue("badgeNumber", badgeNumber.Value);
                insertWorkflowCommand.Parameters.Add("deadlineDate", NpgsqlTypes.NpgsqlDbType.Date).Value =
                    (object?)request.DeadlineDate ?? DBNull.Value;
                insertWorkflowCommand.Parameters.AddWithValue("currentRuntimeStatus", "running");
                insertWorkflowCommand.Parameters.AddWithValue("workflowStatus", "draft");

                await using var reader = await insertWorkflowCommand.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    throw new InvalidOperationException("Workflow runtime instance could not be created.");
                }

                workflowId = reader.GetInt64(0);
                workflowUid = reader.GetGuid(1);
            }

            await PostgresRepositorySharedHelpers.InsertAuditEntry(
                connection,
                transaction,
                workflowId,
                null,
                actorUserId,
                "workflow_definition_runtime_created",
                null,
                "running",
                publishedVersion.WorkflowDefinitionKey);

            var startNode = graph.Nodes.Single(node => string.Equals(node.NodeType, "start", StringComparison.OrdinalIgnoreCase));

            var startNodeInstanceId = await PostgresWorkflowRuntimeRepository.CreateWorkflowNodeInstance(
                connection,
                transaction,
                workflowId,
                startNode,
                PostgresWorkflowRuntimeRepository.NodeInstanceStatusDone,
                PostgresWorkflowRuntimeRepository.CreateJsonbPayload(new { auto = true, reason = "runtime_start" }));

            await PostgresWorkflowRuntimeRepository.InsertWorkflowRuntimeEvent(
                connection,
                transaction,
                workflowId,
                null,
                "workflow_started",
                PostgresWorkflowRuntimeRepository.CreateJsonbPayload(new
                {
                    workflowDefinitionKey = publishedVersion.WorkflowDefinitionKey,
                    workflowDefinitionVersionId = publishedVersion.VersionId
                }));

            await PostgresWorkflowRuntimeRepository.InsertWorkflowRuntimeEvent(
                connection,
                transaction,
                workflowId,
                startNodeInstanceId,
                "node_completed",
                PostgresWorkflowRuntimeRepository.CreateJsonbPayload(new { nodeKey = startNode.NodeKey, nodeType = startNode.NodeType, auto = true }));

            await PostgresWorkflowRuntimeRepository.AdvanceRuntimeUntilWaitOrTerminal(
                connection,
                transaction,
                workflowId,
                graph,
                startNode,
                await PostgresRepositorySharedHelpers.LoadStoredAnswersByKey(connection, transaction, workflowId),
                actorUserId);

            await notificationDispatch.CreateWorkflowNotifications(
                connection,
                transaction,
                workflowId,
                departmentId,
                workflowDefinition.RequiresSupervisorStep,
                publishedVersion.WorkflowDefinitionKey,
                publishedVersion.WorkflowDefinitionName);

            return await PostgresWorkflowRuntimeRepository.GetWorkflowDefinitionRuntimeDetailInternal(connection, transaction, workflowUid)
                ?? throw new InvalidOperationException("Workflow runtime instance could not be loaded after creation.");
        }, cancellationToken);
    }

    public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteFormNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeFormNodeRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteDefinitionRuntimeMutationAsync(async (connection, transaction) =>
        {
            var workflow = await PostgresWorkflowRuntimeRepository.LoadRuntimeWorkflowHeader(connection, transaction, workflowUid);
            if (workflow is null)
            {
                return null;
            }

            var graph = await PostgresRepositorySharedHelpers.LoadWorkflowDefinitionGraph(connection, transaction, workflow.WorkflowDefinitionVersionId);
            var nodeExecution = await PostgresWorkflowRuntimeRepository.LoadNodeExecutionForUpdate(connection, transaction, workflow.WorkflowId, nodeInstanceId);
            PostgresWorkflowRuntimeRepository.EnsureActiveRuntimeNode(nodeExecution, "form");
            await PostgresWorkflowRuntimeRepository.CompleteRuntimeFormNodeInternal(
                connection,
                transaction,
                workflow,
                graph,
                nodeExecution!,
                request.RequirementSelections ?? [],
                actorUserId);

            return await PostgresWorkflowRuntimeRepository.GetWorkflowDefinitionRuntimeDetailInternal(connection, transaction, workflowUid);
        }, cancellationToken);
    }

    public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteApprovalNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeApprovalNodeRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteDefinitionRuntimeMutationAsync(async (connection, transaction) =>
        {
            var workflow = await PostgresWorkflowRuntimeRepository.LoadRuntimeWorkflowHeader(connection, transaction, workflowUid);
            if (workflow is null)
            {
                return null;
            }

            var nodeExecution = await PostgresWorkflowRuntimeRepository.LoadNodeExecutionForUpdate(connection, transaction, workflow.WorkflowId, nodeInstanceId);
            PostgresWorkflowRuntimeRepository.EnsureActiveRuntimeNode(nodeExecution, "approval");
            var linkedTaskId = await PostgresWorkflowRuntimeRepository.LoadWorkflowTaskIdByNodeInstanceId(connection, transaction, nodeInstanceId);
            if (linkedTaskId.HasValue)
            {
                var taskRecord = await statusCalculation.LoadTaskStateForUpdate(connection, transaction, linkedTaskId.Value);
                if (!taskRecord.HasValue)
                {
                    throw new InvalidOperationException("Approval task could not be loaded.");
                }

                var (_, _, taskStatus, _, _, _, taskTitle, _, _) = taskRecord.Value;
                if (!TaskStatusRules.TerminalTaskStatuses.Contains(taskStatus))
                {
                    await statusCalculation.PersistTaskStatus(connection, transaction, linkedTaskId.Value, "done");
                    await auditWrite.InsertAuditEntry(
                        connection,
                        transaction,
                        workflow.WorkflowId,
                        linkedTaskId.Value,
                        actorUserId,
                        "task_status_changed",
                        taskStatus,
                        "done",
                        PostgresRepositorySharedHelpers.BuildTaskStatusAuditDetail(taskTitle));
                    await statusCalculation.SyncPrimaryAssignmentCompletion(connection, transaction, linkedTaskId.Value, "done");
                }
            }

            await PostgresWorkflowRuntimeRepository.ApplyApprovalNodeDecision(
                connection,
                transaction,
                workflow.WorkflowId,
                workflowUid,
                nodeInstanceId,
                request.Approved,
                actorUserId);

            return await PostgresWorkflowRuntimeRepository.GetWorkflowDefinitionRuntimeDetailInternal(connection, transaction, workflowUid);
        }, cancellationToken);
    }

    public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteTaskNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeTaskNodeRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteDefinitionRuntimeMutationAsync(async (connection, transaction) =>
        {
            var workflow = await PostgresWorkflowRuntimeRepository.LoadRuntimeWorkflowHeader(connection, transaction, workflowUid);
            if (workflow is null)
            {
                return null;
            }

            var nodeExecution = await PostgresWorkflowRuntimeRepository.LoadNodeExecutionForUpdate(connection, transaction, workflow.WorkflowId, nodeInstanceId);
            PostgresWorkflowRuntimeRepository.EnsureActiveRuntimeNode(nodeExecution, "task");
            var linkedTaskId = await PostgresWorkflowRuntimeRepository.LoadWorkflowTaskIdByNodeInstanceId(connection, transaction, nodeInstanceId);
            if (linkedTaskId.HasValue)
            {
                var taskRecord = await statusCalculation.LoadTaskStateForUpdate(connection, transaction, linkedTaskId.Value);
                if (!taskRecord.HasValue)
                {
                    throw new InvalidOperationException("Runtime task could not be loaded.");
                }

                var (_, _, taskStatus, _, _, _, taskTitle, _, _) = taskRecord.Value;
                if (!TaskStatusRules.TerminalTaskStatuses.Contains(taskStatus))
                {
                    await statusCalculation.PersistTaskStatus(connection, transaction, linkedTaskId.Value, "done");
                    await auditWrite.InsertAuditEntry(
                        connection,
                        transaction,
                        workflow.WorkflowId,
                        linkedTaskId.Value,
                        actorUserId,
                        "task_status_changed",
                        taskStatus,
                        "done",
                        PostgresRepositorySharedHelpers.BuildTaskStatusAuditDetail(taskTitle));
                    await statusCalculation.SyncPrimaryAssignmentCompletion(connection, transaction, linkedTaskId.Value, "done");
                }
            }

            await PostgresWorkflowRuntimeRepository.CompleteTaskNodeRuntimeSide(
                connection,
                transaction,
                workflow.WorkflowId,
                workflowUid,
                nodeInstanceId,
                actorUserId,
                request.Comment);

            return await PostgresWorkflowRuntimeRepository.GetWorkflowDefinitionRuntimeDetailInternal(connection, transaction, workflowUid);
        }, cancellationToken);
    }

    private static async Task<T> ExecuteDefinitionRuntimeMutationAsync<T>(
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> mutation,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(LifecycleRuntimeSettingsResolver.GetRequiredConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var result = await mutation(connection, transaction);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
