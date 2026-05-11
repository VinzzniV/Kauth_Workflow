using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Tests;

public sealed class RotationNotificationServiceTests
{
    [Fact]
    public async Task ExecuteDailySweepAsync_CreatesAndDispatchesRotationNotifications()
    {
        var repository = new StubRotationRepository
        {
            CreatedNotificationCount = 2,
            DispatchTargets =
            [
                CreateTarget(101, "upcoming_change"),
                CreateTarget(102, "overdue")
            ]
        };
        var sender = new StubEmailNotificationSender
        {
            RotationResults =
            [
                new NotificationDispatchResult
                {
                    NotificationId = 101,
                    Status = "sent",
                    Success = true,
                    Attempted = true
                },
                new NotificationDispatchResult
                {
                    NotificationId = 102,
                    Status = "failed",
                    Success = false,
                    Attempted = true,
                    ErrorMessage = "mail error"
                }
            ]
        };
        var service = new RotationNotificationService(
            repository,
            sender,
            new StubSystemEventLogService(),
            NullLogger<RotationNotificationService>.Instance);

        var result = await service.ExecuteDailySweepAsync();

        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), repository.LastSweepDate);
        Assert.True(sender.WasRotationDispatchCalled);
        Assert.Equal(2, repository.AppliedResults.Count);
        Assert.Equal(2, result.Created);
        Assert.Equal(1, result.Dispatched);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task ExecuteDailySweepAsync_ProcessesDispatchableNotificationsInBatches()
    {
        var targets = Enumerable.Range(1, 250)
            .Select(i => CreateTarget(i, "reminder"))
            .ToList();
        var repository = new StubRotationRepository
        {
            CreatedNotificationCount = 0,
            DispatchTargets = targets
        };
        var sender = new StubEmailNotificationSender
        {
            RotationResultBuilder = batch => batch
                .Select(target => new NotificationDispatchResult
                {
                    NotificationId = target.NotificationId,
                    Status = "sent",
                    Success = true,
                    Attempted = true
                })
                .ToList()
        };
        var service = new RotationNotificationService(
            repository,
            sender,
            new StubSystemEventLogService(),
            NullLogger<RotationNotificationService>.Instance);

        var result = await service.ExecuteDailySweepAsync();

        // 250 targets with batch size 200 → 2 dispatch calls (200 + 50); loop exits on the short batch.
        Assert.Equal(2, sender.RotationDispatchCallCount);
        Assert.Equal(new[] { 200, 50 }, repository.GetDispatchableBatchSizes.ToArray());
        Assert.Equal(250, repository.AppliedResults.Count);
        Assert.Equal(250, result.Dispatched);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task ExecuteDailySweepAsync_DoesNotDispatchWhenNoPendingTargetsExist()
    {
        var repository = new StubRotationRepository
        {
            CreatedNotificationCount = 1,
            DispatchTargets = []
        };
        var sender = new StubEmailNotificationSender();
        var service = new RotationNotificationService(
            repository,
            sender,
            new StubSystemEventLogService(),
            NullLogger<RotationNotificationService>.Instance);

        var result = await service.ExecuteDailySweepAsync();

        Assert.False(sender.WasRotationDispatchCalled);
        Assert.Empty(repository.AppliedResults);
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Dispatched);
    }

    private static RotationNotificationDispatchTarget CreateTarget(long notificationId, string notificationType)
    {
        return new RotationNotificationDispatchTarget
        {
            NotificationId = notificationId,
            NotificationType = notificationType,
            RotationPlanId = 77,
            RotationStationId = 12,
            GeneratedTaskId = 9000 + notificationId,
            RecipientUserId = 55,
            TargetName = "Julia Verantwortlich",
            TargetEmail = "julia@example.com",
            Subject = "Rotation",
            Payload = new RotationNotificationPayload
            {
                DedupeKey = $"key-{notificationId}",
                RecipientName = "Julia Verantwortlich",
                PlanTitle = "Rotation Julia",
                SourceWorkflowUid = Guid.NewGuid(),
                PersonId = 7,
                PersonDisplayName = "Anika Sattler",
                CurrentDepartmentName = "HR",
                NextDepartmentName = "IT",
                ChangeDate = new DateOnly(2026, 6, 1),
                LinkPath = "/tasks/my?taskRef=rot%3A9001",
                Tasks =
                [
                    new RotationNotificationTaskMailItem
                    {
                        GeneratedTaskId = 9001,
                        TaskRef = "rot:9001",
                        Title = "Notebook vorbereiten",
                        Status = "open",
                        DueDate = new DateOnly(2026, 5, 30),
                        DepartmentName = "IT"
                    }
                ]
            }
        };
    }

    private sealed class StubEmailNotificationSender : IWorkflowEmailNotificationSender
    {
        public bool WasRotationDispatchCalled { get; private set; }
        public int RotationDispatchCallCount { get; private set; }
        public IReadOnlyList<NotificationDispatchResult> RotationResults { get; set; } = [];
        public Func<IReadOnlyList<RotationNotificationDispatchTarget>, IReadOnlyList<NotificationDispatchResult>>? RotationResultBuilder { get; set; }

        public Task<IReadOnlyList<NotificationDispatchResult>> SendNotificationsAsync(
            Guid workflowUid,
            IReadOnlyList<WorkflowNotificationDispatchTarget> targets,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NotificationDispatchResult>>([]);

        public Task<IReadOnlyList<NotificationDispatchResult>> SendRotationNotificationsAsync(
            IReadOnlyList<RotationNotificationDispatchTarget> targets,
            CancellationToken cancellationToken = default)
        {
            WasRotationDispatchCalled = true;
            RotationDispatchCallCount++;
            var results = RotationResultBuilder is null ? RotationResults : RotationResultBuilder(targets);
            return Task.FromResult(results);
        }
    }

    private sealed class StubRotationRepository : IRotationRepository
    {
        public DateOnly? LastSweepDate { get; private set; }
        public int CreatedNotificationCount { get; set; }
        public List<RotationNotificationDispatchTarget> DispatchTargets { get; set; } = [];
        public List<NotificationDispatchResult> AppliedResults { get; } = [];

        public Task<bool> DepartmentExists(int departmentId) => Task.FromResult(true);
        public Task<bool> ResponsibilityExists(int responsibilityId) => Task.FromResult(true);
        public Task<WorkflowTargetPersonSourceDto?> GetSourceWorkflow(Guid workflowUid) => Task.FromResult<WorkflowTargetPersonSourceDto?>(null);
        public Task<RotationPlanConflictState> GetRotationPlanConflictState(long personId, Guid sourceWorkflowUid) => Task.FromResult(new RotationPlanConflictState());
        public Task<List<RotationPlanListItemDto>> GetRotationPlans(long? personId, IReadOnlyCollection<int>? observableDepartmentIds = null) => Task.FromResult(new List<RotationPlanListItemDto>());
        public Task<RotationPlanDetailDto?> GetRotationPlan(long planId) => Task.FromResult<RotationPlanDetailDto?>(null);
        public Task<RotationPlanDetailDto> CreateRotationPlan(CreateRotationPlanRequest request, long createdByUserId) => throw new NotSupportedException();
        public Task<List<RotationStationDto>> GetRotationStations(long planId) => Task.FromResult(new List<RotationStationDto>());
        public Task<RotationStationDto?> GetRotationStation(long stationId) => Task.FromResult<RotationStationDto?>(null);
        public Task<RotationStationDto?> CreateRotationStation(long planId, RotationStationUpsertRequest request, long actorUserId) => Task.FromResult<RotationStationDto?>(null);
        public Task<RotationStationDto?> UpdateRotationStation(long stationId, RotationStationUpsertRequest request, long actorUserId) => Task.FromResult<RotationStationDto?>(null);
        public Task<bool> DeleteRotationStation(long stationId, long actorUserId) => Task.FromResult(false);
        public Task<List<RotationGeneratedTaskDto>> GetRotationGeneratedTasks(long planId) => Task.FromResult(new List<RotationGeneratedTaskDto>());
        public Task<RotationGeneratedTaskDto?> GetRotationGeneratedTask(long taskId) => Task.FromResult<RotationGeneratedTaskDto?>(null);
        public Task<List<RotationAuditEntryDto>> GetRotationAuditLog(long planId, int limit = 200, int offset = 0) => Task.FromResult(new List<RotationAuditEntryDto>());
        public Task<List<RotationNotificationDto>> GetRotationNotifications(long planId, int limit = 200, int offset = 0) => Task.FromResult(new List<RotationNotificationDto>());
        public Task<List<long>> GetRotationPlanIdsForDepartment(int departmentId) => Task.FromResult(new List<long>());
        public Task<RotationTaskRegenerationResultDto> SynchronizeRotationGeneratedTasks(long planId, long actorUserId, string reason) => Task.FromResult(new RotationTaskRegenerationResultDto { Created = 0, Updated = 0, Cancelled = 0, Unchanged = 0 });

        public Task<int> CreateDueRotationNotifications(DateOnly asOfDate)
        {
            LastSweepDate = asOfDate;
            return Task.FromResult(CreatedNotificationCount);
        }

        public List<int> GetDispatchableBatchSizes { get; } = [];

        public Task<List<RotationNotificationDispatchTarget>> GetDispatchableRotationNotifications(
            int? limit = null,
            IReadOnlyCollection<long>? excludeNotificationIds = null)
        {
            var exclude = excludeNotificationIds is null
                ? new HashSet<long>()
                : new HashSet<long>(excludeNotificationIds);
            var filtered = DispatchTargets.Where(target => !exclude.Contains(target.NotificationId));
            if (limit is int limitValue && limitValue > 0)
            {
                filtered = filtered.Take(limitValue);
            }
            var result = filtered.ToList();
            GetDispatchableBatchSizes.Add(result.Count);
            return Task.FromResult(result);
        }

        public Task ApplyRotationNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results)
        {
            AppliedResults.AddRange(results);
            return Task.CompletedTask;
        }

        public Task<List<DepartmentActionTemplateDto>> GetDepartmentActionTemplates(int? departmentId, bool? isActive = null) => Task.FromResult(new List<DepartmentActionTemplateDto>());
        public Task<DepartmentActionTemplateDto?> GetDepartmentActionTemplate(int templateId) => Task.FromResult<DepartmentActionTemplateDto?>(null);
        public Task<DepartmentActionTemplateDto> CreateDepartmentActionTemplate(DepartmentActionTemplateUpsertRequest request) => throw new NotSupportedException();
        public Task<DepartmentActionTemplateDto?> UpdateDepartmentActionTemplate(int templateId, DepartmentActionTemplateUpsertRequest request) => Task.FromResult<DepartmentActionTemplateDto?>(null);
        public Task<bool> DeleteDepartmentActionTemplate(int templateId) => Task.FromResult(false);
        public Task<List<TaskWithWorkflowDto>> GetAllRotationTaskEnvelopes() => Task.FromResult(new List<TaskWithWorkflowDto>());
        public Task<List<TaskWithWorkflowDto>> GetRotationTaskEnvelopesForUser(long userId, int[] effectiveResponsibilityIds) => Task.FromResult(new List<TaskWithWorkflowDto>());
        public Task<TaskWithWorkflowDto?> GetRotationTaskEnvelope(long taskId) => Task.FromResult<TaskWithWorkflowDto?>(null);
        public Task<TaskWithWorkflowDto?> GetRotationTaskEnvelopeByRef(string taskRef) => Task.FromResult<TaskWithWorkflowDto?>(null);
        public Task<TaskWithWorkflowDto?> UpdateRotationTaskStatusByRef(string taskRef, string status, long actorUserId) => Task.FromResult<TaskWithWorkflowDto?>(null);
        public Task<TaskWithWorkflowDto?> UpdateRotationTaskAssignmentByRef(string taskRef, TaskAssignRequest request, long actorUserId) => Task.FromResult<TaskWithWorkflowDto?>(null);
        public Task<TaskWithWorkflowDto?> AddRotationTaskCommentByRef(string taskRef, string commentText, long actorUserId) => Task.FromResult<TaskWithWorkflowDto?>(null);
        public Task<TaskWithWorkflowDto?> DecideRotationTaskApprovalByRef(string taskRef, TaskApprovalDecisionRequest request, long actorUserId) => Task.FromResult<TaskWithWorkflowDto?>(null);
    }

    private sealed class StubSystemEventLogService : ISystemEventLogService
    {
        public Task<CursorPageDto<AdminSystemLogEntryDto>> GetAdminLogsAsync(
            SystemEventLogQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CursorPageDto<AdminSystemLogEntryDto> { Items = [], HasMore = false, NextCursor = null });

        public Task<AdminSystemLogSummaryDto> GetAdminLogSummaryAsync(
            SystemEventLogQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminSystemLogSummaryDto
            {
                TotalCount = 0,
                InfoCount = 0,
                WarningCount = 0,
                ErrorCount = 0,
                Sources = []
            });

        public Task WriteAsync(SystemEventLogWriteModel model, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
