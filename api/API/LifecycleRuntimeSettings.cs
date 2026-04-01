using Microsoft.Extensions.Configuration;

namespace API;

internal sealed class LifecycleRuntimeSettings
{
    public required string EnvironmentName { get; init; }
    public required bool IsProduction { get; init; }
    public required string AuthMode { get; init; }
    public required bool DevSimulationEnabled { get; init; }
    public required bool EntraAuthEnabled { get; init; }
    public required bool SwaggerEnabled { get; init; }
    public required bool DirectorySyncEnabled { get; init; }
    public string? ConnectionString { get; init; }
    public string? PublicBaseUrl { get; init; }
    public string? EntraTenantId { get; init; }
    public string? EntraClientId { get; init; }
    public string? EntraAudience { get; init; }
    public string? EntraClientSecret { get; init; }
    public string? GraphClientSecret { get; init; }
    public string? DirectoryGroupPrefix { get; init; }
    public string? DirectoryExplicitGroupIds { get; init; }
    public required bool DirectorySyncScheduled { get; init; }
    public required int DirectorySyncIntervalMinutes { get; init; }
    public string? AutoProvisionDefaultRoleKey { get; init; }
}

internal static class LifecycleRuntimeSettingsResolver
{
    private static readonly Lazy<IConfiguration> EnvironmentOnlyConfiguration = new(
        static () => new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build());

    public static LifecycleRuntimeSettings Resolve(IConfiguration configuration)
    {
        var environmentName = Normalize(configuration["ASPNETCORE_ENVIRONMENT"]) ?? "Production";
        var isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
        var authMode = ResolveAuthMode(configuration, isProduction);

        return new LifecycleRuntimeSettings
        {
            EnvironmentName = environmentName,
            IsProduction = isProduction,
            AuthMode = authMode,
            DevSimulationEnabled = string.Equals(authMode, "dev-sim", StringComparison.OrdinalIgnoreCase),
            EntraAuthEnabled = string.Equals(authMode, "entra", StringComparison.OrdinalIgnoreCase),
            SwaggerEnabled = ResolveSwaggerEnabled(configuration, isProduction),
            DirectorySyncEnabled = string.Equals(authMode, "dev-sim", StringComparison.OrdinalIgnoreCase)
                || string.Equals(authMode, "entra", StringComparison.OrdinalIgnoreCase),
            ConnectionString = GetConnectionStringOrNull(configuration),
            PublicBaseUrl = Normalize(configuration["PUBLIC_BASE_URL"]),
            EntraTenantId = Normalize(configuration["ENTRA_TENANT_ID"]),
            EntraClientId = Normalize(configuration["ENTRA_CLIENT_ID"]),
            EntraAudience = Normalize(configuration["ENTRA_AUDIENCE"]),
            EntraClientSecret = Normalize(configuration["ENTRA_CLIENT_SECRET"]),
            GraphClientSecret = Normalize(configuration["GRAPH_CLIENT_SECRET"]),
            DirectoryGroupPrefix = Normalize(configuration["DIRECTORY_GROUP_PREFIX"]),
            DirectoryExplicitGroupIds = Normalize(configuration["DIRECTORY_EXPLICIT_GROUP_IDS"]),
            DirectorySyncScheduled = GetBoolean(configuration["DIRECTORY_SYNC_SCHEDULED"], defaultValue: true),
            DirectorySyncIntervalMinutes = GetPositiveInt(configuration["DIRECTORY_SYNC_INTERVAL_MINUTES"], defaultValue: 15),
            AutoProvisionDefaultRoleKey = Normalize(configuration["AUTO_PROVISION_DEFAULT_ROLE_KEY"])
        };
    }

    public static LifecycleRuntimeSettings ResolveFromEnvironment()
    {
        return Resolve(EnvironmentOnlyConfiguration.Value);
    }

    public static string? GetConnectionStringOrNull(IConfiguration? configuration = null)
    {
        var source = configuration ?? EnvironmentOnlyConfiguration.Value;
        return Normalize(source.GetConnectionString("Default"))
            ?? Normalize(source["CONNECTION_STRING"]);
    }

    public static string GetRequiredConnectionString(IConfiguration? configuration = null)
    {
        var connectionString = GetConnectionStringOrNull(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Use ConnectionStrings__Default (preferred) or CONNECTION_STRING.");
        }

        return connectionString;
    }

    private static string ResolveAuthMode(IConfiguration configuration, bool isProduction)
    {
        var configuredAuthMode = Normalize(configuration["AUTH_MODE"])?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(configuredAuthMode))
        {
            if (configuredAuthMode is "dev-sim" or "entra")
            {
                return configuredAuthMode;
            }

            throw new InvalidOperationException("AUTH_MODE must be one of: dev-sim, entra.");
        }

        return isProduction ? "none" : "dev-sim";
    }

    private static bool GetBoolean(string? value, bool defaultValue)
    {
        return bool.TryParse(Normalize(value), out var parsed)
            ? parsed
            : defaultValue;
    }

    private static bool ResolveSwaggerEnabled(IConfiguration configuration, bool isProduction)
    {
        return GetBoolean(configuration["SWAGGER_ENABLED"], defaultValue: !isProduction);
    }

    private static int GetPositiveInt(string? value, int defaultValue)
    {
        return int.TryParse(Normalize(value), out var parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
