using Microsoft.Extensions.Logging;

namespace API;

internal sealed class TaskApplicationService(
    IWorkflowRepository repository,
    IAuthorizationPolicyService authorizationPolicyService,
    IWorkflowVisibilityService workflowVisibilityService,
    IWorkflowNotificationDispatchService workflowNotificationDispatchService,
    ILogger<TaskApplicationService> logger) : ITaskApplicationService
{
    public async Task<IReadOnlyList<TaskWithWorkflowDto>> GetTasksAsync(CurrentUser currentUser, CancellationToken cancellationToken = default)
    {
        var tasks = await repository.GetTasks();
        if (!authorizationPolicyService.CanManageAdminConfiguration(currentUser))
        {
            tasks = tasks
                .Where(task => authorizationPolicyService.CanUpdateTaskStatus(currentUser, task))
                .ToList();
        }

        workflowVisibilityService.ApplyTaskPermissions(tasks, currentUser);
        return tasks;
    }

    public async Task<TaskWithWorkflowDto?> GetTaskByIdAsync(long taskId, CurrentUser currentUser, CancellationToken cancellationToken = default)
    {
        var task = await repository.GetTaskById(taskId);
        if (task is null)
        {
            return null;
        }

        if (!authorizationPolicyService.CanManageAdminConfiguration(currentUser)
            && !authorizationPolicyService.CanUpdateTaskStatus(currentUser, task))
        {
            throw new UnauthorizedAccessException("Task visibility requires matching phase responsibility and assignment.");
        }

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
            logger.LogWarning("User {UserId} denied task status update for task {TaskId} (workflow {WorkflowUid}).",
                currentUser.UserId, taskId, currentTask.Workflow.WorkflowUid);
            throw new UnauthorizedAccessException("Task updates require the current workflow phase, matching assignment or Admin override.");
        }

        var task = await repository.UpdateTaskStatus(taskId, request.Status, currentUser.UserId);
        if (task is null)
        {
            return null;
        }

        logger.LogInformation("Task {TaskId} status updated to '{NewStatus}' by user {UserId} (workflow {WorkflowUid}).",
            taskId, request.Status, currentUser.UserId, task.Workflow.WorkflowUid);

        await workflowNotificationDispatchService.DispatchTaskStatusChangeNotificationsAsync(
            task.Workflow.WorkflowUid,
            cancellationToken);

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
                "User {UserId} denied task comment creation for task {TaskId} (workflow {WorkflowUid}).",
                currentUser.UserId,
                taskId,
                currentTask.Workflow.WorkflowUid);
            throw new UnauthorizedAccessException("Kommentare erfordern Sicht auf den Vorgang und passende Bearbeitungsrechte.");
        }

        var task = await repository.AddTaskComment(taskId, request.CommentText, currentUser.UserId);
        if (task is null)
        {
            return null;
        }

        logger.LogInformation(
            "Task {TaskId} comment added by user {UserId} (workflow {WorkflowUid}).",
            taskId,
            currentUser.UserId,
            task.Workflow.WorkflowUid);
        workflowVisibilityService.ApplyTaskPermissions(task, currentUser);
        return task;
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
