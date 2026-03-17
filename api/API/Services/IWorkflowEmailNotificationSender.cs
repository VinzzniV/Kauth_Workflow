namespace API;

public interface IWorkflowEmailNotificationSender
{
    Task<IReadOnlyList<NotificationDispatchResult>> SendNotificationsAsync(
        Guid workflowUid,
        IReadOnlyList<WorkflowNotificationDispatchTarget> targets,
        CancellationToken cancellationToken = default);
}
