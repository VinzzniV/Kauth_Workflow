using System.Text.Json;
using AdAutomationWorker.Core.Handlers;
using AdAutomationWorker.Core.Polling;
using AdAutomationWorker.Tests.Stubs;
using Xunit;

namespace AdAutomationWorker.Tests;

// Verifiziert das Contract-Verhalten von IWorkerJobStore-Konsumenten gegen den Stub.
// Die echten Postgres-SQL-Pfade in PostgresWorkerJobStore werden manuell durch den
// Skeleton-E2E aus dem Plan abgedeckt; hier geht es um den In-Process-Vertrag.
public sealed class WorkerJobStoreContractTests
{
    [Fact]
    public async Task ClaimNextPendingJobAsync_NoPending_ReturnsNull()
    {
        var store = new StubWorkerJobStore();
        var claim = await store.ClaimNextPendingJobAsync("test-worker", CancellationToken.None);
        Assert.Null(claim);
        Assert.Single(store.ClaimedByCalls);
        Assert.Equal("test-worker", store.ClaimedByCalls[0]);
    }

    [Fact]
    public async Task ClaimNextPendingJobAsync_WithPending_ReturnsClaim()
    {
        var store = new StubWorkerJobStore();
        store.ClaimQueue.Enqueue(new WorkerJobClaim
        {
            JobId = 100,
            WorkflowUid = Guid.NewGuid(),
            WorkflowNodeInstanceId = 7,
            ActionKey = "simulated_windows_worker_ping",
            Payload = JsonDocument.Parse("{}").RootElement,
            AttemptNumber = 1
        });

        var claim = await store.ClaimNextPendingJobAsync("test-worker", CancellationToken.None);

        Assert.NotNull(claim);
        Assert.Equal(100, claim!.JobId);
        Assert.Equal("simulated_windows_worker_ping", claim.ActionKey);
    }

    [Fact]
    public async Task ReleaseStaleClaimsAsync_PropagatesTimeoutAndReturn()
    {
        var store = new StubWorkerJobStore { StaleReleaseReturnValue = 3 };
        var released = await store.ReleaseStaleClaimsAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal(3, released);
        Assert.Single(store.StaleReleaseCalls);
        Assert.Equal(TimeSpan.FromMinutes(5), store.StaleReleaseCalls[0]);
    }

    [Fact]
    public async Task MarkJobSucceededAsync_RecordsOutputAndLogs()
    {
        var store = new StubWorkerJobStore();
        var output = JsonDocument.Parse("{\"result\":\"pong\"}").RootElement;
        var logs = new[]
        {
            new WorkerLogEntry { Level = "info", Message = "ping ok" }
        };

        await store.MarkJobSucceededAsync(jobId: 42, attemptNumber: 1, output, logs, CancellationToken.None);

        Assert.Single(store.SuccessCalls);
        var call = store.SuccessCalls[0];
        Assert.Equal(42, call.JobId);
        Assert.Equal(1, call.AttemptNumber);
        Assert.Equal("pong", call.Output!.Value.GetProperty("result").GetString());
        Assert.Single(call.Logs);
        Assert.Equal("ping ok", call.Logs[0].Message);
    }

    [Fact]
    public async Task MarkJobFailedAsync_RecordsErrorAndLogs()
    {
        var store = new StubWorkerJobStore();
        var logs = new[]
        {
            new WorkerLogEntry { Level = "error", Message = "ldap timeout" }
        };

        await store.MarkJobFailedAsync(jobId: 13, attemptNumber: 2, "ldap-host unreachable", logs, CancellationToken.None);

        Assert.Single(store.FailureCalls);
        var call = store.FailureCalls[0];
        Assert.Equal(13, call.JobId);
        Assert.Equal(2, call.AttemptNumber);
        Assert.Equal("ldap-host unreachable", call.ErrorMessage);
        Assert.Single(call.Logs);
        Assert.Equal("ldap timeout", call.Logs[0].Message);
    }
}
