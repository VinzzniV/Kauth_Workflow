using Xunit;

namespace API.Tests;

public sealed class RequirementBehaviorEngineTests
{
    [Fact]
    public void ApplyResetRules_ClearsComparisonBranchWhenAdUserNotTrue()
    {
        var definitions = ToDefinitions(
            CreateDefinition(1, RequirementKeys.AdUserRequested, "boolean"),
            CreateDefinition(2, RequirementKeys.ComparisonUserAvailable, "boolean"),
            CreateDefinition(3, RequirementKeys.ComparisonUserName, "text"));

        var selections = new Dictionary<int, RequirementSelectionStateRecord>
        {
            [1] = CreateSelectionState(valueBoolean: false),
            [2] = CreateSelectionState(valueBoolean: true),
            [3] = CreateSelectionState(valueText: "Max Mustermann")
        };

        RequirementBehaviorEngine.ApplyResetRules(definitions, selections);

        Assert.Null(selections[2].ValueBoolean);
        Assert.Null(selections[3].ValueText);
    }

    [Fact]
    public void ApplyResetRules_ClearsHardwareBranchWhenHardwareIsNotRequested()
    {
        var definitions = ToDefinitions(
            CreateDefinition(1, RequirementKeys.HardwareRequested, "boolean"),
            CreateDefinition(2, RequirementKeys.HardwareAvailable, "boolean"),
            CreateDefinition(3, RequirementKeys.HardwareType, "select", ("laptop", "Laptop"), ("workstation", "Workstation")),
            CreateDefinition(4, RequirementKeys.LaptopVpnType, "select", ("with_vpn", "Mit VPN"), ("without_vpn", "Ohne VPN")));

        var selections = new Dictionary<int, RequirementSelectionStateRecord>
        {
            [1] = CreateSelectionState(valueBoolean: false),
            [2] = CreateSelectionState(valueBoolean: true),
            [3] = CreateSelectionState(selectedOptionId: 1, selectedOptionIds: new List<int> { 1 }),
            [4] = CreateSelectionState(selectedOptionId: 1, selectedOptionIds: new List<int> { 1 })
        };

        RequirementBehaviorEngine.ApplyResetRules(definitions, selections);

        Assert.Null(selections[2].ValueBoolean);
        Assert.Null(selections[3].SelectedOptionId);
        Assert.Empty(selections[3].SelectedOptionIds);
        Assert.Null(selections[4].SelectedOptionId);
        Assert.Empty(selections[4].SelectedOptionIds);
    }

    [Fact]
    public void ApplyResetRules_ClearsLaptopVpnWhenHardwareTypeIsNotLaptop()
    {
        var definitions = ToDefinitions(
            CreateDefinition(1, RequirementKeys.HardwareType, "select", ("laptop", "Laptop"), ("workstation", "Workstation")),
            CreateDefinition(2, RequirementKeys.LaptopVpnType, "select", ("with_vpn", "Mit VPN"), ("without_vpn", "Ohne VPN")));

        var selections = new Dictionary<int, RequirementSelectionStateRecord>
        {
            [1] = CreateSelectionState(selectedOptionId: 2, selectedOptionIds: new List<int> { 2 }),
            [2] = CreateSelectionState(selectedOptionId: 1, selectedOptionIds: new List<int> { 1 })
        };

        RequirementBehaviorEngine.ApplyResetRules(definitions, selections);

        Assert.Null(selections[2].SelectedOptionId);
        Assert.Empty(selections[2].SelectedOptionIds);
    }

