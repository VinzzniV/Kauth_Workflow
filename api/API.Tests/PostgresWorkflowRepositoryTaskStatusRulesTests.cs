using System.Reflection;
using Xunit;

namespace API.Tests;

public sealed class PostgresWorkflowRepositoryTaskStatusRulesTests
{
    [Fact]
    public void NormalizeTaskStatus_RejectsSkipped()
    {
        var exception = Assert.Throws<TargetInvocationException>(() => InvokeNormalizeTaskStatus("skipped"));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("Task status 'skipped' is invalid.", exception.InnerException!.Message);
    }

    [Fact]
    public void NormalizeTaskStatus_RejectsCancelled()
    {
        var exception = Assert.Throws<TargetInvocationException>(() => InvokeNormalizeTaskStatus("cancelled"));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("Task status 'cancelled' is invalid.", exception.InnerException!.Message);
    }

    [Fact]
    public void EnsureTaskTransitionAllowed_RejectsBlockedToDone()
    {
        var exception = Assert.Throws<TargetInvocationException>(() => InvokeEnsureTaskTransitionAllowed("blocked", "done"));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("Task transition from 'blocked' to 'done' is not allowed.", exception.InnerException!.Message);
    }

    [Fact]
    public void EnsureTaskTransitionAllowed_AllowsBlockedToReady()
    {
        var exception = Record.Exception(() => InvokeEnsureTaskTransitionAllowed("blocked", "ready"));
        Assert.Null(exception);
    }

    [Fact]
    public void CanAutoBlockTask_ReturnsFalseForInProgress()
    {
        var canAutoBlock = InvokeCanAutoBlockTask("in_progress");
        Assert.False(canAutoBlock);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("ready")]
    public void CanAutoBlockTask_ReturnsTrueForOpenAndReady(string status)
    {
        var canAutoBlock = InvokeCanAutoBlockTask(status);
        Assert.True(canAutoBlock);
    }

    private static string InvokeNormalizeTaskStatus(string status)
    {
        var method = typeof(PostgresWorkflowRepository).GetMethod(
            "NormalizeTaskStatus",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return (string)method!.Invoke(null, [status])!;
    }

    private static void InvokeEnsureTaskTransitionAllowed(string currentStatus, string requestedStatus)
    {
        var method = typeof(PostgresWorkflowRepository).GetMethod(
            "EnsureTaskTransitionAllowed",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        method!.Invoke(null, [currentStatus, requestedStatus]);
    }

    private static bool InvokeCanAutoBlockTask(string currentStatus)
    {
        var method = typeof(PostgresWorkflowRepository).GetMethod(
            "CanAutoBlockTask",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return (bool)method!.Invoke(null, [currentStatus])!;
    }
}
