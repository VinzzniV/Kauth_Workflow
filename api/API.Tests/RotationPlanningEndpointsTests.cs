using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class RotationPlanningEndpointsTests
{
    [Fact]
    public async Task RotationAuditEndpoint_RejectsNonPositiveLimit()
    {
        var service = new StubRotationPlanningService();
        var app = CreateApp(service, CreateAdminHrUser());
        var endpoint = GetEndpoint(app, "/rotation/plans/{planId:long}/audit", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/rotation/plans/42/audit",
            "?limit=0",
            ("planId", 42));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(0, service.GetRotationAuditLogCallCount);
    }

    [Fact]
    public async Task RotationAuditEndpoint_RejectsNegativeOffset()
    {
        var service = new StubRotationPlanningService();
        var app = CreateApp(service, CreateAdminHrUser());
        var endpoint = GetEndpoint(app, "/rotation/plans/{planId:long}/audit", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/rotation/plans/42/audit",
            "?offset=-1",
            ("planId", 42));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(0, service.GetRotationAuditLogCallCount);
    }

    [Fact]
    public async Task RotationAuditEndpoint_CapsLimitAndPassesOffsetToService()
    {
        var service = new StubRotationPlanningService
        {
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
        var app = CreateApp(service, CreateAdminHrUser());
        var endpoint = GetEndpoint(app, "/rotation/plans/{planId:long}/audit", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/rotation/plans/42/audit",
            "?limit=999&offset=7",
            ("planId", 42));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, service.GetRotationAuditLogCallCount);
        Assert.Equal(500, service.LastAuditLimit);
        Assert.Equal(7, service.LastAuditOffset);
    }

    [Fact]
    public async Task RotationNotificationsEndpoint_ReturnsNotFoundWhenPlanDoesNotExist()
    {
        var service = new StubRotationPlanningService
        {
            Notifications = null
        };
        var app = CreateApp(service, CreateAdminHrUser());
        var endpoint = GetEndpoint(app, "/rotation/plans/{planId:long}/notifications", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/rotation/plans/42/notifications",
            "",
            ("planId", 42));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal(1, service.GetRotationNotificationsCallCount);
    }

    [Fact]
    public async Task RotationNotificationsEndpoint_ReturnsForbiddenWhenServiceRejectsAccess()
    {
        var service = new StubRotationPlanningService
        {
            GetNotificationsException = new UnauthorizedAccessException("forbidden")
        };
        var app = CreateApp(service, CreateAdminHrUser());
        var endpoint = GetEndpoint(app, "/rotation/plans/{planId:long}/notifications", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/rotation/plans/42/notifications",
            "",
            ("planId", 42));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task CreateRotationStationEndpoint_ReturnsConflictForOverlappingDateRange()
    {
        var service = new StubRotationPlanningService
        {
            CreateStationException = new InvalidOperationException("Die Station überschneidet sich mit der bestehenden Station 7.")
        };
        var app = CreateApp(service, CreateAdminHrUser());
        var endpoint = GetEndpoint(app, "/rotation/plans/{planId:long}/stations", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/rotation/plans/42/stations",
            new RotationStationUpsertRequest
            {
                DepartmentId = 4,
                StartDate = new DateOnly(2026, 6, 15),
                EndDate = new DateOnly(2026, 7, 1),
                OrderIndex = 1,
                Status = "planned"
            },
            ("planId", 42));

        await endpoint.RequestDelegate!(context);

        Assert.True(
            context.Response.StatusCode == StatusCodes.Status409Conflict,
            $"Expected 409, got {context.Response.StatusCode}. Service calls: {service.CreateRotationStationCallCount}. Body: {ReadResponseBody(context)}");
        Assert.Equal(1, service.CreateRotationStationCallCount);
    }

    [Fact]
    public async Task UpdateRotationStationEndpoint_ReturnsConflictForDuplicateOrderIndex()
    {
        var service = new StubRotationPlanningService
        {
            UpdateStationException = new InvalidOperationException("orderIndex 1 wird im Durchlaufplan bereits verwendet.")
        };
        var app = CreateApp(service, CreateAdminHrUser());
        var endpoint = GetEndpoint(app, "/rotation/stations/{stationId:long}", HttpMethods.Put);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Put,
            "/rotation/stations/8",
            new RotationStationUpsertRequest
            {
                DepartmentId = 4,
                StartDate = new DateOnly(2026, 6, 21),
                EndDate = new DateOnly(2026, 7, 1),
                OrderIndex = 1,
                Status = "planned"
            },
            ("stationId", 8));

        await endpoint.RequestDelegate!(context);

        Assert.True(
            context.Response.StatusCode == StatusCodes.Status409Conflict,
            $"Expected 409, got {context.Response.StatusCode}. Service calls: {service.UpdateRotationStationCallCount}. Body: {ReadResponseBody(context)}");
        Assert.Equal(1, service.UpdateRotationStationCallCount);
    }

    private static WebApplication CreateApp(StubRotationPlanningService service, CurrentUser user)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IRotationPlanningService>(service);
        builder.Services.AddSingleton<IUserContext>(new StubUserContext(user));
        builder.Services.AddSingleton<IAuthorizationPolicyService, AuthorizationPolicyService>();
        builder.Services.AddSingleton<IWorkflowCatalogService>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<IRotationTaskGenerationService>(_ => throw new NotSupportedException());

        var app = builder.Build();
        app.MapRotationPlanningEndpoints();
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
        string queryString,
        params (string Key, object Value)[] routeValues)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(queryString);
        foreach (var (key, value) in routeValues)
        {
            context.Request.RouteValues[key] = value.ToString();
        }
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        return context;
    }

    private static DefaultHttpContext CreateJsonRequestContext(
        IServiceProvider services,
        RouteEndpoint endpoint,
        string method,
        string path,
        object body,
        params (string Key, object Value)[] routeValues)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.ContentType = "application/json";
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Request.ContentLength = context.Request.Body.Length;
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new TestRequestBodyDetectionFeature());
        foreach (var (key, value) in routeValues)
        {
            context.Request.RouteValues[key] = value.ToString();
        }

        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        return context;
    }

    private static string ReadResponseBody(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static CurrentUser CreateAdminHrUser()
    {
        var adminRole = new CurrentUserRole
        {
            RoleId = 1,
            RoleKey = AuthorizationRoles.Admin,
            RoleName = AuthorizationRoles.Admin,
            RoleKind = AuthorizationRoles.SystemRoleKind,
            AssignmentSource = "test"
        };
        var hrRole = new CurrentUserRole
        {
            RoleId = 2,
            RoleKey = AuthorizationRoles.Hr,
            RoleName = AuthorizationRoles.Hr,
            RoleKind = AuthorizationRoles.SystemRoleKind,
            AssignmentSource = "test"
        };

        return new CurrentUser
        {
            UserId = 99,
            DisplayName = "Admin HR",
            Email = "admin.hr@example.com",
            IsActive = true,
            IdentityProvider = "test",
            Groups = [],
            DirectRoles = [adminRole, hrRole],
            GroupRoles = [],
            EffectiveRoles = [adminRole, hrRole],
            DirectResponsibilities = [],
            GroupResponsibilities = [],
            EffectiveResponsibilities = []
        };
    }

    private sealed class StubRotationPlanningService : IRotationPlanningService
    {
        public IReadOnlyList<RotationAuditEntryDto>? AuditEntries { get; set; } = [];
        public IReadOnlyList<RotationNotificationDto>? Notifications { get; set; } = [];
        public Exception? GetNotificationsException { get; set; }
        public Exception? CreateStationException { get; set; }
        public Exception? UpdateStationException { get; set; }
        public int GetRotationAuditLogCallCount { get; private set; }
        public int GetRotationNotificationsCallCount { get; private set; }
        public int CreateRotationStationCallCount { get; private set; }
        public int UpdateRotationStationCallCount { get; private set; }
        public int? LastAuditLimit { get; private set; }
        public int? LastAuditOffset { get; private set; }

        public Task<IReadOnlyList<RotationPlanListItemDto>> GetRotationPlansAsync(long? personId, CurrentUser currentUser, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RotationPlanDetailDto?> GetRotationPlanAsync(long planId, CurrentUser currentUser, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<RotationAuditEntryDto>?> GetRotationAuditLogAsync(
            long planId,
            int limit,
            int offset,
            CurrentUser currentUser,
            CancellationToken cancellationToken = default)
        {
            GetRotationAuditLogCallCount += 1;
            LastAuditLimit = limit;
            LastAuditOffset = offset;
            return Task.FromResult(AuditEntries);
        }

        public Task<IReadOnlyList<RotationNotificationDto>?> GetRotationNotificationsAsync(
            long planId,
            int limit,
            int offset,
            CurrentUser currentUser,
            CancellationToken cancellationToken = default)
        {
            GetRotationNotificationsCallCount += 1;
            if (GetNotificationsException is not null)
            {
                throw GetNotificationsException;
            }

            return Task.FromResult(Notifications);
        }

        public Task<RotationPlanDetailDto> CreateRotationPlanAsync(CreateRotationPlanRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<RotationStationDto>?> GetRotationStationsAsync(long planId, CurrentUser currentUser, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RotationStationDto?> CreateRotationStationAsync(long planId, RotationStationUpsertRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default)
        {
            CreateRotationStationCallCount += 1;
            if (CreateStationException is not null)
            {
                throw CreateStationException;
            }

            return Task.FromResult<RotationStationDto?>(null);
        }

        public Task<RotationStationDto?> UpdateRotationStationAsync(long stationId, RotationStationUpsertRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default)
        {
            UpdateRotationStationCallCount += 1;
            if (UpdateStationException is not null)
            {
                throw UpdateStationException;
            }

            return Task.FromResult<RotationStationDto?>(null);
        }

        public Task<bool> DeleteRotationStationAsync(long stationId, CurrentUser currentUser, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StubUserContext(CurrentUser user) : IUserContext
    {
        public Task<CurrentUser?> GetCurrentUser(CancellationToken cancellationToken = default)
            => Task.FromResult<CurrentUser?>(user);
    }

    private sealed class TestRequestBodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }
}
