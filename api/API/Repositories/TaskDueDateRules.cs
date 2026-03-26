namespace API;

internal static class TaskDueDateRules
{
    public static DateTime? ToWorkflowDeadlineDueAt(DateOnly? workflowDeadlineDate)
    {
        if (!workflowDeadlineDate.HasValue)
        {
            return null;
        }

        return DateTime.SpecifyKind(workflowDeadlineDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    }

    public static DateTime? ResolveEffectiveDueAt(DateOnly? workflowDeadlineDate, DateTime? taskDueAt)
    {
        var workflowDueAt = ToWorkflowDeadlineDueAt(workflowDeadlineDate);
        if (!workflowDueAt.HasValue)
        {
            return taskDueAt;
        }

        if (!taskDueAt.HasValue)
        {
            return workflowDueAt;
        }

        return taskDueAt.Value <= workflowDueAt.Value ? taskDueAt : workflowDueAt;
    }

    public static string ResolveSlaStatus(
        string taskStatus,
        DateTime? dueAt,
        IReadOnlySet<string> terminalTaskStatuses,
        DateTime? nowUtc = null)
    {
        if (!dueAt.HasValue || terminalTaskStatuses.Contains(taskStatus))
        {
            return "none";
        }

        var currentUtcDate = (nowUtc ?? DateTime.UtcNow).Date;
        var dueUtcDate = dueAt.Value.Date;

        if (dueUtcDate < currentUtcDate)
        {
            return "overdue";
        }

        return dueUtcDate == currentUtcDate ? "due_today" : "on_track";
    }
}
