using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace API.Tests;

public sealed class StartupValidationTests
{
    [Fact]
    public void ValidateLifecycleStartup_RejectsNonHttpsPublicBaseUrlInProduction()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["PUBLIC_BASE_URL"] = "http://prod.example.local",
            ["NotificationEmail:FrontendBaseUrl"] = "http://prod.example.local",
            ["Cors:AllowedOrigins:0"] = "http://prod.example.local"
        });

        var app = CreateApp(
            configuration,
            CreateProductionRuntimeSettings(publicBaseUrl: "http://prod.example.local"));

        var exception = Assert.Throws<InvalidOperationException>(() => app.ValidateLifecycleStartup());

        Assert.Contains("PUBLIC_BASE_URL must use https", exception.Message);
    }

    [Fact]
    public void ValidateLifecycleStartup_RejectsNonHttpsCorsOriginInProduction()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["PUBLIC_BASE_URL"] = "https://prod.example.local",
            ["NotificationEmail:FrontendBaseUrl"] = "https://prod.example.local",
            ["Cors:AllowedOrigins:0"] = "http://prod.example.local"
        });

        var app = CreateApp(
            configuration,
            CreateProductionRuntimeSettings(publicBaseUrl: "https://prod.example.local"));

        var exception = Assert.Throws<InvalidOperationException>(() => app.ValidateLifecycleStartup());

        Assert.Contains("Cors:AllowedOrigins must use https", exception.Message);
    }

    private static WebApplication CreateApp(IConfiguration configuration, LifecycleRuntimeSettings runtimeSettings)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(configuration);
        builder.Services.AddSingleton(runtimeSettings);
        return builder.Build();
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static LifecycleRuntimeSettings CreateProductionRuntimeSettings(string publicBaseUrl)
    {
        return new LifecycleRuntimeSettings
        {
            EnvironmentName = "Production",
            IsProduction = true,
            AuthMode = "entra",
            DevSimulationEnabled = false,
            EntraAuthEnabled = true,
            SwaggerEnabled = false,
            DirectorySyncEnabled = false,
            ConnectionString = null,
            PublicBaseUrl = publicBaseUrl,
            EntraTenantId = "tenant-id",
            EntraClientId = "client-id",
            EntraAudience = "api://client-id",
            EntraClientSecret = null,
            GraphClientSecret = null,
            DirectoryGroupPrefix = null,
            DirectoryExplicitGroupIds = null,
            DirectorySyncScheduled = true,
            DirectorySyncIntervalMinutes = 15,
            AutoProvisionDefaultRoleKey = null
        };
    }
}
