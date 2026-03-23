using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private static async Task<List<RequirementDto>> LoadRequirements(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        var definitions = await LoadAnswerDefinitionRecords(connection, transaction);

        return definitions.Values
            .OrderBy(definition => definition.SortOrder)
            .ThenBy(definition => definition.DefinitionId)
            .Select(definition => new RequirementDto
            {
                Id = definition.DefinitionId,
                Key = definition.Key,
                Title = definition.Title,
                Description = definition.Description,
                Category = definition.Category,
                IconKey = definition.IconKey,
                InputType = definition.InputType,
                IsRequired = definition.IsRequired,
                SortOrder = definition.SortOrder,
                Behavior = definition.Behavior,
                Options = definition.OptionsById.Values
                    .OrderBy(option => option.SortOrder)
                    .ThenBy(option => option.OptionId)
                    .Select(option => new RequirementOptionDto
                    {
                        Id = option.OptionId,
                        Key = option.OptionKey,
                        Value = option.OptionValue,
                        Label = option.OptionLabel,
                        SortOrder = option.SortOrder,
                        IsDefault = false
                    })
                    .ToList()
            })
            .ToList();
    }

    private static async Task<Dictionary<int, AnswerDefinitionRecord>> LoadAnswerDefinitionRecords(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        const string sql = @"
SELECT
    d.id,
    d.answer_key,
    d.title,
    d.description,
    d.category,
    d.icon_key,
    d.input_type,
    d.is_required,
    d.sort_order,
    o.id,
    o.option_key,
    o.option_value,
    o.option_label,
    o.sort_order
FROM workflow_answer_definitions d
LEFT JOIN workflow_answer_options o ON o.answer_definition_id = d.id
WHERE d.is_active = TRUE
ORDER BY d.sort_order, d.id, o.sort_order, o.id;";

        var definitions = new Dictionary<int, AnswerDefinitionRecord>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var definitionId = reader.GetInt32(0);
                if (!definitions.TryGetValue(definitionId, out var definition))
                {
                    definition = new AnswerDefinitionRecord
                    {
                        DefinitionId = definitionId,
                        Key = reader.GetString(1),
                        Title = reader.GetString(2),
                        Description = reader.GetString(3),
                        Category = reader.GetString(4),
                        IconKey = reader.GetString(5),
                        InputType = reader.GetString(6),
                        IsRequired = reader.GetBoolean(7),
                        SortOrder = reader.GetInt32(8),
                        Behavior = CreateEmptyRequirementBehavior(),
                        OptionsById = new Dictionary<int, AnswerOptionRecord>()
                    };

                    definitions.Add(definitionId, definition);
                }

                if (!reader.IsDBNull(9))
                {
                    var optionId = reader.GetInt32(9);
                    definition.OptionsById[optionId] = new AnswerOptionRecord
                    {
                        OptionId = optionId,
                        OptionKey = reader.GetString(10),
                        OptionValue = reader.GetString(11),
                        OptionLabel = reader.GetString(12),
                        SortOrder = reader.GetInt32(13)
                    };
                }
            }
        }

        var behaviorsByDefinitionId = await LoadRequirementBehaviors(connection, transaction);
        foreach (var definition in definitions.Values)
        {
            if (behaviorsByDefinitionId.TryGetValue(definition.DefinitionId, out var behavior))
            {
                definition.Behavior = behavior;
            }
        }

        return definitions;
    }

    private static async Task<Dictionary<int, RequirementBehaviorDto>> LoadRequirementBehaviors(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        var behaviors = new Dictionary<int, RequirementBehaviorDto>();

        const string validationSql = @"
SELECT
    answer_definition_id,
    validation_kind,
    message
FROM workflow_answer_validation_rules;";

        await using (var command = new NpgsqlCommand(validationSql, connection, transaction))
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var answerDefinitionId = reader.GetInt32(0);
                GetOrCreateRequirementBehavior(behaviors, answerDefinitionId).Validation = new RequirementValidationDto
                {
                    Kind = reader.GetString(1),
                    Message = reader.GetString(2)
                };
            }
        }

        const string visibilitySql = @"
