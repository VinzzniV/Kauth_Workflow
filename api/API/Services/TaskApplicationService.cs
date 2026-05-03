using Microsoft.Extensions.Logging;

namespace API;

internal sealed class TaskApplicationService(
    IWorkflowRepository repository,
    IAuthorizationPolicyService authorizationPolicyService,
    IWorkflowVisibilityService workflowVisibilityService,
    IWorkflowNotificationDispatchService workflowNotificationDispatchService,
    IPersonLifecycleProjectionService personLifecycleProjectionService,
    ILogger<TaskApplicationService> logger) : ITaskApplicationService
{
    public async Task<IReadOnlyList<TaskWithWorkflowDto>> GetTasksAsync(CurrentUser currentUser, CancellationToken cancellationToken = default)
    {
        var isAdmin = authorizationPolicyService.CanManageAdminConfiguration(currentUser);
        List<TaskWithWorkflowDto> tasks;

        if (isAdmin)
        {
            tasks = await repository.GetTasks();
        }
        else
        {
            var effectiveResponsibilityIds = currentUser.EffectiveResponsibilities
                .Select(r => r.ResponsibilityId)
                .ToArray();

            // Users without override see only tasks they could possibly act on:
            // primary assignment matches user/responsibility AND workflow not terminal.
            // Mirrors IsAssignedToTask in SQL so a 10k-task table doesn't have
            // to round-trip in full just to be filtered down to a handful in-memory.
            // Override users (rare, non-admin with TasksAssignOverride) keep the
            // full list because they can act on any task.
            var hasAssignmentOverride = authorizationPolicyService.HasPermission(
                currentUser,
                AuthorizationPermissions.TasksAssignOverride);

            tasks = hasAssignmentOverride
                ? await repository.GetTasksForUser(currentUser.UserId, effectiveResponsibilityIds)
                : await repository.GetTasksForUserNarrowed(currentUser.UserId, effectiveResponsibilityIds);

            // In-memory filter still required: SQL narrowing is a superset
            // (it cannot enforce phase-aware role checks like CanRegularlyEditWorkflow).
            tasks = tasks
                .Where(task =>
                    authorizationPolicyService.CanUpdateTaskStatus(currentUser, task)
                    || authorizationPolicyService.CanDecideTaskApproval(currentUser, task))
                .ToList();
        }

        workflowVisibilityService.ApplyTaskPermissions(tasks, currentUser);
        return tasks;
    }

    public async Task<TaskWithWorkflowDto?> GetTaskByIdAsync(long taskId, CurrentUser currentUser, CancellationToken cancellationToken = default)
    {
        var task = await repository.GetTaskById(taskId);
        return EnsureVisibleTask(task, currentUser);
    }

    public async Task<TaskWithWorkflowDto?> GetTaskByRefAsync(
        string taskRef,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var task = await repository.GetTaskByRef(taskRef);
        return EnsureVisibleTask(task, currentUser);
    }

    public async Task<TaskWithWorkflowDto?> DecideTaskApprovalAsync(
        long taskId,
        TaskApprovalDecisionRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var currentTask = await repository.GetTaskById(taskId);
        if (currentTask is null)
        {
            return null;
        }

        if (!authorizationPolicyService.CanDecideTaskApproval(currentUser, currentTask))
        {
            logger.LogWarning(
                "User {UserId} denied approval decision for task {TaskId}.",
                currentUser.UserId,
                taskId);
            throw new UnauthorizedAccessException("Approval decisions require the assigned supervisor responsibility or Admin override.");
        }

        var task = await repository.DecideTaskApproval(taskId, request, currentUser.UserId);
        if (task is null)
        {
            return null;
        }

        await DispatchWorkflowTaskNotificationsAsync(task, cancellationToken);
        await ApplyPersonLifecycleProjectionIfCompletedAsync(task, currentUser.UserId, cancellationToken);

        workflowVisibilityService.ApplyTaskPermissions(task, currentUser);
        return task;
    }

    public async Task<TaskWithWorkflowDto?> UpdateTaskStatusAsync(
        long taskId,
        TaskStatusUpdateRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var currentTask = await repository.GetTaskById(taskId);
        if (currentTask is null)
        {
            return null;
        }

        if (!authorizationPolicyService.CanUpdateTaskStatus(currentUser, currentTask))
        {
            logger.LogWarning("User {UserId} denied task status update for task {TaskId}.",
                currentUser.UserId, taskId);
            throw new UnauthorizedAccessException("Task updates require the current workflow phase, matching assignment or Admin override.");
        }

        var task = await repository.UpdateTaskStatus(taskId, request.Status, currentUser.UserId);
        if (task is null)
        {
            return null;
        }

        logger.LogInformation("Task {TaskId} status updated to '{NewStatus}' by user {UserId}.",
            taskId, request.Status, currentUser.UserId);

        await DispatchWorkflowTaskNotificationsAsync(task, cancellationToken);
        await ApplyPersonLifecycleProjectionIfCompletedAsync(task, currentUser.UserId, cancellationToken);

        workflowVisibilityService.ApplyTaskPermissions(task, currentUser);
        return task;
    }

    public async Task<TaskWithWorkflowDto?> UpdateTaskStatusByRefAsync(
        string taskRef,
        TaskStatusUpdateRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var currentTask = await repository.GetTaskByRef(taskRef);
        if (currentTask is null)
        {
            return null;
        }

        if (!authorizationPolicyService.CanUpdateTaskStatus(currentUser, currentTask))
        {
            logger.LogWarning("User {UserId} denied task status update for task {TaskRef}.", currentUser.UserId, taskRef);
            throw new UnauthorizedAccessException("Task updates require the current workflow phase, matching assignment or Admin override.");
        }

        var task = await repository.UpdateTaskStatusByRef(taskRef, request.Status, currentUser.UserId);
        if (task is null)
        {
            return null;
        }

        await DispatchWorkflowTaskNotificationsAsync(task, cancellationToken);
        await ApplyPersonLifecycleProjectionIfCompletedAsync(task, currentUser.UserId, cancellationToken);
        workflowVisibilityService.ApplyTaskPermissions(task, currentUser);
        return task;
    }

    public async Task<TaskWithWorkflowDto?> UpdateTaskAssignmentAsync(
        long taskId,
        TaskAssignRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var currentTask = await repository.GetTaskById(taskId);
        if (currentTask is null)
        {
            return null;
        }

        if (!authorizationPolicyService.CanUpdateTaskAssignment(currentUser, currentTask))
        {
            logger.LogWarning("User {UserId} denied task assignment update for task {TaskId}.",
                currentUser.UserId, taskId);
            throw new UnauthorizedAccessException("Task assignment updates are limited to Admin override.");
        }

        var updatedTask = await repository.UpdateTaskAssignment(taskId, request, currentUser.UserId);
        if (updatedTask is not null)
        {
            logger.LogInformation("Task {TaskId} assignment updated by user {UserId}.", taskId, currentUser.UserId);
        }

        return updatedTask;
    }

    public async Task<TaskWithWorkflowDto?> UpdateTaskAssignmentByRefAsync(
        string taskRef,
        TaskAssignRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var currentTask = await repository.GetTaskByRef(taskRef);
        if (currentTask is null)
        {
            return null;
        }

        if (!authorizationPolicyService.CanUpdateTaskAssignment(currentUser, currentTask))
        {
            logger.LogWarning("User {UserId} denied task assignment update for task {TaskRef}.",
                currentUser.UserId, taskRef);
            throw new UnauthorizedAccessException("Task assignment updates are limited to Admin override.");
        }

        var updatedTask = await repository.UpdateTaskAssignmentByRef(taskRef, request, currentUser.UserId);
        if (updatedTask is not null)
        {
            logger.LogInformation("Task {TaskRef} assignment updated by user {UserId}.", taskRef, currentUser.UserId);
        }

        return updatedTask;
    }

    public async Task<TaskWithWorkflowDto?> AddTaskCommentAsync(
        long taskId,
        TaskCommentCreateRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var currentTask = await repository.GetTaskById(taskId);
        if (currentTask is null)
        {
            return null;
        }

        var commentAuthorizationUser = currentUser;
        if (authorizationPolicyService.CanAccessSupervisorStep(currentUser)
            && !authorizationPolicyService.CanCreateOrStartWorkflow(currentUser)
            && !authorizationPolicyService.CanManageAdminConfiguration(currentUser))
        {
            if (currentTask.Workflow is null)
            {
                throw new UnauthorizedAccessException("Workflow visibility depends on the current workflow phase and role.");
            }

            var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);
            var canObserveWorkflow = workflowVisibilityService.CanObserveWorkflow(
                currentUser,
                currentTask.Workflow.DepartmentId,
                currentTask.Workflow.WorkflowStatus,
                observableDepartmentIds);

            if (!canObserveWorkflow)
            {
                if (!authorizationPolicyService.CanAccessTechnicalTasks(currentUser))
                {
                    throw new UnauthorizedAccessException("Workflow visibility depends on the current workflow phase and role.");
                }

                commentAuthorizationUser = CreateUserWithoutRole(currentUser, AuthorizationRoles.Manager);
            }
        }

        if (!authorizationPolicyService.CanAddTaskComment(commentAuthorizationUser, currentTask))
        {
            logger.LogWarning(
                "User {UserId} denied task comment creation for task {TaskId}.",
                currentUser.UserId,
                taskId);
            throw new UnauthorizedAccessException("Kommentare erfordern Sicht auf den Vorgang und passende Bearbeitungsrechte.");
        }

        var task = await repository.AddTaskComment(taskId, request.CommentText, currentUser.UserId);
        if (task is null)
        {
            return null;
        }

        logger.LogInformation(
            "Task {TaskId} comment added by user {UserId}.",
            taskId,
            currentUser.UserId);
        workflowVisibilityService.ApplyTaskPermissions(task, currentUser);
        return task;
    }

    public async Task<TaskWithWorkflowDto?> AddTaskCommentByRefAsync(
        string taskRef,
        TaskCommentCreateRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var currentTask = await repository.GetTaskByRef(taskRef);
        if (currentTask is null)
        {
            return null;
        }

        if (!authorizationPolicyService.CanAddTaskComment(currentUser, currentTask))
        {
            logger.LogWarning("User {UserId} denied task comment creation for task {TaskRef}.", currentUser.UserId, taskRef);
            throw new UnauthorizedAccessException("Kommentare erfordern Sicht auf den Vorgang und passende Bearbeitungsrechte.");
        }

        var task = await repository.AddTaskCommentByRef(taskRef, request.CommentText, currentUser.UserId);
        if (task is null)
        {
            return null;
        }

        workflowVisibilityService.ApplyTaskPermissions(task, currentUser);
        return task;
    }

    public async Task<TaskWithWorkflowDto?> DecideTaskApprovalByRefAsync(
        string taskRef,
        TaskApprovalDecisionRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var currentTask = await repository.GetTaskByRef(taskRef);
        if (currentTask is null)
        {
            return null;
        }

        if (!authorizationPolicyService.CanDecideTaskApproval(currentUser, currentTask))
        {
            logger.LogWarning("User {UserId} denied approval decision for task {TaskRef}.", currentUser.UserId, taskRef);
            throw new UnauthorizedAccessException("Approval decisions require the assigned supervisor responsibility or Admin override.");
        }

        var task = await repository.DecideTaskApprovalByRef(taskRef, request, currentUser.UserId);
        if (task is null)
        {
            return null;
        }

        await DispatchWorkflowTaskNotificationsAsync(task, cancellationToken);
        await ApplyPersonLifecycleProjectionIfCompletedAsync(task, currentUser.UserId, cancellationToken);
        workflowVisibilityService.ApplyTaskPermissions(task, currentUser);
        return task;
    }

    private TaskWithWorkflowDto? EnsureVisibleTask(TaskWithWorkflowDto? task, CurrentUser currentUser)
    {
        if (task is null)
        {
            return null;
        }

        if (!authorizationPolicyService.CanManageAdminConfiguration(currentUser)
            && !authorizationPolicyService.CanUpdateTaskStatus(currentUser, task)
            && !authorizationPolicyService.CanDecideTaskApproval(currentUser, task)
            && !authorizationPolicyService.CanAddTaskComment(currentUser, task))
        {
            throw new UnauthorizedAccessException("Task visibility requires matching scope and assignment.");
        }

        workflowVisibilityService.ApplyTaskPermissions(task, currentUser);
        return task;
    }

    private async Task DispatchWorkflowTaskNotificationsAsync(TaskWithWorkflowDto task, CancellationToken cancellationToken)
    {
        if (task.Workflow is null)
        {
            return;
        }

        await workflowNotificationDispatchService.DispatchTaskStatusChangeNotificationsAsync(
            task.Workflow.WorkflowUid,
            cancellationToken);
    }

    private async Task ApplyPersonLifecycleProjectionIfCompletedAsync(
        TaskWithWorkflowDto task,
        long actorUserId,
        CancellationToken cancellationToken)
    {
        if (task.Workflow is null
            || !string.Equals(task.Workflow.WorkflowStatus, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await personLifecycleProjectionService.ApplyCompletedWorkflowProjectionAsync(
            task.Workflow.WorkflowUid,
            actorUserId,
            cancellationToken);
    }

    private static CurrentUser CreateUserWithoutRole(CurrentUser user, string roleKey)
    {
        return new CurrentUser
        {
            UserId = user.UserId,
            ExternalKey = user.ExternalKey,
            DisplayName = user.DisplayName,
            Email = user.Email,
            IsActive = user.IsActive,
            DepartmentId = user.DepartmentId,
            DepartmentName = user.DepartmentName,
            IdentityProvider = user.IdentityProvider,
            Groups = user.Groups,
            DirectRoles = user.DirectRoles
                .Where(role => !string.Equals(role.RoleKey, roleKey, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            GroupRoles = user.GroupRoles
                .Where(role => !string.Equals(role.RoleKey, roleKey, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            EffectiveRoles = user.EffectiveRoles
                .Where(role => !string.Equals(role.RoleKey, roleKey, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            DirectResponsibilities = user.DirectResponsibilities,
            GroupResponsibilities = user.GroupResponsibilities,
            EffectiveResponsibilities = user.EffectiveResponsibilities
        };
    }
}
