using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace API.Tests;

public sealed class WorkflowEndpointsTests
{
    [Fact]
    public async Task AuditLogEndpoint_RejectsNonPositiveLimit()
    {
        var repository = new StubWorkflowRepository();
        var app = CreateApp(repository);
        var endpoint = GetAuditLogEndpoint(app);
        var workflowUid = Guid.NewGuid();
        var context = CreateRequestContext(app.Services, endpoint, workflowUid, "?limit=0");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(0, repository.GetWorkflowByUidCallCount);
        Assert.Equal(0, repository.GetWorkflowAuditLogCallCount);
    }

    [Fact]
    public async Task AuditLogEndpoint_RejectsNegativeOffset()
    {
        var repository = new StubWorkflowRepository();
        var app = CreateApp(repository);
        var endpoint = GetAuditLogEndpoint(app);
        var workflowUid = Guid.NewGuid();
        var context = CreateRequestContext(app.Services, endpoint, workflowUid, "?offset=-1");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(0, repository.GetWorkflowByUidCallCount);
        Assert.Equal(0, repository.GetWorkflowAuditLogCallCount);
    }

    [Fact]
    public async Task AuditLogEndpoint_CapsLimitAndPassesOffsetToRepository()
    {
        var workflowUid = Guid.NewGuid();
        var repository = new StubWorkflowRepository
        {
            Workflow = CreateWorkflowDetail(workflowUid),
            AuditEntries = new List<WorkflowAuditEntryDto>()
        };

        var app = CreateApp(repository);
        var endpoint = GetAuditLogEndpoint(app);
        var context = CreateRequestContext(app.Services, endpoint, workflowUid, "?limit=999&offset=7");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetWorkflowByUidCallCount);
        Assert.Equal(1, repository.GetWorkflowAuditLogCallCount);
        Assert.Equal(500, repository.LastAuditLogLimit);
        Assert.Equal(7, repository.LastAuditLogOffset);
    }

    private static WebApplication CreateApp(StubWorkflowRepository repository)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IWorkflowRepository>(repository);
        builder.Services.AddSingleton<IUserContext>(new StubUserContext(CreateAdminUser()));
        builder.Services.AddSingleton<IAuthorizationPolicyService, AuthorizationPolicyService>();
        builder.Services.AddSingleton<IWorkflowEmailNotificationSender, StubWorkflowEmailNotificationSender>();
        builder.Services.AddSingleton<ISupervisorStepService, StubSupervisorStepService>();

        var app = builder.Build();
        app.MapWorkflowEndpoints();
        return app;
    }

    private static RouteEndpoint GetAuditLogEndpoint(WebApplication app)
    {
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == "/workflows/{uid:guid}/audit-log");
    }

    private static DefaultHttpContext CreateRequestContext(
        IServiceProvider services,
        RouteEndpoint endpoint,
        Guid workflowUid,
        string queryString)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = $"/workflows/{workflowUid}/audit-log";
        context.Request.QueryString = new QueryString(queryString);
        context.Request.RouteValues["uid"] = workflowUid.ToString();
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        return context;
    }

    private static WorkflowDetailDto CreateWorkflowDetail(Guid workflowUid)
    {
        return new WorkflowDetailDto
        {
            Uid = workflowUid,
            FirstName = "Audit",
            LastName = "Workflow",
            EmployeeNumber = 1001,
            BadgeNumber = 2001,
            DepartmentId = 1,
            DepartmentName = "IT",
            RoleId = 1,
            RoleName = "Developer",
            Status = "open",
            WorkflowStatus = "draft",
            CreatedAt = DateTime.UtcNow,
            DeadlineDate = null,
            Requirements = new List<WorkflowRequirementSnapshotDto>(),
            RequirementSummary = new WorkflowRequirementSummaryDto
            {
                TotalCount = 0,
                VisibleCount = 0,
                AnsweredVisibleCount = 0,
                PendingVisibleCount = 0
            },
            Tasks = new List<WorkflowTaskDto>(),
            TaskMetrics = new WorkflowTaskMetricsDto
            {
                Overall = new WorkflowTaskCountSummaryDto
                {
                    TotalCount = 0,
                    OpenCount = 0,
                    InProgressCount = 0,
                    DoneCount = 0,
                    CompletedCount = 0,
                    ActiveCount = 0
                },
                DepartmentPhase = new WorkflowTaskCountSummaryDto
                {
                    TotalCount = 0,
                    OpenCount = 0,
                    InProgressCount = 0,
                    DoneCount = 0,
                    CompletedCount = 0,
                    ActiveCount = 0
                }
            },
            TaskAreas = new List<WorkflowTaskAreaSummaryDto>(),
            Notifications = new List<WorkflowNotificationDto>()
        };
    }

    private static CurrentUser CreateAdminUser()
    {
        return new CurrentUser
        {
            UserId = 99,
            ExternalKey = "admin.test",
            DisplayName = "Admin Test",
            Email = "admin.test@example.com",
            IsActive = true,
            DepartmentId = null,
            DepartmentName = null,
            IdentityProvider = "test",
            Groups = new List<CurrentUserGroup>(),
            DirectRoles = new List<CurrentUserRole>(),
            GroupRoles = new List<CurrentUserRole>(),
            EffectiveRoles = new List<CurrentUserRole>
            {
                new()
                {
                    RoleId = 1,
                    RoleKey = AuthorizationRoles.Admin,
                    RoleName = "Admin",
                    RoleKind = "system",
                    AssignmentSource = "test",
                    GroupId = null,
                    GroupKey = null
                }
            },
            DirectResponsibilities = new List<CurrentUserResponsibility>(),
            GroupResponsibilities = new List<CurrentUserResponsibility>(),
            EffectiveResponsibilities = new List<CurrentUserResponsibility>()
        };
    }

    private sealed class StubUserContext(CurrentUser user) : IUserContext
    {
        public Task<CurrentUser?> GetCurrentUser(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CurrentUser?>(user);
        }
    }

    private sealed class StubWorkflowRepository : IWorkflowRepository
    {
        public WorkflowDetailDto? Workflow { get; set; }
        public List<WorkflowAuditEntryDto> AuditEntries { get; set; } = new();
        public int GetWorkflowByUidCallCount { get; private set; }
        public int GetWorkflowAuditLogCallCount { get; private set; }
        public int? LastAuditLogLimit { get; private set; }
        public int? LastAuditLogOffset { get; private set; }

        public Task<List<DepartmentDto>> GetDepartments() => throw new NotSupportedException();
        public Task<List<RoleDto>> GetRoles() => throw new NotSupportedException();
        public Task<List<RequirementDto>> GetRequirements() => throw new NotSupportedException();
        public Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId) => throw new NotSupportedException();
        public Task<WorkflowCreationResult> CreateWorkflow(CreateWorkflowRequest request, long createdByUserId) => throw new NotSupportedException();
        public Task<WorkflowDetailDto?> CompleteSupervisorStep(Guid workflowUid, IReadOnlyList<RequirementSelectionInputDto> selections, long actorUserId) => throw new NotSupportedException();
        public Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotifications(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowCompletionNotifications(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<Guid>> GetWorkflowUidsWithDisabledNotifications(string notificationType) => throw new NotSupportedException();
        public Task ApplyNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results) => throw new NotSupportedException();
        public Task<List<WorkflowListItemDto>> GetWorkflows() => throw new NotSupportedException();

        public Task<WorkflowDetailDto?> GetWorkflowByUid(Guid workflowUid)
        {
            GetWorkflowByUidCallCount += 1;
            return Task.FromResult(Workflow?.Uid == workflowUid ? Workflow : null);
        }

        public Task<List<WorkflowAuditEntryDto>> GetWorkflowAuditLog(Guid workflowUid, int limit = 200, int offset = 0)
        {
            GetWorkflowAuditLogCallCount += 1;
            LastAuditLogLimit = limit;
            LastAuditLogOffset = offset;
            return Task.FromResult(AuditEntries);
        }

        public Task<HashSet<int>> GetRequirementSelectionDepartmentIds(long userId) => throw new NotSupportedException();
        public Task<List<TaskWithWorkflowDto>> GetTasks() => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> GetTaskById(long taskId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> UpdateTaskStatus(long taskId, string status, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> UpdateTaskAssignment(long taskId, TaskAssignRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> AddTaskComment(long taskId, string commentText, long actorUserId) => throw new NotSupportedException();
    }

    private sealed class StubWorkflowEmailNotificationSender : IWorkflowEmailNotificationSender
    {
        public Task<IReadOnlyList<NotificationDispatchResult>> SendNotificationsAsync(
            Guid workflowUid,
            IReadOnlyList<WorkflowNotificationDispatchTarget> targets,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<NotificationDispatchResult>>(Array.Empty<NotificationDispatchResult>());
        }
    }

    private sealed class StubSupervisorStepService : ISupervisorStepService
    {
        public Task<List<WorkflowListItemDto>> GetAssignedWorkflows(CurrentUser currentUser)
        {
            throw new NotSupportedException();
        }

        public Task<List<WorkflowRequirementSnapshotDto>?> GetRequirements(Guid workflowUid, CurrentUser currentUser)
        {
            throw new NotSupportedException();
        }

        public Task<WorkflowDetailDto?> UpdateRequirements(
            Guid workflowUid,
            IReadOnlyList<RequirementSelectionInputDto> selections,
            CurrentUser currentUser)
        {
            throw new NotSupportedException();
        }
    }
}
