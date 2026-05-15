using AdAutomationWorker.Core.Configuration;
using AdAutomationWorker.Core.Handlers;
using AdAutomationWorker.Core.Polling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdAutomationWorker;

// Hauptschleife des Worker-Service.
// Jeden Zyklus:
//   1. Stale-Claims zuruecksetzen (Lazy-Cleanup; siehe Plan-Schritt-Lease-Konkretisierung).
//   2. Naechsten pending Job atomar claimen.
//   3. Handler aus der HandlerRegistry holen + parallel zum Handler einen Heartbeat-Loop starten.
//   4. Job-Ergebnis via Completer schreiben.
//   5. Sleep PollingInterval (default 5s) — nur wenn kein Job gepickt wurde, sonst sofort weiter.
//
// Bei Handler-Exception wird der Job mit error_message=<exception> + Final-Logs als failed markiert;
// die Retry-/Final-Entscheidung trifft anschliessend der Sweeper auf der Linux-API.
internal sealed class WorkerHostedService : BackgroundService
{
    private readonly IWorkerJobStore store;
    private readonly IWorkerPlanStore planStore;
    private readonly HandlerRegistry handlerRegistry;
    private readonly WorkerHeartbeatLoop heartbeatLoop;
    private readonly WorkerSettings settings;
    private readonly ILogger<WorkerHostedService> logger;
    private readonly string workerId;

