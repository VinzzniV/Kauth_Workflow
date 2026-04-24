namespace API;

internal static class RotationTaskStatusRules
{
    public static readonly HashSet<string> AllowedTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        RotationTaskStatuses.Open,
        RotationTaskStatuses.InProgress,
        RotationTaskStatuses.Completed,
        RotationTaskStatuses.Failed,
        RotationTaskStatuses.Cancelled
    };

    public static readonly HashSet<string> TerminalTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        RotationTaskStatuses.Completed,
        RotationTaskStatuses.Failed,
        RotationTaskStatuses.Cancelled
    };

    public static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedTaskTransitions =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [RotationTaskStatuses.Open] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                RotationTaskStatuses.InProgress,
                RotationTaskStatuses.Completed,
                RotationTaskStatuses.Failed,
                RotationTaskStatuses.Cancelled
            },
            [RotationTaskStatuses.InProgress] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                RotationTaskStatuses.Completed,
                RotationTaskStatuses.Failed,
                RotationTaskStatuses.Cancelled
            },
            [RotationTaskStatuses.Completed] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            [RotationTaskStatuses.Failed] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            [RotationTaskStatuses.Cancelled] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
