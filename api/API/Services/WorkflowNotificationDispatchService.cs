using Microsoft.Extensions.Logging;

namespace API;

internal sealed class WorkflowNotificationDispatchService(
    IWorkflowRepository repository,
    IWorkflowEmailNotificationSender workflowEmailNotificationSender,
    ISystemEventLogService systemEventLogService,
    ILogger<WorkflowNotificationDispatchService> logger) : IWorkflowNotificationDispatchService
{
    public async Task DispatchWorkflowCreatedNotificationsAsync(
        Guid workflowUid,
        IReadOnlyList<WorkflowNotificationDispatchTarget> targets,
        CancellationToken cancellationToken = default)
    {
        await DispatchTargetsAsync(workflowUid, targets, cancellationToken);
    }

    public async Task DispatchReadyTaskNotificationsAsync(Guid workflowUid, CancellationToken cancellationToken = default)
    {
        var targets = await repository.CreateReadyTaskNotifications(workflowUid);
        await DispatchTargetsAsync(workflowUid, targets, cancellationToken);
    }

    public async Task DispatchWorkflowCompletionNotificationsAsync(Guid workflowUid, CancellationToken cancellationToken = default)
    {
        var targets = await repository.CreateWorkflowCompletionNotifications(workflowUid);
        await DispatchTargetsAsync(workflowUid, targets, cancellationToken);
    }

    public async Task DispatchTaskStatusChangeNotificationsAsync(Guid workflowUid, CancellationToken cancellationToken = default)
    {
        await DispatchReadyTaskNotificationsAsync(workflowUid, cancellationToken);
        await DispatchWorkflowCompletionNotificationsAsync(workflowUid, cancellationToken);
    }

    private async Task DispatchTargetsAsync(
        Guid workflowUid,
        IReadOnlyList<WorkflowNotificationDispatchTarget> targets,
        CancellationToken cancellationToken)
    {
        if (targets.Count == 0)
        {
            return;
        }

        var dispatchResults = await workflowEmailNotificationSender.SendNotificationsAsync(
            workflowUid,
            targets,
            cancellationToken);

        if (dispatchResults.Count > 0)
        {
            var failedCount = dispatchResults.Count(r => !r.Success && r.Attempted);
            if (failedCount > 0)
            {
                logger.LogWarning("Workflow {WorkflowUid}: {FailedCount} of {TotalCount} notifications failed to send.",
                    workflowUid, failedCount, dispatchResults.Count);

                await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                {
                    Severity = "warning",
                    Source = "workflow",
                    Category = "notifications",
                    EventKey = "workflow_notification_dispatch_partial_failure",
                    Message = $"Workflow notification dispatch finished with failures: {failedCount} of {dispatchResults.Count}.",
                    WorkflowUid = workflowUid,
                    Details = new
                    {
                        failedCount,
                        totalCount = dispatchResults.Count,
                        statuses = dispatchResults.GroupBy(item => item.Status)
                            .ToDictionary(group => group.Key, group => group.Count())
                    }
                }, cancellationToken);
            }
            else
            {
                logger.LogInformation("Workflow {WorkflowUid}: {TotalCount} notifications dispatched.",
                    workflowUid, dispatchResults.Count);

                await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                {
                    Severity = "info",
                    Source = "workflow",
                    Category = "notifications",
                    EventKey = "workflow_notification_dispatch_succeeded",
                    Message = $"Workflow notifications dispatched successfully ({dispatchResults.Count}).",
                    WorkflowUid = workflowUid,
                    Details = new { totalCount = dispatchResults.Count }
                }, cancellationToken);
            }

            await repository.ApplyNotificationDispatchResults(dispatchResults);
        }
    }
}
