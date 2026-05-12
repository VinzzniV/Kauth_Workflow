using Xunit;

namespace API.Tests;

public sealed class WorkflowAutomationRetryPolicyTests
{
    private static WorkflowAutomationRetrySettings DefaultSettings() => new()
    {
        MaxAttempts = 3,
        FirstRetryDelay = TimeSpan.FromMinutes(1),
        SubsequentRetryDelay = TimeSpan.FromMinutes(5),
    };

    [Fact]
    public void EvaluateRetryOutcome_NonIdempotent_ReturnsFinalFailEvenOnFirstAttempt()
    {
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(DefaultSettings(), attemptNumber: 1, isIdempotent: false);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.FinalFail, outcome.Kind);
    }

    [Fact]
    public void EvaluateRetryOutcome_FirstAttempt_Idempotent_ReturnsRetryWithFirstDelay()
    {
        var settings = DefaultSettings();
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(settings, attemptNumber: 1, isIdempotent: true);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.RetryAfter, outcome.Kind);
        Assert.Equal(settings.FirstRetryDelay, outcome.Delay);
    }

    [Fact]
    public void EvaluateRetryOutcome_SecondAttempt_Idempotent_ReturnsRetryWithSubsequentDelay()
    {
        var settings = DefaultSettings();
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(settings, attemptNumber: 2, isIdempotent: true);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.RetryAfter, outcome.Kind);
        Assert.Equal(settings.SubsequentRetryDelay, outcome.Delay);
    }

    [Fact]
    public void EvaluateRetryOutcome_AtMaxAttempts_ReturnsFinalFail()
    {
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(DefaultSettings(), attemptNumber: 3, isIdempotent: true);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.FinalFail, outcome.Kind);
    }

    [Fact]
    public void EvaluateRetryOutcome_OverMaxAttempts_ReturnsFinalFail()
    {
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(DefaultSettings(), attemptNumber: 7, isIdempotent: true);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.FinalFail, outcome.Kind);
    }
}