    [Fact]
    public void ValidateSelections_RequiresComparisonUserNameOnlyWhenVisible()
    {
        var definitions = ToDefinitions(
            CreateDefinition(1, RequirementKeys.AdUserRequested, "boolean"),
            CreateDefinition(2, RequirementKeys.ComparisonUserAvailable, "boolean"),
            CreateDefinition(3, RequirementKeys.ComparisonUserName, "text"));

        var hiddenAnswers = ToAnswers(
            CreateAnswer(1, RequirementKeys.AdUserRequested, "boolean", valueBoolean: true),
            CreateAnswer(2, RequirementKeys.ComparisonUserAvailable, "boolean", valueBoolean: false),
            CreateAnswer(3, RequirementKeys.ComparisonUserName, "text", valueText: null));

        var visibleAnswers = ToAnswers(
            CreateAnswer(1, RequirementKeys.AdUserRequested, "boolean", valueBoolean: true),
            CreateAnswer(2, RequirementKeys.ComparisonUserAvailable, "boolean", valueBoolean: true),
            CreateAnswer(3, RequirementKeys.ComparisonUserName, "text", valueText: null));

        RequirementBehaviorEngine.ValidateSelections(definitions, hiddenAnswers);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RequirementBehaviorEngine.ValidateSelections(definitions, visibleAnswers));
        Assert.Equal("Bitte den Referenzuser angeben.", exception.Message);
    }

    [Fact]
    public void ValidateSelections_RequiresLaptopVpnAndInternalDriveRolesWhenVisible()
    {
        var laptopDefinitions = ToDefinitions(
            CreateDefinition(1, RequirementKeys.HardwareRequested, "boolean"),
            CreateDefinition(2, RequirementKeys.HardwareType, "select", ("laptop", "Laptop"), ("workstation", "Workstation")),
            CreateDefinition(3, RequirementKeys.LaptopVpnType, "select", ("with_vpn", "Mit VPN"), ("without_vpn", "Ohne VPN")));

        var driveDefinitions = ToDefinitions(
            CreateDefinition(4, RequirementKeys.InternalDriveAccessRequested, "boolean"),
            CreateDefinition(5, RequirementKeys.InternalDriveAccessRoles, "multi_select", ("leitung", "Leitung"), ("bereichsleitung", "Bereichsleitung")));

        var laptopAnswers = ToAnswers(
            CreateAnswer(1, RequirementKeys.HardwareRequested, "boolean", valueBoolean: true),
            CreateAnswer(2, RequirementKeys.HardwareType, "select", selectedOptionId: 1, selectedOptionValue: "laptop"),
            CreateAnswer(3, RequirementKeys.LaptopVpnType, "select"));

        var driveAnswers = ToAnswers(
            CreateAnswer(4, RequirementKeys.InternalDriveAccessRequested, "boolean", valueBoolean: true),
            CreateAnswer(5, RequirementKeys.InternalDriveAccessRoles, "multi_select", selectedOptionIds: new List<int>(), selectedOptionValues: new List<string>()));

        var laptopException = Assert.Throws<InvalidOperationException>(() =>
            RequirementBehaviorEngine.ValidateSelections(laptopDefinitions, laptopAnswers));
        Assert.Equal("Bitte auswählen, ob der Laptop mit VPN oder ohne VPN benötigt wird.", laptopException.Message);

        var driveException = Assert.Throws<InvalidOperationException>(() =>
            RequirementBehaviorEngine.ValidateSelections(driveDefinitions, driveAnswers));
        Assert.Equal("Bitte mindestens eine Funktion für die Laufwerksrechte auswählen.", driveException.Message);
    }

    [Fact]
    public void ValidateSelections_DoesNotRequireInternalDriveRolesWhenParentIsUnanswered()
    {
        var definitions = ToDefinitions(
            CreateDefinition(1, RequirementKeys.InternalDriveAccessRequested, "boolean"),
            CreateDefinition(2, RequirementKeys.InternalDriveAccessRoles, "multi_select", ("leitung", "Leitung")));

        var answers = ToAnswers(
            CreateAnswer(1, RequirementKeys.InternalDriveAccessRequested, "boolean", valueBoolean: null),
            CreateAnswer(2, RequirementKeys.InternalDriveAccessRoles, "multi_select", selectedOptionIds: new List<int>(), selectedOptionValues: new List<string>()));

        RequirementBehaviorEngine.ValidateSelections(definitions, answers);
    }

    private static Dictionary<int, AnswerDefinitionRecord> ToDefinitions(params AnswerDefinitionRecord[] definitions)
    {
        return definitions.ToDictionary(definition => definition.DefinitionId);
    }

    private static Dictionary<string, StoredWorkflowAnswerRecord> ToAnswers(params StoredWorkflowAnswerRecord[] answers)
    {
        return answers.ToDictionary(answer => answer.AnswerKey, StringComparer.OrdinalIgnoreCase);
    }

    private static AnswerDefinitionRecord CreateDefinition(
        int definitionId,
        string key,
        string inputType,
        params (string Value, string Label)[] options)
    {
        var optionsById = new Dictionary<int, AnswerOptionRecord>();
        for (var index = 0; index < options.Length; index += 1)
        {
            optionsById[index + 1] = new AnswerOptionRecord
            {
                OptionId = index + 1,
                OptionKey = options[index].Value,
                OptionValue = options[index].Value,
                OptionLabel = options[index].Label,
                SortOrder = index + 1
            };
        }

        return new AnswerDefinitionRecord
        {
            DefinitionId = definitionId,
            Key = key,
            Title = key,
            Description = key,
            Category = "test",
            IconKey = "test",
            InputType = inputType,
            IsRequired = false,
            SortOrder = definitionId,
            Behavior = CreateBehavior(key),
            OptionsById = optionsById
        };
    }

    private static RequirementBehaviorDto CreateBehavior(string key)
    {
        return key switch
        {
            RequirementKeys.AdUserRequested => new RequirementBehaviorDto
            {
                VisibilityDependencies = new List<RequirementVisibilityDependencyDto>(),
                Validation = null,
                ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>
                {
                    CreateResetTarget(RequirementKeys.ComparisonUserAvailable, clearBoolean: true),
                    CreateResetTarget(RequirementKeys.ComparisonUserName, clearText: true)
                },
                SingleSelectReset = null
            },
            RequirementKeys.ComparisonUserAvailable => new RequirementBehaviorDto
            {
                VisibilityDependencies = new List<RequirementVisibilityDependencyDto>
                {
                    CreateBooleanTrueDependency(RequirementKeys.AdUserRequested, missingResult: true)
                },
                Validation = null,
                ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>
                {
                    CreateResetTarget(RequirementKeys.ComparisonUserName, clearText: true)
                },
                SingleSelectReset = null
            },
            RequirementKeys.ComparisonUserName => new RequirementBehaviorDto
            {
                VisibilityDependencies = new List<RequirementVisibilityDependencyDto>
                {
                    CreateBooleanTrueDependency(RequirementKeys.AdUserRequested, missingResult: false),
                    CreateBooleanTrueDependency(RequirementKeys.ComparisonUserAvailable, missingResult: false)
                },
                Validation = CreateValidation("text_required", "Bitte den Referenzuser angeben."),
                ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>(),
                SingleSelectReset = null
            },
            RequirementKeys.HardwareRequested => new RequirementBehaviorDto
            {
                VisibilityDependencies = new List<RequirementVisibilityDependencyDto>(),
                Validation = null,
                ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>
                {
                    CreateResetTarget(RequirementKeys.HardwareAvailable, clearBoolean: true),
                    CreateResetTarget(RequirementKeys.PhoneRequested, clearBoolean: true),
                    CreateResetTarget(RequirementKeys.HardwareType, clearSelectedOption: true, clearSelectedOptions: true),
                    CreateResetTarget(RequirementKeys.LaptopVpnType, clearSelectedOption: true, clearSelectedOptions: true)
                },
                SingleSelectReset = null
            },
            RequirementKeys.HardwareAvailable => new RequirementBehaviorDto
            {
                VisibilityDependencies = new List<RequirementVisibilityDependencyDto>
                {
                    CreateBooleanTrueDependency(RequirementKeys.HardwareRequested, missingResult: false)
                },
                Validation = null,
                ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>(),
                SingleSelectReset = null
            },
            RequirementKeys.PhoneRequested => new RequirementBehaviorDto
            {
                VisibilityDependencies = new List<RequirementVisibilityDependencyDto>
                {
                    CreateBooleanTrueDependency(RequirementKeys.HardwareRequested, missingResult: true)
                },
                Validation = null,
                ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>(),
                SingleSelectReset = null
            },
            RequirementKeys.HardwareType => new RequirementBehaviorDto
            {
                VisibilityDependencies = new List<RequirementVisibilityDependencyDto>
                {
                    CreateBooleanTrueDependency(RequirementKeys.HardwareRequested, missingResult: true)
                },
                Validation = null,
                ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>(),
                SingleSelectReset = new RequirementSingleSelectResetDto
                {
                    KeepSelectedOptionValues = new List<string> { "laptop" },
                    Targets = new List<RequirementResetTargetDto>
                    {
                        CreateResetTarget(RequirementKeys.LaptopVpnType, clearSelectedOption: true, clearSelectedOptions: true)
                    }
                }
            },
            RequirementKeys.LaptopVpnType => new RequirementBehaviorDto
            {
                VisibilityDependencies = new List<RequirementVisibilityDependencyDto>
                {
                    CreateBooleanTrueDependency(RequirementKeys.HardwareRequested, missingResult: true),
                    CreateSelectedOptionDependency(RequirementKeys.HardwareType, "laptop", missingResult: false)
                },
                Validation = CreateValidation(
                    "single_select_required",
                    "Bitte auswählen, ob der Laptop mit VPN oder ohne VPN benötigt wird."),
                ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>(),
                SingleSelectReset = null
            },
            RequirementKeys.InternalDriveAccessRequested => new RequirementBehaviorDto
            {
                VisibilityDependencies = new List<RequirementVisibilityDependencyDto>(),
                Validation = null,
                ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>
                {
                    CreateResetTarget(RequirementKeys.InternalDriveAccessRoles, clearSelectedOptions: true)
                },
                SingleSelectReset = null
            },
            RequirementKeys.InternalDriveAccessRoles => new RequirementBehaviorDto
            {
                VisibilityDependencies = new List<RequirementVisibilityDependencyDto>
                {
                    CreateBooleanTrueDependency(RequirementKeys.InternalDriveAccessRequested, missingResult: false)
                },
                Validation = CreateValidation(
                    "multi_select_required",
                    "Bitte mindestens eine Funktion für die Laufwerksrechte auswählen."),
                ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>(),
                SingleSelectReset = null
            },
            _ => CreateEmptyBehavior()
        };
    }

    private static RequirementBehaviorDto CreateEmptyBehavior()
    {
        return new RequirementBehaviorDto
        {
            VisibilityDependencies = new List<RequirementVisibilityDependencyDto>(),
            Validation = null,
            ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>(),
            SingleSelectReset = null
        };
    }

    private static RequirementVisibilityDependencyDto CreateBooleanTrueDependency(
        string dependencyKey,
        bool missingResult)
    {
        return new RequirementVisibilityDependencyDto
        {
            DependencyKey = dependencyKey,
            Kind = "boolean_true",
            ExpectedValue = null,
            MissingResult = missingResult
        };
    }

    private static RequirementVisibilityDependencyDto CreateSelectedOptionDependency(
        string dependencyKey,
        string expectedValue,
        bool missingResult)
    {
        return new RequirementVisibilityDependencyDto
        {
            DependencyKey = dependencyKey,
            Kind = "selected_option_value",
            ExpectedValue = expectedValue,
            MissingResult = missingResult
        };
    }

    private static RequirementValidationDto CreateValidation(string kind, string message)
    {
        return new RequirementValidationDto
        {
            Kind = kind,
            Message = message
        };
    }

    private static RequirementResetTargetDto CreateResetTarget(
        string requirementKey,
        bool clearBoolean = false,
        bool clearText = false,
        bool clearNumber = false,
        bool clearSelectedOption = false,
        bool clearSelectedOptions = false)
    {
        return new RequirementResetTargetDto
        {
            RequirementKey = requirementKey,
            ClearBoolean = clearBoolean,
            ClearText = clearText,
            ClearNumber = clearNumber,
            ClearSelectedOption = clearSelectedOption,
            ClearSelectedOptions = clearSelectedOptions
        };
    }

    private static RequirementSelectionStateRecord CreateSelectionState(
        bool? valueBoolean = null,
        string? valueText = null,
        decimal? valueNumber = null,
        int? selectedOptionId = null,
        List<int>? selectedOptionIds = null)
    {
        return new RequirementSelectionStateRecord
        {
            ValueBoolean = valueBoolean,
            ValueText = valueText,
            ValueNumber = valueNumber,
            SelectedOptionId = selectedOptionId,
            SelectedOptionIds = selectedOptionIds ?? new List<int>()
        };
    }

    private static StoredWorkflowAnswerRecord CreateAnswer(
        int definitionId,
        string key,
        string inputType,
        bool? valueBoolean = null,
        string? valueText = null,
        decimal? valueNumber = null,
        int? selectedOptionId = null,
        string? selectedOptionValue = null,
        List<int>? selectedOptionIds = null,
        List<string>? selectedOptionValues = null)
    {
        return new StoredWorkflowAnswerRecord
        {
            WorkflowAnswerId = definitionId,
            AnswerDefinitionId = definitionId,
            AnswerKey = key,
            InputType = inputType,
            ValueBoolean = valueBoolean,
            ValueText = valueText,
            ValueNumber = valueNumber,
            SelectedOptionId = selectedOptionId,
            SelectedOptionValue = selectedOptionValue,
            SelectedOptionIds = selectedOptionIds ?? new List<int>(),
            SelectedOptionValues = selectedOptionValues ?? new List<string>()
        };
    }
}
