namespace API;

internal interface IWorkflowEmailNotificationSender
{
    Task<IReadOnlyList<NotificationDispatchResult>> SendNotificationsAsync(
        Guid workflowUid,
        IReadOnlyList<WorkflowNotificationDispatchTarget> targets,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDispatchResult>> SendRotationNotificationsAsync(
        IReadOnlyList<RotationNotificationDispatchTarget> targets,
        CancellationToken cancellationToken = default);
}
