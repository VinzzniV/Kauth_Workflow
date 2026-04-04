using Xunit;

namespace API.Tests;

public sealed class TaskConditionEvaluatorTests
{
    [Fact]
    public void ShouldCreateTask_ReturnsTrue_WhenNoConditionsExist()
    {
        var result = TaskConditionEvaluator.ShouldCreateTask([], new Dictionary<string, StoredWorkflowAnswerRecord>());

        Assert.True(result);
    }

    [Fact]
    public void EvaluateCondition_SupportsIsTrueAndIsFalse()
    {
        var trueAnswer = CreateAnswer("flag", valueBoolean: true);
        var falseAnswer = CreateAnswer("other_flag", valueBoolean: false);
        var answers = new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["flag"] = trueAnswer,
            ["other_flag"] = falseAnswer
        };

        Assert.True(TaskConditionEvaluator.EvaluateCondition(CreateCondition("flag", "is_true"), answers));
        Assert.True(TaskConditionEvaluator.EvaluateCondition(CreateCondition("other_flag", "is_false"), answers));
    }

    [Fact]
    public void EvaluateCondition_SupportsEqAgainstSelectedOptionValue_CaseInsensitive()
    {
        var answers = new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["hardware_type"] = CreateAnswer("hardware_type", selectedOptionValue: "Laptop")
        };

        var result = TaskConditionEvaluator.EvaluateCondition(
            CreateCondition("hardware_type", "eq", expectedValueText: "laptop"),
            answers);

        Assert.True(result);
    }

    [Fact]
    public void ShouldCreateTask_UsesOrAcrossConditionGroups()
    {
        var answers = new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["flag_a"] = CreateAnswer("flag_a", valueBoolean: false),
            ["flag_b"] = CreateAnswer("flag_b", valueBoolean: true)
        };

        var conditions = new List<TaskTemplateConditionRecord>
        {
            CreateCondition("flag_a", "is_true", conditionGroup: 1),
            CreateCondition("flag_b", "is_true", conditionGroup: 2)
        };

        var result = TaskConditionEvaluator.ShouldCreateTask(conditions, answers);

        Assert.True(result);
    }

    [Fact]
    public void IsAnswerEmpty_ReturnsTrue_ForEmptyAnswer()
    {
        var answer = CreateAnswer("empty");

        Assert.True(TaskConditionEvaluator.IsAnswerEmpty(answer));
    }

    [Fact]
    public void GetPostSupervisorTemplateIds_ReturnsAllTransitivelyDependentTemplates()
    {
        var templates = new List<TaskTemplateRecord>
        {
            CreateTemplate(1, "supervisor_step"),
            CreateTemplate(2, "hardware_setup"),
            CreateTemplate(3, "account_setup"),
            CreateTemplate(4, "handover")
        };
        var dependencies = new List<TaskTemplateDependencyRecord>
        {
            CreateDependency(2, 1),
            CreateDependency(3, 2),
            CreateDependency(4, 1)
        };

        var result = TaskConditionEvaluator.GetPostSupervisorTemplateIds(
            templates,
            dependencies,
            "supervisor_step");

        Assert.Equal([2, 3, 4], result.OrderBy(id => id).ToArray());
    }

    [Fact]
    public void BuildTaskDescription_AppendsHardwareContext()
    {
        var template = CreateTemplate(1, "hardware_setup", description: "Gerät vorbereiten");
        var answers = new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase)
        {
            [RequirementKeys.HardwareType] = CreateAnswer(RequirementKeys.HardwareType, selectedOptionValue: "laptop"),
            [RequirementKeys.LaptopVpnType] = CreateAnswer(RequirementKeys.LaptopVpnType, selectedOptionValue: "cisco_anyconnect")
        };

        var result = TaskConditionEvaluator.BuildTaskDescription(template, answers);

        Assert.Equal("Gerät vorbereiten Gewünschte Hardware: Laptop (Cisco Anyconnect).", result);
    }

    private static StoredWorkflowAnswerRecord CreateAnswer(
        string key,
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
            WorkflowAnswerId = 1,
            AnswerDefinitionId = 1,
            AnswerKey = key,
            InputType = "text",
            ValueBoolean = valueBoolean,
            ValueText = valueText,
            ValueNumber = valueNumber,
            SelectedOptionId = selectedOptionId,
            SelectedOptionValue = selectedOptionValue,
            SelectedOptionIds = selectedOptionIds ?? [],
            SelectedOptionValues = selectedOptionValues ?? (selectedOptionValue is null ? [] : [selectedOptionValue])
        };
    }

    private static TaskTemplateConditionRecord CreateCondition(
        string answerKey,
        string @operator,
        int conditionGroup = 1,
        string? expectedValueText = null,
        bool? expectedValueBoolean = null,
        decimal? expectedValueNumber = null)
    {
        return new TaskTemplateConditionRecord
        {
            TaskTemplateId = 1,
            ConditionGroup = conditionGroup,
            AnswerKey = answerKey,
            Operator = @operator,
            ExpectedValueText = expectedValueText,
            ExpectedValueBoolean = expectedValueBoolean,
            ExpectedValueNumber = expectedValueNumber
        };
    }

    private static TaskTemplateRecord CreateTemplate(int id, string key, string description = "")
    {
        return new TaskTemplateRecord
        {
            Id = id,
            TemplateKey = key,
            Title = key,
            Description = description,
            Category = "IT",
            IconKey = "pc",
            DefaultResponsibilityId = null,
            ProcessAreaLabel = null,
            IsDepartmentPhaseTask = true,
            IsRequired = true,
            DueInDays = null,
            SortOrder = id
        };
    }

    private static TaskTemplateDependencyRecord CreateDependency(int taskTemplateId, int dependsOnTaskTemplateId)
    {
        return new TaskTemplateDependencyRecord
        {
            TaskTemplateId = taskTemplateId,
            DependsOnTaskTemplateId = dependsOnTaskTemplateId,
            RequiredStatus = "done"
        };
    }
}