SELECT
    vr.answer_definition_id,
    dependency.answer_key,
    vr.dependency_kind,
    vr.expected_value_text,
    vr.missing_result
FROM workflow_answer_visibility_rules vr
JOIN workflow_answer_definitions dependency ON dependency.id = vr.dependency_answer_definition_id
ORDER BY vr.answer_definition_id, vr.sort_order, vr.id;";

        await using (var command = new NpgsqlCommand(visibilitySql, connection, transaction))
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var answerDefinitionId = reader.GetInt32(0);
                GetOrCreateRequirementBehavior(behaviors, answerDefinitionId).VisibilityDependencies.Add(
                    new RequirementVisibilityDependencyDto
                    {
                        DependencyKey = reader.GetString(1),
                        Kind = reader.GetString(2),
                        ExpectedValue = reader.IsDBNull(3) ? null : reader.GetString(3),
                        MissingResult = reader.GetBoolean(4)
                    });
            }
        }

        const string resetSql = @"
SELECT
    rr.answer_definition_id,
    rr.trigger_kind,
    target.answer_key,
    rr.clear_boolean,
    rr.clear_text,
    rr.clear_number,
    rr.clear_selected_option,
    rr.clear_selected_options
FROM workflow_answer_reset_rules rr
JOIN workflow_answer_definitions target ON target.id = rr.target_answer_definition_id
ORDER BY rr.answer_definition_id, rr.trigger_kind, rr.sort_order, rr.id;";

        await using (var command = new NpgsqlCommand(resetSql, connection, transaction))
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var answerDefinitionId = reader.GetInt32(0);
                var resetTarget = new RequirementResetTargetDto
                {
                    RequirementKey = reader.GetString(2),
                    ClearBoolean = reader.GetBoolean(3),
                    ClearText = reader.GetBoolean(4),
                    ClearNumber = reader.GetBoolean(5),
                    ClearSelectedOption = reader.GetBoolean(6),
                    ClearSelectedOptions = reader.GetBoolean(7)
                };

                var behavior = GetOrCreateRequirementBehavior(behaviors, answerDefinitionId);
                var triggerKind = reader.GetString(1);
                if (string.Equals(triggerKind, "when_not_true", StringComparison.OrdinalIgnoreCase))
                {
                    behavior.ResetTargetsWhenNotTrue.Add(resetTarget);
                    continue;
                }

                behavior.SingleSelectReset ??= new RequirementSingleSelectResetDto
                {
                    KeepSelectedOptionValues = new List<string>(),
                    Targets = new List<RequirementResetTargetDto>()
                };
                behavior.SingleSelectReset.Targets.Add(resetTarget);
            }
        }

        const string keepValueSql = @"
SELECT
    answer_definition_id,
    option_value
