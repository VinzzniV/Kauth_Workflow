namespace API;

// Pure-Logic-Domain-Engine: vergleicht Soll- und Ist-Stand der Rotation-Tasks und liefert einen Plan
// (Create/Update/Cancel/Unchanged). Macht KEINE DB-Operationen — der Caller (PostgresRotationRepository)
// fuehrt den Plan via SQL aus. Erlaubt Unit-Tests ohne Postgres.
internal static class RotationTaskRegenerationEngine
{
    public static RotationTaskRegenerationPlan Plan(
        IReadOnlyList<RotationGeneratedTaskRecord> existingTasks,
        IReadOnlyList<RotationDesiredTaskRecord> desiredTasks)
    {
        ArgumentNullException.ThrowIfNull(existingTasks);
        ArgumentNullException.ThrowIfNull(desiredTasks);

        var existingByKey = existingTasks
            .Where(task => task.RotationStationId.HasValue && task.TemplateId.HasValue)
            .ToDictionary(
                task => BuildMatchKey(task.RotationStationId!.Value, task.TemplateId!.Value),
                task => task);

        var toCreate = new List<RotationDesiredTaskRecord>();
        var toUpdate = new List<RotationTaskUpdate>();
        var toCancel = new List<RotationGeneratedTaskRecord>();
        var matchedTaskIds = new HashSet<long>();
        var unchanged = 0;

        foreach (var desiredTask in desiredTasks)
        {
            var key = BuildMatchKey(desiredTask.RotationStationId, desiredTask.TemplateId);
            if (!existingByKey.TryGetValue(key, out var existingTask))
            {
                toCreate.Add(desiredTask);
                continue;
            }

            matchedTaskIds.Add(existingTask.Id);

            if (RotationTaskStatusRules.TerminalTaskStatuses.Contains(existingTask.Status))
            {
                unchanged++;
                continue;
            }

            if (NeedsUpdate(existingTask, desiredTask))
            {
                toUpdate.Add(new RotationTaskUpdate
                {
                    Existing = existingTask,
                    Desired = desiredTask
                });
            }
            else
            {
                unchanged++;
            }
        }

        foreach (var existingTask in existingTasks)
        {
            if (matchedTaskIds.Contains(existingTask.Id))
            {
                continue;
            }

            if (!string.Equals(existingTask.Status, RotationTaskStatuses.Open, StringComparison.OrdinalIgnoreCase))
            {
                unchanged++;
                continue;
            }

            toCancel.Add(existingTask);
        }

        return new RotationTaskRegenerationPlan
        {
            ToCreate = toCreate,
            ToUpdate = toUpdate,
            ToCancel = toCancel,
            UnchangedCount = unchanged
        };
    }

    public static bool NeedsUpdate(RotationGeneratedTaskRecord existingTask, RotationDesiredTaskRecord desiredTask)
    {
        return existingTask.RotationStationId != desiredTask.RotationStationId
               || existingTask.DepartmentId != desiredTask.DepartmentId
               || existingTask.TemplateId != desiredTask.TemplateId
               || !string.Equals(existingTask.TriggerType, desiredTask.TriggerType, StringComparison.OrdinalIgnoreCase)
               || existingTask.AnchorDate != desiredTask.AnchorDate
               || !string.Equals(existingTask.Title, desiredTask.Title, StringComparison.Ordinal)
               || !string.Equals(existingTask.Description ?? string.Empty, desiredTask.Description ?? string.Empty, StringComparison.Ordinal)
               || !string.Equals(existingTask.TaskType, desiredTask.TaskType, StringComparison.OrdinalIgnoreCase)
               || existingTask.ResponsibilityId != desiredTask.ResponsibilityId
               || existingTask.DueDate != desiredTask.DueDate;
    }

    public static string BuildMatchKey(long stationId, int templateId)
    {
        return $"{stationId}:{templateId}";
    }
}

internal sealed class RotationTaskRegenerationPlan
{
    public required IReadOnlyList<RotationDesiredTaskRecord> ToCreate { get; init; }
    public required IReadOnlyList<RotationTaskUpdate> ToUpdate { get; init; }
    public required IReadOnlyList<RotationGeneratedTaskRecord> ToCancel { get; init; }
    public required int UnchangedCount { get; init; }
}

internal sealed class RotationTaskUpdate
{
    public required RotationGeneratedTaskRecord Existing { get; init; }
    public required RotationDesiredTaskRecord Desired { get; init; }
}
