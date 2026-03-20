namespace API;

internal static class WorkflowSummaryBuilder
{

    public static WorkflowRequirementSummaryDto CreateEmptyRequirementSummary()
    {
        return new WorkflowRequirementSummaryDto
        {
            TotalCount = 0,
            VisibleCount = 0,
            AnsweredVisibleCount = 0,
            PendingVisibleCount = 0
        };
    }

    public static WorkflowTaskCountSummaryDto CreateTaskCountSummary(
        int totalCount,
        int openCount,
        int inProgressCount,
        int doneCount,
        int endedCount)
    {
        return new WorkflowTaskCountSummaryDto
        {
            TotalCount = totalCount,
            OpenCount = openCount,
            InProgressCount = inProgressCount,
            DoneCount = doneCount,
            EndedCount = endedCount,
            CompletedCount = doneCount + endedCount,
            ActiveCount = openCount + inProgressCount
        };
    }

    public static WorkflowTaskMetricsDto CreateEmptyTaskMetrics()
    {
        var emptyCounts = CreateTaskCountSummary(0, 0, 0, 0, 0);

        return new WorkflowTaskMetricsDto
        {
            Overall = emptyCounts,
            DepartmentPhase = emptyCounts
        };
    }

    public static WorkflowRequirementSummaryDto BuildRequirementSummary(
        IEnumerable<AnswerDefinitionRecord> definitions,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        var totalCount = 0;
        var visibleCount = 0;
        var answeredVisibleCount = 0;

        foreach (var definition in definitions.OrderBy(item => item.SortOrder).ThenBy(item => item.DefinitionId))
        {
            totalCount += 1;

            if (!RequirementBehaviorEngine.IsVisible(definition.Behavior, answersByKey))
            {
                continue;
            }

            visibleCount += 1;
            answersByKey.TryGetValue(definition.Key, out var answer);
            if (HasAnswer(definition.InputType, answer))
            {
                answeredVisibleCount += 1;
            }
        }

        return new WorkflowRequirementSummaryDto
        {
            TotalCount = totalCount,
            VisibleCount = visibleCount,
            AnsweredVisibleCount = answeredVisibleCount,
            PendingVisibleCount = Math.Max(0, visibleCount - answeredVisibleCount)
        };
    }

    public static WorkflowRequirementSummaryDto BuildRequirementSummary(
        IReadOnlyList<WorkflowRequirementSnapshotDto> requirements)
    {
        var answersByKey = new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var requirement in requirements)
        {
            var answer = CreateStoredWorkflowAnswerRecord(requirement);
            if (!HasAnswer(requirement.InputType, answer))
            {
                continue;
            }

            answersByKey[requirement.Key] = answer;
        }

        var totalCount = 0;
        var visibleCount = 0;
        var answeredVisibleCount = 0;

        foreach (var requirement in requirements.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
        {
            totalCount += 1;

            if (!RequirementBehaviorEngine.IsVisible(requirement.Behavior, answersByKey))
            {
                continue;
            }

            visibleCount += 1;
            answersByKey.TryGetValue(requirement.Key, out var answer);
            if (HasAnswer(requirement.InputType, answer))
            {
                answeredVisibleCount += 1;
            }
        }

        return new WorkflowRequirementSummaryDto
        {
            TotalCount = totalCount,
            VisibleCount = visibleCount,
            AnsweredVisibleCount = answeredVisibleCount,
            PendingVisibleCount = Math.Max(0, visibleCount - answeredVisibleCount)
        };
    }

    public static WorkflowTaskMetricsDto BuildTaskMetrics(IReadOnlyList<WorkflowTaskDto> tasks)
    {
        var orderedTasks = tasks
            .OrderBy(task => task.SortOrder)
            .ThenBy(task => task.Id)
            .ToList();

        return new WorkflowTaskMetricsDto
        {
            Overall = BuildTaskCountSummary(orderedTasks),
            DepartmentPhase = BuildTaskCountSummary(orderedTasks.Where(task => task.IsDepartmentPhaseTask))
        };
    }