    public WorkerHostedService(
        IWorkerJobStore store,
        IWorkerPlanStore planStore,
        HandlerRegistry handlerRegistry,
        WorkerHeartbeatLoop heartbeatLoop,
        IOptions<WorkerSettings> options,
        ILogger<WorkerHostedService> logger)
    {
        this.store = store;
        this.planStore = planStore;
        this.handlerRegistry = handlerRegistry;
        this.heartbeatLoop = heartbeatLoop;
        this.settings = options.Value;
        this.logger = logger;
        this.workerId = ResolveWorkerId(this.settings.WorkerId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "AD-Automation worker started (workerId={WorkerId}, polling={PollingSeconds}s, heartbeat={HeartbeatSeconds}s, stale={StaleMinutes}min).",
            workerId,
            settings.PollingIntervalSeconds,
            settings.HeartbeatIntervalSeconds,
            settings.StaleClaimTimeoutMinutes);

        var pollingInterval = TimeSpan.FromSeconds(Math.Max(1, settings.PollingIntervalSeconds));
        var heartbeatInterval = TimeSpan.FromSeconds(Math.Max(1, settings.HeartbeatIntervalSeconds));
        var staleTimeout = TimeSpan.FromMinutes(Math.Max(1, settings.StaleClaimTimeoutMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await store.ReleaseStaleClaimsAsync(staleTimeout, stoppingToken);
                await planStore.ReleaseStaleClaimsAsync(TimeSpan.FromSeconds(30), stoppingToken);

                var claim = await store.ClaimNextPendingJobAsync(workerId, stoppingToken);
                if (claim is not null)
                {
                    await ProcessClaimAsync(claim, heartbeatInterval, stoppingToken);
                    continue;
                }

                var planClaim = await planStore.ClaimNextPlanRequestAsync(workerId, stoppingToken);
                if (planClaim is not null)
                {
                    await TryProcessNextPlanRequestAsync(planClaim, stoppingToken);
                    continue;
                }

                await Task.Delay(pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Worker polling cycle crashed; backing off for {Seconds}s.", pollingInterval.TotalSeconds);
                try
                {
                    await Task.Delay(pollingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        logger.LogInformation("AD-Automation worker stopped (workerId={WorkerId}).", workerId);
    }

    private async Task TryProcessNextPlanRequestAsync(WorkerPlanClaim claim, CancellationToken stoppingToken)
    {
        if (!handlerRegistry.TryGet(claim.ActionKey, out var handler))
        {
            logger.LogError(
                "No handler registered for action_key '{ActionKey}' on plan request {RequestId}; failing request.",
                claim.ActionKey,
                claim.RequestId);
            await planStore.MarkPlanRequestFailedAsync(
                claim.RequestId,
                $"No worker handler registered for action_key '{claim.ActionKey}'.",
                stoppingToken);
            return;
        }

        var context = new WorkerPlanContext
        {
            WorkflowInstanceUid = claim.WorkflowInstanceUid,
            ActionKey = claim.ActionKey,
            Payload = claim.PayloadJson,
        };

        WorkerPlanResult result;
        try
        {
            result = await handler.PlanAsync(context, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PlanAsync {ActionKey} threw for plan request {RequestId}.", claim.ActionKey, claim.RequestId);
            await planStore.MarkPlanRequestFailedAsync(claim.RequestId, ex.Message, stoppingToken);
            return;
        }

        if (result.IsSuccess && result.Plan is not null)
        {
            await planStore.MarkPlanRequestCompletedAsync(claim.RequestId, result.Plan, stoppingToken);
        }
        else
        {
            await planStore.MarkPlanRequestFailedAsync(
                claim.RequestId,
                result.ErrorMessage ?? "PlanAsync reported failure without message.",
                stoppingToken);
        }
    }

    private async Task ProcessClaimAsync(WorkerJobClaim claim, TimeSpan heartbeatInterval, CancellationToken stoppingToken)
    {
        if (!handlerRegistry.TryGet(claim.ActionKey, out var handler))
        {
            logger.LogError(
                "No handler registered for action_key '{ActionKey}' on job {JobId}; failing job.",
                claim.ActionKey,
                claim.JobId);
            await store.MarkJobFailedAsync(
                claim.JobId,
                claim.AttemptNumber,
                $"No worker handler registered for action_key '{claim.ActionKey}'.",
                Array.Empty<WorkerLogEntry>(),
                stoppingToken,
                failureKind: WorkerFailureKinds.Permanent);
            return;
        }

        using var handlerCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeatTask = heartbeatLoop.RunAsync(claim.JobId, workerId, heartbeatInterval, handlerCts.Token);

        var context = new WorkerHandlerContext
        {
            JobId = claim.JobId,
            WorkflowUid = claim.WorkflowUid,
            WorkflowNodeInstanceId = claim.WorkflowNodeInstanceId,
            ActionKey = claim.ActionKey,
            Payload = claim.Payload,
            AttemptNumber = claim.AttemptNumber
        };

        WorkerHandlerResult result;
        try
        {
            result = await handler.ExecuteAsync(context, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            handlerCts.Cancel();
            await SafeAwait(heartbeatTask);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Handler {ActionKey} threw for job {JobId}.", claim.ActionKey, claim.JobId);
            handlerCts.Cancel();
            await SafeAwait(heartbeatTask);
            // Unbehandelte Handler-Exceptions sind per Default transient — der Handler hatte
            // keine Chance zu klassifizieren. Linux-API behandelt das wie bisher (Retry je nach
            // is_idempotent).
            await store.MarkJobFailedAsync(
                claim.JobId,
                claim.AttemptNumber,
                ex.Message,
                Array.Empty<WorkerLogEntry>(),
                stoppingToken,
                failureKind: WorkerFailureKinds.Transient);
            return;
        }

        handlerCts.Cancel();
        await SafeAwait(heartbeatTask);

        if (result.IsSuccess)
        {
            await store.MarkJobSucceededAsync(claim.JobId, claim.AttemptNumber, result.Output, result.Logs, stoppingToken, result.VaultWrite);
        }
        else
        {
            await store.MarkJobFailedAsync(
                claim.JobId,
                claim.AttemptNumber,
                result.ErrorMessage ?? "Handler reported failure without message.",
                result.Logs,
                stoppingToken,
                failureKind: result.FailureKind,
                output: result.Output);
        }
    }

    private static async Task SafeAwait(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // Heartbeat-Loop hat sich selbst geloggt; hier nicht erneut werfen.
        }
    }

    private static string ResolveWorkerId(string configuredId)
    {
        if (!string.IsNullOrWhiteSpace(configuredId))
        {
            return configuredId.Trim();
        }

        return $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    }
}
