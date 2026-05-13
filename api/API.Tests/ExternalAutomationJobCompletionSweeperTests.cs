using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Tests;

public sealed class ExternalAutomationJobCompletionSweeperTests
{
    [Fact]
    public async Task ProcessNextBatchAsync_NoClaims_ReturnsZeroAndDoesNotCallLifecycle()
    {
        var repo = new StubAutomationRepository();
        var lifecycle = new StubLifecycleService();

        var count = await ExternalAutomationJobCompletionSweeper.ProcessNextBatchAsync(repo, lifecycle, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Equal(0, lifecycle.SuccessCalls.Count);
        Assert.Equal(0, lifecycle.FailureCalls.Count);
    }

    [Fact]
    public async Task ProcessNextBatchAsync_SucceededClaim_TriggersSuccessLifecycle()
    {
        var repo = new StubAutomationRepository
        {
            ClaimedBatch =
            [
                new ExternalCompletionClaim { JobId = 42, Status = "succeeded" }
            ]
        };
        var lifecycle = new StubLifecycleService();

        var count = await ExternalAutomationJobCompletionSweeper.ProcessNextBatchAsync(repo, lifecycle, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Single(lifecycle.SuccessCalls);
        Assert.Equal(42, lifecycle.SuccessCalls[0]);
        Assert.Empty(lifecycle.FailureCalls);
    }

    [Fact]
    public async Task ProcessNextBatchAsync_FailedClaim_TriggersFailureLifecycle()
    {
        var repo = new StubAutomationRepository
        {
            ClaimedBatch =
            [
                new ExternalCompletionClaim { JobId = 13, Status = "failed" }
            ]
        };
        var lifecycle = new StubLifecycleService();

        var count = await ExternalAutomationJobCompletionSweeper.ProcessNextBatchAsync(repo, lifecycle, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Empty(lifecycle.SuccessCalls);
        Assert.Single(lifecycle.FailureCalls);
        Assert.Equal(13, lifecycle.FailureCalls[0]);
    }

    [Fact]
    public async Task ProcessNextBatchAsync_UnknownStatus_SkipsWithoutLifecycleCall()
    {
        var repo = new StubAutomationRepository
        {
            ClaimedBatch =
            [
                new ExternalCompletionClaim { JobId = 7, Status = "weird" }
            ]
        };
        var lifecycle = new StubLifecycleService();

        var count = await ExternalAutomationJobCompletionSweeper.ProcessNextBatchAsync(repo, lifecycle, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Empty(lifecycle.SuccessCalls);
        Assert.Empty(lifecycle.FailureCalls);
    }

    [Fact]
    public async Task ProcessNextBatchAsync_LifecycleException_DoesNotPropagate_OtherClaimsContinue()
    {
        var repo = new StubAutomationRepository
        {
            ClaimedBatch =
            [
                new ExternalCompletionClaim { JobId = 1, Status = "succeeded" },
                new ExternalCompletionClaim { JobId = 2, Status = "succeeded" },
            ]
        };
        var lifecycle = new StubLifecycleService
        {
            ThrowOnSuccessForJobId = 1
        };

        var count = await ExternalAutomationJobCompletionSweeper.ProcessNextBatchAsync(repo, lifecycle, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(2, count);
        // Beide wurden versucht, aber nur Job 2 erfolgreich verarbeitet. Job 1 bleibt mit
        // completion_claimed_at gesetzt; der naechste Sweep nach 5min greift erneut zu.
        Assert.Equal(new[] { 1L, 2L }, lifecycle.SuccessCalls.ToArray());
    }

    private sealed class StubAutomationRepository : IWorkflowAutomationRepository
    {
        public List<ExternalCompletionClaim> ClaimedBatch { get; set; } = [];

        public Task<IReadOnlyList<ExternalCompletionClaim>> ClaimExternalCompletionsBatchAsync(int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExternalCompletionClaim>>(ClaimedBatch);

        public Task<ClaimedAutomationJobRecord?> ClaimNextPendingAutomationJob(CancellationToken cancellationToken = default)
            => Task.FromResult<ClaimedAutomationJobRecord?>(null);
        public Task CompleteAutomationJobFailure(ClaimedAutomationJobRecord job, string errorMessage, bool shouldRetry, DateTime? retryAvailableAt, IReadOnlyList<WorkflowAutomationLogEntry> logs, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task UnclaimAutomationJobAsync(long jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ExternalCompletionContext?> LoadExternalCompletionContextAsync(long jobId, CancellationToken cancellationToken = default)
            => Task.FromResult<ExternalCompletionContext?>(null);
        public Task ApplyExternalCompletionSuccessAsync(long jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApplyExternalCompletionFailureAsync(long jobId, WorkflowAutomationRetryOutcome outcome, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> ReleaseStaleWorkerClaimsAsync(TimeSpan staleTimeout, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class StubLifecycleService : IWorkflowLifecycleService
    {
        public List<long> SuccessCalls { get; } = [];
        public List<long> FailureCalls { get; } = [];
        public long? ThrowOnSuccessForJobId { get; set; }

        public Task OnExternalAutomationJobSucceededAsync(long jobId, CancellationToken cancellationToken)
        {
            SuccessCalls.Add(jobId);
            if (ThrowOnSuccessForJobId == jobId)
            {
                throw new InvalidOperationException("Simulated lifecycle failure.");
            }
            return Task.CompletedTask;
        }

        public Task OnExternalAutomationJobFailedAsync(long jobId, CancellationToken cancellationToken)
        {
            FailureCalls.Add(jobId);
            return Task.CompletedTask;
        }

        public Task<TaskWithWorkflowDto?> UpdateTaskStatusAsync(long taskId, string status, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> UpdateTaskStatusByRefAsync(string taskRef, string status, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> DecideTaskApprovalAsync(long taskId, TaskApprovalDecisionRequest request, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> DecideTaskApprovalByRefAsync(string taskRef, TaskApprovalDecisionRequest request, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task OnAutomationJobCompletedAsync(ClaimedAutomationJobRecord job, WorkflowAutomationHandlerResult result, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowInstanceAsync(CreateWorkflowDefinitionInstanceRequest request, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteFormNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeFormNodeRequest request, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteApprovalNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeApprovalNodeRequest request, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteTaskNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeTaskNodeRequest request, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