    public static List<WorkflowTaskAreaSummaryDto> BuildTaskAreaSummaries(IReadOnlyList<WorkflowTaskDto> tasks)
    {
        var tasksByArea = new Dictionary<string, List<WorkflowTaskDto>>(StringComparer.Ordinal);
        var orderedAreaNames = new List<string>();

        foreach (var task in tasks.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
        {
            if (string.IsNullOrWhiteSpace(task.ProcessArea))
            {
                continue;
            }

            var areaName = task.ProcessArea.Trim();
            if (!tasksByArea.TryGetValue(areaName, out var areaTasks))
            {
                areaTasks = new List<WorkflowTaskDto>();
                tasksByArea[areaName] = areaTasks;
                orderedAreaNames.Add(areaName);
            }

            areaTasks.Add(task);
        }

        return orderedAreaNames
            .Select(areaName =>
            {
                var counts = BuildTaskCountSummary(tasksByArea[areaName]);
                return new WorkflowTaskAreaSummaryDto
                {
                    Name = areaName,
                    IsCurrentArea = counts.ActiveCount > 0,
                    Counts = counts
                };
            })
            .ToList();
    }

    public static string BuildTaskSummaryText(WorkflowTaskMetricsDto taskMetrics)
    {
        if (taskMetrics.Overall.TotalCount == 0)
        {
            return "Keine Aufgaben";
        }

        return $"Offen: {taskMetrics.Overall.ActiveCount} | Erledigt: {taskMetrics.Overall.DoneCount} | Beendet: {taskMetrics.Overall.EndedCount}";
    }

    private static WorkflowTaskCountSummaryDto BuildTaskCountSummary(IEnumerable<WorkflowTaskDto> tasks)
    {
        var totalCount = 0;
        var openCount = 0;
        var inProgressCount = 0;
        var doneCount = 0;
        var endedCount = 0;

        foreach (var task in tasks)
        {
            totalCount += 1;

            if (IsOpenTaskStatus(task.Status))
            {
                openCount += 1;
                continue;
            }

            if (IsInProgressTaskStatus(task.Status))
            {
                inProgressCount += 1;
                continue;
            }

            if (string.Equals(task.Status, "done", StringComparison.OrdinalIgnoreCase))
            {
                doneCount += 1;
                continue;
            }

            if (string.Equals(task.Status, "skipped", StringComparison.OrdinalIgnoreCase)
                || string.Equals(task.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                endedCount += 1;
            }
        }

        return CreateTaskCountSummary(totalCount, openCount, inProgressCount, doneCount, endedCount);
    }

    private static bool IsOpenTaskStatus(string status)
    {
        return string.Equals(status, "open", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "ready", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "blocked", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInProgressTaskStatus(string status)
    {
        return string.Equals(status, "in_progress", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnswer(string inputType, StoredWorkflowAnswerRecord? answer)
    {
        return inputType switch
        {
            "boolean" => answer?.ValueBoolean is not null,
            "text" => !string.IsNullOrWhiteSpace(answer?.ValueText),
            "select" => answer is not null && (answer.SelectedOptionId.HasValue || answer.SelectedOptionIds.Count > 0),
            "multi_select" => answer is not null && answer.SelectedOptionIds.Count > 0,
            _ => false
        };
    }

    private static StoredWorkflowAnswerRecord CreateStoredWorkflowAnswerRecord(WorkflowRequirementSnapshotDto requirement)
    {
        return new StoredWorkflowAnswerRecord
        {
            WorkflowAnswerId = requirement.WorkflowRequirementId,
            AnswerDefinitionId = requirement.Id,
            AnswerKey = requirement.Key,
            InputType = requirement.InputType,
            ValueBoolean = requirement.Value.ValueBoolean,
            ValueText = requirement.Value.ValueText,
            ValueNumber = requirement.Value.ValueNumber,
            SelectedOptionId = requirement.Value.SelectedOptionId.HasValue
                ? Convert.ToInt32(requirement.Value.SelectedOptionId.Value)
                : null,
            SelectedOptionValue = requirement.Value.SelectedOptionValue,
            SelectedOptionIds = requirement.Value.SelectedOptions
                .Select(option => Convert.ToInt32(option.Id))
                .ToList(),
            SelectedOptionValues = requirement.Value.SelectedOptions
                .Select(option => option.Value)
                .ToList()
        };
    }
}
