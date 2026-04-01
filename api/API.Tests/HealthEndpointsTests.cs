using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task HealthLive_Returns200Ok()
    {
        var app = CreateApp(CreateRuntimeSettings(), _ => new HttpResponseMessage(HttpStatusCode.OK));
        var endpoint = GetEndpoint(app, "/health/live", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/health/live");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var document = await ReadResponseJsonAsync(context);
        Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HealthReady_Returns503WhenDatabaseIsNotConfigured()
    {
        var app = CreateApp(CreateRuntimeSettings(), _ => new HttpResponseMessage(HttpStatusCode.OK));
        var endpoint = GetEndpoint(app, "/health/ready", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/health/ready");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        using var document = await ReadResponseJsonAsync(context);
        Assert.Equal("degraded", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("not_configured", document.RootElement.GetProperty("database").GetString());
    }

    [Fact]
    public async Task Health_Returns200EvenWhenExternalEntraCheckFails()
    {
        var app = CreateApp(
            CreateRuntimeSettings(
                authMode: "entra",
                devSimulationEnabled: false,
                entraAuthEnabled: true,
                entraTenantId: "tenant-id",
                entraClientId: "client-id",
                entraAudience: "api://client-id"),
            _ => throw new HttpRequestException("Simulated network failure."));
        var endpoint = GetEndpoint(app, "/health", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/health");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var document = await ReadResponseJsonAsync(context);
        Assert.Equal("degraded", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("not_configured", document.RootElement.GetProperty("database").GetString());

        var auth = document.RootElement.GetProperty("auth");
        Assert.Equal("entra", auth.GetProperty("mode").GetString());
        Assert.Equal("enabled", auth.GetProperty("status").GetString());
        Assert.Equal("unreachable", auth.GetProperty("reachability").GetString());
    }

    [Fact]
    public void ValidateLifecycleRouteRegistration_AcceptsHealthReadyAsCriticalRoute()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(CreateRuntimeSettings());
        builder.Services.AddHttpClient("health")
            .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var app = builder.Build();
        app.MapLifecycleHealthEndpoints();
        app.MapGet("/me", () => Results.Ok());
        app.MapGet("/auth/current-user", () => Results.Ok());

        var exception = Record.Exception(() => app.ValidateLifecycleRouteRegistration());

        Assert.Null(exception);
    }

    private static WebApplication CreateApp(
        LifecycleRuntimeSettings runtimeSettings,
        Func<HttpRequestMessage, HttpResponseMessage> sendAsync)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(runtimeSettings);
        builder.Services.AddHttpClient("health")
            .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(sendAsync));

        var app = builder.Build();
        app.MapLifecycleHealthEndpoints();
        return app;
    }

    private static LifecycleRuntimeSettings CreateRuntimeSettings(
        string authMode = "dev-sim",
        bool devSimulationEnabled = true,
        bool entraAuthEnabled = false,
        string? connectionString = null,
        string? entraTenantId = null,
        string? entraClientId = null,
        string? entraAudience = null)
    {
        return new LifecycleRuntimeSettings
        {
            EnvironmentName = "Development",
            IsProduction = false,
            AuthMode = authMode,
            DevSimulationEnabled = devSimulationEnabled,
            EntraAuthEnabled = entraAuthEnabled,
            SwaggerEnabled = true,
            DirectorySyncEnabled = true,
            ConnectionString = connectionString,
            PublicBaseUrl = "http://localhost:5173",
            EntraTenantId = entraTenantId,
            EntraClientId = entraClientId,
            EntraAudience = entraAudience,
            EntraClientSecret = null,
            GraphClientSecret = null,
            DirectoryGroupPrefix = null,
            DirectoryExplicitGroupIds = null,
            DirectorySyncScheduled = true,
            DirectorySyncIntervalMinutes = 15,
            AutoProvisionDefaultRoleKey = null
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

    private static async Task<JsonDocument> ReadResponseJsonAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(sendAsync(request));
        }
    }
}
