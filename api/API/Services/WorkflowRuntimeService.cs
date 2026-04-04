using Microsoft.Extensions.Logging;

namespace API;

internal sealed class WorkflowRuntimeService(
    IWorkflowRepository repository,
    IAuthorizationPolicyService authorizationPolicyService,
    IWorkflowVisibilityService workflowVisibilityService,
    IWorkflowNotificationDispatchService workflowNotificationDispatchService,
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
        if (string.IsNullOrWhiteSpace(request.ProcessTypeKey))
        {
            throw new InvalidOperationException("Der Prozesstyp ist erforderlich.");
        }

        var normalizedProcessTypeKey = request.ProcessTypeKey.Trim().ToLowerInvariant();
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

        var requestValidationError = ValidateCreateWorkflowRequest(request, selectedProcessType);
        if (requestValidationError is not null)
        {
            throw new InvalidOperationException(requestValidationError);
        }

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
        if (requestedDepartmentId.HasValue
            && authorizationPolicyService.HasPermission(currentUser, AuthorizationPermissions.WorkflowCreate(normalizedProcessTypeKey))
            && !authorizationPolicyService.HasPermission(currentUser, AuthorizationPermissions.WorkflowCreate(normalizedProcessTypeKey), requestedDepartmentId.Value)
            && !authorizationPolicyService.HasPermission(currentUser, AuthorizationPermissions.WorkflowsViewAll)
            && !authorizationPolicyService.HasAnyRole(currentUser, AuthorizationRoles.Hr, AuthorizationRoles.Admin))
        {
            logger.LogWarning("User {UserId} denied workflow creation for process type {ProcessTypeKey}: department {DepartmentId} not permitted.",
                currentUser.UserId, normalizedProcessTypeKey, requestedDepartmentId.Value);
            throw new UnauthorizedAccessException("Der gewählte Vorgang ist nicht für die ausgewählte Abteilung freigegeben.");
        }

        if (request.DeadlineDate.HasValue && request.DeadlineDate.Value < DateOnly.FromDateTime(DateTime.Today))
        {
            throw new InvalidOperationException("Die Deadline darf nicht in der Vergangenheit liegen.");
        }

        var creation = await repository.CreateWorkflow(request, currentUser.UserId);
        await workflowNotificationDispatchService.DispatchWorkflowCreatedNotificationsAsync(
            creation.Uid,
            creation.NotificationTargets,
            cancellationToken);

        var workflow = await repository.GetWorkflowByUid(creation.Uid);
        if (workflow is null)
        {
            throw new WorkflowRuntimeConsistencyException("Workflow was created but could not be loaded afterwards.");
        }

        var taskCount = workflow.Tasks.Count;
        var assignmentCount = workflow.Tasks.Sum(task => task.Assignments.Count);
        var pendingNotifications = workflow.Notifications.Count(notification => notification.Status == "pending");
        var failedNotifications = workflow.Notifications.Count(notification => notification.Status == "failed");

        logger.LogInformation(
            "Workflow {WorkflowUid} created by user {UserId} (process: {ProcessTypeKey}, tasks: {TaskCount}, notifications: {NotificationCount}, failed: {FailedNotifications}).",
            creation.Uid, currentUser.UserId, normalizedProcessTypeKey, taskCount, workflow.Notifications.Count, failedNotifications);

        return new WorkflowCreateResponse
        {
            Uid = creation.Uid,
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
                }
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
        WorkflowProcessTypeDto selectedProcessType)
    {
        if (selectedProcessType.RequiresTargetPerson)
        {
            return request.TargetPersonId.HasValue
                ? null
                : $"Der Prozesstyp '{selectedProcessType.Name}' erfordert eine bestehende Zielperson.";
        }

        if (request.TargetPersonId.HasValue)
        {
            return $"Der Prozesstyp '{selectedProcessType.Name}' darf nicht mit einer bestehenden Zielperson angelegt werden.";
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return $"Der Prozesstyp '{selectedProcessType.Name}' erfordert Vorname und Nachname der neuen Person.";
        }

        if (!request.EmployeeNumber.HasValue || request.EmployeeNumber.Value <= 0)
        {
            return $"Der Prozesstyp '{selectedProcessType.Name}' erfordert eine gültige Personalnummer.";
        }

        if (!request.BadgeNumber.HasValue || request.BadgeNumber.Value <= 0)
        {
            return $"Der Prozesstyp '{selectedProcessType.Name}' erfordert eine gültige Kartennummer.";
        }

        if (!request.DepartmentId.HasValue || !request.RoleId.HasValue)
        {
            return $"Der Prozesstyp '{selectedProcessType.Name}' erfordert Abteilung und Stelle.";
        }

        return null;
    }
}
