using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace API;

internal static class LifecycleStartupValidationExtensions
{
    private const string OnboardingProcessTypeKey = "onboarding";

    public static WebApplication ValidateLifecycleStartup(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var configuration = app.Services.GetRequiredService<IConfiguration>();
        var runtimeSettings = app.Services.GetRequiredService<LifecycleRuntimeSettings>();
        var isProduction = runtimeSettings.IsProduction;
        var devSimulationActive = runtimeSettings.DevSimulationEnabled;
        var entraEnabled = runtimeSettings.EntraAuthEnabled;

        if (isProduction && !entraEnabled)
        {
            logger.LogWarning(
                "ASPNETCORE_ENVIRONMENT is Production but AUTH_MODE is not entra. " +
                "No productive authentication is configured. The application will reject all requests.");
        }

        if (isProduction && devSimulationActive)
        {
            // This should not happen because the service collection already blocks non-Entra auth in Production,
            // but log defensively in case the logic is changed later.
            logger.LogWarning(
                "Development simulation auth is unexpectedly active in Production. This is a security risk.");
        }

        if (!isProduction && devSimulationActive)
        {
            logger.LogInformation("Development simulation auth endpoints are active (non-production environment).");
        }

        ValidateEntraConfiguration(logger, runtimeSettings);
        ValidateProductionPublicUrls(configuration, runtimeSettings, logger);
        ValidateDatabaseConfigurationAsync(logger, runtimeSettings).GetAwaiter().GetResult();
        return app;
    }

    private static void ValidateEntraConfiguration(ILogger logger, LifecycleRuntimeSettings runtimeSettings)
    {
        if (!runtimeSettings.DirectorySyncEnabled && !runtimeSettings.EntraAuthEnabled)
        {
            return;
        }

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(runtimeSettings.EntraTenantId))
        {
            missing.Add("ENTRA_TENANT_ID");
        }

        if (string.IsNullOrWhiteSpace(runtimeSettings.EntraClientId))
        {
            missing.Add("ENTRA_CLIENT_ID");
        }

        if (runtimeSettings.EntraAuthEnabled && string.IsNullOrWhiteSpace(runtimeSettings.EntraAudience))
        {
            missing.Add("ENTRA_AUDIENCE");
        }

        if (runtimeSettings.DirectorySyncEnabled
            && string.IsNullOrWhiteSpace(runtimeSettings.EntraClientSecret)
            && string.IsNullOrWhiteSpace(runtimeSettings.GraphClientSecret))
        {
            missing.Add("ENTRA_CLIENT_SECRET/GRAPH_CLIENT_SECRET");
        }

        if (missing.Count == 0)
        {
            logger.LogInformation("Startup validation passed: Entra directory/auth configuration is present.");
            return;
        }

