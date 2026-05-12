using Xunit;

namespace API.Tests;

public sealed class TaskStatusRulesTests
{
    [Fact]
    public void TerminalTaskStatuses_ContainsDoneAndCancelled()
    {
        Assert.Contains("done", TaskStatusRules.TerminalTaskStatuses);
        Assert.Contains("cancelled", TaskStatusRules.TerminalTaskStatuses);
    }

    [Fact]
    public void TerminalTaskStatuses_IsCaseInsensitive()
    {
        Assert.Contains("DONE", TaskStatusRules.TerminalTaskStatuses);
        Assert.Contains("Cancelled", TaskStatusRules.TerminalTaskStatuses);
    }

    [Fact]
    public void AllowedTaskStatuses_DoesNotIncludeCancelled()
    {
        // Storno wird ausschliesslich ueber den Workflow-Cancel-Pfad gesetzt, nicht
        // ueber den regulaeren Task-Status-Update-Pfad.
        Assert.DoesNotContain("cancelled", TaskStatusRules.AllowedTaskStatuses);
    }
}
