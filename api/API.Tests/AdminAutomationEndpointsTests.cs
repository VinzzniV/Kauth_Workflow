using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class AdminAutomationEndpointsTests
{
    [Fact]
    public async Task ActionCatalogEndpoint_ReturnsConfiguredActions()
    {
        var automationService = new StubWorkflowAutomationService
        {
            ActionDefinitions =
            [
                new ActionDefinitionDto
                {
                    Id = 1,
                    Key = "CreateAdUser",
                    Name = "Create AD User",
                    Description = "Simulated",
                    HandlerType = "simulated_directory",
                    IsSimulated = true,
                    ParameterSchema = JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone(),
                    IsActive = true,
                    RequiresApproval = false,
                    IsIdempotent = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            ]
        };

        var app = CreateApp(automationService);
        var endpoint = GetEndpoint(app, "/admin/config/action-definitions", HttpMethods.Get);
        var context = CreateRequestContext(app.Services, endpoint, HttpMethods.Get, "/admin/config/action-definitions");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, automationService.GetActionDefinitionsCallCount);
    }

    [Theory]
    [InlineData("simulated_directory", true)]
    [InlineData("simulated_mailbox", true)]
    [InlineData("simulated_directory_groups", true)]
    [InlineData("SIMULATED_ERP", true)]
    [InlineData("real_ad_write", false)]
    [InlineData("graph_mail", false)]
    public void ActionDefinitionDto_IsSimulated_DerivedFromHandlerType(string handlerType, bool expectedIsSimulated)
    {
        var dto = new ActionDefinitionDto
        {
            Id = 1,
            Key = "TestAction",
            Name = "Test",
            Description = null,
            HandlerType = handlerType,
            IsSimulated = handlerType.StartsWith("simulated", StringComparison.OrdinalIgnoreCase),
            ParameterSchema = null,
            IsActive = true,
            RequiresApproval = false,
            IsIdempotent = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.Equal(expectedIsSimulated, dto.IsSimulated);
    }

    [Fact]
    public async Task AutomationJobsEndpoint_ReturnsConfiguredJobs()
    {
        var workflowUid = Guid.NewGuid();
        var automationService = new StubWorkflowAutomationService
        {
            Jobs =
            [
                new AutomationJobDetailDto
                {
                    Id = 10,
                    WorkflowId = 42,
                    WorkflowNodeInstanceId = 12,
                    NodeKey = "auto",
                    ActionKey = "CreateAdUser",
                    ActionName = "Create AD User",
                    ExecutionOrder = 10,
                    OnErrorBehavior = "fail_workflow",
                    Status = "pending",
                    Payload = JsonDocument.Parse("""{"employeeNumber":1234}""").RootElement.Clone(),
                    AvailableAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    StartedAt = null,
                    CompletedAt = null,
                    Attempts = new List<AutomationJobAttemptDto>(),
                    Logs = new List<AutomationJobLogDto>()
                }
            ]
        };

        var app = CreateApp(automationService);
        var endpoint = GetEndpoint(app, "/admin/runtime/workflow-instances/{uid:guid}/automation-jobs", HttpMethods.Get);
        var context = CreateRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Get,
            $"/admin/runtime/workflow-instances/{workflowUid}/automation-jobs",
            ("uid", workflowUid));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(workflowUid, automationService.LastWorkflowUid);
    }

    private static WebApplication CreateApp(StubWorkflowAutomationService automationService)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IWorkflowAutomationService>(automationService);
        builder.Services.AddSingleton<IWorkflowRepository>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<IWorkflowDefinitionRuntimeRepository>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<IUserContext>(new StubUserContext(CreateAdminUser()));
        builder.Services.AddSingleton<IAuthorizationPolicyService, AuthorizationPolicyService>();
        builder.Services.AddSingleton<IWorkflowDefinitionRuntimeService>(new StubWorkflowDefinitionRuntimeService());

        var app = builder.Build();
        app.MapAdminWorkflowDefinitionConfigEndpoints();
        app.MapAdminWorkflowRuntimeEndpoints();
        return app;
    }

    private static RouteEndpoint GetEndpoint(WebApplication app, string routePattern, string httpMethod)
    {
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint =>
                endpoint.RoutePattern.RawText == routePattern
                && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(httpMethod) == true);
    }

    private static DefaultHttpContext CreateRequestContext(
        IServiceProvider services,
        RouteEndpoint endpoint,
        string httpMethod,
        string path,
        params (string Key, object Value)[] routeValues)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Method = httpMethod;
        context.Request.Path = path;
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        context.Request.ContentLength = 2;
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new StubHttpRequestBodyDetectionFeature());
        foreach (var (key, value) in routeValues)
        {
            context.Request.RouteValues[key] = value.ToString();
        }

        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        return context;
    }

    private static CurrentUser CreateAdminUser()
    {
        var adminRole = new CurrentUserRole
        {
            RoleId = 1,
            RoleKey = AuthorizationRoles.Admin,
            RoleName = AuthorizationRoles.Admin,
            RoleKind = AuthorizationRoles.SystemRoleKind,
            AssignmentSource = "test",
            GroupId = null,
            GroupKey = null
        };

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
            DirectRoles = [adminRole],
            GroupRoles = new List<CurrentUserRole>(),
            EffectiveRoles = [adminRole],
            DirectResponsibilities = new List<CurrentUserResponsibility>(),
            GroupResponsibilities = new List<CurrentUserResponsibility>(),
            EffectiveResponsibilities = new List<CurrentUserResponsibility>()
        };
    }

    private sealed class StubHttpRequestBodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private sealed class StubUserContext(CurrentUser user) : IUserContext
    {
        public Task<CurrentUser?> GetCurrentUser(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CurrentUser?>(user);
        }
    }

    private sealed class StubWorkflowAutomationService : IWorkflowAutomationService
    {
        public IReadOnlyList<ActionDefinitionDto> ActionDefinitions { get; set; } = [];
        public IReadOnlyList<AutomationJobDetailDto> Jobs { get; set; } = [];
        public int GetActionDefinitionsCallCount { get; private set; }
        public Guid? LastWorkflowUid { get; private set; }

        public Task<AdminListPageDto<ActionDefinitionDto>> GetActionDefinitionsAsync(AdminListQuery query, CancellationToken cancellationToken = default)
        {
            GetActionDefinitionsCallCount += 1;
            return Task.FromResult(new AdminListPageDto<ActionDefinitionDto>
            {
                Items = ActionDefinitions,
                Total = ActionDefinitions.Count,
                Limit = query.Limit,
                Offset = query.Offset
            });
        }

        public Task<CursorPageDto<AutomationJobDetailDto>> GetWorkflowAutomationJobsAsync(Guid workflowUid, CursorPageQuery query, CancellationToken cancellationToken = default)
        {
            LastWorkflowUid = workflowUid;
            return Task.FromResult(new CursorPageDto<AutomationJobDetailDto>
            {
                Items = Jobs,
                HasMore = false,
                NextCursor = null
            });
        }

        public Task<bool> TryProcessNextPendingJobAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class StubWorkflowDefinitionRuntimeService : IWorkflowDefinitionRuntimeService
    {
        public Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowInstanceAsync(CreateWorkflowDefinitionInstanceRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinitionRuntimeDetailDto?> GetWorkflowInstanceAsync(Guid workflowUid, CurrentUser currentUser, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CursorPageDto<WorkflowRuntimeEventDto>> GetWorkflowInstanceEventsAsync(Guid workflowUid, CursorPageQuery query, CurrentUser currentUser, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteFormNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeFormNodeRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteApprovalNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeApprovalNodeRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteTaskNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeTaskNodeRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
