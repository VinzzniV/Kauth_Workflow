using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class AdminWorkflowRuntimeEndpointsTests
{
    [Fact]
    public async Task CreateRuntimeWorkflowEndpoint_ReturnsCreated()
    {
        var workflowUid = Guid.NewGuid();
        var runtimeService = new StubWorkflowDefinitionRuntimeService
        {
            CreatedInstance = CreateRuntimeDetail(workflowUid)
        };

        var app = CreateApp(runtimeService);
        var endpoint = GetEndpoint(app, "/admin/runtime/workflow-instances", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/admin/runtime/workflow-instances",
            new CreateWorkflowDefinitionInstanceRequest
            {
                WorkflowDefinitionKey = "hr-onboarding",
                DepartmentId = 2,
                RoleId = 5,
                FirstName = "Ada",
                LastName = "Lovelace",
                EmployeeNumber = 1234,
                BadgeNumber = 99
            });

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
        Assert.Equal(1, runtimeService.CreateCallCount);
        Assert.Equal("hr-onboarding", runtimeService.LastCreateRequest?.WorkflowDefinitionKey);
    }

    [Fact]
    public async Task GetRuntimeWorkflowEndpoint_ReturnsNotFoundForUnknownInstance()
    {
        var app = CreateApp(new StubWorkflowDefinitionRuntimeService());
        var endpoint = GetEndpoint(app, "/admin/runtime/workflow-instances/{uid:guid}", HttpMethods.Get);
        var uid = Guid.NewGuid();
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            $"/admin/runtime/workflow-instances/{uid}",
            ("uid", uid));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task CompleteFormNodeEndpoint_ReturnsBadRequestForInvalidNodeState()
    {
        var runtimeService = new StubWorkflowDefinitionRuntimeService
        {
            CompleteFormException = new InvalidOperationException("Workflow node instance is not active.")
        };

        var app = CreateApp(runtimeService);
        var endpoint = GetEndpoint(
            app,
            "/admin/runtime/workflow-instances/{uid:guid}/nodes/{nodeInstanceId:long}/form-completions",
            HttpMethods.Post);
        var uid = Guid.NewGuid();
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            $"/admin/runtime/workflow-instances/{uid}/nodes/15/form-completions",
            new CompleteRuntimeFormNodeRequest
            {
                RequirementSelections = new List<RequirementSelectionInputDto>()
            },
            ("uid", uid),
            ("nodeInstanceId", 15L));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(1, runtimeService.CompleteFormCallCount);
    }

    private static WebApplication CreateApp(StubWorkflowDefinitionRuntimeService runtimeService)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IWorkflowDefinitionRuntimeService>(runtimeService);
        builder.Services.AddSingleton<IUserContext>(new StubUserContext(CreateAdminUser()));
        builder.Services.AddSingleton<IAuthorizationPolicyService, AuthorizationPolicyService>();

        var app = builder.Build();
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

    private static DefaultHttpContext CreateGetRequestContext(
        IServiceProvider services,
        RouteEndpoint endpoint,
        string path,
        params (string Key, object Value)[] routeValues)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        foreach (var (key, value) in routeValues)
        {
            context.Request.RouteValues[key] = value.ToString();
        }

        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        return context;
    }

    private static DefaultHttpContext CreateJsonRequestContext<TBody>(
        IServiceProvider services,
        RouteEndpoint endpoint,
        string httpMethod,
        string path,
        TBody body,
        params (string Key, object Value)[] routeValues)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Method = httpMethod;
        context.Request.Path = path;
        context.Request.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(body);
        var bytes = Encoding.UTF8.GetBytes(payload);
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new StubHttpRequestBodyDetectionFeature());
        foreach (var (key, value) in routeValues)
        {
            context.Request.RouteValues[key] = value.ToString();
        }

        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        return context;
    }

    private static WorkflowDefinitionRuntimeDetailDto CreateRuntimeDetail(Guid workflowUid)
    {
        return new WorkflowDefinitionRuntimeDetailDto
        {
            WorkflowId = 42,
            WorkflowUid = workflowUid,
            WorkflowDefinitionKey = "hr-onboarding",
            WorkflowDefinitionName = "HR Onboarding",
            WorkflowDefinitionVersionId = 12,
            WorkflowDefinitionVersionNumber = 1,
            CurrentRuntimeStatus = "waiting_on_node",
            DepartmentId = 2,
            RoleId = 5,
            FirstName = "Ada",
            LastName = "Lovelace",
            EmployeeNumber = 1234,
            BadgeNumber = 99,
            TargetPersonId = null,
            DeadlineDate = null,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            CompletedAt = null,
            NodeInstances = new List<WorkflowNodeInstanceDto>()
        };
    }

    private sealed class StubHttpRequestBodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
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

    private sealed class StubUserContext(CurrentUser user) : IUserContext
    {
        public Task<CurrentUser?> GetCurrentUser(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CurrentUser?>(user);
        }
    }

    private sealed class StubWorkflowDefinitionRuntimeService : IWorkflowDefinitionRuntimeService
    {
        public WorkflowDefinitionRuntimeDetailDto? CreatedInstance { get; set; }
        public Exception? CompleteFormException { get; set; }
        public int CreateCallCount { get; private set; }
        public int CompleteFormCallCount { get; private set; }
        public CreateWorkflowDefinitionInstanceRequest? LastCreateRequest { get; private set; }

        public Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowInstanceAsync(
            CreateWorkflowDefinitionInstanceRequest request,
            CurrentUser currentUser,
            CancellationToken cancellationToken = default)
        {
            _ = currentUser;
            CreateCallCount += 1;
            LastCreateRequest = request;
            return Task.FromResult(CreatedInstance ?? throw new InvalidOperationException("No created instance configured."));
        }

        public Task<WorkflowDefinitionRuntimeDetailDto?> GetWorkflowInstanceAsync(
            Guid workflowUid,
            CurrentUser currentUser,
            CancellationToken cancellationToken = default)
        {
            _ = workflowUid;
            _ = currentUser;
            return Task.FromResult<WorkflowDefinitionRuntimeDetailDto?>(null);
        }

        public Task<CursorPageDto<WorkflowRuntimeEventDto>> GetWorkflowInstanceEventsAsync(
            Guid workflowUid,
            CursorPageQuery query,
            CurrentUser currentUser,
            CancellationToken cancellationToken = default)
        {
            _ = workflowUid;
            _ = query;
            _ = currentUser;
            return Task.FromResult(new CursorPageDto<WorkflowRuntimeEventDto>
            {
                Items = [],
                HasMore = false,
                NextCursor = null
            });
        }

        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteFormNodeAsync(
            Guid workflowUid,
            long nodeInstanceId,
            CompleteRuntimeFormNodeRequest request,
            CurrentUser currentUser,
            CancellationToken cancellationToken = default)
        {
            _ = workflowUid;
            _ = nodeInstanceId;
            _ = request;
            _ = currentUser;
            CompleteFormCallCount += 1;
            if (CompleteFormException is not null)
            {
                throw CompleteFormException;
            }

            return Task.FromResult<WorkflowDefinitionRuntimeDetailDto?>(CreatedInstance);
        }

        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteApprovalNodeAsync(
            Guid workflowUid,
            long nodeInstanceId,
            CompleteRuntimeApprovalNodeRequest request,
            CurrentUser currentUser,
            CancellationToken cancellationToken = default)
        {
            _ = workflowUid;
            _ = nodeInstanceId;
            _ = request;
            _ = currentUser;
            return Task.FromResult<WorkflowDefinitionRuntimeDetailDto?>(CreatedInstance);
        }

        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteTaskNodeAsync(
            Guid workflowUid,
            long nodeInstanceId,
            CompleteRuntimeTaskNodeRequest request,
            CurrentUser currentUser,
            CancellationToken cancellationToken = default)
        {
            _ = workflowUid;
            _ = nodeInstanceId;
            _ = request;
            _ = currentUser;
            return Task.FromResult<WorkflowDefinitionRuntimeDetailDto?>(CreatedInstance);
        }
    }
}
