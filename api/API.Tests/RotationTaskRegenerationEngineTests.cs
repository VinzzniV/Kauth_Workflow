using Xunit;

namespace API.Tests;

public sealed class RotationTaskRegenerationEngineTests
{
    [Fact]
    public void Plan_AllNew_ReturnsOnlyCreates()
    {
        var existing = new List<RotationGeneratedTaskRecord>();
        var desired = new List<RotationDesiredTaskRecord>
        {
            CreateDesired(stationId: 1, templateId: 10, title: "Einweisung Tablet"),
            CreateDesired(stationId: 2, templateId: 11, title: "Sicherheitsbriefing")
        };

        var plan = RotationTaskRegenerationEngine.Plan(existing, desired);

        Assert.Equal(2, plan.ToCreate.Count);
        Assert.Empty(plan.ToUpdate);
        Assert.Empty(plan.ToCancel);
        Assert.Equal(0, plan.UnchangedCount);
    }

    [Fact]
    public void Plan_AllMatchingAndUnchanged_ReturnsOnlyUnchanged()
    {
        var existing = new List<RotationGeneratedTaskRecord>
        {
            CreateGenerated(id: 100, stationId: 1, templateId: 10, title: "Einweisung Tablet")
        };
        var desired = new List<RotationDesiredTaskRecord>
        {
            CreateDesired(stationId: 1, templateId: 10, title: "Einweisung Tablet")
        };

        var plan = RotationTaskRegenerationEngine.Plan(existing, desired);

        Assert.Empty(plan.ToCreate);
        Assert.Empty(plan.ToUpdate);
        Assert.Empty(plan.ToCancel);
        Assert.Equal(1, plan.UnchangedCount);
    }

    [Fact]
    public void Plan_TitleChanged_ReturnsUpdate()
    {
        var existing = new List<RotationGeneratedTaskRecord>
        {
            CreateGenerated(id: 100, stationId: 1, templateId: 10, title: "Alter Titel")
        };
        var desired = new List<RotationDesiredTaskRecord>
        {
            CreateDesired(stationId: 1, templateId: 10, title: "Neuer Titel")
        };

        var plan = RotationTaskRegenerationEngine.Plan(existing, desired);

        Assert.Empty(plan.ToCreate);
        Assert.Single(plan.ToUpdate);
        Assert.Equal(100, plan.ToUpdate[0].Existing.Id);
        Assert.Equal("Neuer Titel", plan.ToUpdate[0].Desired.Title);
        Assert.Empty(plan.ToCancel);
        Assert.Equal(0, plan.UnchangedCount);
    }

    [Fact]
    public void Plan_DesiredRemoved_ReturnsCancelForOpenExisting()
    {
        var existing = new List<RotationGeneratedTaskRecord>
        {
            CreateGenerated(id: 100, stationId: 1, templateId: 10, title: "Veraltet")
        };
        var desired = new List<RotationDesiredTaskRecord>();

        var plan = RotationTaskRegenerationEngine.Plan(existing, desired);

        Assert.Empty(plan.ToCreate);
        Assert.Empty(plan.ToUpdate);
        Assert.Single(plan.ToCancel);
        Assert.Equal(100, plan.ToCancel[0].Id);
        Assert.Equal(0, plan.UnchangedCount);
    }

    [Fact]
    public void Plan_TerminalExisting_NeverUpdatedNorCancelled()
    {
        var existing = new List<RotationGeneratedTaskRecord>
        {
            CreateGenerated(id: 100, stationId: 1, templateId: 10, title: "Erledigt", status: RotationTaskStatuses.Completed)
        };
        var desired = new List<RotationDesiredTaskRecord>
        {
            CreateDesired(stationId: 1, templateId: 10, title: "Geaendert nach Erledigung")
        };

        var plan = RotationTaskRegenerationEngine.Plan(existing, desired);

        Assert.Empty(plan.ToCreate);
        Assert.Empty(plan.ToUpdate);
        Assert.Empty(plan.ToCancel);
        Assert.Equal(1, plan.UnchangedCount);
    }

    [Fact]
    public void Plan_DesiredRemovedButExistingNonOpen_ReturnsUnchangedNotCancel()
    {
        var existing = new List<RotationGeneratedTaskRecord>
        {
            CreateGenerated(id: 100, stationId: 1, templateId: 10, title: "InProgress", status: RotationTaskStatuses.InProgress)
        };
        var desired = new List<RotationDesiredTaskRecord>();

        var plan = RotationTaskRegenerationEngine.Plan(existing, desired);

        Assert.Empty(plan.ToCancel);
        Assert.Equal(1, plan.UnchangedCount);
    }

