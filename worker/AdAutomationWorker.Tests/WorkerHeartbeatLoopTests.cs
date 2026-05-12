using AdAutomationWorker.Core.Polling;
using AdAutomationWorker.Tests.Stubs;
using Xunit;

namespace AdAutomationWorker.Tests;

public sealed class WorkerHeartbeatLoopTests
{
    [Fact]
    public async Task RunAsync_StopsImmediately_WhenCancelled()
    {
        var store = new StubWorkerJobStore();
        var loop = new WorkerHeartbeatLoop(store);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await loop.RunAsync(jobId: 1, workerId: "w", TimeSpan.FromMilliseconds(50), cts.Token);

        Assert.Empty(store.HeartbeatCalls);
    }

    [Fact]
    public async Task RunAsync_TicksHeartbeat_UntilCancelled()
    {
        var store = new StubWorkerJobStore();
        var loop = new WorkerHeartbeatLoop(store);
        using var cts = new CancellationTokenSource();

        var task = loop.RunAsync(jobId: 7, workerId: "w", TimeSpan.FromMilliseconds(20), cts.Token);
        await Task.Delay(150);
        cts.Cancel();
        await task;

        Assert.NotEmpty(store.HeartbeatCalls);
        Assert.All(store.HeartbeatCalls, c =>
        {
            Assert.Equal(7, c.JobId);
            Assert.Equal("w", c.WorkerId);
        });
    }

    [Fact]
    public async Task RunAsync_LeaseLost_ExitsAfterZeroAffected()
    {
        var store = new StubWorkerJobStore();
        store.HeartbeatReturnValues.Enqueue(0); // simulate lost lease on first tick
        var loop = new WorkerHeartbeatLoop(store);

        await loop.RunAsync(jobId: 11, workerId: "w", TimeSpan.FromMilliseconds(10), CancellationToken.None);

        Assert.Single(store.HeartbeatCalls);
    }
}
