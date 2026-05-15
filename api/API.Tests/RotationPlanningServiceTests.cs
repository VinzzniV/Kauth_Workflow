using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Tests;

public sealed class RotationPlanningServiceTests
{
    [Fact]
    public async Task CreateRotationPlanAsync_RejectsWhenSourceWorkflowBelongsToDifferentPerson()
    {
        var repository = new StubRotationRepository
        {
            SourceWorkflow = CreateSource(personId: 10, departmentId: 2)
        };
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRotationPlanAsync(
            new CreateRotationPlanRequest
            {
                PersonId = 11,
                SourceWorkflowUid = repository.SourceWorkflow!.WorkflowUid,
                Status = "draft"
            },
            CreateUser()));

        Assert.Contains("dieselbe Person", ex.Message);
    }

    [Fact]
    public async Task CreateRotationPlanAsync_RejectsActiveStatusOnCreate()
    {
        // Z21-S7: Neue Plaene starten immer als Entwurf. Direkt-aktivieren ist
        // unterbunden, weil ein Plan ohne Stationen fachlich nicht aktiv sein darf.
        var repository = new StubRotationRepository
        {
            SourceWorkflow = CreateSource(personId: 10, departmentId: 2)
        };
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRotationPlanAsync(
            new CreateRotationPlanRequest
            {
                PersonId = 10,
                SourceWorkflowUid = repository.SourceWorkflow!.WorkflowUid,
                Status = "active"
            },
            CreateUser()));

        Assert.Contains("Entwurf", ex.Message);
    }

    // Z21-S7: Activate-Pfad-Tests

    [Fact]
    public async Task ActivateRotationPlanAsync_ReturnsNoStations_WhenPlanHasNoStations()
    {
        var repository = new StubRotationRepository
        {
            Plan = CreatePlanWithStations()
        };
        var service = CreateService(repository);

        var result = await service.ActivateRotationPlanAsync(42, CreateUser());

        Assert.Equal(RotationPlanActivationResult.ResultKind.NoStations, result.Kind);
        Assert.Equal(0, repository.ActivateCallCount);
    }

    [Fact]
    public async Task ActivateRotationPlanAsync_ReturnsPersonHasActivePlan_WhenConflictExists()
    {
        var repository = new StubRotationRepository
        {
            Plan = CreatePlanWithStations(
                new RotationStationDto
                {
                    Id = 1,
                    RotationPlanId = 42,
                    DepartmentId = 3,
                    DepartmentName = "IT",
                    StartDate = new DateOnly(2026, 6, 1),
                    EndDate = new DateOnly(2026, 6, 20),
                    OrderIndex = 0,
                    Status = "planned",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }),
            ConflictState = new RotationPlanConflictState
            {
                HasActivePlanForPerson = true
            }
        };
        var service = CreateService(repository);

        var result = await service.ActivateRotationPlanAsync(42, CreateUser());

        Assert.Equal(RotationPlanActivationResult.ResultKind.PersonHasActivePlan, result.Kind);
        Assert.Equal(0, repository.ActivateCallCount);
    }

    [Fact]
    public async Task ActivateRotationPlanAsync_ReturnsInvalidStatus_WhenPlanIsNotDraft()
    {
        var basePlan = CreatePlanWithStations(
            new RotationStationDto
            {
                Id = 1,
                RotationPlanId = 42,
                DepartmentId = 3,
                DepartmentName = "IT",
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 6, 20),
                OrderIndex = 0,
                Status = "planned",
                CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
        var plan = new RotationPlanDetailDto
        {
            Id = basePlan.Id,
            PersonId = basePlan.PersonId,
            SourceWorkflowUid = basePlan.SourceWorkflowUid,
            DisplayName = basePlan.DisplayName,
            FirstName = basePlan.FirstName,
            LastName = basePlan.LastName,
            DepartmentId = basePlan.DepartmentId,
            DepartmentName = basePlan.DepartmentName,
            Title = basePlan.Title,
            Status = "active",
            CreatedAt = basePlan.CreatedAt,
            UpdatedAt = basePlan.UpdatedAt,
            Stations = basePlan.Stations
        };
        var repository = new StubRotationRepository { Plan = plan };
        var service = CreateService(repository);

        var result = await service.ActivateRotationPlanAsync(42, CreateUser());

        Assert.Equal(RotationPlanActivationResult.ResultKind.InvalidStatus, result.Kind);
        Assert.Equal("active", result.CurrentStatus);
        Assert.Equal(0, repository.ActivateCallCount);
    }

    [Fact]
    public async Task ActivateRotationPlanAsync_Activates_WhenStationsExistAndNoConflict()
    {
        var repository = new StubRotationRepository
        {
            Plan = CreatePlanWithStations(
                new RotationStationDto
                {
                    Id = 1,
                    RotationPlanId = 42,
                    DepartmentId = 3,
                    DepartmentName = "IT",
                    StartDate = new DateOnly(2026, 6, 1),
                    EndDate = new DateOnly(2026, 6, 20),
                    OrderIndex = 0,
                    Status = "planned",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                })
        };
        var service = CreateService(repository);

        var result = await service.ActivateRotationPlanAsync(42, CreateUser());

        Assert.Equal(RotationPlanActivationResult.ResultKind.Activated, result.Kind);
        Assert.Equal(1, repository.ActivateCallCount);
    }

    [Fact]
    public async Task CreateRotationStationAsync_RejectsOverlappingDateRange()
    {
        var repository = new StubRotationRepository
        {
            Plan = CreatePlanWithStations(
                new RotationStationDto
                {
                    Id = 7,
                    RotationPlanId = 42,
                    DepartmentId = 3,
                    DepartmentName = "IT",
                    StartDate = new DateOnly(2026, 6, 1),
                    EndDate = new DateOnly(2026, 6, 20),
                    OrderIndex = 0,
                    Status = "planned",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                })
        };
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRotationStationAsync(
            42,
            new RotationStationUpsertRequest
            {
                DepartmentId = 4,
                StartDate = new DateOnly(2026, 6, 15),
                EndDate = new DateOnly(2026, 7, 1),
                OrderIndex = 1,
                Status = "planned"
            },
            CreateUser()));

        Assert.Contains("überschneidet", ex.Message);
    }

    [Fact]
    public async Task UpdateRotationStationAsync_RejectsDuplicateOrderIndex()
    {
        var repository = new StubRotationRepository
        {
            Plan = CreatePlanWithStations(
                new RotationStationDto
                {
                    Id = 7,
                    RotationPlanId = 42,
                    DepartmentId = 3,
                    DepartmentName = "IT",
                    StartDate = new DateOnly(2026, 6, 1),
                    EndDate = new DateOnly(2026, 6, 20),
                    OrderIndex = 0,
                    Status = "planned",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new RotationStationDto
                {
                    Id = 8,
                    RotationPlanId = 42,
                    DepartmentId = 4,
                    DepartmentName = "QS",
                    StartDate = new DateOnly(2026, 6, 21),
                    EndDate = new DateOnly(2026, 7, 1),
                    OrderIndex = 1,
                    Status = "planned",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }),
            Station = new RotationStationDto
            {
                Id = 8,
                RotationPlanId = 42,
                DepartmentId = 4,
                DepartmentName = "QS",
                StartDate = new DateOnly(2026, 6, 21),
                EndDate = new DateOnly(2026, 7, 1),
                OrderIndex = 1,
                Status = "planned",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateRotationStationAsync(
            8,
            new RotationStationUpsertRequest
            {
                DepartmentId = 4,
                StartDate = new DateOnly(2026, 6, 21),
                EndDate = new DateOnly(2026, 7, 1),
                OrderIndex = 0,
                Status = "planned"
            },
            CreateUser()));

        Assert.Contains("orderIndex", ex.Message);
    }

    [Fact]
    public async Task CreateRotationPlanAsync_TriggersTaskRegeneration()
    {
        var repository = new StubRotationRepository
        {
            SourceWorkflow = CreateSource(personId: 10, departmentId: 2),
            Plan = CreatePlanWithStations()
        };
        var generationService = new StubRotationTaskGenerationService();
        var service = CreateService(repository, generationService);

        var result = await service.CreateRotationPlanAsync(
            new CreateRotationPlanRequest
            {
                PersonId = 10,
                SourceWorkflowUid = repository.SourceWorkflow!.WorkflowUid,
                Status = "draft"
            },
            CreateUser());

        Assert.Equal(result.Id, generationService.LastPlanId);
        Assert.Equal("plan_created", generationService.LastReason);
    }

    [Fact]
    public async Task GetRotationAuditLogAsync_ReturnsEntriesForVisiblePlan()
    {
        var repository = new StubRotationRepository
        {
            Plan = CreatePlanWithStations(),
            AuditEntries =
            [
                new RotationAuditEntryDto
                {
                    Id = 1,
                    RotationPlanId = 42,
                    RotationStationId = null,
                    GeneratedTaskId = null,
                    ActorUserId = 99,
                    ActorUserName = "HR",
                    EventType = "rotation_plan_created",
                    Detail = "Plan erstellt",
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
        var service = CreateService(repository);

        var result = await service.GetRotationAuditLogAsync(42, 50, 0, CreateUser());

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal("rotation_plan_created", result[0].EventType);
    }

    [Fact]
    public async Task GetRotationNotificationsAsync_ReturnsNullWhenPlanDoesNotExist()
    {
        var repository = new StubRotationRepository
        {
            Plan = null
        };
        var service = CreateService(repository);

        var result = await service.GetRotationNotificationsAsync(42, 50, 0, CreateUser());

        Assert.Null(result);
    }

    private static RotationPlanningService CreateService(
        StubRotationRepository repository,
        StubRotationTaskGenerationService? generationService = null)
    {
        return new RotationPlanningService(
            repository,
            generationService ?? new StubRotationTaskGenerationService(),
            new StubWorkflowVisibilityService(),
            NullLogger<RotationPlanningService>.Instance);
    }

    private static CurrentUser CreateUser()
    {
        return new CurrentUser
        {
            UserId = 99,
            DisplayName = "HR",
            Email = "hr@example.com",
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

    private static WorkflowTargetPersonSourceDto CreateSource(long personId, int? departmentId)
    {
        return new WorkflowTargetPersonSourceDto
        {
            WorkflowUid = Guid.NewGuid(),
            PersonId = personId,
            DisplayName = "Anika Sattler",
            FirstName = "Anika",
            LastName = "Sattler",
            DepartmentId = departmentId,
            DepartmentName = "IT",
            RoleId = 5,
            RoleName = "Studentin",
            EmployeeNumber = 12345,
            BadgeNumber = 99,
            CompletedAt = DateTime.UtcNow
        };
    }

    private static RotationPlanDetailDto CreatePlanWithStations(params RotationStationDto[] stations)
    {
        return new RotationPlanDetailDto
        {
            Id = 42,
            PersonId = 10,
            SourceWorkflowUid = Guid.NewGuid(),
            DisplayName = "Anika Sattler",
            FirstName = "Anika",
            LastName = "Sattler",
            DepartmentId = 2,
            DepartmentName = "HR",
            Title = "Anika - Durchlauf",
            Status = "draft",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Stations = stations.ToList()
        };
    }

    private sealed class StubRotationRepository : IRotationRepository
    {
        public WorkflowTargetPersonSourceDto? SourceWorkflow { get; set; }
        public RotationPlanConflictState ConflictState { get; set; } = new();
        public RotationPlanDetailDto? Plan { get; set; }
        public RotationStationDto? Station { get; set; }
        public List<RotationAuditEntryDto> AuditEntries { get; set; } = [];
        public List<RotationNotificationDto> Notifications { get; set; } = [];

        public Task<bool> DepartmentExists(int departmentId)
            => Task.FromResult(true);

        public Task<bool> ResponsibilityExists(int responsibilityId)
            => Task.FromResult(true);

        public Task<WorkflowTargetPersonSourceDto?> GetSourceWorkflow(Guid workflowUid)
            => Task.FromResult(SourceWorkflow);

        public Task<RotationPlanConflictState> GetRotationPlanConflictState(long personId, Guid sourceWorkflowUid)
            => Task.FromResult(ConflictState);

        public Task<List<RotationPlanListItemDto>> GetRotationPlans(long? personId, IReadOnlyCollection<int>? observableDepartmentIds = null)
            => Task.FromResult(new List<RotationPlanListItemDto>());

        public Task<RotationPlanDetailDto?> GetRotationPlan(long planId)
            => Task.FromResult(Plan);

        public Task<RotationPlanDetailDto> CreateRotationPlan(CreateRotationPlanRequest request, long createdByUserId)
            => Task.FromResult(Plan ?? throw new NotSupportedException());

        public int ActivateCallCount { get; private set; }
        public RotationPlanDetailDto? ActivatedPlan { get; set; }

        public Task<RotationPlanDetailDto?> ActivateRotationPlan(long planId, long actorUserId)
        {
            ActivateCallCount++;
            return Task.FromResult(ActivatedPlan ?? Plan);
        }

        public Task<List<RotationStationDto>> GetRotationStations(long planId)
            => Task.FromResult(Plan?.Stations ?? new List<RotationStationDto>());

        public Task<RotationStationDto?> GetRotationStation(long stationId)
            => Task.FromResult(Station);

        public Task<RotationStationDto?> CreateRotationStation(long planId, RotationStationUpsertRequest request, long actorUserId)
            => Task.FromResult<RotationStationDto?>(null);

        public Task<RotationStationDto?> UpdateRotationStation(long stationId, RotationStationUpsertRequest request, long actorUserId)
            => Task.FromResult<RotationStationDto?>(null);

        public Task<bool> DeleteRotationStation(long stationId, long actorUserId)
            => Task.FromResult(true);

        public Task<List<RotationGeneratedTaskDto>> GetRotationGeneratedTasks(long planId)
            => Task.FromResult(new List<RotationGeneratedTaskDto>());

        public Task<RotationGeneratedTaskDto?> GetRotationGeneratedTask(long taskId)
            => Task.FromResult<RotationGeneratedTaskDto?>(null);

        public Task<List<RotationAuditEntryDto>> GetRotationAuditLog(long planId, int limit = 200, int offset = 0)
            => Task.FromResult(AuditEntries.Skip(offset).Take(limit).ToList());

        public Task<List<RotationNotificationDto>> GetRotationNotifications(long planId, int limit = 200, int offset = 0)
            => Task.FromResult(Notifications.Skip(offset).Take(limit).ToList());

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

        public Task<int> CreateDueRotationNotifications(DateOnly asOfDate)
            => Task.FromResult(0);

        public Task<List<RotationNotificationDispatchTarget>> GetDispatchableRotationNotifications(
            int? limit = null,
            IReadOnlyCollection<long>? excludeNotificationIds = null)
            => Task.FromResult(new List<RotationNotificationDispatchTarget>());

        public Task ApplyRotationNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results)
            => Task.CompletedTask;

        public Task<List<DepartmentActionTemplateDto>> GetDepartmentActionTemplates(int? departmentId, bool? isActive = null)
            => Task.FromResult(new List<DepartmentActionTemplateDto>());

        public Task<DepartmentActionTemplateDto?> GetDepartmentActionTemplate(int templateId)
            => Task.FromResult<DepartmentActionTemplateDto?>(null);

        public Task<DepartmentActionTemplateDto> CreateDepartmentActionTemplate(DepartmentActionTemplateUpsertRequest request)
            => throw new NotSupportedException();

        public Task<DepartmentActionTemplateDto?> UpdateDepartmentActionTemplate(int templateId, DepartmentActionTemplateUpsertRequest request)
            => Task.FromResult<DepartmentActionTemplateDto?>(null);

        public Task<bool> DeleteDepartmentActionTemplate(int templateId)
            => Task.FromResult(true);

        public Task<List<TaskWithWorkflowDto>> GetAllRotationTaskEnvelopes()
            => Task.FromResult(new List<TaskWithWorkflowDto>());

        public Task<List<TaskWithWorkflowDto>> GetRotationTaskEnvelopesForUser(long userId, int[] effectiveResponsibilityIds)
            => Task.FromResult(new List<TaskWithWorkflowDto>());

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
        public long? LastPlanId { get; private set; }
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
        {
            LastPlanId = planId;
            LastReason = reason;
            return Task.FromResult<RotationTaskRegenerationResultDto?>(new RotationTaskRegenerationResultDto
            {
                Created = 0,
                Updated = 0,
                Cancelled = 0,
                Unchanged = 0
            });
        }

        public Task RegenerateDepartmentPlansAsync(int departmentId, CurrentUser currentUser, string reason, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubWorkflowVisibilityService : IWorkflowVisibilityService
    {
        public Task<HashSet<int>?> GetObservableWorkflowDepartmentIds(CurrentUser currentUser)
            => Task.FromResult<HashSet<int>?>(null);

        public bool CanObserveWorkflow(CurrentUser currentUser, int workflowDepartmentId, string workflowStatus, HashSet<int>? observableDepartmentIds)
            => true;

        public void ApplyWorkflowTaskPermissions(WorkflowDetailDto workflow, CurrentUser currentUser)
            => throw new NotSupportedException();

        public void ApplyTaskPermissions(IEnumerable<TaskWithWorkflowDto> tasks, CurrentUser currentUser)
            => throw new NotSupportedException();

        public void ApplyTaskPermissions(TaskWithWorkflowDto task, CurrentUser currentUser)
            => throw new NotSupportedException();
    }
}
