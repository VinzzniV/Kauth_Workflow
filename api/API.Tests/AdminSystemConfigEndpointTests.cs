using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class AdminSystemConfigEndpointTests
{
    [Fact]
    public async Task GetSystemConfig_ReturnsUnauthorized_WhenNoUser()
    {
        var app = CreateApp(null, BuildRuntimeSettings(), BuildEmailOptions(), vaultKey: null);
        var endpoint = GetEndpoint(app, "/admin/system/config", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/system/config");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task GetSystemConfig_ReturnsForbidden_WhenNonAdminUser()
    {
        var app = CreateApp(CreateNonAdminUser(), BuildRuntimeSettings(), BuildEmailOptions(), vaultKey: null);
        var endpoint = GetEndpoint(app, "/admin/system/config", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/system/config");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task GetSystemConfig_ReturnsOk_WhenAdminUser()
    {
        var app = CreateApp(CreateAdminUser(), BuildRuntimeSettings(), BuildEmailOptions(), vaultKey: "any-value");
        var endpoint = GetEndpoint(app, "/admin/system/config", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/system/config");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task GetSystemConfig_RedactsSecrets()
    {
        var runtime = BuildRuntimeSettings(entraClientSecret: "real-client-secret-please-do-not-leak");

        var app = CreateApp(CreateAdminUser(), runtime, BuildEmailOptions(), vaultKey: "real-vault-key");
        var endpoint = GetEndpoint(app, "/admin/system/config", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/system/config");

        await endpoint.RequestDelegate!(context);

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        var root = doc.RootElement;

        var entra = root.GetProperty("entra");
        Assert.Equal("set", entra.GetProperty("clientSecretStatus").GetString());
        Assert.Equal("unset", entra.GetProperty("graphClientSecretStatus").GetString());
        Assert.Equal("present", root.GetProperty("vault").GetProperty("keyStatus").GetString());

        // Klartext-Secrets duerfen nirgendwo im Payload auftauchen.
        var raw = root.GetRawText();
        Assert.DoesNotContain("real-client-secret-please-do-not-leak", raw);
        Assert.DoesNotContain("real-vault-key", raw);
    }

    [Fact]
    public async Task GetSystemConfig_VaultMissing_ReportsMissingStatus()
    {
        var app = CreateApp(CreateAdminUser(), BuildRuntimeSettings(), BuildEmailOptions(), vaultKey: null);
        var endpoint = GetEndpoint(app, "/admin/system/config", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/system/config");

        await endpoint.RequestDelegate!(context);

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("missing", doc.RootElement.GetProperty("vault").GetProperty("keyStatus").GetString());
    }

    [Fact]
    public async Task GetSystemConfig_IncludesAllSections()
    {
        var app = CreateApp(CreateAdminUser(), BuildRuntimeSettings(), BuildEmailOptions(), vaultKey: "k");
        var endpoint = GetEndpoint(app, "/admin/system/config", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/system/config");

        await endpoint.RequestDelegate!(context);

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("environment", out _));
        Assert.True(root.TryGetProperty("authMode", out _));
        Assert.True(root.TryGetProperty("publicBaseUrl", out _));
        Assert.True(root.TryGetProperty("entra", out _));
        Assert.True(root.TryGetProperty("directory", out _));
        Assert.True(root.TryGetProperty("email", out _));
        Assert.True(root.TryGetProperty("vault", out _));
        Assert.True(root.TryGetProperty("retry", out _));
        Assert.True(root.TryGetProperty("workerLease", out _));
        Assert.True(root.TryGetProperty("hostHealth", out _));
    }

    // --- Helpers ---

    private static WebApplication CreateApp(
        CurrentUser? user,
        LifecycleRuntimeSettings runtimeSettings,
        NotificationEmailOptions emailOptions,
        string? vaultKey)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(runtimeSettings);
        builder.Services.AddSingleton(new WorkflowAutomationRetrySettings());
        builder.Services.AddSingleton(new WorkerLeaseSettings());
        builder.Services.AddSingleton<IOptions<NotificationEmailOptions>>(Options.Create(emailOptions));

        var inMemory = new Dictionary<string, string?>();
        if (vaultKey is not null) inMemory["KAUTH_VAULT_KEY"] = vaultKey;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        builder.Services.AddSingleton<IConfiguration>(configuration);

        builder.Services.AddSingleton<ISystemConfigSnapshotService, SystemConfigSnapshotService>();
        builder.Services.AddSingleton<IUserContext>(new StubUserContext(user));
        builder.Services.AddSingleton<IAuthorizationPolicyService, AuthorizationPolicyService>();

        var app = builder.Build();
        app.MapAdminSystemConfigEndpoints();
        return app;
    }

    private static LifecycleRuntimeSettings BuildRuntimeSettings(
        string? entraClientSecret = null,
        string? graphClientSecret = null)
    {
        return new LifecycleRuntimeSettings
        {
            EnvironmentName = "Test",
            IsProduction = false,
            AuthMode = "dev-sim",
            DevSimulationEnabled = true,
            EntraAuthEnabled = false,
            SwaggerEnabled = true,
            DirectorySyncEnabled = true,
            ConnectionString = "Host=localhost",
            PublicBaseUrl = "http://localhost:5173",
            EntraTenantId = null,
            EntraClientId = null,
            EntraAudience = null,
            EntraClientSecret = entraClientSecret,
            GraphClientSecret = graphClientSecret,
            DirectoryGroupPrefix = "Onboarding-App-",
            DirectoryExplicitGroupIds = null,
            DirectorySyncScheduled = true,
            DirectorySyncIntervalMinutes = 60,
            AutoProvisionDefaultRoleKey = null,
            RuntimeHealthStoragePaths = null,
            HostRuntimeHealthEnabled = false,
            HostRuntimeProcfsPath = null,
            HostRuntimeRootPath = null
        };
    }

    private static NotificationEmailOptions BuildEmailOptions()
    {
        return new NotificationEmailOptions
        {
            Enabled = false,
            Provider = "MicrosoftGraph",
            SenderEmail = null,
            FrontendBaseUrl = "http://localhost:5173",
            SaveToSentItems = true
        };
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
            Groups = [],
            DirectRoles = [adminRole],
            GroupRoles = [],
            EffectiveRoles = [adminRole],
            DirectResponsibilities = [],
            GroupResponsibilities = [],
            EffectiveResponsibilities = []
        };
    }

    private static CurrentUser CreateNonAdminUser()
    {
        return new CurrentUser
        {
            UserId = 50,
            ExternalKey = "regular.user",
            DisplayName = "Regular User",
            Email = "regular.user@example.com",
            IsActive = true,
            DepartmentId = null,
            DepartmentName = null,
            IdentityProvider = "test",
            Groups = [],
            DirectRoles = [],
            GroupRoles = [],
            EffectiveRoles = [],
            DirectResponsibilities = [],
            GroupResponsibilities = [],
            EffectiveResponsibilities = []
        };
    }

    private sealed class StubUserContext(CurrentUser? user) : IUserContext
    {
        public Task<CurrentUser?> GetCurrentUser(CancellationToken cancellationToken = default)
            => Task.FromResult(user);
    }
}
