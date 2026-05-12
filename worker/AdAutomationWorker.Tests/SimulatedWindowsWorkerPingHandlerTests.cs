using System.Diagnostics;
using System.Text.Json;
using AdAutomationWorker.Core.Handlers;
using AdAutomationWorker.Core.Handlers.Simulated;
using Xunit;

namespace AdAutomationWorker.Tests;

public sealed class SimulatedWindowsWorkerPingHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WithoutDelay_ReturnsPongInstantly()
    {
        var handler = new SimulatedWindowsWorkerPingHandler();
        var context = BuildContext(JsonDocument.Parse("{}").RootElement);

        var sw = Stopwatch.StartNew();
        var result = await handler.ExecuteAsync(context, CancellationToken.None);
        sw.Stop();

        Assert.True(result.IsSuccess);
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(500), $"Expected fast return, actual={sw.Elapsed}");
        Assert.NotNull(result.Output);
        Assert.Equal("pong", result.Output!.Value.GetProperty("result").GetString());
        Assert.Single(result.Logs);
    }

    [Fact]
    public async Task ExecuteAsync_WithDelaySeconds_RespectsDelay()
    {
        var handler = new SimulatedWindowsWorkerPingHandler();
        var context = BuildContext(JsonDocument.Parse("{\"delaySeconds\":1}").RootElement);

        var sw = Stopwatch.StartNew();
        var result = await handler.ExecuteAsync(context, CancellationToken.None);
        sw.Stop();

        Assert.True(result.IsSuccess);
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(900), $"Expected ~1s delay, actual={sw.Elapsed}");
    }

    [Fact]
    public async Task ExecuteAsync_NegativeDelay_IsClampedToZero()
    {
        var handler = new SimulatedWindowsWorkerPingHandler();
        var context = BuildContext(JsonDocument.Parse("{\"delaySeconds\":-10}").RootElement);

        var sw = Stopwatch.StartNew();
        var result = await handler.ExecuteAsync(context, CancellationToken.None);
        sw.Stop();

        Assert.True(result.IsSuccess);
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(500));
        Assert.Equal(0, result.Output!.Value.GetProperty("delaySeconds").GetInt32());
    }

    private static WorkerHandlerContext BuildContext(JsonElement payload) => new()
    {
        JobId = 42,
        WorkflowUid = Guid.NewGuid(),
        WorkflowNodeInstanceId = 7,
        ActionKey = "simulated_windows_worker_ping",
        Payload = payload,
        AttemptNumber = 1
    };
}
