using Microsoft.Extensions.Logging;

namespace API;

internal sealed class WorkflowDefinitionRuntimeService(
    IWorkflowDefinitionRuntimeRepository repository,
    IWorkflowLifecycleService lifecycleService,
    IWorkflowNotificationDispatchService workflowNotificationDispatchService,
    IPersonLifecycleProjectionService personLifecycleProjectionService,
    ILogger<WorkflowDefinitionRuntimeService> logger) : IWorkflowDefinitionRuntimeService
{
    public async Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowInstanceAsync(
        CreateWorkflowDefinitionInstanceRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var created = await lifecycleService.CreateWorkflowInstanceAsync(request, currentUser.UserId, cancellationToken);
        await workflowNotificationDispatchService.DispatchReadyTaskNotificationsAsync(created.WorkflowUid, cancellationToken);
        if (string.Equals(created.CurrentRuntimeStatus, "completed", StringComparison.OrdinalIgnoreCase))
        {
            await personLifecycleProjectionService.ApplyCompletedWorkflowProjectionAsync(
                created.WorkflowUid,
                currentUser.UserId,
                cancellationToken);
        }
        logger.LogInformation(
            "Workflow definition runtime instance {WorkflowUid} created by user {UserId} for definition {DefinitionKey}.",
            created.WorkflowUid,
            currentUser.UserId,
            created.WorkflowDefinitionKey);
        return created;
    }

    public Task<WorkflowDefinitionRuntimeDetailDto?> GetWorkflowInstanceAsync(
        Guid workflowUid,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        _ = currentUser;
        return repository.GetWorkflowDefinitionRuntimeDetail(workflowUid);
    }

    public async Task<IReadOnlyList<WorkflowRuntimeEventDto>> GetWorkflowInstanceEventsAsync(
        Guid workflowUid,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        _ = currentUser;
        return await repository.GetWorkflowDefinitionRuntimeEvents(workflowUid);
    }

    public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteFormNodeAsync(
        Guid workflowUid,
        long nodeInstanceId,
        CompleteRuntimeFormNodeRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        return CompleteAndDispatchAsync(
            lifecycleService.CompleteFormNodeAsync(workflowUid, nodeInstanceId, request, currentUser.UserId, cancellationToken),
            cancellationToken);
    }

    public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteApprovalNodeAsync(
        Guid workflowUid,
        long nodeInstanceId,
        CompleteRuntimeApprovalNodeRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        return CompleteAndDispatchAsync(
            lifecycleService.CompleteApprovalNodeAsync(workflowUid, nodeInstanceId, request, currentUser.UserId, cancellationToken),
            cancellationToken);
    }

    public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteTaskNodeAsync(
        Guid workflowUid,
        long nodeInstanceId,
        CompleteRuntimeTaskNodeRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        return CompleteAndDispatchAsync(
            lifecycleService.CompleteTaskNodeAsync(workflowUid, nodeInstanceId, request, currentUser.UserId, cancellationToken),
            cancellationToken);
    }

    private async Task<WorkflowDefinitionRuntimeDetailDto?> CompleteAndDispatchAsync(
        Task<WorkflowDefinitionRuntimeDetailDto?> updateTask,
        CancellationToken cancellationToken)
    {
        var updated = await updateTask;
        if (updated is not null)
        {
            await workflowNotificationDispatchService.DispatchTaskStatusChangeNotificationsAsync(
                updated.WorkflowUid,
                cancellationToken);
            if (string.Equals(updated.CurrentRuntimeStatus, "completed", StringComparison.OrdinalIgnoreCase))
            {
                await personLifecycleProjectionService.ApplyCompletedWorkflowProjectionAsync(
                    updated.WorkflowUid,
                    actorUserId: null,
                    cancellationToken);
            }
        }

        return updated;
    }
}
