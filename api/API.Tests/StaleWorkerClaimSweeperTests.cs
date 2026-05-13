using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Tests;

public sealed class StaleWorkerClaimSweeperTests
{
    [Fact]
    public async Task ProcessNextSweepAsync_NoStaleClaims_ReturnsZero()
    {
        var repo = new StubAutomationRepository { ReleaseReturnValue = 0 };

        var count = await StaleWorkerClaimSweeper.ProcessNextSweepAsync(repo, TimeSpan.FromMinutes(5), NullLogger.Instance, CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Single(repo.ReleaseCalls);
        Assert.Equal(TimeSpan.FromMinutes(5), repo.ReleaseCalls[0]);
    }

    [Fact]
    public async Task ProcessNextSweepAsync_OneStaleClaim_ReleasesAndReturnsCount()
    {
        var repo = new StubAutomationRepository { ReleaseReturnValue = 1 };

        var count = await StaleWorkerClaimSweeper.ProcessNextSweepAsync(repo, TimeSpan.FromMinutes(5), NullLogger.Instance, CancellationToken.None);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ProcessNextSweepAsync_RepositoryException_ReturnsZeroDoesNotPropagate()
    {
        var repo = new StubAutomationRepository { ReleaseException = new InvalidOperationException("DB down") };

        var count = await StaleWorkerClaimSweeper.ProcessNextSweepAsync(repo, TimeSpan.FromMinutes(5), NullLogger.Instance, CancellationToken.None);

        // Bewusst geschluckt: ein einzelner Sweep-Crash darf den HostedService nicht killen.
        // Der Failure-Backoff im aeusseren Loop sorgt fuer Retry.
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ProcessNextSweepAsync_Cancellation_Propagates()
    {
        var repo = new StubAutomationRepository
        {
            ReleaseException = new OperationCanceledException(),
        };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => StaleWorkerClaimSweeper.ProcessNextSweepAsync(repo, TimeSpan.FromMinutes(5), NullLogger.Instance, cts.Token));
    }

    private sealed class StubAutomationRepository : IWorkflowAutomationRepository
    {
        public List<TimeSpan> ReleaseCalls { get; } = [];
        public int ReleaseReturnValue { get; set; }
        public Exception? ReleaseException { get; set; }

        public Task<int> ReleaseStaleWorkerClaimsAsync(TimeSpan staleTimeout, CancellationToken cancellationToken = default)
        {
            ReleaseCalls.Add(staleTimeout);
            if (ReleaseException is not null) throw ReleaseException;
            return Task.FromResult(ReleaseReturnValue);
        }

        // Restliche Methoden des IWorkflowAutomationRepository sind hier irrelevant — der Sweeper
        // ruft nur ReleaseStaleWorkerClaimsAsync.
        public Task<IReadOnlyList<ExternalCompletionClaim>> ClaimExternalCompletionsBatchAsync(int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExternalCompletionClaim>>([]);
        public Task<ClaimedAutomationJobRecord?> ClaimNextPendingAutomationJob(CancellationToken cancellationToken = default)
            => Task.FromResult<ClaimedAutomationJobRecord?>(null);
        public Task CompleteAutomationJobFailure(ClaimedAutomationJobRecord job, string errorMessage, bool shouldRetry, DateTime? retryAvailableAt, IReadOnlyList<WorkflowAutomationLogEntry> logs, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task UnclaimAutomationJobAsync(long jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ExternalCompletionContext?> LoadExternalCompletionContextAsync(long jobId, CancellationToken cancellationToken = default)
            => Task.FromResult<ExternalCompletionContext?>(null);
        public Task ApplyExternalCompletionSuccessAsync(long jobId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApplyExternalCompletionFailureAsync(long jobId, WorkflowAutomationRetryOutcome outcome, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
