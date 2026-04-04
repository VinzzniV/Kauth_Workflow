namespace API;

internal static class TaskConditionEvaluator
{
    public static bool ShouldCreateTask(
        IReadOnlyList<TaskTemplateConditionRecord> conditions,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        if (conditions.Count == 0)
        {
            return true;
        }

        foreach (var conditionGroup in conditions.GroupBy(condition => condition.ConditionGroup))
        {
            if (conditionGroup.All(condition => EvaluateCondition(condition, answersByKey)))
            {
                return true;
            }
        }

        return false;
    }

    public static bool EvaluateCondition(
        TaskTemplateConditionRecord condition,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        if (!answersByKey.TryGetValue(condition.AnswerKey, out var answer))
        {
            return false;
        }

        return condition.Operator switch
        {
            "is_true" => answer.ValueBoolean == true,
            "is_false" => answer.ValueBoolean == false,
            "is_null" => IsAnswerEmpty(answer),
            "is_not_null" => !IsAnswerEmpty(answer),
            "eq" => EvaluateEquality(answer, condition),
            "neq" => !EvaluateEquality(answer, condition),
            _ => false
        };
    }

    public static bool EvaluateEquality(StoredWorkflowAnswerRecord answer, TaskTemplateConditionRecord condition)
    {
        if (condition.ExpectedValueBoolean.HasValue)
        {
            return answer.ValueBoolean.HasValue && answer.ValueBoolean.Value == condition.ExpectedValueBoolean.Value;
        }

        if (condition.ExpectedValueNumber.HasValue)
        {
            return answer.ValueNumber.HasValue && answer.ValueNumber.Value == condition.ExpectedValueNumber.Value;
        }

        if (!string.IsNullOrWhiteSpace(condition.ExpectedValueText))
        {
            var expected = condition.ExpectedValueText.Trim();
            if (!string.IsNullOrWhiteSpace(answer.ValueText)
                && string.Equals(answer.ValueText.Trim(), expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(answer.SelectedOptionValue)
                && string.Equals(answer.SelectedOptionValue.Trim(), expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return answer.SelectedOptionValues.Any(
                optionValue => string.Equals(optionValue.Trim(), expected, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    public static bool IsAnswerEmpty(StoredWorkflowAnswerRecord answer)
    {
        return answer.ValueBoolean is null
               && string.IsNullOrWhiteSpace(answer.ValueText)
               && answer.ValueNumber is null
               && answer.SelectedOptionId is null
               && answer.SelectedOptionIds.Count == 0;
    }

    public static HashSet<int> GetPostSupervisorTemplateIds(
        IReadOnlyList<TaskTemplateRecord> templates,
        IReadOnlyList<TaskTemplateDependencyRecord> dependencies,
        string? approvalTaskTemplateKey)
    {
        if (string.IsNullOrWhiteSpace(approvalTaskTemplateKey))
        {
            return new HashSet<int>();
        }

        var supervisorTemplateId = templates
            .FirstOrDefault(template => template.TemplateKey.Equals(approvalTaskTemplateKey, StringComparison.OrdinalIgnoreCase))
            ?.Id;

        if (!supervisorTemplateId.HasValue)
        {
            throw new InvalidOperationException(
                $"Der konfigurierte Approval-Task '{approvalTaskTemplateKey}' existiert nicht unter den aktiven Task-Templates des Prozesstyps.");
        }

        var dependentsByTemplateId = dependencies
            .GroupBy(dependency => dependency.DependsOnTaskTemplateId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.TaskTemplateId).ToList());

        var postSupervisorTemplateIds = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(supervisorTemplateId.Value);

        while (queue.Count > 0)
        {
            var currentTemplateId = queue.Dequeue();
            if (!dependentsByTemplateId.TryGetValue(currentTemplateId, out var dependentTemplateIds))
            {
                continue;
            }

            foreach (var dependentTemplateId in dependentTemplateIds)
            {
                if (postSupervisorTemplateIds.Add(dependentTemplateId))
                {
                    queue.Enqueue(dependentTemplateId);
                }
            }
        }

        return postSupervisorTemplateIds;
    }

    public static string BuildTaskDescription(
        TaskTemplateRecord template,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        var description = template.Description.Trim();

        return template.TemplateKey switch
        {
            "permissions_from_reference_user" => AppendTaskContext(
                description,
                GetTextAnswer(answersByKey, RequirementKeys.ComparisonUserName) is { Length: > 0 } comparisonUserName
                    ? $"Referenzuser: {comparisonUserName}."
                    : null),
            "hardware_procure" or "hardware_setup" or "hardware_handover" => AppendTaskContext(
                description,
                GetHardwareTypeText(answersByKey) is { Length: > 0 } hardwareType
                    ? $"Gewünschte Hardware: {hardwareType}."
                    : null),
            _ => description
        };
    }

    private static string AppendTaskContext(string description, string? context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return description;
        }

        return string.IsNullOrWhiteSpace(description)
            ? context.Trim()
            : $"{description} {context.Trim()}";
    }

    private static string? GetTextAnswer(
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        string answerKey)
    {
        if (!answersByKey.TryGetValue(answerKey, out var answer) || string.IsNullOrWhiteSpace(answer.ValueText))
        {
            return null;
        }

        return answer.ValueText.Trim();
    }

    private static string? GetHardwareTypeText(
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        if (!answersByKey.TryGetValue(RequirementKeys.HardwareType, out var answer))
        {
            return null;
        }

        var rawValue = !string.IsNullOrWhiteSpace(answer.SelectedOptionValue)
            ? answer.SelectedOptionValue
            : answer.SelectedOptionValues.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var hardwareType = NormalizeOptionDisplayText(rawValue);
        if (!string.Equals(rawValue.Trim(), "laptop", StringComparison.OrdinalIgnoreCase))
        {
            return hardwareType;
        }

        if (!answersByKey.TryGetValue(RequirementKeys.LaptopVpnType, out var laptopVpnAnswer))
        {
            return hardwareType;
        }

        var vpnValue = !string.IsNullOrWhiteSpace(laptopVpnAnswer.SelectedOptionValue)
            ? laptopVpnAnswer.SelectedOptionValue
            : laptopVpnAnswer.SelectedOptionValues.FirstOrDefault();

        return string.IsNullOrWhiteSpace(vpnValue)
            ? hardwareType
            : $"{hardwareType} ({NormalizeOptionDisplayText(vpnValue)})";
    }

    private static string NormalizeOptionDisplayText(string rawValue)
    {
        return string.Join(
            " ",
            rawValue
                .Trim()
                .Replace("-", " ")
                .Replace("_", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(token => char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant()));
    }
}
