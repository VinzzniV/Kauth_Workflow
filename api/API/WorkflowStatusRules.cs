namespace API;

internal static class WorkflowStatusRules
{
    public const string Draft = "draft";
    public const string WaitingForSupervisor = "waiting_for_supervisor";
    public const string WaitingForDepartment = "waiting_for_department";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";

    public static string Normalize(string workflowStatus)
    {
        return workflowStatus.Trim().ToLowerInvariant();
    }

    public static bool IsTerminal(string workflowStatus)
    {
        var normalized = Normalize(workflowStatus);
        return normalized == Completed;
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

    public static string? EnsureApprovalTaskConfiguration(
        string workflowDefinitionName,
        bool requiresSupervisorStep,
        string? approvalSpecKey)
    {
        var normalizedApprovalSpecKey = NormalizeTaskTemplateKey(approvalSpecKey);
        if (requiresSupervisorStep && string.IsNullOrWhiteSpace(normalizedApprovalSpecKey))
        {
            throw new InvalidOperationException(
                $"Die Workflow-Definition '{workflowDefinitionName}' verlangt einen Supervisor-Schritt, aber kein Approval-Task ist konfiguriert.");
        }

        return normalizedApprovalSpecKey;
    }

    public static string DetermineActiveWorkflowStatus(
        IReadOnlyList<(string TaskKey, string Status, bool IsRequired)> taskStates,
        string workflowDefinitionName,
        bool requiresSupervisorStep,
        string? approvalSpecKey)
    {
        approvalSpecKey = EnsureApprovalTaskConfiguration(
            workflowDefinitionName,
            requiresSupervisorStep,
            approvalSpecKey);

        var supervisorTask = taskStates.FirstOrDefault(task =>
            !string.IsNullOrWhiteSpace(approvalSpecKey)
            && task.TaskKey.Equals(approvalSpecKey, StringComparison.OrdinalIgnoreCase));
        var departmentTasks = taskStates
            .Where(task =>
                string.IsNullOrWhiteSpace(approvalSpecKey)
                || !task.TaskKey.Equals(approvalSpecKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var hasDepartmentTasksInProgress = departmentTasks.Any(task =>
            task.Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase));
        var hasActiveDepartmentTasks = departmentTasks.Any(task =>
            task.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
            || task.Status.Equals("ready", StringComparison.OrdinalIgnoreCase)
            || task.Status.Equals("blocked", StringComparison.OrdinalIgnoreCase)
            || task.Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase));

        if (requiresSupervisorStep && !string.IsNullOrWhiteSpace(supervisorTask.TaskKey))
        {
            if (supervisorTask.Status.Equals("ready", StringComparison.OrdinalIgnoreCase)
                || supervisorTask.Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase))
            {
                return WaitingForSupervisor;
            }

            if (supervisorTask.Status.Equals("done", StringComparison.OrdinalIgnoreCase))
            {
                return hasDepartmentTasksInProgress ? InProgress : WaitingForDepartment;
            }

            return Draft;
        }

        if (hasDepartmentTasksInProgress)
        {
            return InProgress;
        }

        return hasActiveDepartmentTasks ? WaitingForDepartment : Draft;
    }

    private static string? NormalizeTaskTemplateKey(string? taskTemplateKey)
    {
        return string.IsNullOrWhiteSpace(taskTemplateKey)
            ? null
            : taskTemplateKey.Trim();
    }
}
