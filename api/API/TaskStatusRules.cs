namespace API;

public static class TaskStatusRules
{
    public static readonly HashSet<string> AllowedTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "open",
        "ready",
        "in_progress",
        "blocked",
        "done"
    };

    public static readonly HashSet<string> TerminalTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "done",
        "cancelled"
    };

    public static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedTaskTransitions =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["open"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ready", "in_progress", "blocked", "done" },
            ["ready"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "in_progress", "blocked", "done" },
            ["in_progress"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "done", "blocked" },
            ["blocked"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ready" },
            ["done"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };

    public static string NormalizeTaskStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new InvalidOperationException("Status is required.");
        }

        var normalizedStatus = status.Trim().ToLowerInvariant();
        if (!AllowedTaskStatuses.Contains(normalizedStatus))
        {
            throw new InvalidOperationException($"Task status '{status}' is invalid.");
        }

        return normalizedStatus;
    }

    public static void EnsureTaskTransitionAllowed(string currentStatus, string requestedStatus)
    {
        if (currentStatus.Equals(requestedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!AllowedTaskTransitions.TryGetValue(currentStatus, out var allowedTransitions))
        {
            throw new InvalidOperationException($"Current task status '{currentStatus}' is not supported.");
        }

        if (!allowedTransitions.Contains(requestedStatus))
        {
            throw new InvalidOperationException(
                $"Task transition from '{currentStatus}' to '{requestedStatus}' is not allowed.");
        }
    }

    public static bool RequiresSatisfiedDependencies(string requestedStatus)
    {
        return requestedStatus.Equals("ready", StringComparison.OrdinalIgnoreCase)
               || requestedStatus.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
               || requestedStatus.Equals("done", StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanAutoBlockTask(string currentStatus)
    {
        return currentStatus.Equals("ready", StringComparison.OrdinalIgnoreCase)
               || currentStatus.Equals("open", StringComparison.OrdinalIgnoreCase);
    }
}
