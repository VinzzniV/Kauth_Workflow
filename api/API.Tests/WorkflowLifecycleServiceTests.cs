using Npgsql;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class WorkflowLifecycleServiceTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=26432;Database=appdb;Username=app;Password=app_pw";
    private const long TestActorUserId = 1;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateTaskStatusByRefAsync_WorkflowTaskRef_UsesLifecycleScope()
    {
        var repository = new StubWorkflowRepository();
        var scopedRepository = new TestScopedRepository
        {
            OnUpdateTaskStatusInScope = (_, _, _, _, _) => Task.FromResult<TaskStatusUpdateScopeResult?>(null)
        };
        var service = CreateService(repository, scopedRepository);

        await WithConnectionStringAsync(GetTestConnectionString(), async () =>
        {
            var result = await service.UpdateTaskStatusByRefAsync(
                WorkflowTaskRef.Build(42),
                "done",
                TestActorUserId);

            Assert.Null(result);
        });

        Assert.Equal(1, scopedRepository.UpdateTaskStatusInScopeCalls);
        Assert.Equal(0, repository.UpdateTaskStatusByRefCalls);
    }

    [Fact]
    public async Task UpdateTaskStatusByRefAsync_RotationTaskRef_DelegatesToRepository()
    {
        var expected = CreateTaskEnvelope(RotationTaskRef.Build(77), TaskFamilyNames.Rotation);
        var repository = new StubWorkflowRepository
        {
            UpdateTaskStatusByRefResult = expected
        };
        var scopedRepository = new TestScopedRepository();
        var service = CreateService(repository, scopedRepository);

        var result = await service.UpdateTaskStatusByRefAsync(
            RotationTaskRef.Build(77),
            "done",
            TestActorUserId);

        Assert.Same(expected, result);
        Assert.Equal(1, repository.UpdateTaskStatusByRefCalls);
        Assert.Equal(0, scopedRepository.UpdateTaskStatusInScopeCalls);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DecideTaskApprovalByRefAsync_WorkflowTaskRef_UsesLifecycleScope()
    {
        var repository = new StubWorkflowRepository();
        var scopedRepository = new TestScopedRepository
        {
            OnDecideTaskApprovalInScope = (_, _, _, _, _) => Task.FromResult<DecideTaskApprovalScopeResult?>(null)
        };
        var service = CreateService(repository, scopedRepository);

        await WithConnectionStringAsync(GetTestConnectionString(), async () =>
        {
            var result = await service.DecideTaskApprovalByRefAsync(
                WorkflowTaskRef.Build(11),
                new TaskApprovalDecisionRequest { Approved = true },
                TestActorUserId);

            Assert.Null(result);
        });

        Assert.Equal(1, scopedRepository.DecideTaskApprovalInScopeCalls);
        Assert.Equal(0, repository.DecideTaskApprovalByRefCalls);
    }

    [Fact]
    public async Task DecideTaskApprovalByRefAsync_RotationTaskRef_DelegatesToRepository()
    {
        var expected = CreateTaskEnvelope(RotationTaskRef.Build(81), TaskFamilyNames.Rotation);
        var repository = new StubWorkflowRepository
        {
            DecideTaskApprovalByRefResult = expected
        };
        var scopedRepository = new TestScopedRepository();
        var service = CreateService(repository, scopedRepository);

        var result = await service.DecideTaskApprovalByRefAsync(
            RotationTaskRef.Build(81),
            new TaskApprovalDecisionRequest { Approved = false, CommentText = "nope" },
            TestActorUserId);

        Assert.Same(expected, result);
        Assert.Equal(1, repository.DecideTaskApprovalByRefCalls);
        Assert.Equal(0, scopedRepository.DecideTaskApprovalInScopeCalls);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateTaskStatusAsync_RollsBackScopedWrite_WhenRuntimeStepFails()
    {
        var connectionString = GetTestConnectionString();
        var testData = await CreateIsolatedWorkflowAsync(
            connectionString,
            ("rollback_probe", "ready", 10));

        var repository = new StubWorkflowRepository();
        var scopedRepository = new TestScopedRepository
        {
            OnUpdateTaskStatusInScope = async (connection, transaction, taskId, normalizedStatus, _) =>
            {
                await using var command = new NpgsqlCommand(
                    """
                    UPDATE workflow_tasks
                    SET status = @status
                    WHERE id = @taskId;
                    """,
                    connection,
                    transaction);
                command.Parameters.AddWithValue("status", normalizedStatus);
                command.Parameters.AddWithValue("taskId", taskId);
                await command.ExecuteNonQueryAsync();

                return new TaskStatusUpdateScopeResult(
                    ShouldCompleteRuntimeTaskNode: true,
                    RuntimeNodeInstanceId: 999999,
                    WorkflowId: testData.WorkflowId,
                    WorkflowUid: testData.WorkflowUid,
                    ShouldTryAdvanceRuntimeSetup: false);
            }
        };
        var service = CreateService(repository, scopedRepository);

        try
        {
            await WithConnectionStringAsync(connectionString, async () =>
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    service.UpdateTaskStatusAsync(testData.TaskIds[0], "done", TestActorUserId));

                Assert.Equal("Workflow runtime instance was not found.", exception.Message);
            });

            var reloadedStatus = await LoadTaskStatusAsync(connectionString, testData.TaskIds[0]);
            Assert.Equal("ready", reloadedStatus);
        }
        finally
        {
            await CleanupIsolatedWorkflowAsync(connectionString, testData);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task OnAutomationJobCompletedAsync_UsesScopedRepositoryWithinOpenTransaction()
    {
        var repository = new StubWorkflowRepository();
        var scopedRepository = new TestScopedRepository();
        scopedRepository.OnCompleteAutomationJobSuccessInScope = (connection, transaction, job, result, cancellationToken) =>
        {
            scopedRepository.LastAutomationJob = job;
            scopedRepository.LastAutomationResult = result;
            scopedRepository.LastAutomationConnectionIsOpen = connection.State == System.Data.ConnectionState.Open;
            scopedRepository.LastAutomationTransactionMatchesConnection = ReferenceEquals(transaction.Connection, connection);
            scopedRepository.LastAutomationCancellationToken = cancellationToken;
            return Task.CompletedTask;
        };
        var service = CreateService(repository, scopedRepository);
        var job = new ClaimedAutomationJobRecord
        {
            JobId = 9,
            WorkflowId = 12,
            WorkflowUid = Guid.NewGuid(),
            WorkflowNodeInstanceId = 15,
            WorkflowNodeId = 21,
            NodeKey = "automation-node",
            NodeType = "automation",
            WorkflowNodeActionId = 34,
            ExecutionOrder = 10,
            OnErrorBehavior = "fail_workflow",
            ActionDefinitionId = 55,
            ActionKey = "CreateAdUser",
            ActionName = "Create AD User",
            HandlerType = "simulated_directory",
            IsIdempotent = true,
            CreatedByUserId = 99,
            AttemptNumber = 1
        };
        var result = new WorkflowAutomationHandlerResult
        {
            Logs = []
        };
        using var cancellationSource = new CancellationTokenSource();

        await WithConnectionStringAsync(GetTestConnectionString(), async () =>
        {
            await service.OnAutomationJobCompletedAsync(job, result, cancellationSource.Token);
        });

        Assert.Equal(1, scopedRepository.CompleteAutomationJobSuccessInScopeCalls);
        Assert.Same(job, scopedRepository.LastAutomationJob);
        Assert.Same(result, scopedRepository.LastAutomationResult);
        Assert.True(scopedRepository.LastAutomationConnectionIsOpen);
        Assert.True(scopedRepository.LastAutomationTransactionMatchesConnection);
        Assert.Equal(cancellationSource.Token, scopedRepository.LastAutomationCancellationToken);
    }

    private static WorkflowLifecycleService CreateService(
        IWorkflowRepository workflowRepository,
        IWorkflowLifecycleScopedRepository scopedRepository)
    {
        return new WorkflowLifecycleService(
            workflowRepository,
            scopedRepository,
            null!,
            null!,
            null!);
    }

    private static string GetTestConnectionString()
    {
        return Environment.GetEnvironmentVariable("ONBOARDING_TEST_CONNECTION_STRING")
               ?? DefaultTestConnectionString;
    }

    private static async Task WithConnectionStringAsync(string connectionString, Func<Task> action)
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        try
        {
            await action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);
        }
    }

    private static async Task<TestWorkflowData> CreateIsolatedWorkflowAsync(
        string connectionString,
        params (string TaskKey, string Status, int SortOrder)[] tasks)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var employeeNumber = Math.Abs(suffix[..8].GetHashCode());
        var badgeNumber = Math.Abs(suffix[8..16].GetHashCode());

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        int departmentId;
        await using (var departmentCommand = new NpgsqlCommand(
                         """
                         INSERT INTO departments (name)
                         VALUES (@name)
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            departmentCommand.Parameters.AddWithValue("name", $"Lifecycle Integration {suffix}");
            departmentId = (int)(await departmentCommand.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Department could not be created."));
        }

        int roleId;
        await using (var roleCommand = new NpgsqlCommand(
                         """
                         INSERT INTO app_roles (department_id, role_key, name, role_kind, is_active)
                         VALUES (@departmentId, @roleKey, @name, 'position', TRUE)
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            roleCommand.Parameters.AddWithValue("departmentId", departmentId);
            roleCommand.Parameters.AddWithValue("roleKey", $"lifecycle_role_{suffix}");
            roleCommand.Parameters.AddWithValue("name", $"Lifecycle Role {suffix}");
            roleId = (int)(await roleCommand.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Role could not be created."));
        }

        long workflowId;
        Guid workflowUid;
        await using (var workflowCommand = new NpgsqlCommand(
                         """
                         INSERT INTO workflows (
                             workflow_definition_id,
                             department_id,
                             position_role_id,
                             first_name,
                             last_name,
                             employee_number,
                             badge_number,
                             status
                         )
                         VALUES (
                             (SELECT id FROM workflow_definitions WHERE definition_key = 'onboarding'),
                             @departmentId,
                             @roleId,
                             'Lifecycle',
                             @lastName,
                             @employeeNumber,
                             @badgeNumber,
                             'waiting_for_department'
                         )
                         RETURNING id, uid;
                         """,
                         connection,
                         transaction))
        {
            workflowCommand.Parameters.AddWithValue("departmentId", departmentId);
            workflowCommand.Parameters.AddWithValue("roleId", roleId);
            workflowCommand.Parameters.AddWithValue("lastName", suffix);
            workflowCommand.Parameters.AddWithValue("employeeNumber", employeeNumber);
            workflowCommand.Parameters.AddWithValue("badgeNumber", badgeNumber);
            await using var reader = await workflowCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Workflow could not be created.");
            }

            workflowId = reader.GetInt64(0);
            workflowUid = reader.GetGuid(1);
        }

        var taskIds = new List<long>();
        foreach (var (taskKey, status, sortOrder) in tasks)
        {
            await using var taskCommand = new NpgsqlCommand(
                """
                INSERT INTO workflow_tasks (
                    workflow_id,
                    task_key,
                    title,
                    category,
                    description,
                    icon_key,
                    status,
                    is_required,
                    sort_order,
                    ready_at
                )
                VALUES (
                    @workflowId,
                    @taskKey,
                    @title,
                    'integration',
                    @description,
                    'integration',
                    @status,
                    TRUE,
                    @sortOrder,
                    CASE WHEN @status = 'ready' THEN NOW() ELSE NULL END
                )
                RETURNING id;
                """,
                connection,
                transaction);

            taskCommand.Parameters.AddWithValue("workflowId", workflowId);
            taskCommand.Parameters.AddWithValue("taskKey", $"{taskKey}_{suffix}");
            taskCommand.Parameters.AddWithValue("title", $"Task {taskKey}");
            taskCommand.Parameters.AddWithValue("description", $"Lifecycle integration task {taskKey}");
            taskCommand.Parameters.AddWithValue("status", status);
            taskCommand.Parameters.AddWithValue("sortOrder", sortOrder);

            taskIds.Add((long)(await taskCommand.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Workflow task could not be created.")));
        }

        await transaction.CommitAsync();

        return new TestWorkflowData
        {
            DepartmentId = departmentId,
            RoleId = roleId,
            WorkflowId = workflowId,
            WorkflowUid = workflowUid,
            TaskIds = taskIds
        };
    }

    private static async Task CleanupIsolatedWorkflowAsync(
        string connectionString,
        TestWorkflowData testData)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var deleteWorkflowCommand = new NpgsqlCommand(
                         "DELETE FROM workflows WHERE id = @workflowId;",
                         connection,
                         transaction))
        {
            deleteWorkflowCommand.Parameters.AddWithValue("workflowId", testData.WorkflowId);
            await deleteWorkflowCommand.ExecuteNonQueryAsync();
        }

        await using (var deleteRoleCommand = new NpgsqlCommand(
                         "DELETE FROM app_roles WHERE id = @roleId;",
                         connection,
                         transaction))
        {
            deleteRoleCommand.Parameters.AddWithValue("roleId", testData.RoleId);
            await deleteRoleCommand.ExecuteNonQueryAsync();
        }

        await using (var deleteDepartmentCommand = new NpgsqlCommand(
                         "DELETE FROM departments WHERE id = @departmentId;",
                         connection,
                         transaction))
        {
            deleteDepartmentCommand.Parameters.AddWithValue("departmentId", testData.DepartmentId);
            await deleteDepartmentCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task<string?> LoadTaskStatusAsync(string connectionString, long taskId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT status
            FROM workflow_tasks
            WHERE id = @taskId;
            """,
            connection);
        command.Parameters.AddWithValue("taskId", taskId);
        return await command.ExecuteScalarAsync() as string;
    }

    private static TaskWithWorkflowDto CreateTaskEnvelope(string taskRef, string taskFamily)
    {
        return new TaskWithWorkflowDto
        {
            TaskRef = taskRef,
            TaskFamily = taskFamily,
            Task = new WorkflowTaskDto
            {
                Id = 1,
                TaskKey = "task",
                Title = "Task",
                Description = "Task",
                Category = "test",
                IconKey = "check",
                Status = "ready",
                IsRequired = true,
                SortOrder = 10,
                CreatedAt = DateTime.UtcNow,
                SlaStatus = "on_track",
                Assignments = [],
                Dependencies = [],
                Comments = []
            },
            Rotation = taskFamily == TaskFamilyNames.Rotation
                ? new TaskRotationContextDto
                {
                    RotationPlanId = 1,
                    PlanStatus = "draft",
                    PlanTitle = "Rotation Plan",
                    SourceWorkflowUid = Guid.NewGuid(),
                    PersonId = 5,
                    DisplayName = "Rotation User",
                    DepartmentId = 2,
                    DepartmentName = "IT"
                }
                : null,
            Workflow = taskFamily == TaskFamilyNames.Workflow
                ? new TaskWorkflowContextDto
                {
                    WorkflowId = 10,
                    WorkflowUid = Guid.NewGuid(),
                    WorkflowStatus = "ready",
                    WorkflowCreatedAt = DateTime.UtcNow,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = 1234,
                    BadgeNumber = 5678,
                    DepartmentId = 2,
                    DepartmentName = "IT",
                    RoleId = 3,
                    RoleName = "Engineer"
                }
                : null
        };
    }

    private sealed class TestWorkflowData
    {
        public required int DepartmentId { get; init; }
        public required int RoleId { get; init; }
        public required long WorkflowId { get; init; }
        public required Guid WorkflowUid { get; init; }
        public required List<long> TaskIds { get; init; }
    }

    private sealed class TestScopedRepository : IWorkflowLifecycleScopedRepository
    {
        public Func<NpgsqlConnection, NpgsqlTransaction, long, string, long, Task<TaskStatusUpdateScopeResult?>>? OnUpdateTaskStatusInScope { get; set; }
        public Func<NpgsqlConnection, NpgsqlTransaction, long, TaskApprovalDecisionRequest, long, Task<DecideTaskApprovalScopeResult?>>? OnDecideTaskApprovalInScope { get; set; }
        public Func<NpgsqlConnection, NpgsqlTransaction, ClaimedAutomationJobRecord, WorkflowAutomationHandlerResult, CancellationToken, Task>? OnCompleteAutomationJobSuccessInScope { get; set; }
        public Func<NpgsqlConnection, NpgsqlTransaction, long, Guid, long, long, string?, Task>? OnCompleteRuntimeTaskNodeInScope { get; set; }
        public Func<NpgsqlConnection, NpgsqlTransaction, long, Guid, long, Task>? OnTryAdvanceRuntimeSetupInScope { get; set; }
        public Func<NpgsqlConnection, NpgsqlTransaction, long, Guid, long, bool, long, Task>? OnApplyApprovalNodeDecisionInScope { get; set; }

        public int UpdateTaskStatusInScopeCalls { get; private set; }
        public int DecideTaskApprovalInScopeCalls { get; private set; }
        public int CompleteAutomationJobSuccessInScopeCalls { get; private set; }
        public ClaimedAutomationJobRecord? LastAutomationJob { get; set; }
        public WorkflowAutomationHandlerResult? LastAutomationResult { get; set; }
        public bool LastAutomationConnectionIsOpen { get; set; }
        public bool LastAutomationTransactionMatchesConnection { get; set; }
        public CancellationToken LastAutomationCancellationToken { get; set; }

        public Task<TaskStatusUpdateScopeResult?> UpdateTaskStatusInScope(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long taskId,
            string normalizedStatus,
            long actorUserId)
        {
            UpdateTaskStatusInScopeCalls += 1;
            return OnUpdateTaskStatusInScope is null
                ? Task.FromResult<TaskStatusUpdateScopeResult?>(null)
                : OnUpdateTaskStatusInScope(connection, transaction, taskId, normalizedStatus, actorUserId);
        }

        public Task<DecideTaskApprovalScopeResult?> DecideTaskApprovalInScope(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long taskId,
            TaskApprovalDecisionRequest request,
            long actorUserId)
        {
            DecideTaskApprovalInScopeCalls += 1;
            return OnDecideTaskApprovalInScope is null
                ? Task.FromResult<DecideTaskApprovalScopeResult?>(null)
                : OnDecideTaskApprovalInScope(connection, transaction, taskId, request, actorUserId);
        }

        public Task CompleteAutomationJobSuccessInScope(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            ClaimedAutomationJobRecord job,
            WorkflowAutomationHandlerResult result,
            CancellationToken cancellationToken = default)
        {
            CompleteAutomationJobSuccessInScopeCalls += 1;
            return OnCompleteAutomationJobSuccessInScope is null
                ? Task.CompletedTask
                : OnCompleteAutomationJobSuccessInScope(connection, transaction, job, result, cancellationToken);
        }

        public Task CompleteRuntimeTaskNodeInScope(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long workflowId,
            Guid workflowUid,
            long nodeInstanceId,
            long actorUserId,
            string? comment = null)
        {
            return OnCompleteRuntimeTaskNodeInScope is not null
                ? OnCompleteRuntimeTaskNodeInScope(connection, transaction, workflowId, workflowUid, nodeInstanceId, actorUserId, comment)
                : PostgresWorkflowRuntimeRepository.CompleteTaskNodeRuntimeSide(connection, transaction, workflowId, workflowUid, nodeInstanceId, actorUserId, comment);
        }

        public Task TryAdvanceRuntimeSetupInScope(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long workflowId,
            Guid workflowUid,
            long actorUserId)
        {
            return OnTryAdvanceRuntimeSetupInScope is not null
                ? OnTryAdvanceRuntimeSetupInScope(connection, transaction, workflowId, workflowUid, actorUserId)
                : PostgresWorkflowRuntimeRepository.TryAdvanceSetupNodeIfReady(connection, transaction, workflowId, workflowUid, actorUserId);
        }

        public Task ApplyApprovalNodeDecisionInScope(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long workflowId,
            Guid workflowUid,
            long nodeInstanceId,
            bool approved,
            long actorUserId)
        {
            return OnApplyApprovalNodeDecisionInScope is not null
                ? OnApplyApprovalNodeDecisionInScope(connection, transaction, workflowId, workflowUid, nodeInstanceId, approved, actorUserId)
                : PostgresWorkflowRuntimeRepository.ApplyApprovalNodeDecision(connection, transaction, workflowId, workflowUid, nodeInstanceId, approved, actorUserId);
        }
    }

    private sealed class StubWorkflowRepository : IWorkflowRepository
    {
        public TaskWithWorkflowDto? UpdateTaskStatusByRefResult { get; init; }
        public TaskWithWorkflowDto? DecideTaskApprovalByRefResult { get; init; }
        public int UpdateTaskStatusByRefCalls { get; private set; }
        public int DecideTaskApprovalByRefCalls { get; private set; }

        public Task<AdminListPageDto<DepartmentDto>> GetDepartments(AdminListQuery query) => throw new NotSupportedException();
        public Task<AdminListPageDto<RoleDto>> GetRoles(AdminListQuery query) => throw new NotSupportedException();
        public Task<AdminListPageDto<PersonDirectoryItemDto>> GetPeopleDirectory(AdminListQuery query) => throw new NotSupportedException();
        public Task<List<WorkflowStartableDefinitionDto>> GetStartableWorkflowDefinitions() => throw new NotSupportedException();
        public Task<List<RequirementDto>> GetRequirements(string legacyProcessTypeKey) => throw new NotSupportedException();
        public Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId, string legacyProcessTypeKey) => throw new NotSupportedException();
        public Task<bool> IsManagerCreatableDefinition(string workflowDefinitionKey) => throw new NotSupportedException();
        public Task<IReadOnlySet<string>> GetManagerCreatableDefinitionKeys() => throw new NotSupportedException();
        public Task<WorkflowTargetPersonDto> CreatePerson(CreatePersonRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<WorkflowCreationResult> CreateWorkflow(CreateWorkflowRequest request, long createdByUserId) => throw new NotSupportedException();
        public Task<WorkflowDetailDto?> CompleteSupervisorStep(Guid workflowUid, IReadOnlyList<RequirementSelectionInputDto> selections, long actorUserId) => throw new NotSupportedException();
        public Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotifications(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowCompletionNotifications(Guid workflowUid) => throw new NotSupportedException();
        public Task ApplyNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results) => throw new NotSupportedException();
        public Task<List<WorkflowListItemDto>> GetWorkflows() => throw new NotSupportedException();
        public Task<WorkflowListResult> GetFilteredWorkflows(WorkflowListQuery query) => throw new NotSupportedException();
        public Task<WorkflowDetailDto?> GetWorkflowByUid(Guid workflowUid) => throw new NotSupportedException();
        public Task<HashSet<int>> GetRequirementSelectionDepartmentIds(long userId) => throw new NotSupportedException();
        public Task<List<TaskWithWorkflowDto>> GetTasks() => throw new NotSupportedException();
        public Task<List<TaskWithWorkflowDto>> GetTasksForUser(long userId, int[] effectiveResponsibilityIds) => throw new NotSupportedException();
        public Task<List<TaskWithWorkflowDto>> GetTasksForUserNarrowed(long userId, int[] effectiveResponsibilityIds) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> GetTaskById(long taskId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> GetTaskByRef(string taskRef) => throw new NotSupportedException();

        public Task<TaskWithWorkflowDto?> UpdateTaskStatusByRef(string taskRef, string status, long actorUserId)
        {
            UpdateTaskStatusByRefCalls += 1;
            return Task.FromResult(UpdateTaskStatusByRefResult);
        }

        public Task<TaskWithWorkflowDto?> DecideTaskApprovalByRef(string taskRef, TaskApprovalDecisionRequest request, long actorUserId)
        {
            DecideTaskApprovalByRefCalls += 1;
            return Task.FromResult(DecideTaskApprovalByRefResult);
        }

        public Task<TaskWithWorkflowDto?> UpdateTaskAssignment(long taskId, TaskAssignRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> UpdateTaskAssignmentByRef(string taskRef, TaskAssignRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> AddTaskComment(long taskId, string commentText, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> AddTaskCommentByRef(string taskRef, string commentText, long actorUserId) => throw new NotSupportedException();
        public Task<bool> ArchiveWorkflow(Guid workflowUid, long actorUserId) => throw new NotSupportedException();
        public Task<bool> DeleteDraftWorkflow(Guid workflowUid) => throw new NotSupportedException();
        public Task<PersonWorkflowHistoryDto?> GetPersonWorkflowHistory(long personId) => throw new NotSupportedException();
        public Task<List<WorkflowLinkDto>> GetWorkflowLinks(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<RelatedWorkflowSummaryDto>> GetRelatedWorkflows(Guid workflowUid) => throw new NotSupportedException();
        public Task<WorkflowLinkDto?> CreateWorkflowLink(Guid targetWorkflowUid, CreateWorkflowLinkRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<bool> DeleteWorkflowLink(Guid workflowUid, long linkId, long actorUserId) => throw new NotSupportedException();
        public Task<List<WorkflowTargetPersonSourceDto>> SearchWorkflowTargetPersonSources(string? search, int limit = 20, IReadOnlyCollection<int>? observableDepartmentIds = null) => throw new NotSupportedException();
        public Task<List<WorkflowTargetPersonDto>> SearchWorkflowTargetPeople(string? query, int limit = 20, IReadOnlyCollection<int>? observableDepartmentIds = null) => throw new NotSupportedException();
        public Task<List<WorkflowTargetPersonDto>> SearchRotationEligiblePeople(string? query, int limit = 20, IReadOnlyCollection<int>? observableDepartmentIds = null) => throw new NotSupportedException();
        public Task ApplyPersonLifecycleProjection(Guid workflowUid, long? actorUserId = null) => throw new NotSupportedException();
        public Task<List<LinkableWorkflowDto>> FindLinkableWorkflows(int employeeNumber, Guid? excludeWorkflowUid = null) => throw new NotSupportedException();
        public Task<List<DerivedAnswerDto>> GetDerivedAnswers(Guid sourceWorkflowUid, string targetWorkflowDefinitionKey) => throw new NotSupportedException();
        public Task<AdminListPageDto<WorkflowDefinitionSummaryDto>> GetAdminWorkflowDefinitions(AdminListQuery query) => throw new NotSupportedException();
        public Task<WorkflowDefinitionSummaryDto> CreateAdminWorkflowDefinition(CreateWorkflowDefinitionRequest request) => throw new NotSupportedException();
        public Task<WorkflowDefinitionSummaryDto?> UpdateAdminWorkflowDefinition(int definitionId, UpdateWorkflowDefinitionRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminWorkflowDefinition(int definitionId) => throw new NotSupportedException();
        public Task<WorkflowDefinitionVersionSummaryDto?> CreateAdminWorkflowDefinitionVersion(int definitionId, CreateWorkflowDefinitionVersionRequest request) => throw new NotSupportedException();
        public Task<WorkflowDefinitionVersionDetailDto?> EnsureAdminWorkflowDefinitionWorkingDraft(int definitionId) => throw new NotSupportedException();
        public Task<WorkflowDefinitionVersionDetailDto?> GetAdminWorkflowDefinitionVersion(long versionId) => throw new NotSupportedException();
        public Task<WorkflowDefinitionVersionDetailDto?> ReplaceAdminWorkflowDefinitionVersion(long versionId, ReplaceWorkflowDefinitionVersionRequest request) => throw new NotSupportedException();
        public Task<AdminListPageDto<AdminTaskTemplateDto>> GetAdminTaskTemplates(int workflowDefinitionId, AdminListQuery query) => throw new NotSupportedException();
        public Task<AdminTaskTemplateDto> CreateAdminTaskTemplate(AdminTaskTemplateUpsertRequest request) => throw new NotSupportedException();
        public Task<AdminTaskTemplateDto?> UpdateAdminTaskTemplate(int templateId, AdminTaskTemplateUpsertRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminTaskTemplate(int templateId) => throw new NotSupportedException();
        public Task<AdminListPageDto<AdminTaskTemplateConditionDto>> GetAdminTaskTemplateConditions(int templateId, AdminListQuery query) => throw new NotSupportedException();
        public Task<AdminTaskTemplateConditionDto> CreateAdminTaskTemplateCondition(int templateId, AdminTaskTemplateConditionCreateRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminTaskTemplateCondition(int templateId, long conditionId) => throw new NotSupportedException();
        public Task<AdminListPageDto<AdminTaskTemplateDependencyDto>> GetAdminTaskTemplateDependencies(int templateId, AdminListQuery query) => throw new NotSupportedException();
        public Task<AdminTaskTemplateDependencyDto> CreateAdminTaskTemplateDependency(int templateId, AdminTaskTemplateDependencyCreateRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminTaskTemplateDependency(int templateId, long dependencyId) => throw new NotSupportedException();
        public Task<AdminListPageDto<AdminAnswerDefinitionDto>> GetAdminAnswerDefinitions(int workflowDefinitionId, AdminListQuery query) => throw new NotSupportedException();
        public Task<AdminAnswerDefinitionDto> CreateAdminAnswerDefinition(AdminAnswerDefinitionUpsertRequest request) => throw new NotSupportedException();
        public Task<AdminAnswerDefinitionDto?> UpdateAdminAnswerDefinition(int definitionId, AdminAnswerDefinitionUpsertRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminAnswerDefinition(int definitionId) => throw new NotSupportedException();
        public Task<AdminListPageDto<AdminRoleAnswerDefaultDto>> GetAdminRoleAnswerDefaults(int workflowDefinitionId, AdminListQuery query) => throw new NotSupportedException();
        public Task<List<AdminRoleAnswerDefaultDto>> UpsertAdminRoleAnswerDefaults(AdminRoleAnswerDefaultsBulkUpsertRequest request) => throw new NotSupportedException();
        public Task<AdminDependencyGraphDto> GetAdminDependencyGraph(int workflowDefinitionId) => throw new NotSupportedException();
        public Task<bool> UpdatePersonCoreFields(long personId, DateOnly? entryDate, int? badgeNumber) => throw new NotSupportedException();
        public Task<AdminListPageDto<UnlinkedDirectoryIdentityDto>> GetUnlinkedDirectoryIdentities(string? departmentFilter, bool? onlyEnabled, int limit, int offset) => throw new NotSupportedException();
        public Task<ImportPeopleFromDirectoryResultDto> ImportPeopleFromDirectory(List<long> directoryIdentityIds, long? actorUserId) => throw new NotSupportedException();
    }
}
