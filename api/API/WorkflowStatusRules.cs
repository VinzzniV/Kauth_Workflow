namespace API;

internal static class WorkflowStatusRules
{
    public const string Draft = "draft";
    public const string WaitingForSupervisor = "waiting_for_supervisor";
    public const string WaitingForDepartment = "waiting_for_department";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string OpenLegacy = "open";

    public static string Normalize(string workflowStatus)
    {
        return workflowStatus.Trim().ToLowerInvariant();
    }

    public static bool IsTerminal(string workflowStatus)
    {
        var normalized = Normalize(workflowStatus);
        return normalized == Completed || normalized == "cancelled";
    }

    public static bool IsWaitingForSupervisor(string workflowStatus)
    {
        return Normalize(workflowStatus) == WaitingForSupervisor;
    }

    public static bool IsDepartmentPhase(string workflowStatus)
    {
        var normalized = Normalize(workflowStatus);
        return normalized is WaitingForDepartment or InProgress;
    }

    public static string ToLegacyStatus(string workflowStatus)
    {
        return Normalize(workflowStatus) switch
        {
            Completed => Completed,
            _ => OpenLegacy,
        };
    }
}
