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

    public Task<CursorPageDto<AutomationJobDetailDto>> GetWorkflowAutomationJobsAsync(
        Guid workflowUid,
        CursorPageQuery query,
        CancellationToken cancellationToken = default)
    {
        return readRepository.GetAutomationJobs(workflowUid, query, cancellationToken);
    }

    public async Task<bool> TryProcessNextPendingJobAsync(CancellationToken cancellationToken = default)
    {
        var job = await repository.ClaimNextPendingAutomationJob(cancellationToken);
        if (job is null)
        {
            return false;
        }

        WorkflowAutomationHandlerResult? result = null;
        try
        {
            var handler = handlerRegistry.Resolve(job.ActionKey);
            result = await handler.ExecuteAsync(new WorkflowAutomationHandlerContext
            {
                JobId = job.JobId,
                WorkflowUid = job.WorkflowUid,
                ActionKey = job.ActionKey,
                AttemptNumber = job.AttemptNumber,
                Payload = job.Payload
            }, cancellationToken);

            if (!result.IsSuccess)
            {
                // Strukturierter Failure-Pfad (Etappe 9a Schritt 5 Sub-A): Handler hat
                // result.IsSuccess=false + optional FailureKind geliefert. Retry-Policy laeuft mit
                // dem FailureKind-Marker — "permanent" -> sofort FinalFail, sonst attempt-basiert.
                await HandleHandlerFailureAsync(
                    job,
                    errorMessage: result.ErrorMessage ?? "Handler reported failure without message.",
                    failureKind: result.FailureKind,
                    handlerLogs: result.Logs,
                    cancellationToken);
                return true;
            }

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
            // Exception-Pfad: Handler hat keinen strukturierten Failure-Marker — failureKind bleibt null.
            // Default-Retry-Logik (is_idempotent + attemptNumber) entscheidet.
            await HandleHandlerFailureAsync(
                job,
                errorMessage: ex.Message,
                failureKind: null,
                handlerLogs: Array.Empty<WorkflowAutomationLogEntry>(),
                cancellationToken,
                exceptionForLogging: ex);
        }

        return true;
    }

    private async Task HandleHandlerFailureAsync(
        ClaimedAutomationJobRecord job,
        string errorMessage,
        string? failureKind,
        IReadOnlyList<WorkflowAutomationLogEntry> handlerLogs,
        CancellationToken cancellationToken,
        Exception? exceptionForLogging = null)
    {
        var retryOutcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            retrySettings,
            job.AttemptNumber,
            job.IsIdempotent,
            failureKind,
            job.MaxAttemptsOverride,
            job.SubsequentRetryDelaySecondsOverride is { } seconds && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : null);
        var shouldRetry = retryOutcome.Kind == WorkflowAutomationRetryOutcome.OutcomeKind.RetryAfter;
        DateTime? retryAvailableAt = shouldRetry
            ? DateTime.UtcNow + retryOutcome.Delay
            : null;

        // Handler-Logs zuerst, dann ein synthetischer Marker mit der Retry-Entscheidung.
        var combinedLogs = handlerLogs.Concat(new[]
        {
            new WorkflowAutomationLogEntry
            {
                Level = "error",
                Message = shouldRetry
                    ? $"Automation action '{job.ActionKey}' failed and will be retried."
                    : $"Automation action '{job.ActionKey}' failed permanently.",
                Details = System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    error = errorMessage,
                    attemptNumber = job.AttemptNumber,
                    shouldRetry,
                    retryAvailableAt,
                    failureKind
                })
            }
        }).ToArray();

        try
        {
            await repository.CompleteAutomationJobFailure(
                job,
                errorMessage,
                shouldRetry,
                retryAvailableAt,
                combinedLogs,
                cancellationToken,
                failureKind: failureKind);
        }
        catch (Exception failureEx) when (failureEx is not OperationCanceledException)
        {
            logger.LogError(
                failureEx,
                "Failed to record failure for automation job {JobId} (action {ActionKey}). Attempting unclaim to allow retry.",
                job.JobId,
                job.ActionKey);
            try
            {
                await repository.UnclaimAutomationJobAsync(job.JobId, cancellationToken);
            }
            catch (Exception unclaimEx) when (unclaimEx is not OperationCanceledException)
            {
                logger.LogError(
                    unclaimEx,
                    "Failed to unclaim automation job {JobId}. Job may remain stuck in claimed state.",
                    job.JobId);
            }
        }

        logger.LogWarning(
            exceptionForLogging,
            "Automation job {JobId} for action {ActionKey} failed on attempt {AttemptNumber}. Retry={ShouldRetry} FailureKind={FailureKind}.",
            job.JobId,
            job.ActionKey,
            job.AttemptNumber,
            shouldRetry,
            failureKind ?? "<none>");

        try
        {
            await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "error",
                Source = "automation",
                Category = "job",
                EventKey = shouldRetry ? "automation_job_failed_retrying" : "automation_job_failed",
                Message = $"Automation job {job.JobId} for action {job.ActionKey} failed: {errorMessage}",
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
                    failureKind,
                    error = errorMessage
                }
            }, cancellationToken);
        }
        catch (Exception logEx) when (logEx is not OperationCanceledException)
        {
            logger.LogWarning(logEx, "Failed to write system event log for automation job {JobId} failure.", job.JobId);
        }
    }
}
