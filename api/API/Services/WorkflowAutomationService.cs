using Microsoft.Extensions.Logging;

namespace API;

internal sealed class WorkflowAutomationService(
    IWorkflowAutomationRepository repository,
    IWorkflowAutomationReadRepository readRepository,
    IWorkflowAutomationHandlerRegistry handlerRegistry,
    ISystemEventLogService systemEventLogService,
    IWorkflowLifecycleService lifecycleService,
    WorkflowAutomationRetrySettings retrySettings,
    ILogger<WorkflowAutomationService> logger) : IWorkflowAutomationService
{
    public Task<AdminListPageDto<ActionDefinitionDto>> GetActionDefinitionsAsync(AdminListQuery query, CancellationToken cancellationToken = default)
    {
        return readRepository.GetAdminActionDefinitions(query, cancellationToken);
    }

    public Task<IReadOnlyList<AutomationJobDetailDto>> GetWorkflowAutomationJobsAsync(
        Guid workflowUid,
        CancellationToken cancellationToken = default)
    {
        return readRepository.GetAutomationJobs(workflowUid, cancellationToken);
    }

    public async Task<bool> TryProcessNextPendingJobAsync(CancellationToken cancellationToken = default)
    {
        var job = await repository.ClaimNextPendingAutomationJob(cancellationToken);
        if (job is null)
        {
            return false;
        }

        try
        {
            var handler = handlerRegistry.Resolve(job.ActionKey);
            var result = await handler.ExecuteAsync(new WorkflowAutomationHandlerContext
            {
                JobId = job.JobId,
                WorkflowUid = job.WorkflowUid,
                ActionKey = job.ActionKey,
                AttemptNumber = job.AttemptNumber,
                Payload = job.Payload
            }, cancellationToken);

            await lifecycleService.OnAutomationJobCompletedAsync(job, result, cancellationToken);
            logger.LogInformation(
                "Automation job {JobId} for action {ActionKey} completed successfully on attempt {AttemptNumber}.",
                job.JobId,
                job.ActionKey,
                job.AttemptNumber);

            await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "info",
                Source = "automation",
                Category = "job",
                EventKey = "automation_job_succeeded",
                Message = $"Automation job {job.JobId} for action {job.ActionKey} completed successfully.",
                WorkflowUid = job.WorkflowUid,
                EntityType = "automation_job",
                EntityId = job.JobId.ToString(),
                Details = new
                {
                    job.JobId,
                    job.ActionKey,
                    job.AttemptNumber
                }
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var shouldRetry = job.IsIdempotent && job.AttemptNumber < retrySettings.MaxAttempts;
            DateTime? retryAvailableAt = shouldRetry
                ? DateTime.UtcNow + retrySettings.ResolveRetryDelay(job.AttemptNumber)
                : null;

            await repository.CompleteAutomationJobFailure(
                job,
                ex.Message,
                shouldRetry,
                retryAvailableAt,
                [
                    new WorkflowAutomationLogEntry
                    {
                        Level = "error",
                        Message = shouldRetry
                            ? $"Automation action '{job.ActionKey}' failed and will be retried."
                            : $"Automation action '{job.ActionKey}' failed permanently.",
                        Details = System.Text.Json.JsonSerializer.SerializeToElement(new
                        {
                            error = ex.Message,
                            attemptNumber = job.AttemptNumber,
                            shouldRetry,
                            retryAvailableAt
                        })
                    }
                ],
                cancellationToken);

            logger.LogWarning(
                ex,
                "Automation job {JobId} for action {ActionKey} failed on attempt {AttemptNumber}. Retry={ShouldRetry}.",
                job.JobId,
                job.ActionKey,
                job.AttemptNumber,
                shouldRetry);

            await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "error",
                Source = "automation",
                Category = "job",
                EventKey = shouldRetry ? "automation_job_failed_retrying" : "automation_job_failed",
                Message = $"Automation job {job.JobId} for action {job.ActionKey} failed: {ex.Message}",
                WorkflowUid = job.WorkflowUid,
                EntityType = "automation_job",
                EntityId = job.JobId.ToString(),
                Details = new
                {
                    job.JobId,
                    job.ActionKey,
                    job.AttemptNumber,
                    shouldRetry,
                    retryAvailableAt,
                    error = ex.Message
                }
            }, cancellationToken);
        }

        return true;
    }
}
