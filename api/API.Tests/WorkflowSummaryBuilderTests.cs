using Xunit;

namespace API.Tests;

public sealed class WorkflowSummaryBuilderTests
{
    [Fact]
    public void BuildRequirementSummary_CountsVisibleAnsweredAndPendingRequirements()
    {
        var requirements = new List<WorkflowRequirementSnapshotDto>
        {
            CreateRequirement(
                1,
                RequirementKeys.AdUserRequested,
                "boolean",
                CreateSelection(valueBoolean: true)),
            CreateRequirement(
                2,
                RequirementKeys.ComparisonUserAvailable,
                "boolean",
                CreateSelection(valueBoolean: true),
                behavior: new RequirementBehaviorDto
                {
                    VisibilityDependencies = new List<RequirementVisibilityDependencyDto>
                    {
                        CreateBooleanTrueDependency(RequirementKeys.AdUserRequested, missingResult: true)
                    },
                    Validation = null,
                    ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>(),
                    SingleSelectReset = null
                }),
            CreateRequirement(
                3,
                RequirementKeys.ComparisonUserName,
                "text",
                CreateSelection(valueText: null),
                behavior: new RequirementBehaviorDto
                {
                    VisibilityDependencies = new List<RequirementVisibilityDependencyDto>
                    {
                        CreateBooleanTrueDependency(RequirementKeys.AdUserRequested, missingResult: false),
                        CreateBooleanTrueDependency(RequirementKeys.ComparisonUserAvailable, missingResult: false)
                    },
                    Validation = new RequirementValidationDto
                    {
                        Kind = "text_required",
                        Message = "Bitte den Referenzuser angeben."
                    },
                    ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>(),
                    SingleSelectReset = null
                }),
            CreateRequirement(
                4,
                RequirementKeys.HardwareRequested,
                "boolean",
                CreateSelection(valueBoolean: false))
        };

        var summary = WorkflowSummaryBuilder.BuildRequirementSummary(requirements);

        Assert.Equal(4, summary.TotalCount);
        Assert.Equal(4, summary.VisibleCount);
        Assert.Equal(3, summary.AnsweredVisibleCount);
        Assert.Equal(1, summary.PendingVisibleCount);
    }

    [Fact]
    public void BuildRequirementSummary_TreatsUnansweredDependenciesAsMissing()
    {
        var requirements = new List<WorkflowRequirementSnapshotDto>
        {
            CreateRequirement(
                1,
                RequirementKeys.AdUserRequested,
                "boolean",
                CreateSelection(valueBoolean: null)),
            CreateRequirement(
                2,
                RequirementKeys.ComparisonUserAvailable,
                "boolean",
                CreateSelection(valueBoolean: null),
                behavior: new RequirementBehaviorDto
                {
                    VisibilityDependencies = new List<RequirementVisibilityDependencyDto>
                    {
                        CreateBooleanTrueDependency(RequirementKeys.AdUserRequested, missingResult: true)
                    },
                    Validation = null,
                    ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>(),
                    SingleSelectReset = null
                }),
            CreateRequirement(
                3,
                RequirementKeys.ComparisonUserName,
                "text",
                CreateSelection(valueText: null),
                behavior: new RequirementBehaviorDto
                {
                    VisibilityDependencies = new List<RequirementVisibilityDependencyDto>
                    {
                        CreateBooleanTrueDependency(RequirementKeys.AdUserRequested, missingResult: false),
                        CreateBooleanTrueDependency(RequirementKeys.ComparisonUserAvailable, missingResult: false)
                    },
                    Validation = null,
                    ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>(),
                    SingleSelectReset = null
                })
        };

        var summary = WorkflowSummaryBuilder.BuildRequirementSummary(requirements);

        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(2, summary.VisibleCount);
        Assert.Equal(0, summary.AnsweredVisibleCount);
        Assert.Equal(2, summary.PendingVisibleCount);
    }

