using System.Text.Json;
using AdAutomationWorker.Core.Handlers;
using Xunit;

namespace AdAutomationWorker.Tests;

public sealed class HandlerRegistryTests
{
    [Fact]
    public void TryGet_RegisteredKey_ReturnsHandler()
    {
        var registry = new HandlerRegistry(new IWorkerHandler[] { new FakeHandler("ping") });

        var found = registry.TryGet("ping", out var handler);

        Assert.True(found);
        Assert.Equal("ping", handler.ActionKey);
    }

    [Fact]
    public void TryGet_RegisteredKey_IsCaseInsensitive()
    {
        var registry = new HandlerRegistry(new IWorkerHandler[] { new FakeHandler("ping") });

        var found = registry.TryGet("PING", out var handler);

        Assert.True(found);
        Assert.Equal("ping", handler.ActionKey);
    }

    [Fact]
    public void TryGet_UnknownKey_ReturnsFalse()
    {
        var registry = new HandlerRegistry(new IWorkerHandler[] { new FakeHandler("ping") });

        var found = registry.TryGet("nothing", out _);

        Assert.False(found);
    }

    [Fact]
    public void Get_UnknownKey_Throws()
    {
        var registry = new HandlerRegistry(Array.Empty<IWorkerHandler>());

        Assert.Throws<InvalidOperationException>(() => registry.Get("nothing"));
    }

    private sealed class FakeHandler : IWorkerHandler
    {
        public FakeHandler(string key) => ActionKey = key;
        public string ActionKey { get; }
        public Task<WorkerHandlerResult> ExecuteAsync(WorkerHandlerContext context, CancellationToken cancellationToken)
            => Task.FromResult(WorkerHandlerResult.Success(default(JsonElement?), Array.Empty<WorkerLogEntry>()));
    }
}
