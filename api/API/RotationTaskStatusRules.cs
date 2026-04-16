namespace API;

internal static class RotationTaskStatusRules
{
    public static readonly HashSet<string> AllowedTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "open",
        "in_progress",
        "completed",
        "failed",
        "cancelled"
    };

    public static readonly HashSet<string> TerminalTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "completed",
        "failed",
        "cancelled"
    };

    public static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedTaskTransitions =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["open"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "in_progress", "completed", "failed", "cancelled" },
            ["in_progress"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "completed", "failed", "cancelled" },
            ["completed"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ["failed"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ["cancelled"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
            throw new InvalidOperationException($"Rotation task status '{status}' is invalid.");
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
            throw new InvalidOperationException($"Current rotation task status '{currentStatus}' is not supported.");
        }

        if (!allowedTransitions.Contains(requestedStatus))
        {
            throw new InvalidOperationException(
                $"Rotation task transition from '{currentStatus}' to '{requestedStatus}' is not allowed.");
        }
    }
}