FROM workflow_answer_single_select_keep_values
ORDER BY answer_definition_id, sort_order, id;";

        await using (var command = new NpgsqlCommand(keepValueSql, connection, transaction))
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var answerDefinitionId = reader.GetInt32(0);
                var behavior = GetOrCreateRequirementBehavior(behaviors, answerDefinitionId);
                behavior.SingleSelectReset ??= new RequirementSingleSelectResetDto
                {
                    KeepSelectedOptionValues = new List<string>(),
                    Targets = new List<RequirementResetTargetDto>()
                };
                behavior.SingleSelectReset.KeepSelectedOptionValues.Add(reader.GetString(1));
            }
        }

        return behaviors;
    }

    private static RequirementBehaviorDto GetOrCreateRequirementBehavior(
        IDictionary<int, RequirementBehaviorDto> behaviors,
        int answerDefinitionId)
    {
        if (!behaviors.TryGetValue(answerDefinitionId, out var behavior))
        {
            behavior = CreateEmptyRequirementBehavior();
            behaviors[answerDefinitionId] = behavior;
        }

        return behavior;
    }

    private static RequirementBehaviorDto CreateEmptyRequirementBehavior()
    {
        return new RequirementBehaviorDto
        {
            VisibilityDependencies = new List<RequirementVisibilityDependencyDto>(),
            Validation = null,
            ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>(),
            SingleSelectReset = null
        };
    }

    private static async Task<RoleRecommendationsDto> LoadRoleRecommendations(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int roleId)
    {
        const string sql = @"
SELECT
    ard.answer_definition_id,
    ard.is_recommended,
    ard.is_default,
    ard.default_value_boolean,
    ard.default_value_text,
    ard.default_value_number,
    MAX(CASE WHEN ardo.is_default THEN ardo.answer_option_id END) AS default_selected_option_id,
    COALESCE(
        ARRAY_AGG(ardo.answer_option_id ORDER BY o.sort_order) FILTER (WHERE ardo.is_default),
        ARRAY[]::INTEGER[]
    ) AS default_selected_option_ids
FROM app_role_answer_defaults ard
LEFT JOIN app_role_answer_default_options ardo
    ON ardo.app_role_id = ard.app_role_id
    AND ardo.answer_definition_id = ard.answer_definition_id
LEFT JOIN workflow_answer_options o ON o.id = ardo.answer_option_id
WHERE ard.app_role_id = @roleId
GROUP BY
    ard.answer_definition_id,
    ard.is_recommended,
    ard.is_default,
    ard.default_value_boolean,
    ard.default_value_text,
    ard.default_value_number,
    ard.sort_order
ORDER BY ard.sort_order, ard.answer_definition_id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("roleId", roleId);

        await using var reader = await command.ExecuteReaderAsync();

        var recommendedRequirementIds = new List<int>();
        var defaultValues = new List<RoleRecommendationDefaultValueDto>();
        var defaultSelectedOptions = new List<RoleRecommendationSelectedOptionsDto>();

        while (await reader.ReadAsync())
        {
            var requirementId = reader.GetInt32(0);
            var isRecommended = reader.GetBoolean(1);
            var isDefault = reader.GetBoolean(2);
            bool? defaultValueBoolean = reader.IsDBNull(3) ? null : reader.GetBoolean(3);
            var defaultValueText = reader.IsDBNull(4) ? null : reader.GetString(4);
            decimal? defaultValueNumber = reader.IsDBNull(5) ? null : reader.GetDecimal(5);
            int? selectedOptionId = reader.IsDBNull(6) ? null : reader.GetInt32(6);
            var selectedOptionIds = reader.IsDBNull(7)
                ? new List<int>()
                : ((int[])reader.GetValue(7)).Distinct().ToList();

            if (isRecommended)
            {
                recommendedRequirementIds.Add(requirementId);
            }

            if (isDefault || defaultValueBoolean.HasValue || !string.IsNullOrWhiteSpace(defaultValueText) || defaultValueNumber.HasValue)
            {
                defaultValues.Add(new RoleRecommendationDefaultValueDto
                {
                    RequirementId = requirementId,
                    ValueBoolean = defaultValueBoolean,
                    ValueText = defaultValueText,
                    ValueNumber = defaultValueNumber
                });
            }

            if (isDefault || selectedOptionId.HasValue || selectedOptionIds.Count > 0)
            {
                defaultSelectedOptions.Add(new RoleRecommendationSelectedOptionsDto
                {
                    RequirementId = requirementId,
                    SelectedOptionId = selectedOptionId,
                    SelectedOptionIds = selectedOptionIds
                });
            }
        }

        return new RoleRecommendationsDto
        {
            RecommendedRequirementIds = recommendedRequirementIds.Distinct().ToList(),
            DefaultValues = defaultValues,
            DefaultSelectedOptions = defaultSelectedOptions
        };
    }

    private static async Task<Dictionary<int, RoleDefaultRecord>> LoadRoleDefaultRecords(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int roleId)
    {
        const string sql = @"
SELECT
    ard.answer_definition_id,
    ard.default_value_boolean,
    ard.default_value_text,
    ard.default_value_number,
    MAX(CASE WHEN ardo.is_default THEN ardo.answer_option_id END) AS default_selected_option_id,
    COALESCE(
        ARRAY_AGG(ardo.answer_option_id ORDER BY o.sort_order) FILTER (WHERE ardo.is_default),
        ARRAY[]::INTEGER[]
    ) AS default_selected_option_ids
FROM app_role_answer_defaults ard
LEFT JOIN app_role_answer_default_options ardo
    ON ardo.app_role_id = ard.app_role_id
    AND ardo.answer_definition_id = ard.answer_definition_id
LEFT JOIN workflow_answer_options o ON o.id = ardo.answer_option_id
WHERE ard.app_role_id = @roleId
GROUP BY
    ard.answer_definition_id,
    ard.default_value_boolean,
    ard.default_value_text,
    ard.default_value_number,
    ard.sort_order
ORDER BY ard.sort_order, ard.answer_definition_id;";

        var defaults = new Dictionary<int, RoleDefaultRecord>();

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("roleId", roleId);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var answerDefinitionId = reader.GetInt32(0);
            defaults[answerDefinitionId] = new RoleDefaultRecord
            {
                AnswerDefinitionId = answerDefinitionId,
                DefaultValueBoolean = reader.IsDBNull(1) ? null : reader.GetBoolean(1),
                DefaultValueText = reader.IsDBNull(2) ? null : reader.GetString(2),
                DefaultValueNumber = reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                DefaultSelectedOptionId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                DefaultSelectedOptionIds = reader.IsDBNull(5)
                    ? new List<int>()
                    : ((int[])reader.GetValue(5)).Distinct().ToList()
            };
        }

        return defaults;
    }

    private static async Task<List<StoredWorkflowAnswerRecord>> PersistWorkflowAnswers(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        IReadOnlyList<RequirementSelectionInputDto> selections,
        IReadOnlyDictionary<int, AnswerDefinitionRecord> definitions,
        IReadOnlyDictionary<int, RoleDefaultRecord> roleDefaults)
    {
        var submittedByDefinitionId = new Dictionary<int, RequirementSelectionInputDto>();
        foreach (var selection in selections)
        {
            if (definitions.ContainsKey(selection.RequirementId))
            {
                submittedByDefinitionId[selection.RequirementId] = selection;
            }
        }

        var selectionStatesByDefinitionId = BuildEffectiveSelectionStates(
            submittedByDefinitionId,
            definitions,
            roleDefaults);

        RequirementBehaviorEngine.ApplyResetRules(definitions, selectionStatesByDefinitionId);

        const string insertSql = @"
INSERT INTO workflow_answers (
    workflow_id,
    answer_definition_id,
    answer_key,
    input_type,
    value_boolean,
    value_text,
    value_number,
    selected_option_id
)
VALUES (
    @workflowId,
    @answerDefinitionId,
    @answerKey,
    @inputType,
    @valueBoolean,
    @valueText,
    @valueNumber,
    @selectedOptionId
)
RETURNING id;";

        const string insertSelectedOptionSql = @"
INSERT INTO workflow_answer_selected_options (workflow_answer_id, answer_option_id)
VALUES (@workflowAnswerId, @answerOptionId)
ON CONFLICT (workflow_answer_id, answer_option_id) DO NOTHING;";

        var persistedAnswers = new List<StoredWorkflowAnswerRecord>();

        foreach (var definition in definitions.Values.OrderBy(item => item.SortOrder).ThenBy(item => item.DefinitionId))
        {
            if (!selectionStatesByDefinitionId.TryGetValue(definition.DefinitionId, out var selectionState))
            {
                selectionState = new RequirementSelectionStateRecord
                {
                    SelectedOptionIds = new List<int>()
                };
                selectionStatesByDefinitionId[definition.DefinitionId] = selectionState;
            }

            NormalizeSelectionState(definition, selectionState);

            long workflowAnswerId;
            await using (var insertCommand = new NpgsqlCommand(insertSql, connection, transaction))
            {
                insertCommand.Parameters.AddWithValue("workflowId", workflowId);
                insertCommand.Parameters.AddWithValue("answerDefinitionId", definition.DefinitionId);
                insertCommand.Parameters.AddWithValue("answerKey", definition.Key);
                insertCommand.Parameters.AddWithValue("inputType", definition.InputType);
                insertCommand.Parameters.AddWithValue("valueBoolean", (object?)selectionState.ValueBoolean ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("valueText", (object?)selectionState.ValueText ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("valueNumber", (object?)selectionState.ValueNumber ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("selectedOptionId", (object?)selectionState.SelectedOptionId ?? DBNull.Value);

                var scalar = await insertCommand.ExecuteScalarAsync();
                if (scalar is null)
                {
                    throw new InvalidOperationException("Workflow answer could not be stored.");
                }

                workflowAnswerId = (long)scalar;
            }

            if (definition.InputType == "multi_select")
            {
                foreach (var optionId in selectionState.SelectedOptionIds)
                {
                    await using var insertSelectedOptionCommand = new NpgsqlCommand(insertSelectedOptionSql, connection, transaction);
                    insertSelectedOptionCommand.Parameters.AddWithValue("workflowAnswerId", workflowAnswerId);
                    insertSelectedOptionCommand.Parameters.AddWithValue("answerOptionId", optionId);
                    await insertSelectedOptionCommand.ExecuteNonQueryAsync();
                }
            }

            var selectedOptionValue = selectionState.SelectedOptionId.HasValue
                && definition.OptionsById.TryGetValue(selectionState.SelectedOptionId.Value, out var selectedOption)
                ? selectedOption.OptionValue
                : null;

            var selectedOptionValues = selectionState.SelectedOptionIds
                .Where(optionId => definition.OptionsById.ContainsKey(optionId))
                .Select(optionId => definition.OptionsById[optionId].OptionValue)
                .ToList();

            persistedAnswers.Add(new StoredWorkflowAnswerRecord
            {
                WorkflowAnswerId = workflowAnswerId,
                AnswerDefinitionId = definition.DefinitionId,
                AnswerKey = definition.Key,
                InputType = definition.InputType,
                ValueBoolean = selectionState.ValueBoolean,
                ValueText = selectionState.ValueText,
                ValueNumber = selectionState.ValueNumber,
                SelectedOptionId = selectionState.SelectedOptionId,
                SelectedOptionValue = selectedOptionValue,
                SelectedOptionIds = selectionState.SelectedOptionIds.ToList(),
                SelectedOptionValues = selectedOptionValues
            });
        }

        return persistedAnswers;
    }

    private static Dictionary<int, RequirementSelectionStateRecord> BuildEffectiveSelectionStates(
        IReadOnlyDictionary<int, RequirementSelectionInputDto> submittedByDefinitionId,
        IReadOnlyDictionary<int, AnswerDefinitionRecord> definitions,
        IReadOnlyDictionary<int, RoleDefaultRecord> roleDefaults)
    {
        var selectionStates = new Dictionary<int, RequirementSelectionStateRecord>();

        foreach (var definition in definitions.Values.OrderBy(item => item.SortOrder).ThenBy(item => item.DefinitionId))
        {
            submittedByDefinitionId.TryGetValue(definition.DefinitionId, out var submitted);
            roleDefaults.TryGetValue(definition.DefinitionId, out var roleDefault);

            var selectedOptionId = submitted?.SelectedOptionId ?? roleDefault?.DefaultSelectedOptionId;
            if (selectedOptionId.HasValue && !definition.OptionsById.ContainsKey(selectedOptionId.Value))
            {
                throw new InvalidOperationException($"Invalid option '{selectedOptionId.Value}' for requirement '{definition.DefinitionId}'.");
            }

            var selectedOptionIds = submitted?.SelectedOptionIds?.Distinct().ToList()
                ?? (roleDefault?.DefaultSelectedOptionIds.ToList() ?? new List<int>());

            selectionStates[definition.DefinitionId] = new RequirementSelectionStateRecord
            {
                ValueBoolean = submitted?.ValueBoolean ?? roleDefault?.DefaultValueBoolean,
                ValueText = NormalizeOptionalText(submitted?.ValueText ?? roleDefault?.DefaultValueText),
                ValueNumber = submitted?.ValueNumber ?? roleDefault?.DefaultValueNumber,
                SelectedOptionId = selectedOptionId,
                SelectedOptionIds = selectedOptionIds
                    .Where(optionId => definition.OptionsById.ContainsKey(optionId))
                    .Distinct()
                    .ToList()
            };
        }

        return selectionStates;
    }

    private static void NormalizeSelectionState(
        AnswerDefinitionRecord definition,
        RequirementSelectionStateRecord selectionState)
    {
        selectionState.ValueText = NormalizeOptionalText(selectionState.ValueText);
        var normalizedSelectedOptionIds = selectionState.SelectedOptionIds
            .Where(optionId => definition.OptionsById.ContainsKey(optionId))
            .Distinct()
            .ToList();
        selectionState.SelectedOptionIds.Clear();
        selectionState.SelectedOptionIds.AddRange(normalizedSelectedOptionIds);

        switch (definition.InputType)
        {
            case "boolean":
                selectionState.ValueText = null;
                selectionState.ValueNumber = null;
                selectionState.SelectedOptionId = null;
                selectionState.SelectedOptionIds.Clear();

                if (definition.IsRequired && !selectionState.ValueBoolean.HasValue)
                {
                    throw new InvalidOperationException($"Requirement {definition.DefinitionId} needs a boolean value.");
                }
                break;

            case "text":
                selectionState.ValueBoolean = null;
                selectionState.ValueNumber = null;
                selectionState.SelectedOptionId = null;
                selectionState.SelectedOptionIds.Clear();

                if (definition.IsRequired && string.IsNullOrWhiteSpace(selectionState.ValueText))
                {
                    throw new InvalidOperationException($"Requirement {definition.DefinitionId} needs text input.");
                }
                break;

            case "select":
                selectionState.ValueBoolean = null;
                selectionState.ValueText = null;
                selectionState.ValueNumber = null;

                if (!selectionState.SelectedOptionId.HasValue && selectionState.SelectedOptionIds.Count == 1)
                {
                    selectionState.SelectedOptionId = selectionState.SelectedOptionIds[0];
                }

                selectionState.SelectedOptionIds.Clear();
                if (selectionState.SelectedOptionId.HasValue)
                {
                    selectionState.SelectedOptionIds.Add(selectionState.SelectedOptionId.Value);
                }

                if (definition.IsRequired && !selectionState.SelectedOptionId.HasValue)
                {
                    throw new InvalidOperationException($"Requirement {definition.DefinitionId} needs a selected option.");
                }
                break;

            case "multi_select":
                selectionState.ValueBoolean = null;
                selectionState.ValueText = null;
                selectionState.ValueNumber = null;
                selectionState.SelectedOptionId = null;

                if (definition.IsRequired && selectionState.SelectedOptionIds.Count == 0)
                {
                    throw new InvalidOperationException($"Requirement {definition.DefinitionId} needs one or more selected options.");
                }
                break;

            default:
                throw new InvalidOperationException($"Unsupported input type '{definition.InputType}'.");
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateSupervisorSelections(
        IReadOnlyDictionary<int, AnswerDefinitionRecord> definitions,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        RequirementBehaviorEngine.ValidateSelections(definitions, answersByKey);
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

        var hardwareType = string.Join(
            " ",
            rawValue
                .Trim()
                .Replace("-", " ")
                .Replace("_", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(token => char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant()));

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

    private static async Task LoadWorkflowRequirements(
        NpgsqlConnection connection,
        long workflowId,
        List<WorkflowRequirementSnapshotDto> requirements)
    {
        var behaviorsByDefinitionId = await LoadRequirementBehaviors(connection, null);

        const string sql = @"
SELECT
    d.id,
    d.answer_key,
    d.title,
    d.description,
    d.category,
    d.icon_key,
    d.input_type,
    d.is_required,
    d.sort_order,
    o.id,
    o.option_key,
    o.option_value,
    o.option_label,
    o.sort_order,
    a.id,
    a.value_boolean,
    a.value_text,
    a.value_number,
    a.selected_option_id
FROM workflow_answer_definitions d
LEFT JOIN workflow_answer_options o ON o.answer_definition_id = d.id
LEFT JOIN workflow_answers a
    ON a.workflow_id = @workflowId
    AND a.answer_definition_id = d.id
WHERE d.is_active = TRUE
ORDER BY d.sort_order, d.id, o.sort_order, o.id;";

        var requirementById = new Dictionary<int, WorkflowRequirementSnapshotDto>();

        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var definitionId = reader.GetInt32(0);
                if (!requirementById.TryGetValue(definitionId, out var requirement))
                {
                    var workflowRequirementId = reader.IsDBNull(14) ? definitionId : reader.GetInt64(14);
                    int? selectedOptionId = reader.IsDBNull(18) ? null : reader.GetInt32(18);

                    requirement = new WorkflowRequirementSnapshotDto
                    {
                        WorkflowRequirementId = workflowRequirementId,
                        Id = definitionId,
                        Key = reader.GetString(1),
                        Title = reader.GetString(2),
                        Description = reader.GetString(3),
                        Category = reader.GetString(4),
                        IconKey = reader.GetString(5),
                        InputType = reader.GetString(6),
                        IsRequired = reader.GetBoolean(7),
                        IsVisible = true,
                        SortOrder = reader.GetInt32(8),
                        Behavior = behaviorsByDefinitionId.TryGetValue(definitionId, out var behavior)
                            ? behavior
                            : CreateEmptyRequirementBehavior(),
                        Options = new List<WorkflowRequirementOptionSnapshotDto>(),
                        Value = new WorkflowRequirementValueDto
                        {
                            ValueBoolean = reader.IsDBNull(15) ? null : reader.GetBoolean(15),
                            ValueText = reader.IsDBNull(16) ? null : reader.GetString(16),
                            ValueNumber = reader.IsDBNull(17) ? null : reader.GetDecimal(17),
                            SelectedOptionId = selectedOptionId,
                            SelectedOptionKey = null,
                            SelectedOptionValue = null,
                            SelectedOptionLabel = null,
                            SelectedOptions = new List<WorkflowRequirementSelectedOptionDto>()
                        }
                    };

                    requirementById.Add(definitionId, requirement);
                    requirements.Add(requirement);
                }

                if (!reader.IsDBNull(9))
                {
                    requirement.Options.Add(new WorkflowRequirementOptionSnapshotDto
                    {
                        Id = reader.GetInt32(9),
                        SourceOptionId = reader.GetInt32(9),
                        Key = reader.GetString(10),
                        Value = reader.GetString(11),
                        Label = reader.GetString(12),
                        SortOrder = reader.GetInt32(13)
                    });
                }
            }
        }

        const string selectedOptionsSql = @"
SELECT
    a.answer_definition_id,
    o.id,
    o.option_key,
    o.option_value,
    o.option_label,
    o.sort_order
FROM workflow_answers a
JOIN workflow_answer_selected_options aso ON aso.workflow_answer_id = a.id
JOIN workflow_answer_options o ON o.id = aso.answer_option_id
WHERE a.workflow_id = @workflowId
ORDER BY a.answer_definition_id, o.sort_order, o.id;";

        await using (var selectedOptionsCommand = new NpgsqlCommand(selectedOptionsSql, connection))
        {
            selectedOptionsCommand.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await selectedOptionsCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var definitionId = reader.GetInt32(0);
                if (!requirementById.TryGetValue(definitionId, out var requirement))
                {
                    continue;
                }

                requirement.Value.SelectedOptions.Add(new WorkflowRequirementSelectedOptionDto
                {
                    Id = reader.GetInt32(1),
                    Key = reader.GetString(2),
                    Value = reader.GetString(3),
                    Label = reader.GetString(4),
                    SortOrder = reader.GetInt32(5)
                });
            }
        }

        foreach (var requirement in requirements)
        {
            var selectedOption = requirement.Value.SelectedOptionId.HasValue
                ? requirement.Options.FirstOrDefault(option => option.Id == requirement.Value.SelectedOptionId.Value)
                : null;

            requirement.Value = new WorkflowRequirementValueDto
            {
                ValueBoolean = requirement.Value.ValueBoolean,
                ValueText = requirement.Value.ValueText,
                ValueNumber = requirement.Value.ValueNumber,
                SelectedOptionId = requirement.Value.SelectedOptionId,
                SelectedOptionKey = selectedOption?.Key,
                SelectedOptionValue = selectedOption?.Value,
                SelectedOptionLabel = selectedOption?.Label,
                SelectedOptions = requirement.Value.SelectedOptions
            };
        }
    }
}
