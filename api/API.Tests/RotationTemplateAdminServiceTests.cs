using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Tests;

public sealed class RotationTemplateAdminServiceTests
{
    [Fact]
    public async Task CreateDepartmentActionTemplateAsync_RejectsUnknownTriggerType()
    {
        var repository = new StubRotationRepository();
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDepartmentActionTemplateAsync(
            CreateRequest(triggerType: "start"),
            CreateUser()));

        Assert.Contains("triggerType", ex.Message);
    }

    [Fact]
    public async Task CreateDepartmentActionTemplateAsync_RejectsImplausibleDueOffset()
    {
        var repository = new StubRotationRepository();
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDepartmentActionTemplateAsync(
            CreateRequest(dueOffsetDays: 366),
            CreateUser()));

        Assert.Contains("dueOffsetDays", ex.Message);
    }

    [Fact]
    public async Task CreateDepartmentActionTemplateAsync_RejectsUnknownDepartment()
    {
        var repository = new StubRotationRepository
        {
            DepartmentExistsResult = false
        };
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDepartmentActionTemplateAsync(
            CreateRequest(),
            CreateUser()));

        Assert.Contains("Abteilung", ex.Message);
    }

    [Fact]
    public async Task CreateDepartmentActionTemplateAsync_RejectsUnknownResponsibility()
    {
        var repository = new StubRotationRepository
        {
            ResponsibilityExistsResult = false
        };
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDepartmentActionTemplateAsync(
            CreateRequest(defaultResponsibilityId: 91),
            CreateUser()));

        Assert.Contains("Verantwortlichkeit", ex.Message);
    }

    [Fact]
    public async Task CreateDepartmentActionTemplateAsync_NormalizesValuesBeforePersisting()
    {
        var repository = new StubRotationRepository();
        var service = CreateService(repository);

        var created = await service.CreateDepartmentActionTemplateAsync(
            CreateRequest(
                triggerType: " ENTER ",
                taskType: " technical ",
                title: "  Ordnerrechte Einkauf setzen  ",
                description: "  Beschreibung  ",
                isAutomatable: true,
                automationKey: "  folder-rights  "),
            CreateUser());

        Assert.Equal("enter", created.TriggerType);
        Assert.Equal("technical", created.TaskType);
        Assert.Equal("Ordnerrechte Einkauf setzen", created.Title);
        Assert.Equal("Beschreibung", created.Description);
        Assert.Equal("folder-rights", created.AutomationKey);
    }

    [Fact]
    public async Task UpdateDepartmentActionTemplateAsync_ReturnsNullWhenTemplateDoesNotExist()
    {
        var repository = new StubRotationRepository
        {
            CurrentTemplate = null
        };
        var service = CreateService(repository);

        var result = await service.UpdateDepartmentActionTemplateAsync(42, CreateRequest(), CreateUser());

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateDepartmentActionTemplateAsync_TriggersDepartmentRegeneration()
    {
        var repository = new StubRotationRepository();
        var generationService = new StubRotationTaskGenerationService();
        var service = CreateService(repository, generationService);

        var created = await service.CreateDepartmentActionTemplateAsync(CreateRequest(), CreateUser());

        Assert.Equal(created.DepartmentId, generationService.LastDepartmentId);
        Assert.Equal("template_created", generationService.LastReason);
    }

    private static RotationTemplateAdminService CreateService(
        StubRotationRepository repository,
        StubRotationTaskGenerationService? generationService = null)
    {
        return new RotationTemplateAdminService(
            repository,
            generationService ?? new StubRotationTaskGenerationService(),
            NullLogger<RotationTemplateAdminService>.Instance);
    }

    private static CurrentUser CreateUser()
    {
        return new CurrentUser
        {
            UserId = 99,
            DisplayName = "Admin",
            Email = "admin@example.com",
            IsActive = true,
            IdentityProvider = "dev",
            Groups = [],
            DirectRoles = [],
            GroupRoles = [],
            EffectiveRoles = [],
            DirectResponsibilities = [],
            GroupResponsibilities = [],
            EffectiveResponsibilities = []
        };
    }

    private static DepartmentActionTemplateUpsertRequest CreateRequest(
        int departmentId = 3,
        string triggerType = "enter",
        string title = "Ordnerrechte Einkauf setzen",
        string? description = null,
        string taskType = "manual",
        int? defaultResponsibilityId = null,
        int dueOffsetDays = -2,
        int? reminderOffsetDays = 1,
        bool isAutomatable = false,
        string? automationKey = null,
        bool isActive = true)
    {
        return new DepartmentActionTemplateUpsertRequest
        {
            DepartmentId = departmentId,
            TriggerType = triggerType,
            Title = title,
            Description = description,
            TaskType = taskType,
            DefaultResponsibilityId = defaultResponsibilityId,
            DueOffsetDays = dueOffsetDays,
            ReminderOffsetDays = reminderOffsetDays,
            IsAutomatable = isAutomatable,
            AutomationKey = automationKey,
            IsActive = isActive
        };
    }

    private sealed class StubRotationRepository : IRotationRepository
    {
        public bool DepartmentExistsResult { get; set; } = true;
        public bool ResponsibilityExistsResult { get; set; } = true;
        public DepartmentActionTemplateDto? CurrentTemplate { get; set; } = new()
        {
            Id = 42,
            DepartmentId = 3,
            DepartmentName = "Einkauf",
            TriggerType = "enter",
            Title = "Bestehende Vorlage",
            TaskType = "manual",
            DueOffsetDays = 0,
            IsAutomatable = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        public Task<bool> DepartmentExists(int departmentId)
            => Task.FromResult(DepartmentExistsResult);

        public Task<bool> ResponsibilityExists(int responsibilityId)
            => Task.FromResult(ResponsibilityExistsResult);

        public Task<WorkflowTargetPersonSourceDto?> GetCompletedOnboardingSource(Guid workflowUid)
            => Task.FromResult<WorkflowTargetPersonSourceDto?>(null);

        public Task<RotationPlanConflictState> GetRotationPlanConflictState(long personId, Guid sourceWorkflowUid)
            => Task.FromResult(new RotationPlanConflictState());

        public Task<List<RotationPlanListItemDto>> GetRotationPlans(long? personId, IReadOnlyCollection<int>? observableDepartmentIds = null)
            => Task.FromResult(new List<RotationPlanListItemDto>());

        public Task<RotationPlanDetailDto?> GetRotationPlan(long planId)
            => Task.FromResult<RotationPlanDetailDto?>(null);

        public Task<RotationPlanDetailDto> CreateRotationPlan(CreateRotationPlanRequest request, long createdByUserId)
            => throw new NotSupportedException();

        public Task<List<RotationStationDto>> GetRotationStations(long planId)
            => Task.FromResult(new List<RotationStationDto>());

        public Task<RotationStationDto?> GetRotationStation(long stationId)
            => Task.FromResult<RotationStationDto?>(null);

        public Task<RotationStationDto?> CreateRotationStation(long planId, RotationStationUpsertRequest request)
            => Task.FromResult<RotationStationDto?>(null);

        public Task<RotationStationDto?> UpdateRotationStation(long stationId, RotationStationUpsertRequest request)
            => Task.FromResult<RotationStationDto?>(null);

        public Task<bool> DeleteRotationStation(long stationId)
            => Task.FromResult(true);

        public Task<List<RotationGeneratedTaskDto>> GetRotationGeneratedTasks(long planId)
            => Task.FromResult(new List<RotationGeneratedTaskDto>());

        public Task<RotationGeneratedTaskDto?> GetRotationGeneratedTask(long taskId)
            => Task.FromResult<RotationGeneratedTaskDto?>(null);

        public Task<List<long>> GetRotationPlanIdsForDepartment(int departmentId)
            => Task.FromResult(new List<long>());

        public Task<RotationTaskRegenerationResultDto> SynchronizeRotationGeneratedTasks(long planId, long actorUserId, string reason)
            => Task.FromResult(new RotationTaskRegenerationResultDto
            {
                Created = 0,
                Updated = 0,
                Cancelled = 0,
                Unchanged = 0
            });

        public Task<List<DepartmentActionTemplateDto>> GetDepartmentActionTemplates(int? departmentId, bool? isActive = null)
            => Task.FromResult(new List<DepartmentActionTemplateDto>());

        public Task<DepartmentActionTemplateDto?> GetDepartmentActionTemplate(int templateId)
            => Task.FromResult(CurrentTemplate);

        public Task<DepartmentActionTemplateDto> CreateDepartmentActionTemplate(DepartmentActionTemplateUpsertRequest request)
        {
            return Task.FromResult(new DepartmentActionTemplateDto
            {
                Id = 77,
                DepartmentId = request.DepartmentId,
                DepartmentName = "Einkauf",
                TriggerType = request.TriggerType,
                Title = request.Title,
                Description = request.Description,
                TaskType = request.TaskType,
                DefaultResponsibilityId = request.DefaultResponsibilityId,
                DefaultResponsibilityName = request.DefaultResponsibilityId.HasValue ? "AD" : null,
                DueOffsetDays = request.DueOffsetDays,
                ReminderOffsetDays = request.ReminderOffsetDays,
                IsAutomatable = request.IsAutomatable,
                AutomationKey = request.AutomationKey,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        public Task<DepartmentActionTemplateDto?> UpdateDepartmentActionTemplate(int templateId, DepartmentActionTemplateUpsertRequest request)
        {
            if (CurrentTemplate is null)
            {
                return Task.FromResult<DepartmentActionTemplateDto?>(null);
            }

            return Task.FromResult<DepartmentActionTemplateDto?>(new DepartmentActionTemplateDto
            {
                Id = templateId,
                DepartmentId = request.DepartmentId,
                DepartmentName = "Einkauf",
                TriggerType = request.TriggerType,
                Title = request.Title,
                Description = request.Description,
                TaskType = request.TaskType,
                DefaultResponsibilityId = request.DefaultResponsibilityId,
                DefaultResponsibilityName = request.DefaultResponsibilityId.HasValue ? "AD" : null,
                DueOffsetDays = request.DueOffsetDays,
                ReminderOffsetDays = request.ReminderOffsetDays,
                IsAutomatable = request.IsAutomatable,
                AutomationKey = request.AutomationKey,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        public Task<bool> DeleteDepartmentActionTemplate(int templateId)
            => Task.FromResult(true);

        public Task<TaskWithWorkflowDto?> GetRotationTaskEnvelope(long taskId)
            => Task.FromResult<TaskWithWorkflowDto?>(null);

        public Task<TaskWithWorkflowDto?> GetRotationTaskEnvelopeByRef(string taskRef)
            => Task.FromResult<TaskWithWorkflowDto?>(null);

        public Task<TaskWithWorkflowDto?> UpdateRotationTaskStatusByRef(string taskRef, string status, long actorUserId)
            => Task.FromResult<TaskWithWorkflowDto?>(null);

        public Task<TaskWithWorkflowDto?> UpdateRotationTaskAssignmentByRef(string taskRef, TaskAssignRequest request, long actorUserId)
            => Task.FromResult<TaskWithWorkflowDto?>(null);

        public Task<TaskWithWorkflowDto?> AddRotationTaskCommentByRef(string taskRef, string commentText, long actorUserId)
            => Task.FromResult<TaskWithWorkflowDto?>(null);

        public Task<TaskWithWorkflowDto?> DecideRotationTaskApprovalByRef(string taskRef, TaskApprovalDecisionRequest request, long actorUserId)
            => Task.FromResult<TaskWithWorkflowDto?>(null);
    }

    private sealed class StubRotationTaskGenerationService : IRotationTaskGenerationService
    {
        public int? LastDepartmentId { get; private set; }
        public string? LastReason { get; private set; }

        public Task<IReadOnlyList<RotationGeneratedTaskDto>?> GetGeneratedTasksAsync(long planId, CurrentUser currentUser, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RotationGeneratedTaskDto>?>([]);

        public Task<RotationTaskRegenerationResultDto?> RegeneratePlanTasksAsync(long planId, CurrentUser currentUser, string reason = "manual_regenerate", CancellationToken cancellationToken = default)
            => Task.FromResult<RotationTaskRegenerationResultDto?>(new RotationTaskRegenerationResultDto
            {
                Created = 0,
                Updated = 0,
                Cancelled = 0,
                Unchanged = 0
            });

        public Task<RotationTaskRegenerationResultDto?> RegeneratePlanTasksUncheckedAsync(long planId, CurrentUser currentUser, string reason, CancellationToken cancellationToken = default)
            => Task.FromResult<RotationTaskRegenerationResultDto?>(new RotationTaskRegenerationResultDto
            {
                Created = 0,
                Updated = 0,
                Cancelled = 0,
                Unchanged = 0
            });

        public Task RegenerateDepartmentPlansAsync(int departmentId, CurrentUser currentUser, string reason, CancellationToken cancellationToken = default)
        {
            LastDepartmentId = departmentId;
            LastReason = reason;
            return Task.CompletedTask;
        }
    }
}