    [Fact]
    public void BuildTaskMetrics_SplitsOverallAndDepartmentPhaseCounts()
    {
        var tasks = new List<WorkflowTaskDto>
        {
            CreateTask(1, "supervisor_fills_document", "done", "HR", 10, isDepartmentPhaseTask: false),
            CreateTask(2, "hardware_procure", "open", "IT", 20, isDepartmentPhaseTask: true),
            CreateTask(3, "hardware_setup", "in_progress", "IT", 30, isDepartmentPhaseTask: true),
            CreateTask(4, "phone_prepare", "cancelled", "IT", 40, isDepartmentPhaseTask: true)
        };

        var summary = WorkflowSummaryBuilder.BuildTaskMetrics(tasks);

        Assert.Equal(4, summary.Overall.TotalCount);
        Assert.Equal(1, summary.Overall.OpenCount);
        Assert.Equal(1, summary.Overall.InProgressCount);
        Assert.Equal(1, summary.Overall.DoneCount);
        Assert.Equal(1, summary.Overall.EndedCount);
        Assert.Equal(2, summary.Overall.ActiveCount);
        Assert.Equal(2, summary.Overall.CompletedCount);

        Assert.Equal(3, summary.DepartmentPhase.TotalCount);
        Assert.Equal(1, summary.DepartmentPhase.OpenCount);
        Assert.Equal(1, summary.DepartmentPhase.InProgressCount);
        Assert.Equal(0, summary.DepartmentPhase.DoneCount);
        Assert.Equal(1, summary.DepartmentPhase.EndedCount);
        Assert.Equal(2, summary.DepartmentPhase.ActiveCount);
        Assert.Equal(1, summary.DepartmentPhase.CompletedCount);
    }

    [Fact]
    public void BuildTaskAreaSummaries_GroupsAreasAndMarksCurrentArea()
    {
        var tasks = new List<WorkflowTaskDto>
        {
            CreateTask(1, "hardware_procure", "open", "IT", 10),
            CreateTask(2, "hardware_setup", "done", "IT", 20),
            CreateTask(3, "supervisor_fills_document", "done", "HR", 30)
        };

        var areas = WorkflowSummaryBuilder.BuildTaskAreaSummaries(tasks);

        var itArea = Assert.Single(areas, area => area.Name == "IT");
        Assert.True(itArea.IsCurrentArea);
        Assert.Equal(2, itArea.Counts.TotalCount);
        Assert.Equal(1, itArea.Counts.OpenCount);
        Assert.Equal(1, itArea.Counts.CompletedCount);

        var hrArea = Assert.Single(areas, area => area.Name == "HR");
        Assert.False(hrArea.IsCurrentArea);
        Assert.Equal(1, hrArea.Counts.TotalCount);
        Assert.Equal(1, hrArea.Counts.CompletedCount);
    }

    private static WorkflowRequirementSnapshotDto CreateRequirement(
        int id,
        string key,
        string inputType,
        WorkflowRequirementValueDto value,
        RequirementBehaviorDto? behavior = null)
    {
        return new WorkflowRequirementSnapshotDto
        {
            WorkflowRequirementId = id,
            Id = id,
            Key = key,
            Title = key,
            Description = key,
            Category = "test",
            IconKey = "test",
            InputType = inputType,
            IsRequired = false,
            SortOrder = id,
            Options = new List<WorkflowRequirementOptionSnapshotDto>(),
            Behavior = behavior ?? new RequirementBehaviorDto
            {
                VisibilityDependencies = new List<RequirementVisibilityDependencyDto>(),
                Validation = null,
                ResetTargetsWhenNotTrue = new List<RequirementResetTargetDto>(),
                SingleSelectReset = null
            },
            Value = value
        };
    }

    private static WorkflowRequirementValueDto CreateSelection(
        bool? valueBoolean = null,
        string? valueText = null)
    {
        return new WorkflowRequirementValueDto
        {
            ValueBoolean = valueBoolean,
            ValueText = valueText,
            ValueNumber = null,
            SelectedOptionId = null,
            SelectedOptionKey = null,
            SelectedOptionValue = null,
            SelectedOptionLabel = null,
            SelectedOptions = new List<WorkflowRequirementSelectedOptionDto>()
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

    private static WorkflowTaskDto CreateTask(
        long id,
        string taskKey,
        string status,
        string processArea,
        int sortOrder,
        bool isDepartmentPhaseTask = false)
    {
        return new WorkflowTaskDto
        {
            Id = id,
            TaskTemplateId = null,
            TaskKey = taskKey,
            Title = taskKey,
            Description = taskKey,
            Category = "test",
            IconKey = "test",
            Status = status,
            IsRequired = true,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow,
            ReadyAt = null,
            StartedAt = null,
            CompletedAt = null,
            CancelledAt = null,
            ProcessArea = processArea,
            IsDepartmentPhaseTask = isDepartmentPhaseTask,
            CanUpdateStatus = false,
            Assignments = new List<WorkflowTaskAssignmentDto>(),
            Dependencies = new List<WorkflowTaskDependencyDto>()
        };
    }
}