        throw new InvalidOperationException(
            $"The current auth mode depends on Entra directory/auth configuration, but required settings are missing: {string.Join(", ", missing)}. Startup aborted.");
    }

    private static void ValidateProductionPublicUrls(
        IConfiguration configuration,
        LifecycleRuntimeSettings runtimeSettings,
        ILogger logger)
    {
        if (!runtimeSettings.IsProduction)
        {
            return;
        }

        var publicBaseUrl = NormalizeConfiguredOrigin(
            runtimeSettings.PublicBaseUrl,
            "PUBLIC_BASE_URL");
        var frontendBaseUrl = NormalizeConfiguredOrigin(
            configuration[$"{NotificationEmailOptions.SectionName}:FrontendBaseUrl"],
            "NotificationEmail:FrontendBaseUrl");

        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => NormalizeConfiguredOrigin(origin, "Cors:AllowedOrigins"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];

        if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "Production startup requires at least one non-local CORS origin.");
        }

        if (!allowedOrigins.Contains(publicBaseUrl, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"PUBLIC_BASE_URL '{publicBaseUrl}' must also be configured in Cors:AllowedOrigins. Startup aborted.");
        }

        if (!string.Equals(frontendBaseUrl, publicBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "NotificationEmail:FrontendBaseUrl must match PUBLIC_BASE_URL in Production. Startup aborted.");
        }

        logger.LogInformation(
            "Startup validation passed: PUBLIC_BASE_URL, CORS origins and notification frontend base URL are production-safe.");
    }

    private static string NormalizeConfiguredOrigin(string? value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Production startup requires {settingName} to be set to an absolute non-local origin.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                $"{settingName} must be an absolute URL. Value '{value}' is invalid. Startup aborted.");
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                $"{settingName} must not contain query strings or fragments. Startup aborted.");
        }

        if (uri.AbsolutePath is not "/" and not "")
        {
            throw new InvalidOperationException(
                $"{settingName} must be configured as an origin without path suffix. Startup aborted.");
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{settingName} must not point to localhost in Production. Startup aborted.");
        }

        if (IPAddress.TryParse(uri.Host, out var ipAddress) && IPAddress.IsLoopback(ipAddress))
        {
            throw new InvalidOperationException(
                $"{settingName} must not point to a loopback address in Production. Startup aborted.");
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static async Task ValidateDatabaseConfigurationAsync(
        ILogger logger,
        LifecycleRuntimeSettings runtimeSettings)
    {
        if (string.IsNullOrWhiteSpace(runtimeSettings.ConnectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
        }

        NpgsqlConnection connection;
        try
        {
            connection = new NpgsqlConnection(runtimeSettings.ConnectionString);
            await connection.OpenAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Startup validation skipped: could not connect to database. " +
                "The application will start but may not function correctly.");
            return;
        }

        await using (connection)
        {
            await ValidateSupervisorConfigurationAsync(connection, logger);
            await ValidateNotificationFrontendBaseUrlAsync(connection, logger, runtimeSettings);
        }
    }

    private static async Task ValidateSupervisorConfigurationAsync(NpgsqlConnection connection, ILogger logger)
    {
        const string sql = @"
SELECT pt.requires_supervisor_step, pt.approval_task_template_key
FROM process_types pt
WHERE pt.key = @processTypeKey
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("processTypeKey", OnboardingProcessTypeKey);

        bool requiresSupervisorStep;
        string? approvalTaskTemplateKey;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException(
                    "Required process type 'onboarding' is missing in process_types. Startup aborted.");
            }

            requiresSupervisorStep = reader.GetBoolean(0);
            approvalTaskTemplateKey = reader.IsDBNull(1) ? null : reader.GetString(1);
        }

        if (!requiresSupervisorStep)
        {
            logger.LogInformation("Startup validation passed: supervisor step not required for 'onboarding'.");
            return;
        }

        if (string.IsNullOrWhiteSpace(approvalTaskTemplateKey))
        {
            throw new InvalidOperationException(
                "Process type 'onboarding' requires a supervisor step but has no approval_task_template_key configured. Startup aborted.");
        }

        const string taskTemplateSql = @"
SELECT EXISTS(
    SELECT 1
    FROM task_templates tt
    JOIN process_types pt ON pt.id = tt.process_type_id
    WHERE pt.key = @processTypeKey
      AND tt.template_key = @taskKey
      AND tt.is_active = TRUE
);";

        await using var taskTemplateCommand = new NpgsqlCommand(taskTemplateSql, connection);
        taskTemplateCommand.Parameters.AddWithValue("processTypeKey", OnboardingProcessTypeKey);
        taskTemplateCommand.Parameters.AddWithValue("taskKey", approvalTaskTemplateKey);

        var exists = (bool)(await taskTemplateCommand.ExecuteScalarAsync() ?? false);
        if (!exists)
        {
            throw new InvalidOperationException(
                $"Configured approval task template '{approvalTaskTemplateKey}' for process type 'onboarding' is missing in task_templates. Startup aborted.");
        }

        logger.LogInformation(
            "Startup validation passed: approval task template '{Key}' found.", approvalTaskTemplateKey);
    }

    private static async Task ValidateNotificationFrontendBaseUrlAsync(
        NpgsqlConnection connection,
        ILogger logger,
        LifecycleRuntimeSettings runtimeSettings)
    {
        if (!runtimeSettings.IsProduction)
        {
            return;
        }

        const string sql = @"
SELECT frontend_base_url
FROM notification_email_settings
WHERE id = 1
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection);
        var storedFrontendBaseUrl = await command.ExecuteScalarAsync() as string;
        if (string.IsNullOrWhiteSpace(storedFrontendBaseUrl))
        {
            logger.LogInformation("Startup validation passed: notification email frontend base URL not stored yet.");
            return;
        }

        var publicBaseUrl = NormalizeConfiguredOrigin(
            runtimeSettings.PublicBaseUrl,
            "PUBLIC_BASE_URL");
        var normalizedStoredFrontendBaseUrl = NormalizeConfiguredOrigin(
            storedFrontendBaseUrl,
            "notification_email_settings.frontend_base_url");

        if (!string.Equals(normalizedStoredFrontendBaseUrl, publicBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Stored notification frontend_base_url must match PUBLIC_BASE_URL in Production. Startup aborted.");
        }

        logger.LogInformation(
            "Startup validation passed: stored notification email frontend base URL matches PUBLIC_BASE_URL.");
    }
}
