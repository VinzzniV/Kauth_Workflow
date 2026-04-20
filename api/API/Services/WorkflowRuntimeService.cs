using Microsoft.Extensions.Logging;

namespace API;

internal sealed class WorkflowRuntimeService(
    IWorkflowRepository repository,
    IWorkflowDefinitionRuntimeRepository workflowDefinitionRuntimeRepository,
    IAuthorizationPolicyService authorizationPolicyService,
    IWorkflowVisibilityService workflowVisibilityService,
    IWorkflowNotificationDispatchService workflowNotificationDispatchService,
    IPersonLifecycleProjectionService personLifecycleProjectionService,
    ILogger<WorkflowRuntimeService> logger) : IWorkflowRuntimeService
{
    private static readonly HashSet<string> SupportedWorkflowRuntimeStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "draft",
        "waiting_for_supervisor",
        "waiting_for_department",
        "in_progress",
        "completed"
    };

    public async Task<WorkflowCreateResponse> CreateWorkflowAsync(
        CreateWorkflowRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var startableDefinitions = await repository.GetStartableWorkflowDefinitions();
        var selectedDefinition = ResolveRequestedWorkflowDefinition(request, startableDefinitions);

        var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);

        PersonWorkflowHistoryDto? targetPersonHistory = null;
        if (request.TargetPersonId.HasValue)
        {
            targetPersonHistory = await repository.GetPersonWorkflowHistory(request.TargetPersonId.Value);
        }

        if (observableDepartmentIds is not null && request.TargetPersonId.HasValue)
        {
            if (targetPersonHistory is null)
            {
                throw new InvalidOperationException("Die angegebene Zielperson wurde nicht gefunden.");
            }

            if (!targetPersonHistory.DepartmentId.HasValue
                || !observableDepartmentIds.Contains(targetPersonHistory.DepartmentId.Value))
            {
                throw new UnauthorizedAccessException("Die ausgewählte Zielperson liegt außerhalb Ihrer freigegebenen Abteilungen.");
            }
        }

        var requestedDepartmentId = request.DepartmentId ?? targetPersonHistory?.DepartmentId;
        var normalizedLegacyProcessTypeKey = string.IsNullOrWhiteSpace(request.ProcessTypeKey)
            ? null
            : request.ProcessTypeKey.Trim().ToLowerInvariant();
        var primaryPermissionKey = selectedDefinition?.DefinitionKey ?? normalizedLegacyProcessTypeKey;
        var legacyPermissionKey = selectedDefinition?.PrimaryLegacyProcessTypeKey ?? normalizedLegacyProcessTypeKey;
        if (requestedDepartmentId.HasValue
            && HasUnscopedWorkflowCreatePermission(currentUser, primaryPermissionKey, legacyPermissionKey)
            && !HasScopedWorkflowCreatePermission(currentUser, requestedDepartmentId.Value, primaryPermissionKey, legacyPermissionKey)
            && !authorizationPolicyService.HasPermission(currentUser, AuthorizationPermissions.WorkflowsViewAll)
            && !authorizationPolicyService.HasAnyRole(currentUser, AuthorizationRoles.Hr, AuthorizationRoles.Admin))
        {
            logger.LogWarning(
                "User {UserId} denied workflow creation for key {WorkflowKey}: department {DepartmentId} not permitted.",
                currentUser.UserId,
                primaryPermissionKey,
                requestedDepartmentId.Value);
            throw new UnauthorizedAccessException("Der gewählte Workflow ist nicht für die ausgewählte Abteilung freigegeben.");
        }

        if (request.DeadlineDate.HasValue && request.DeadlineDate.Value < DateOnly.FromDateTime(DateTime.Today))
        {
            throw new InvalidOperationException("Die Deadline darf nicht in der Vergangenheit liegen.");
        }

        Guid workflowUid;
        if (selectedDefinition is not null)
        {
            var managerCreatableDefinition = await repository.IsManagerCreatableProcessType(selectedDefinition.PrimaryLegacyProcessTypeKey);
            if (!CanCreateWorkflowDefinition(currentUser, selectedDefinition.DefinitionKey, selectedDefinition.PrimaryLegacyProcessTypeKey, managerCreatableDefinition))
            {
                logger.LogWarning(
                    "User {UserId} denied workflow creation for definition {DefinitionKey}: insufficient role.",
                    currentUser.UserId,
                    selectedDefinition.DefinitionKey);
                throw new UnauthorizedAccessException("Der gewählte Workflow ist für Ihre Rolle nicht freigegeben.");
            }

            var requestValidationError = ValidateCreateWorkflowRequest(
                request,
                selectedDefinition.Name,
                selectedDefinition.RequiresTargetPerson);
            if (requestValidationError is not null)
            {
                throw new InvalidOperationException(requestValidationError);
            }

            var created = await workflowDefinitionRuntimeRepository.CreateWorkflowDefinitionInstance(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = selectedDefinition.DefinitionKey,
                    DepartmentId = request.DepartmentId,
                    RoleId = request.RoleId,
                    TargetPersonId = request.TargetPersonId,
                    SourceWorkflowUid = request.SourceWorkflowUid,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    EmployeeNumber = request.EmployeeNumber,
                    BadgeNumber = request.BadgeNumber,
                    DeadlineDate = request.DeadlineDate
                },
                currentUser.UserId);

            workflowUid = created.WorkflowUid;
            await workflowNotificationDispatchService.DispatchWorkflowCreatedNotificationsAsync(
                workflowUid,
                await repository.GetWorkflowCreatedNotificationDispatchTargets(workflowUid),
                cancellationToken);
            await workflowNotificationDispatchService.DispatchReadyTaskNotificationsAsync(
                workflowUid,
                cancellationToken);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(normalizedLegacyProcessTypeKey))
            {
                throw new InvalidOperationException("workflowDefinitionKey oder processTypeKey ist erforderlich.");
            }

            var normalizedProcessTypeKey = normalizedLegacyProcessTypeKey;
            var managerCreatableProcessType = await repository.IsManagerCreatableProcessType(normalizedProcessTypeKey);
            if (!authorizationPolicyService.CanCreateWorkflowForProcessType(currentUser, normalizedProcessTypeKey, managerCreatableProcessType))
            {
                logger.LogWarning("User {UserId} denied workflow creation for process type {ProcessTypeKey}: insufficient role.",
                    currentUser.UserId, normalizedProcessTypeKey);
                throw new UnauthorizedAccessException("Der gewählte Prozesstyp ist für Ihre Rolle nicht freigegeben.");
            }

            var selectedProcessType = (await repository.GetActiveProcessTypes())
                .FirstOrDefault(processType => string.Equals(processType.Key, normalizedProcessTypeKey, StringComparison.OrdinalIgnoreCase));
            if (selectedProcessType is null)
            {
                throw new InvalidOperationException($"Unbekannter oder inaktiver Prozesstyp '{normalizedProcessTypeKey}'.");
            }

            var requestValidationError = ValidateCreateWorkflowRequest(
                request,
                selectedProcessType.Name,
                selectedProcessType.RequiresTargetPerson);
            if (requestValidationError is not null)
            {
                throw new InvalidOperationException(requestValidationError);
            }

            var creation = await repository.CreateWorkflow(request, currentUser.UserId);
            workflowUid = creation.Uid;
            await workflowNotificationDispatchService.DispatchWorkflowCreatedNotificationsAsync(
                creation.Uid,
                creation.NotificationTargets,
                cancellationToken);
        }

        var workflow = await repository.GetWorkflowByUid(workflowUid);
        if (workflow is null)
        {
            throw new WorkflowRuntimeConsistencyException("Workflow was created but could not be loaded afterwards.");
        }

        await ApplyPersonLifecycleProjectionIfCompletedAsync(workflow, currentUser.UserId, cancellationToken);

        var taskCount = workflow.Tasks.Count;
        var assignmentCount = workflow.Tasks.Sum(task => task.Assignments.Count);
        var pendingNotifications = workflow.Notifications.Count(notification => notification.Status == "pending");
        var failedNotifications = workflow.Notifications.Count(notification => notification.Status == "failed");

        logger.LogInformation(
            "Workflow {WorkflowUid} created by user {UserId} (tasks: {TaskCount}, notifications: {NotificationCount}, failed: {FailedNotifications}).",
            workflowUid, currentUser.UserId, taskCount, workflow.Notifications.Count, failedNotifications);

        return new WorkflowCreateResponse
        {
            Uid = workflowUid,
            NotificationTargets = workflow.Notifications.Count,
            FailedNotifications = failedNotifications,
            Summary = new WorkflowCreateSummaryDto
            {
                WorkflowStatus = workflow.WorkflowStatus,
                TaskCount = taskCount,
                ReadyTaskCount = workflow.Tasks.Count(task => task.Status == "ready"),
                BlockedTaskCount = workflow.Tasks.Count(task => task.Status == "blocked"),
                DoneTaskCount = workflow.Tasks.Count(task => task.Status == "done"),
                AssignmentCount = assignmentCount,
                PendingNotifications = pendingNotifications
            }
        };
    }

    public async Task<WorkflowTargetPersonDto> CreatePersonAsync(
        CreatePersonRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.DepartmentId.HasValue || request.DepartmentId.Value <= 0)
        {
            throw new InvalidOperationException("Die Stamm-Abteilung ist erforderlich.");
        }

        var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);
        if (observableDepartmentIds is not null && !observableDepartmentIds.Contains(request.DepartmentId.Value))
        {
            throw new UnauthorizedAccessException("Die ausgewählte Stamm-Abteilung liegt außerhalb Ihrer freigegebenen Abteilungen.");
        }

        return await repository.CreatePerson(request, currentUser.UserId);
    }

    public async Task<WorkflowListPageDto> GetWorkflowsAsync(
        string? status,
        int? department,
        string? processTypeKey,
        string? search,
        string? responsibility,
        int? limit,
        int? offset,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        if (limit.HasValue && limit.Value <= 0)
        {
            throw new InvalidOperationException("limit must be greater than 0.");
        }

        if (offset.HasValue && offset.Value < 0)
        {
            throw new InvalidOperationException("offset must be greater than or equal to 0.");
        }

        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? null
            : status.Trim().ToLowerInvariant();
        if (normalizedStatus is not null && !SupportedWorkflowRuntimeStatuses.Contains(normalizedStatus))
        {
            throw new InvalidOperationException($"status '{status}' is not supported.");
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToLowerInvariant();
        var normalizedProcessTypeKey = string.IsNullOrWhiteSpace(processTypeKey)
            ? null
            : processTypeKey.Trim().ToLowerInvariant();
        var normalizedResponsibility = string.IsNullOrWhiteSpace(responsibility)
            ? null
            : responsibility.Trim();
        var isPaged = limit.HasValue || offset.HasValue;
        var effectiveOffset = offset ?? 0;

        var query = new WorkflowListQuery
        {
            ReaderOnly = !authorizationPolicyService.CanCreateWorkflow(currentUser),
            Status = normalizedStatus,
            ProcessTypeKey = normalizedProcessTypeKey,
            Search = normalizedSearch,
            DepartmentId = department,
            Responsibility = normalizedResponsibility,
            Limit = limit,
            Offset = effectiveOffset,
            IncludeFilterOptions = isPaged,
            ObservableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser)
        };

        var result = await repository.GetFilteredWorkflows(query);
        return new WorkflowListPageDto
        {
            Items = result.Items,
            Count = result.TotalCount,
            Offset = effectiveOffset,
            Limit = limit,
            DepartmentOptions = result.DepartmentOptions,
            ResponsibilityOptions = result.ResponsibilityOptions
        };
    }

    public async Task<WorkflowDetailDto?> GetWorkflowByUidAsync(
        Guid workflowUid,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var workflow = await repository.GetWorkflowByUid(workflowUid);
        if (workflow is null)
        {
            return null;
        }

        await EnsureWorkflowVisibilityAsync(workflow.DepartmentId, workflow.WorkflowStatus, currentUser);
        workflowVisibilityService.ApplyWorkflowTaskPermissions(workflow, currentUser);
        return workflow;
    }

    public async Task<IReadOnlyList<WorkflowAuditEntryDto>?> GetWorkflowAuditLogAsync(
        Guid workflowUid,
        int limit,
        int offset,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new InvalidOperationException("limit must be greater than 0.");
        }

        if (offset < 0)
        {
            throw new InvalidOperationException("offset must be greater than or equal to 0.");
        }

        var workflow = await repository.GetWorkflowByUid(workflowUid);
        if (workflow is null)
        {
            return null;
        }

        await EnsureWorkflowVisibilityAsync(workflow.DepartmentId, workflow.WorkflowStatus, currentUser);
        var effectiveLimit = Math.Min(limit, 500);
        return await repository.GetWorkflowAuditLog(workflowUid, effectiveLimit, offset);
    }

    public async Task<IReadOnlyList<TaskWithWorkflowDto>?> GetWorkflowTasksAsync(
        Guid workflowUid,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var workflow = await repository.GetWorkflowByUid(workflowUid);
        if (workflow is null)
        {
            return null;
        }

        await EnsureWorkflowVisibilityAsync(workflow.DepartmentId, workflow.WorkflowStatus, currentUser);
        workflowVisibilityService.ApplyWorkflowTaskPermissions(workflow, currentUser);
        return workflow.Tasks
            .Select(task => new TaskWithWorkflowDto
            {
                TaskRef = WorkflowTaskRef.Build(task.Id),
                TaskFamily = TaskFamilyNames.Workflow,
                Task = task,
                Workflow = new TaskWorkflowContextDto
                {
                    WorkflowId = 0,
                    WorkflowUid = workflow.Uid,
                    WorkflowStatus = workflow.WorkflowStatus,
                    WorkflowLegacyStatus = WorkflowStatusRules.ToLegacyStatus(workflow.WorkflowStatus),
                    WorkflowCreatedAt = workflow.CreatedAt,
                    FirstName = workflow.FirstName,
                    LastName = workflow.LastName,
                    EmployeeNumber = workflow.EmployeeNumber,
                    BadgeNumber = workflow.BadgeNumber,
                    DepartmentId = workflow.DepartmentId,
                    DepartmentName = workflow.DepartmentName,
                    RoleId = workflow.RoleId,
                    RoleName = workflow.RoleName
                },
                Rotation = null
            })
            .ToList();
    }

    public async Task<bool> ArchiveWorkflowAsync(Guid workflowUid, CurrentUser currentUser, CancellationToken cancellationToken = default)
    {
        var archived = await repository.ArchiveWorkflow(workflowUid, currentUser.UserId);
        if (archived)
        {
            logger.LogInformation("Workflow {WorkflowUid} archived by user {UserId}.", workflowUid, currentUser.UserId);
        }

        return archived;
    }

    public async Task<bool> DeleteWorkflowAsync(Guid workflowUid, CurrentUser currentUser, CancellationToken cancellationToken = default)
    {
        var deleted = await repository.DeleteDraftWorkflow(workflowUid);
        if (deleted)
        {
            logger.LogInformation("Workflow draft {WorkflowUid} deleted by user {UserId}.", workflowUid, currentUser.UserId);
        }

        return deleted;
    }

    public async Task<PersonWorkflowHistoryDto?> GetPersonWorkflowHistoryAsync(
        long personId,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var history = await repository.GetPersonWorkflowHistory(personId);
        if (history is null)
        {
            return null;
        }

        var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);
        if (observableDepartmentIds is not null
            && (!history.DepartmentId.HasValue || !observableDepartmentIds.Contains(history.DepartmentId.Value)))
        {
            throw new UnauthorizedAccessException("Die Person liegt außerhalb Ihrer freigegebenen Abteilungen.");
        }

        return history;
    }

    private async Task EnsureWorkflowVisibilityAsync(int departmentId, string workflowStatus, CurrentUser currentUser)
    {
        var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);
        if (!workflowVisibilityService.CanObserveWorkflow(currentUser, departmentId, workflowStatus, observableDepartmentIds))
        {
            throw new UnauthorizedAccessException("Workflow visibility depends on the current workflow phase and role.");
        }
    }

    private static string? ValidateCreateWorkflowRequest(
        CreateWorkflowRequest request,
        string workflowLabel,
        bool requiresTargetPerson)
    {
        if (!request.TargetPersonId.HasValue)
        {
            return requiresTargetPerson
                ? $"Der Workflow '{workflowLabel}' erfordert eine bestehende Zielperson."
                : $"Der Workflow '{workflowLabel}' erfordert einen bestehenden Person-Stammsatz. Für neue Mitarbeitende muss zuerst die Person angelegt werden.";
        }

        if (requiresTargetPerson)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return $"Der Workflow '{workflowLabel}' erfordert Vorname und Nachname der Person als Snapshot.";
        }

        if (!request.EmployeeNumber.HasValue || request.EmployeeNumber.Value <= 0)
        {
            return $"Der Workflow '{workflowLabel}' erfordert eine gültige Personalnummer.";
        }

        if (!request.BadgeNumber.HasValue || request.BadgeNumber.Value <= 0)
        {
            return $"Der Workflow '{workflowLabel}' erfordert eine gültige Kartennummer.";
        }

        if (!request.DepartmentId.HasValue || !request.RoleId.HasValue)
        {
            return $"Der Workflow '{workflowLabel}' erfordert Abteilung und Stelle.";
        }

        return null;
    }

    private async Task ApplyPersonLifecycleProjectionIfCompletedAsync(
        WorkflowDetailDto workflow,
        long actorUserId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(workflow.WorkflowStatus, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await personLifecycleProjectionService.ApplyCompletedWorkflowProjectionAsync(
            workflow.Uid,
            actorUserId,
            cancellationToken);
    }

    private bool CanCreateWorkflowDefinition(
        CurrentUser currentUser,
        string workflowDefinitionKey,
        string? primaryLegacyProcessTypeKey,
        bool managerCreatableDefinition)
    {
        if (authorizationPolicyService.HasPermission(
                currentUser,
                AuthorizationPermissions.WorkflowCreate(workflowDefinitionKey)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(primaryLegacyProcessTypeKey)
            && authorizationPolicyService.HasPermission(
                currentUser,
                AuthorizationPermissions.WorkflowCreate(primaryLegacyProcessTypeKey)))
        {
            return true;
        }

        if (authorizationPolicyService.HasAnyRole(currentUser, AuthorizationRoles.Admin, AuthorizationRoles.Hr))
        {
            return true;
        }

        return managerCreatableDefinition
            && authorizationPolicyService.HasAnyRole(currentUser, AuthorizationRoles.Manager);
    }

    private bool HasUnscopedWorkflowCreatePermission(
        CurrentUser currentUser,
        string? workflowDefinitionKey,
        string? legacyProcessTypeKey)
    {
        return HasWorkflowCreatePermission(currentUser, workflowDefinitionKey)
            || HasWorkflowCreatePermission(currentUser, legacyProcessTypeKey);
    }

    private bool HasScopedWorkflowCreatePermission(
        CurrentUser currentUser,
        int departmentId,
        string? workflowDefinitionKey,
        string? legacyProcessTypeKey)
    {
        return HasWorkflowCreatePermission(currentUser, workflowDefinitionKey, departmentId)
            || HasWorkflowCreatePermission(currentUser, legacyProcessTypeKey, departmentId);
    }

    private bool HasWorkflowCreatePermission(CurrentUser currentUser, string? workflowKey, int? departmentId = null)
    {
        if (string.IsNullOrWhiteSpace(workflowKey))
        {
            return false;
        }

        var permissionKey = AuthorizationPermissions.WorkflowCreate(workflowKey);
        return departmentId.HasValue
            ? authorizationPolicyService.HasPermission(currentUser, permissionKey, departmentId.Value)
            : authorizationPolicyService.HasPermission(currentUser, permissionKey);
    }

    private static WorkflowStartableDefinitionDto? ResolveRequestedWorkflowDefinition(
        CreateWorkflowRequest request,
        IReadOnlyList<WorkflowStartableDefinitionDto> definitions)
    {
        if (!string.IsNullOrWhiteSpace(request.WorkflowDefinitionKey))
        {
            var normalizedDefinitionKey = request.WorkflowDefinitionKey.Trim().ToLowerInvariant();
            return definitions.FirstOrDefault(definition =>
                string.Equals(definition.DefinitionKey, normalizedDefinitionKey, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.ProcessTypeKey))
        {
            var normalizedProcessTypeKey = request.ProcessTypeKey.Trim().ToLowerInvariant();
            return definitions.FirstOrDefault(definition =>
                string.Equals(definition.DefinitionKey, normalizedProcessTypeKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(definition.PrimaryLegacyProcessTypeKey, normalizedProcessTypeKey, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }
}
