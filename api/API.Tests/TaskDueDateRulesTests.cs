using Xunit;

namespace API.Tests;

public sealed class TaskDueDateRulesTests
{
    [Fact]
    public void ResolveEffectiveDueAt_UsesEarlierTaskDueAtBeforeWorkflowDeadline()
    {
        var workflowDeadline = new DateOnly(2026, 3, 30);
        var taskDueAt = new DateTime(2026, 3, 27, 12, 0, 0, DateTimeKind.Utc);

        var result = TaskDueDateRules.ResolveEffectiveDueAt(workflowDeadline, taskDueAt);

        Assert.Equal(taskDueAt, result);
    }

    [Fact]
    public void ResolveEffectiveDueAt_UsesWorkflowDeadlineWhenTaskDueAtIsLater()
    {
        var workflowDeadline = new DateOnly(2026, 3, 30);
        var taskDueAt = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);

        var result = TaskDueDateRules.ResolveEffectiveDueAt(workflowDeadline, taskDueAt);

        Assert.Equal(new DateTime(2026, 3, 30, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void ResolveSlaStatus_TreatsDeadlineDayAsDueTodayInsteadOfOverdue()
    {
        var dueAt = new DateTime(2026, 3, 24, 0, 0, 0, DateTimeKind.Utc);

        var result = TaskDueDateRules.ResolveSlaStatus(
            "ready",
            dueAt,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "done" },
            new DateTime(2026, 3, 24, 14, 30, 0, DateTimeKind.Utc));

        Assert.Equal("due_today", result);
    }

    [Fact]
    public void ResolveSlaStatus_MarksEarlierDatesAsOverdue()
    {
        var dueAt = new DateTime(2026, 3, 23, 23, 59, 0, DateTimeKind.Utc);

        var result = TaskDueDateRules.ResolveSlaStatus(
            "ready",
            dueAt,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "done" },
            new DateTime(2026, 3, 24, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal("overdue", result);
    }
}
