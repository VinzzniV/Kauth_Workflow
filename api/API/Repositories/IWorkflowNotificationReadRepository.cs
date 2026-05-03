namespace API;

internal interface IWorkflowNotificationReadRepository
{
    Task<List<WorkflowNotificationDispatchTarget>> GetWorkflowCreatedNotificationDispatchTargets(Guid workflowUid);
    Task<List<Guid>> GetWorkflowUidsWithDisabledNotifications(string notificationType);
}
