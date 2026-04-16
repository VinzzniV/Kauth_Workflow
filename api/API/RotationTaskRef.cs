namespace API;

internal static class TaskFamilyNames
{
    public const string Workflow = "workflow";
    public const string Rotation = "rotation";
}

internal static class RotationTaskRef
{
    public static string Build(long taskId) => $"rot:{taskId}";

    public static bool TryParse(string? taskRef, out long taskId)
    {
        taskId = 0;
        if (string.IsNullOrWhiteSpace(taskRef))
        {
            return false;
        }

        var trimmed = taskRef.Trim();
        if (!trimmed.StartsWith("rot:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return long.TryParse(trimmed[4..], out taskId) && taskId > 0;
    }
}

internal static class WorkflowTaskRef
{
    public static string Build(long taskId) => $"wf:{taskId}";

    public static bool TryParse(string? taskRef, out long taskId)
    {
        taskId = 0;
        if (string.IsNullOrWhiteSpace(taskRef))
        {
            return false;
        }

        var trimmed = taskRef.Trim();
        if (!trimmed.StartsWith("wf:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return long.TryParse(trimmed[3..], out taskId) && taskId > 0;
    }
}
