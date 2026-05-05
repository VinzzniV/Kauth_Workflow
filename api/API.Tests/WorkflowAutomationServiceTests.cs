using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Tests;

public sealed class WorkflowAutomationServiceTests
{
    [Fact]
    public async Task TryProcessNextPendingJobAsync_ReturnsFalseWhenNoJobExists()
    {
        var repository = new StubWorkflowAutomationRepository();
        var service = new WorkflowAutomationService(
            repository,
            repository,
            new StubWorkflowAutomationHandlerRegistry(_ => throw new InvalidOperationException("no handler")),
            new StubSystemEventLogService(),
            new StubWorkflowLifecycleService(),
            new WorkflowAutomationRetrySettings(),
            NullLogger<WorkflowAutomationService>.Instance);

        var processed = await service.TryProcessNextPendingJobAsync();

        Assert.False(processed);
    }

    [Fact]
    public async Task TryProcessNextPendingJobAsync_SchedulesRetryForFirstIdempotentFailure()
    {
        var repository = new StubWorkflowAutomationRepository
        {
            ClaimedJob = new ClaimedAutomationJobRecord
            {
                JobId = 1,
                WorkflowId = 2,
                WorkflowUid = Guid.NewGuid(),
                WorkflowNodeInstanceId = 3,
                WorkflowNodeId = 4,
                NodeKey = "auto",
                NodeType = "automation",
                WorkflowNodeActionId = 5,
                ExecutionOrder = 10,
                OnErrorBehavior = "fail_workflow",
                ActionDefinitionId = 6,
                ActionKey = "CreateAdUser",
                ActionName = "Create AD User",
                HandlerType = "simulated_directory",
                IsIdempotent = true,
                AttemptNumber = 1,
                CreatedByUserId = 99
            }
        };

        var service = new WorkflowAutomationService(
            repository,
            repository,
            new StubWorkflowAutomationHandlerRegistry(_ => new ThrowingAutomationHandler("CreateAdUser")),
            new StubSystemEventLogService(),
            new StubWorkflowLifecycleService(),
            new WorkflowAutomationRetrySettings(),
            NullLogger<WorkflowAutomationService>.Instance);

        var processed = await service.TryProcessNextPendingJobAsync();

        Assert.True(processed);
        Assert.True(repository.LastFailureShouldRetry);
        Assert.NotNull(repository.LastFailureRetryAvailableAt);
    }

    [Fact]
    public async Task TryProcessNextPendingJobAsync_CompletesFailureWithoutRetryForNonIdempotentAction()
    {
        var repository = new StubWorkflowAutomationRepository
        {
            ClaimedJob = new ClaimedAutomationJobRecord
            {
                JobId = 1,
                WorkflowId = 2,
                WorkflowUid = Guid.NewGuid(),
                WorkflowNodeInstanceId = 3,
                WorkflowNodeId = 4,
                NodeKey = "auto",
                NodeType = "automation",
                WorkflowNodeActionId = 5,
                ExecutionOrder = 10,
                OnErrorBehavior = "fail_workflow",
                ActionDefinitionId = 6,
                ActionKey = "CreateErpEmployee",
                ActionName = "Create ERP Employee",
                HandlerType = "simulated_erp",
                IsIdempotent = false,
                AttemptNumber = 1,
                CreatedByUserId = 99
            }
        };

        var service = new WorkflowAutomationService(
            repository,
            repository,
            new StubWorkflowAutomationHandlerRegistry(_ => new ThrowingAutomationHandler("CreateErpEmployee")),
            new StubSystemEventLogService(),
            new StubWorkflowLifecycleService(),
            new WorkflowAutomationRetrySettings(),
            NullLogger<WorkflowAutomationService>.Instance);

        await service.TryProcessNextPendingJobAsync();

        Assert.False(repository.LastFailureShouldRetry);
        Assert.Null(repository.LastFailureRetryAvailableAt);
    }

    private sealed class StubWorkflowAutomationRepository : IWorkflowAutomationRepository, IWorkflowAutomationReadRepository
    {
        public ClaimedAutomationJobRecord? ClaimedJob { get; set; }
        public bool LastFailureShouldRetry { get; private set; }
        public DateTime? LastFailureRetryAvailableAt { get; private set; }

        public Task<IReadOnlyList<ActionDefinitionDto>> GetAdminActionDefinitions(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ActionDefinitionDto>>([]);

        public Task<IReadOnlyList<AutomationJobDetailDto>> GetAutomationJobs(Guid workflowUid, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AutomationJobDetailDto>>([]);

        public Task<ClaimedAutomationJobRecord?> ClaimNextPendingAutomationJob(CancellationToken cancellationToken = default)
            => Task.FromResult(ClaimedJob);

        public Task CompleteAutomationJobFailure(
            ClaimedAutomationJobRecord job,
            string errorMessage,
            bool shouldRetry,
            DateTime? retryAvailableAt,
            IReadOnlyList<WorkflowAutomationLogEntry> logs,
            CancellationToken cancellationToken = default)
        {
            LastFailureShouldRetry = shouldRetry;
            LastFailureRetryAvailableAt = retryAvailableAt;
            return Task.CompletedTask;
        }
    }

    private sealed class StubWorkflowAutomationHandlerRegistry(Func<string, IWorkflowAutomationActionHandler> factory)
        : IWorkflowAutomationHandlerRegistry
    {
        public IReadOnlyCollection<string> GetRegisteredKeys() => ["CreateAdUser", "CreateErpEmployee"];

        public IWorkflowAutomationActionHandler Resolve(string actionKey) => factory(actionKey);
    }

    private sealed class ThrowingAutomationHandler(string actionKey) : IWorkflowAutomationActionHandler
    {
        public string ActionKey => actionKey;

        public Task<WorkflowAutomationHandlerResult> ExecuteAsync(WorkflowAutomationHandlerContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"Handler failed for {actionKey}.");
    }

    private sealed class StubWorkflowLifecycleService : IWorkflowLifecycleService
    {
        public Task<TaskWithWorkflowDto?> UpdateTaskStatusAsync(long taskId, string status, long actorUserId) => throw new NotImplementedException();
        public Task<TaskWithWorkflowDto?> UpdateTaskStatusByRefAsync(string taskRef, string status, long actorUserId) => throw new NotImplementedException();
        public Task<TaskWithWorkflowDto?> DecideTaskApprovalAsync(long taskId, TaskApprovalDecisionRequest request, long actorUserId) => throw new NotImplementedException();
        public Task<TaskWithWorkflowDto?> DecideTaskApprovalByRefAsync(string taskRef, TaskApprovalDecisionRequest request, long actorUserId) => throw new NotImplementedException();
        public Task OnAutomationJobCompletedAsync(ClaimedAutomationJobRecord job, WorkflowAutomationHandlerResult result, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowInstanceAsync(CreateWorkflowDefinitionInstanceRequest request, long actorUserId) => throw new NotImplementedException();
        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteFormNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeFormNodeRequest request, long actorUserId) => throw new NotImplementedException();
        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteApprovalNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeApprovalNodeRequest request, long actorUserId) => throw new NotImplementedException();
        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteTaskNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeTaskNodeRequest request, long actorUserId) => throw new NotImplementedException();
    }

    private sealed class StubSystemEventLogService : ISystemEventLogService
    {
        public Task<IReadOnlyList<AdminSystemLogEntryDto>> GetAdminLogsAsync(
            SystemEventLogQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AdminSystemLogEntryDto>>([]);

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
