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

    [Fact]
    public void EvaluateRetryOutcome_PermanentFailureKind_FirstAttemptIdempotent_ReturnsFinalFail()
    {
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            DefaultSettings(), attemptNumber: 1, isIdempotent: true, failureKind: WorkflowAutomationRetryPolicy.FailureKindPermanent);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.FinalFail, outcome.Kind);
    }

    [Fact]
    public void EvaluateRetryOutcome_PermanentFailureKind_NonIdempotent_StillFinalFail()
    {
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            DefaultSettings(), attemptNumber: 1, isIdempotent: false, failureKind: WorkflowAutomationRetryPolicy.FailureKindPermanent);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.FinalFail, outcome.Kind);
    }

    [Fact]
    public void EvaluateRetryOutcome_TransientFailureKind_Idempotent_FollowsExistingRetryLogic()
    {
        var settings = DefaultSettings();
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            settings, attemptNumber: 1, isIdempotent: true, failureKind: WorkflowAutomationRetryPolicy.FailureKindTransient);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.RetryAfter, outcome.Kind);
        Assert.Equal(settings.FirstRetryDelay, outcome.Delay);
    }

    [Fact]
    public void EvaluateRetryOutcome_NullFailureKind_FallsBackToLegacyLogic()
    {
        // Regression-Schutz: Linux-Handler ohne Tagging (heutiger Default) behalten die alte
        // Semantik. failureKind=null fuehrt nicht zu FinalFail, wenn idempotent+attempts<max.
        var settings = DefaultSettings();
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            settings, attemptNumber: 1, isIdempotent: true, failureKind: null);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.RetryAfter, outcome.Kind);
    }

    [Fact]
    public void EvaluateRetryOutcome_PermanentMixedCase_IsCaseInsensitive()
    {
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            DefaultSettings(), attemptNumber: 1, isIdempotent: true, failureKind: "PERMANENT");
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.FinalFail, outcome.Kind);
    }

    // Etappe 9a Schritt 7 Sub-A: per-Action-Override-Tests.

    [Fact]
    public void EvaluateRetryOutcome_MaxAttemptsOverrideHoeher_RetriedLaengerAlsDefault()
    {
        // Default MaxAttempts=3 -> Versuch 7 ist FinalFail.
        // Override auf 10 -> Versuch 7 muss noch retried werden.
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            DefaultSettings(), attemptNumber: 7, isIdempotent: true,
            maxAttemptsOverride: 10);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.RetryAfter, outcome.Kind);
    }

    [Fact]
    public void EvaluateRetryOutcome_MaxAttemptsOverrideHoeher_FinalFailAmOverrideLimit()
    {
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            DefaultSettings(), attemptNumber: 10, isIdempotent: true,
            maxAttemptsOverride: 10);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.FinalFail, outcome.Kind);
    }

    [Fact]
    public void EvaluateRetryOutcome_MaxAttemptsOverrideNiedriger_FinalFailFrueher()
    {
        // Override auf 2 < Default 3 -> Versuch 2 ist FinalFail.
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            DefaultSettings(), attemptNumber: 2, isIdempotent: true,
            maxAttemptsOverride: 2);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.FinalFail, outcome.Kind);
    }

    [Fact]
    public void EvaluateRetryOutcome_SubsequentDelayOverride_GreiftAbZweitemRetry()
    {
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            DefaultSettings(), attemptNumber: 2, isIdempotent: true,
            subsequentRetryDelayOverride: TimeSpan.FromMinutes(15));
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.RetryAfter, outcome.Kind);
        Assert.Equal(TimeSpan.FromMinutes(15), outcome.Delay);
    }

    [Fact]
    public void EvaluateRetryOutcome_SubsequentDelayOverride_BetrifftErstenRetryNicht()
    {
        // FirstRetryDelay bleibt immer global (60s) -- der erste Retry-Check direkt nach Lag
        // soll schnell sein.
        var settings = DefaultSettings();
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            settings, attemptNumber: 1, isIdempotent: true,
            subsequentRetryDelayOverride: TimeSpan.FromMinutes(15));
        Assert.Equal(settings.FirstRetryDelay, outcome.Delay);
    }

    [Fact]
    public void EvaluateRetryOutcome_OverridesNull_DefaultsGelten()
    {
        // Backwards-Compat: null/nicht angegeben verhaelt sich exakt wie vorher.
        var settings = DefaultSettings();
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            settings, attemptNumber: 2, isIdempotent: true,
            maxAttemptsOverride: null, subsequentRetryDelayOverride: null);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.RetryAfter, outcome.Kind);
        Assert.Equal(settings.SubsequentRetryDelay, outcome.Delay);
    }

    [Fact]
    public void EvaluateRetryOutcome_OverridesNonPositive_DefaultsGelten()
    {
        // Defensiv: 0 oder negative Werte werden als "nicht gesetzt" interpretiert.
        var settings = DefaultSettings();
        var outcome = WorkflowAutomationRetryPolicy.EvaluateRetryOutcome(
            settings, attemptNumber: 2, isIdempotent: true,
            maxAttemptsOverride: 0, subsequentRetryDelayOverride: TimeSpan.Zero);
        Assert.Equal(WorkflowAutomationRetryOutcome.OutcomeKind.RetryAfter, outcome.Kind);
        Assert.Equal(settings.SubsequentRetryDelay, outcome.Delay);
    }
}