    [Fact]
    public void Plan_MixedScenario_PartitionsAllFourBuckets()
    {
        var existing = new List<RotationGeneratedTaskRecord>
        {
            CreateGenerated(id: 100, stationId: 1, templateId: 10, title: "Unchanged"),
            CreateGenerated(id: 101, stationId: 2, templateId: 11, title: "Alt"),
            CreateGenerated(id: 102, stationId: 3, templateId: 12, title: "Veraltet"),
            CreateGenerated(id: 103, stationId: 4, templateId: 13, title: "Done", status: RotationTaskStatuses.Completed)
        };
        var desired = new List<RotationDesiredTaskRecord>
        {
            CreateDesired(stationId: 1, templateId: 10, title: "Unchanged"),
            CreateDesired(stationId: 2, templateId: 11, title: "Neu"),
            CreateDesired(stationId: 4, templateId: 13, title: "Ignored fuer terminal"),
            CreateDesired(stationId: 5, templateId: 14, title: "Brandneu")
        };

        var plan = RotationTaskRegenerationEngine.Plan(existing, desired);

        Assert.Single(plan.ToCreate);
        Assert.Equal(5, plan.ToCreate[0].RotationStationId);

        Assert.Single(plan.ToUpdate);
        Assert.Equal(101, plan.ToUpdate[0].Existing.Id);
        Assert.Equal("Neu", plan.ToUpdate[0].Desired.Title);

        Assert.Single(plan.ToCancel);
        Assert.Equal(102, plan.ToCancel[0].Id);

        Assert.Equal(2, plan.UnchangedCount);
    }

    [Fact]
    public void Plan_ExistingWithoutStationOrTemplate_NeverMatched()
    {
        var existing = new List<RotationGeneratedTaskRecord>
        {
            CreateGenerated(id: 200, stationId: null, templateId: 10, title: "Loose")
        };
        var desired = new List<RotationDesiredTaskRecord>
        {
            CreateDesired(stationId: 1, templateId: 10, title: "Sollwert")
        };

        var plan = RotationTaskRegenerationEngine.Plan(existing, desired);

        Assert.Single(plan.ToCreate);
        Assert.Equal(0, plan.UnchangedCount);
    }

    [Fact]
    public void NeedsUpdate_DescriptionNullVsEmpty_TreatedEqual()
    {
        var existing = CreateGenerated(id: 1, stationId: 1, templateId: 10, title: "X", description: null);
        var desired = CreateDesired(stationId: 1, templateId: 10, title: "X", description: "");

        Assert.False(RotationTaskRegenerationEngine.NeedsUpdate(existing, desired));
    }

    [Fact]
    public void NeedsUpdate_TriggerTypeCaseInsensitive()
    {
        var existing = CreateGenerated(id: 1, stationId: 1, templateId: 10, title: "X", triggerType: "ON_START");
        var desired = CreateDesired(stationId: 1, templateId: 10, title: "X", triggerType: "on_start");

        Assert.False(RotationTaskRegenerationEngine.NeedsUpdate(existing, desired));
    }

    [Fact]
    public void NeedsUpdate_ResponsibilityChange_DetectsUpdate()
    {
        var existing = CreateGenerated(id: 1, stationId: 1, templateId: 10, title: "X", responsibilityId: 7);
        var desired = CreateDesired(stationId: 1, templateId: 10, title: "X", responsibilityId: 8);

        Assert.True(RotationTaskRegenerationEngine.NeedsUpdate(existing, desired));
    }

    [Fact]
    public void BuildMatchKey_IsStable()
    {
        Assert.Equal(
            RotationTaskRegenerationEngine.BuildMatchKey(42, 7),
            RotationTaskRegenerationEngine.BuildMatchKey(42, 7));
        Assert.NotEqual(
            RotationTaskRegenerationEngine.BuildMatchKey(42, 7),
            RotationTaskRegenerationEngine.BuildMatchKey(7, 42));
    }

    private static RotationDesiredTaskRecord CreateDesired(
        long stationId,
        int templateId,
        string title,
        string? description = null,
        string triggerType = "on_start",
        string taskType = RotationTaskTypes.Information,
        int? responsibilityId = null,
        DateOnly? dueDate = null)
    {
        return new RotationDesiredTaskRecord
        {
            RotationPlanId = 1,
            RotationStationId = stationId,
            PersonId = 1,
            DepartmentId = 5,
            DepartmentName = "Test-Abteilung",
            OrderIndex = 0,
            TemplateId = templateId,
            TemplateTitle = title,
            TriggerType = triggerType,
            AnchorDate = new DateOnly(2026, 5, 1),
            Title = title,
            Description = description,
            TaskType = taskType,
            ResponsibilityId = responsibilityId,
            ResponsibilityName = null,
            DueDate = dueDate
        };
    }

    private static RotationGeneratedTaskRecord CreateGenerated(
        long id,
        long? stationId,
        int? templateId,
        string title,
        string? description = null,
        string triggerType = "on_start",
        string taskType = RotationTaskTypes.Information,
        int? responsibilityId = null,
        DateOnly? dueDate = null,
        string status = RotationTaskStatuses.Open)
    {
        var now = DateTime.UtcNow;
        return new RotationGeneratedTaskRecord
        {
            Id = id,
            RotationPlanId = 1,
            RotationStationId = stationId,
            PersonId = 1,
            DepartmentId = 5,
            DepartmentName = "Test-Abteilung",
            TemplateId = templateId,
            TemplateTitle = title,
            TriggerType = triggerType,
            AnchorDate = new DateOnly(2026, 5, 1),
            Title = title,
            Description = description,
            TaskType = taskType,
            ResponsibilityId = responsibilityId,
            ResponsibilityName = null,
            DueDate = dueDate,
            Status = status,
            CompletionNote = null,
            StartedAt = null,
            CompletedAt = null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
