using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class AdminPeopleEndpointsTests
{
    // --- Authorization ---

    [Fact]
    public async Task GetPeopleDirectory_ReturnsUnauthorized_WhenNoUser()
    {
        var app = CreateApp(new StubWorkflowCatalogService(), null);
        var endpoint = GetEndpoint(app, "/admin/people", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/people");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(AuthorizationRoles.Worker)]
    [InlineData(AuthorizationRoles.Reader)]
    [InlineData(AuthorizationRoles.Manager)]
    public async Task GetPeopleDirectory_ReturnsForbidden_ForNonHrAdminRoles(string role)
    {
        var app = CreateApp(new StubWorkflowCatalogService(), CreateUserWithRole(role));
        var endpoint = GetEndpoint(app, "/admin/people", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/people");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(AuthorizationRoles.Admin)]
    [InlineData(AuthorizationRoles.Hr)]
    public async Task GetPeopleDirectory_Returns200_ForHrAndAdmin(string role)
    {
        var stub = new StubWorkflowCatalogService
        {
            PeopleDirectoryPage = new AdminListPageDto<PersonDirectoryItemDto>
            {
                Items = new List<PersonDirectoryItemDto>(),
                Total = 0,
                Limit = 50,
                Offset = 0
            }
        };

        var app = CreateApp(stub, CreateUserWithRole(role));
        var endpoint = GetEndpoint(app, "/admin/people", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/people");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, stub.GetPeopleDirectoryCallCount);
    }

    [Fact]
    public async Task GetPeopleDirectory_ReturnsExpectedShape()
    {
        var stub = new StubWorkflowCatalogService
        {
            PeopleDirectoryPage = new AdminListPageDto<PersonDirectoryItemDto>
            {
                Items = new List<PersonDirectoryItemDto>
                {
                    new PersonDirectoryItemDto
                    {
                        PersonId = 7,
                        DisplayName = "Max Mustermann",
                        DepartmentId = 2,
                        DepartmentName = "IT",
                        EmploymentStatus = "active",
                        DirectoryLinkStatus = "linked"
                    }
                },
                Total = 1,
                Limit = 50,
                Offset = 0
            }
        };

        var app = CreateApp(stub, CreateUserWithRole(AuthorizationRoles.Admin));
        var endpoint = GetEndpoint(app, "/admin/people", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/people");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("items", out var items));
        Assert.True(root.TryGetProperty("total", out _));
        Assert.True(root.TryGetProperty("limit", out _));
        Assert.True(root.TryGetProperty("offset", out _));
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(7, items[0].GetProperty("personId").GetInt64());
        Assert.Equal("Max Mustermann", items[0].GetProperty("displayName").GetString());
    }

    // --- Helpers ---

    private static WebApplication CreateApp(StubWorkflowCatalogService catalogService, CurrentUser? user)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IWorkflowCatalogService>(catalogService);
        builder.Services.AddSingleton<IUserContext>(new StubUserContext(user));
        builder.Services.AddSingleton<IAuthorizationPolicyService, AuthorizationPolicyService>();

        var app = builder.Build();
        app.MapAdminPeopleEndpoints();
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
        string path)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        return context;
    }

    private static CurrentUser CreateUserWithRole(string roleKey)
    {
        var role = new CurrentUserRole
        {
            RoleId = 1,
            RoleKey = roleKey,
            RoleName = roleKey,
            RoleKind = AuthorizationRoles.SystemRoleKind,
            AssignmentSource = "test",
            GroupId = null,
            GroupKey = null
        };

        return new CurrentUser
        {
            UserId = 42,
            ExternalKey = "test.user",
            DisplayName = "Test User",
            Email = "test@example.com",
            IsActive = true,
            DepartmentId = null,
            DepartmentName = null,
            IdentityProvider = "test",
            Groups = new List<CurrentUserGroup>(),
            DirectRoles = [role],
            GroupRoles = new List<CurrentUserRole>(),
            EffectiveRoles = [role],
            DirectResponsibilities = new List<CurrentUserResponsibility>(),
            GroupResponsibilities = new List<CurrentUserResponsibility>(),
            EffectiveResponsibilities = new List<CurrentUserResponsibility>()
        };
    }

    private sealed class StubUserContext(CurrentUser? user) : IUserContext
    {
        public Task<CurrentUser?> GetCurrentUser(CancellationToken cancellationToken = default)
            => Task.FromResult(user);
    }

    private sealed class StubWorkflowCatalogService : IWorkflowCatalogService
    {
        public AdminListPageDto<PersonDirectoryItemDto>? PeopleDirectoryPage { get; set; }
        public int GetPeopleDirectoryCallCount { get; private set; }

        public Task<AdminListPageDto<PersonDirectoryItemDto>> GetPeopleDirectoryAsync(
            AdminListQuery query,
            CancellationToken cancellationToken = default)
        {
            GetPeopleDirectoryCallCount += 1;
            return Task.FromResult(PeopleDirectoryPage ?? new AdminListPageDto<PersonDirectoryItemDto>
            {
                Items = new List<PersonDirectoryItemDto>(),
                Total = 0,
                Limit = query.Limit,
                Offset = query.Offset
            });
        }

        public Task<AdminListPageDto<DepartmentDto>> GetDepartmentsAsync(AdminListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminListPageDto<RoleDto>> GetRolesAsync(AdminListQuery query, CurrentUser currentUser, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkflowStartableDefinitionDto>> GetStartableWorkflowDefinitionsAsync(CurrentUser currentUser, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkflowTargetPersonSourceDto>> SearchWorkflowTargetPersonSourcesAsync(string? search, CurrentUser currentUser, int limit = 20, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkflowTargetPersonDto>> SearchWorkflowTargetPeopleAsync(string? search, CurrentUser currentUser, int limit = 20, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkflowTargetPersonDto>> SearchRotationEligiblePeopleAsync(string? search, CurrentUser currentUser, int limit = 20, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RequirementDto>> GetRequirementsAsync(string? workflowDefinitionKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkflowConfigDto?> GetWorkflowConfigAsync(int? roleId, string? workflowDefinitionKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminListPageDto<UnlinkedDirectoryIdentityDto>> GetUnlinkedDirectoryIdentitiesAsync(string? departmentFilter, bool? onlyEnabled, int limit, int offset, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ImportPeopleFromDirectoryResultDto> ImportPeopleFromDirectoryAsync(ImportPeopleFromDirectoryRequest request, long? actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdatePersonAsync(long personId, UpdatePersonRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
