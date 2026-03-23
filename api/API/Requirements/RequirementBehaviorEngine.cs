namespace API;

internal static class RequirementBehaviorEngine
{
    public static void ApplyResetRules(
        IReadOnlyDictionary<int, AnswerDefinitionRecord> definitions,
        IDictionary<int, RequirementSelectionStateRecord> selectionsByDefinitionId)
    {
        var definitionsByKey = definitions.Values.ToDictionary(
            definition => definition.Key,
            definition => definition,
            StringComparer.OrdinalIgnoreCase);

        var orderedDefinitions = definitions.Values
            .OrderBy(definition => definition.SortOrder)
            .ThenBy(definition => definition.DefinitionId)
            .ToList();

        var maxIterations = Math.Max(1, orderedDefinitions.Count * 2);
        for (var iteration = 0; iteration < maxIterations; iteration += 1)
        {
            var changed = false;

            foreach (var definition in orderedDefinitions)
            {
                if (!selectionsByDefinitionId.TryGetValue(definition.DefinitionId, out var selection))
                {
                    continue;
                }

                if (selection.ValueBoolean != true)
                {
                    foreach (var target in definition.Behavior.ResetTargetsWhenNotTrue)
                    {
                        changed |= ApplyResetTarget(target, definitionsByKey, selectionsByDefinitionId);
                    }
                }

                var singleSelectReset = definition.Behavior.SingleSelectReset;
                if (singleSelectReset is null)
                {
                    continue;
                }

                var selectedOptionValue = GetSelectedOptionValue(definition, selection);
                var keepSelected = !string.IsNullOrWhiteSpace(selectedOptionValue)
                    && singleSelectReset.KeepSelectedOptionValues.Any(value =>
                        string.Equals(value, selectedOptionValue, StringComparison.OrdinalIgnoreCase));

                if (keepSelected)
                {
                    continue;
                }

                foreach (var target in singleSelectReset.Targets)
                {
                    changed |= ApplyResetTarget(target, definitionsByKey, selectionsByDefinitionId);
                }
            }

            if (!changed)
            {
                break;
            }
        }
    }

    public static void ValidateSelections(
        IReadOnlyDictionary<int, AnswerDefinitionRecord> definitions,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        foreach (var definition in definitions.Values.OrderBy(item => item.SortOrder).ThenBy(item => item.DefinitionId))
        {
            if (!IsVisible(definition.Behavior, answersByKey))
            {
                continue;
            }

            answersByKey.TryGetValue(definition.Key, out var answer);

            switch (definition.InputType)
            {
                case "boolean" when definition.IsRequired && answer?.ValueBoolean is null:
                    throw new InvalidOperationException($"Bitte für \"{definition.Title}\" Ja oder Nein auswählen.");
                case "select" when definition.IsRequired && !HasSelectedOption(answer):
                    if (definition.Behavior.Validation is { Kind: "single_select_required" } selectValidation)
                    {
                        throw new InvalidOperationException(selectValidation.Message);
                    }

                    throw new InvalidOperationException($"Bitte für \"{definition.Title}\" eine Auswahl treffen.");
            }

            if (definition.Behavior.Validation is null)
            {
                continue;
            }

            switch (definition.Behavior.Validation.Kind)
            {
                case "text_required" when !HasTextAnswer(answer):
                    throw new InvalidOperationException(definition.Behavior.Validation.Message);
                case "multi_select_required" when answer is null || answer.SelectedOptionIds.Count == 0:
                    throw new InvalidOperationException(definition.Behavior.Validation.Message);
                case "single_select_required" when !HasSelectedOption(answer):
                    throw new InvalidOperationException(definition.Behavior.Validation.Message);
            }
        }
    }

    public static bool IsVisible(
        RequirementBehaviorDto behavior,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        if (behavior.VisibilityDependencies.Count == 0)
        {
            return true;
        }

        foreach (var dependency in behavior.VisibilityDependencies)
        {
            if (!answersByKey.TryGetValue(dependency.DependencyKey, out var dependencyAnswer))
            {
                if (!dependency.MissingResult)
                {
                    return false;
                }

                continue;
            }

            var satisfied = dependency.Kind switch
            {
                "boolean_true" => dependencyAnswer.ValueBoolean == true,
                "selected_option_value" => IsSelectedOptionValue(dependencyAnswer, dependency.ExpectedValue),
                _ => true
            };

            if (!satisfied)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ApplyResetTarget(
        RequirementResetTargetDto target,
        IReadOnlyDictionary<string, AnswerDefinitionRecord> definitionsByKey,
        IDictionary<int, RequirementSelectionStateRecord> selectionsByDefinitionId)
    {
        if (!definitionsByKey.TryGetValue(target.RequirementKey, out var targetDefinition))
        {
            return false;
        }

        if (!selectionsByDefinitionId.TryGetValue(targetDefinition.DefinitionId, out var selection))
        {
            selection = new RequirementSelectionStateRecord
            {
                SelectedOptionIds = new List<int>()
            };
            selectionsByDefinitionId[targetDefinition.DefinitionId] = selection;
        }

        var changed = false;

        if (target.ClearBoolean && selection.ValueBoolean is not null)
        {
            selection.ValueBoolean = null;
            changed = true;
        }

        if (target.ClearText && !string.IsNullOrWhiteSpace(selection.ValueText))
        {
            selection.ValueText = null;
            changed = true;
        }

        if (target.ClearNumber && selection.ValueNumber is not null)
        {
            selection.ValueNumber = null;
            changed = true;
        }

        if (target.ClearSelectedOption && selection.SelectedOptionId is not null)
        {
            selection.SelectedOptionId = null;
            changed = true;
        }

        if (target.ClearSelectedOptions && selection.SelectedOptionIds.Count > 0)
        {
            selection.SelectedOptionIds.Clear();
            changed = true;
        }

        return changed;
    }

    private static string? GetSelectedOptionValue(
        AnswerDefinitionRecord definition,
        RequirementSelectionStateRecord selection)
    {
        if (!selection.SelectedOptionId.HasValue)
        {
            return null;
        }

        return definition.OptionsById.TryGetValue(selection.SelectedOptionId.Value, out var option)
            ? option.OptionValue
            : null;
    }

    private static bool HasSelectedOption(StoredWorkflowAnswerRecord? answer)
    {
        return answer is not null && (answer.SelectedOptionId.HasValue || answer.SelectedOptionIds.Count > 0);
    }

    private static bool HasTextAnswer(StoredWorkflowAnswerRecord? answer)
    {
        return answer is not null && !string.IsNullOrWhiteSpace(answer.ValueText);
    }

    private static bool IsSelectedOptionValue(StoredWorkflowAnswerRecord answer, string? expectedValue)
    {
        if (string.IsNullOrWhiteSpace(expectedValue))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(answer.SelectedOptionValue)
            && string.Equals(answer.SelectedOptionValue.Trim(), expectedValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return answer.SelectedOptionValues.Any(value =>
            string.Equals(value.Trim(), expectedValue, StringComparison.OrdinalIgnoreCase));
    }
}
