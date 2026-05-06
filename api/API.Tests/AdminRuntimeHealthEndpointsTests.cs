using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class AdminRuntimeHealthEndpointsTests
{
    // --- Authorization ---

    [Fact]
    public async Task GetRuntimeHealth_ReturnsUnauthorized_WhenNoUser()
    {
        var app = CreateApp(new StubAdminRuntimeHealthService(), null);
        var endpoint = GetEndpoint(app, "/admin/runtime-health", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/runtime-health");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task GetRuntimeHealth_ReturnsForbidden_WhenNonAdminUser()
    {
        var app = CreateApp(new StubAdminRuntimeHealthService(), CreateNonAdminUser());
        var endpoint = GetEndpoint(app, "/admin/runtime-health", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/runtime-health");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task GetRuntimeHealth_Returns200_WhenAdminUser()
    {
        var stub = new StubAdminRuntimeHealthService
        {
            Health = CreateSampleHealth()
        };

        var app = CreateApp(stub, CreateAdminUser());
        var endpoint = GetEndpoint(app, "/admin/runtime-health", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/runtime-health");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, stub.GetRuntimeHealthCallCount);
    }

    [Fact]
    public async Task GetRuntimeHealth_ReturnsExpectedShape()
    {
        var generatedAt = DateTime.UtcNow;
        var stub = new StubAdminRuntimeHealthService
        {
            Health = CreateSampleHealth(generatedAt)
        };

        var app = CreateApp(stub, CreateAdminUser());
        var endpoint = GetEndpoint(app, "/admin/runtime-health", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/admin/runtime-health");

        await endpoint.RequestDelegate!(context);

        context.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("overallSeverity", out _));
        Assert.True(root.TryGetProperty("application", out _));
        Assert.True(root.TryGetProperty("dependencies", out _));
        Assert.True(root.TryGetProperty("directory", out _));
        Assert.True(root.TryGetProperty("storage", out _));
        Assert.Equal("ok", root.GetProperty("overallSeverity").GetString());
    }

    // --- Severity unit tests ---

    [Theory]
    [InlineData(true, 500L, "ok")]
    [InlineData(true, 1000L, "warning")]
    [InlineData(true, 2500L, "warning")]
    [InlineData(true, 3001L, "critical")]
    [InlineData(false, null, "critical")]
    public void ComputeDatabaseSeverity_MatchesThresholds(bool reachable, long? latencyMs, string expectedSeverity)
    {
        var severity = AdminRuntimeHealthService.ComputeDatabaseSeverity(reachable, latencyMs);
        Assert.Equal(expectedSeverity, severity);
    }

    [Theory]
    [InlineData("reachable", 1000L, "ok")]
    [InlineData("reachable", 2000L, "warning")]
    [InlineData("reachable", 4000L, "warning")]
    [InlineData("reachable", 5001L, "critical")]
    [InlineData("unreachable", null, "critical")]
    public void ComputeAuthSeverity_MatchesThresholds(string reachability, long? latencyMs, string expectedSeverity)
    {
        var severity = AdminRuntimeHealthService.ComputeAuthSeverity(reachability, latencyMs);
        Assert.Equal(expectedSeverity, severity);
    }

    [Theory]
    [InlineData("disabled", "complete", null, "ok")]
    [InlineData("enabled", "complete", "sender@example.com", "ok")]
    [InlineData("sandbox", "complete", "sender@example.com", "warning")]
    [InlineData("enabled", "incomplete", "sender@example.com", "warning")]
    [InlineData("enabled", "incomplete", null, "critical")]
    [InlineData("enabled", "incomplete", "", "critical")]
    public void ComputeMailSeverity_MatchesThresholds(string mode, string configStatus, string? senderEmail, string expected)
    {
        var severity = AdminRuntimeHealthService.ComputeMailSeverity(mode, configStatus, senderEmail);
        Assert.Equal(expected, severity);
    }

    [Theory]
    [InlineData(0L, 1000L, "ok")]
    [InlineData(749L, 1000L, "ok")]
    [InlineData(750L, 1000L, "warning")]
    [InlineData(900L, 1000L, "warning")]
    [InlineData(901L, 1000L, "critical")]
    [InlineData(100L, 0L, "unknown")]
    public void ComputeHeapSeverity_MatchesThresholds(long heapBytes, long thresholdBytes, string expected)
    {
        long? threshold = thresholdBytes > 0 ? thresholdBytes : null;
        var severity = AdminRuntimeHealthService.ComputeHeapSeverity(heapBytes, threshold);
        Assert.Equal(expected, severity);
    }

    [Theory]
    [InlineData(50.0, "ok")]
    [InlineData(79.9, "ok")]
    [InlineData(80.0, "warning")]
    [InlineData(90.0, "warning")]
    [InlineData(90.1, "critical")]
    public void ComputeStorageSeverity_MatchesThresholds(double usedPercent, string expected)
    {
        var severity = AdminRuntimeHealthService.ComputeStorageSeverity(usedPercent);
        Assert.Equal(expected, severity);
    }

    [Fact]
    public void ComputeDirectorySeverity_NeverRun_ReturnsUnknown()
    {
        var severity = AdminRuntimeHealthService.ComputeDirectorySeverity(null, "never_run", 60);
        Assert.Equal("unknown", severity);
    }

    [Fact]
    public void ComputeDirectorySeverity_Failed_ReturnsCritical()
    {
        var severity = AdminRuntimeHealthService.ComputeDirectorySeverity(DateTime.UtcNow.AddMinutes(-30), "failed", 60);
        Assert.Equal("critical", severity);
    }

    [Fact]
    public void ComputeDirectorySeverity_RecentSuccess_ReturnsOk()
    {
        var severity = AdminRuntimeHealthService.ComputeDirectorySeverity(DateTime.UtcNow.AddMinutes(-30), "success", 60);
        Assert.Equal("ok", severity);
    }

    [Fact]
    public void ComputeDirectorySeverity_OldSuccess_ReturnsCritical()
    {
        var severity = AdminRuntimeHealthService.ComputeDirectorySeverity(DateTime.UtcNow.AddHours(-6), "success", 60);
        Assert.Equal("critical", severity);
    }

    [Fact]
    public void ComputeDirectorySeverity_Partial_ReturnsWarning()
    {
        var severity = AdminRuntimeHealthService.ComputeDirectorySeverity(DateTime.UtcNow.AddMinutes(-30), "partial", 60);
        Assert.Equal("warning", severity);
    }

    [Fact]
    public void ComputeDirectorySeverity_SlightlyStale_ReturnsWarning()
    {
        // 3 intervals old — 2x threshold exceeded but not 5x
        var severity = AdminRuntimeHealthService.ComputeDirectorySeverity(DateTime.UtcNow.AddMinutes(-181), "success", 60);
        Assert.Equal("warning", severity);
    }

    [Fact]
    public void AggregateSeverities_CriticalWins()
    {
        var result = AdminRuntimeHealthService.AggregateSeverities("ok", "critical", "warning");
        Assert.Equal("critical", result);
    }

    [Fact]
    public void AggregateSeverities_WarningWithNoCritical()
    {
        var result = AdminRuntimeHealthService.AggregateSeverities("ok", "warning", "ok");
        Assert.Equal("warning", result);
    }

    [Fact]
    public void AggregateSeverities_AllOk()
    {
        var result = AdminRuntimeHealthService.AggregateSeverities("ok", "ok", "ok");
        Assert.Equal("ok", result);
    }

    [Fact]
    public void AggregateSeverities_OkAndUnknown_ReturnsOk()
    {
        var result = AdminRuntimeHealthService.AggregateSeverities("ok", "unknown");
        Assert.Equal("ok", result);
    }

    [Fact]
    public void AggregateSeverities_AllUnknown_ReturnsUnknown()
    {
        var result = AdminRuntimeHealthService.AggregateSeverities("unknown", "unknown");
        Assert.Equal("unknown", result);
    }

    // --- Helpers ---

    private static WebApplication CreateApp(StubAdminRuntimeHealthService healthService, CurrentUser? user)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IAdminRuntimeHealthService>(healthService);
        builder.Services.AddSingleton<IUserContext>(new StubUserContext(user));
        builder.Services.AddSingleton<IAuthorizationPolicyService, AuthorizationPolicyService>();

        var app = builder.Build();
        app.MapAdminRuntimeHealthEndpoints();
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
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        return context;
    }

    private static AdminRuntimeHealthDto CreateSampleHealth(DateTime? generatedAt = null)
    {
        var ts = generatedAt ?? DateTime.UtcNow;
        var application = new ApplicationHealthDto
        {
            Severity = "ok",
            ProcessStartedAt = ts.AddMinutes(-30),
            UptimeSeconds = 1800,
            ManagedHeapBytes = 50_000_000,
            ManagedHeapHighThresholdBytes = 200_000_000,
            WorkingSetBytes = 80_000_000,
            ThreadPool = null
        };
        var database = new DependencyHealthDto
        {
            Severity = "ok",
            Reachable = true,
            LastCheckedAt = ts,
            LatencyMs = 5,
            LastError = null
        };
        var auth = new AuthDependencyHealthDto
        {
            Severity = "ok",
            Mode = "dev-sim",
            Reachability = "not_applicable",
            LastCheckedAt = ts,
            LatencyMs = null,
            LastError = null
        };
        var mail = new MailDependencyHealthDto
        {
            Severity = "ok",
            Mode = "disabled",
            ConfigurationStatus = "complete",
            LastProbeAt = null,
            LastProbeStatus = "never_run"
        };
        var dependencies = new DependenciesHealthDto
        {
            Severity = "ok",
            Database = database,
            Auth = auth,
            Mail = mail
        };
        var directory = new DirectoryHealthDto
        {
            Severity = "ok",
            LastSyncAt = ts.AddMinutes(-15),
            LastSyncStatus = "success",
            LastError = null,
            NextScheduledSyncAt = ts.AddMinutes(45),
            PendingImportsCount = 0
        };

        return new AdminRuntimeHealthDto
        {
            GeneratedAt = ts,
            OverallSeverity = "ok",
            Application = application,
            Dependencies = dependencies,
            Directory = directory,
            Storage = [],
            Host = null
        };
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

    // --- Host memory severity unit tests ---

    [Theory]
    [InlineData(0.0, "ok")]
    [InlineData(84.9, "ok")]
    [InlineData(85.0, "warning")]
    [InlineData(95.0, "warning")]
    [InlineData(95.1, "critical")]
    public void ComputeHostMemorySeverity_MatchesThresholds(double usedPercent, string expected)
    {
        var severity = AdminRuntimeHealthService.ComputeHostMemorySeverity(usedPercent);
        Assert.Equal(expected, severity);
    }

    [Fact]
    public void ComputeOverallSeverity_WithNullHost_ExcludesHost()
    {
        var result = AdminRuntimeHealthService.ComputeOverallSeverity("ok", "ok", "ok", [], "dev-sim", null);
        Assert.Equal("ok", result);
    }

    [Fact]
    public void ComputeOverallSeverity_WithCriticalHost_ReturnsCritical()
    {
        var host = new HostHealthDto
        {
            Severity = "critical",
            UptimeSeconds = 3600,
            LoadAverage1m = 2.0,
            MemTotalBytes = 8_000_000_000L,
            MemAvailableBytes = 200_000_000L,
            MemUsedPercent = 97.5,
            RootFsTotalBytes = 100_000_000_000L,
            RootFsFreeBytes = 5_000_000_000L,
            RootFsUsedPercent = 95.0
        };

        var result = AdminRuntimeHealthService.ComputeOverallSeverity("ok", "ok", "ok", [], "entra", host);
        Assert.Equal("critical", result);
    }

    [Fact]
    public void ComputeOverallSeverity_WithWarningHost_ReturnsWarning()
    {
        var host = new HostHealthDto
        {
            Severity = "warning",
            UptimeSeconds = 3600,
            LoadAverage1m = 1.5,
            MemTotalBytes = 8_000_000_000L,
            MemAvailableBytes = 900_000_000L,
            MemUsedPercent = 88.0,
            RootFsTotalBytes = 100_000_000_000L,
            RootFsFreeBytes = 30_000_000_000L,
            RootFsUsedPercent = 70.0
        };

        var result = AdminRuntimeHealthService.ComputeOverallSeverity("ok", "ok", "ok", [], "entra", host);
        Assert.Equal("warning", result);
    }

    private sealed class StubAdminRuntimeHealthService : IAdminRuntimeHealthService
    {
        public AdminRuntimeHealthDto? Health { get; set; }
        public int GetRuntimeHealthCallCount { get; private set; }

        public Task<AdminRuntimeHealthDto> GetRuntimeHealthAsync(CancellationToken cancellationToken = default)
        {
            GetRuntimeHealthCallCount++;
            return Task.FromResult(Health ?? throw new InvalidOperationException("Health not configured."));
        }
    }
}
