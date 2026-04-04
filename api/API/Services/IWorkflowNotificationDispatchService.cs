namespace API;

internal interface IWorkflowNotificationDispatchService
{
    Task DispatchWorkflowCreatedNotificationsAsync(
        Guid workflowUid,
        IReadOnlyList<WorkflowNotificationDispatchTarget> targets,
        CancellationToken cancellationToken = default);

    Task DispatchReadyTaskNotificationsAsync(Guid workflowUid, CancellationToken cancellationToken = default);
    Task DispatchWorkflowCompletionNotificationsAsync(Guid workflowUid, CancellationToken cancellationToken = default);
    Task DispatchTaskStatusChangeNotificationsAsync(Guid workflowUid, CancellationToken cancellationToken = default);
}
