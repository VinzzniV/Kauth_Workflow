using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace API;

internal static class LifecycleStartupValidationExtensions
{
    internal sealed record SupervisorProcessValidationRecord(
        string ProcessTypeKey,
        bool RequiresSupervisorStep,
        string? ApprovalTaskTemplateKey,
        bool ApprovalTaskTemplateExists);

    internal sealed record PublishedWorkflowDefinitionStartupValidationRecord(
        string DefinitionKey,
        int VersionNumber,
        string? PrimaryLegacyProcessTypeKey,
        List<WorkflowDefinitionNodeDto> Nodes,
        List<WorkflowDefinitionEdgeDto> Edges,
        List<WorkflowDefinitionValidationIssue> ReferenceIssues);

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

        ValidateSwaggerConfiguration(logger, runtimeSettings);
        ValidateEntraConfiguration(logger, runtimeSettings);
        ValidateProductionPublicUrls(configuration, runtimeSettings, logger);
        var validationService = app.Services.GetRequiredService<IWorkflowDefinitionValidationService>();
        ValidateDatabaseConfigurationAsync(logger, runtimeSettings, validationService).GetAwaiter().GetResult();
        return app;
    }

    internal static void ValidateSupervisorProcessConfiguration(
        IReadOnlyCollection<SupervisorProcessValidationRecord> processTypes,
        ILogger logger)
    {
        if (processTypes.Count == 0)
        {
            logger.LogInformation(
                "Startup validation passed: no active process types currently require a supervisor step.");
            return;
        }

        foreach (var processType in processTypes)
        {
            if (!processType.RequiresSupervisorStep)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(processType.ApprovalTaskTemplateKey))
            {
                throw new InvalidOperationException(
                    $"Process type '{processType.ProcessTypeKey}' requires a supervisor step but has no approval_task_template_key configured. Startup aborted.");
            }

            if (!processType.ApprovalTaskTemplateExists)
            {
                throw new InvalidOperationException(
                    $"Configured approval task template '{processType.ApprovalTaskTemplateKey}' for process type '{processType.ProcessTypeKey}' is missing in task_templates or inactive. Startup aborted.");
            }
        }

        logger.LogInformation(
            "Startup validation passed: {ProcessTypeCount} active process type(s) with supervisor step are configured consistently.",
            processTypes.Count);
    }

    internal static void ValidatePublishedWorkflowDefinitionsConfiguration(
        IReadOnlyCollection<PublishedWorkflowDefinitionStartupValidationRecord> definitions,
        IWorkflowDefinitionValidationService validationService,
        ILogger logger)
    {
        foreach (var definition in definitions)
        {
            var snapshot = validationService.ValidateSnapshot(new WorkflowDefinitionValidationContext
            {
                Nodes = definition.Nodes,
                Edges = definition.Edges,
                ReferenceIssues = definition.ReferenceIssues
            });

            if (!snapshot.CanPublish)
            {
                throw new InvalidOperationException(
                    $"Published workflow definition '{definition.DefinitionKey}' version {definition.VersionNumber} is inconsistent: {string.Join(" ", snapshot.Issues.Select(issue => issue.Message))} Startup aborted.");
            }
        }

        logger.LogInformation(
            "Startup validation passed: {DefinitionCount} published workflow definition version(s) are configured consistently.",
            definitions.Count);
    }

    private static void ValidateSwaggerConfiguration(ILogger logger, LifecycleRuntimeSettings runtimeSettings)
    {
        if (runtimeSettings.IsProduction && runtimeSettings.SwaggerEnabled)
        {
            throw new InvalidOperationException(
                "SWAGGER_ENABLED=true is not allowed in Production. Swagger must stay disabled there. Startup aborted.");
        }

        logger.LogInformation(
            "Startup validation passed: Swagger is {SwaggerState} for environment {EnvironmentName}.",
            runtimeSettings.SwaggerEnabled ? "enabled" : "disabled",
            runtimeSettings.EnvironmentName);
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

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{settingName} must use https in Production. Startup aborted.");
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
        LifecycleRuntimeSettings runtimeSettings,
        IWorkflowDefinitionValidationService validationService)
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
            await ValidatePublishedWorkflowDefinitionsConfigurationAsync(connection, validationService, logger);
            await ValidateNotificationFrontendBaseUrlAsync(connection, logger, runtimeSettings);
        }
    }

    private static async Task ValidateSupervisorConfigurationAsync(NpgsqlConnection connection, ILogger logger)
    {
        const string sql = @"
SELECT
    pt.key,
    pt.requires_supervisor_step,
    pt.approval_task_template_key,
    EXISTS(
        SELECT 1
        FROM task_templates tt
        WHERE tt.process_type_id = pt.id
          AND tt.template_key = pt.approval_task_template_key
          AND tt.is_active = TRUE
    ) AS approval_task_exists
FROM process_types pt
WHERE pt.is_active = TRUE
  AND pt.requires_supervisor_step = TRUE
ORDER BY pt.sort_order, pt.name, pt.key;";

        await using var command = new NpgsqlCommand(sql, connection);
        var processTypes = new List<SupervisorProcessValidationRecord>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            processTypes.Add(new SupervisorProcessValidationRecord(
                reader.GetString(0),
                reader.GetBoolean(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetBoolean(3)));
        }

        ValidateSupervisorProcessConfiguration(processTypes, logger);
    }

    private static async Task ValidatePublishedWorkflowDefinitionsConfigurationAsync(
        NpgsqlConnection connection,
        IWorkflowDefinitionValidationService validationService,
        ILogger logger)
    {
        const string sql = """
SELECT
    d.definition_key,
    v.version_number,
    pt.key AS primary_legacy_process_type_key,
    n.node_key,
    n.node_type,
    n.title,
    n.sort_order,
    nc.config_json::text,
    source_node.node_key AS source_node_key,
    target_node.node_key AS target_node_key,
    e.priority,
    e.condition_expression
FROM workflow_definition_versions v
INNER JOIN workflow_definitions d
    ON d.id = v.workflow_definition_id
LEFT JOIN process_types pt
    ON pt.id = v.primary_legacy_process_type_id
LEFT JOIN workflow_nodes n
    ON n.workflow_definition_version_id = v.id
LEFT JOIN workflow_node_configs nc
    ON nc.workflow_node_id = n.id
LEFT JOIN workflow_edges e
    ON e.workflow_definition_version_id = v.id
LEFT JOIN workflow_nodes source_node
    ON source_node.id = e.source_workflow_node_id
LEFT JOIN workflow_nodes target_node
    ON target_node.id = e.target_workflow_node_id
WHERE v.status = 'published'
ORDER BY d.definition_key, v.version_number, n.sort_order, n.node_key, e.priority;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var definitions = new Dictionary<string, PublishedWorkflowDefinitionStartupValidationRecord>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            var definitionKey = reader.GetString(0);
            var versionNumber = reader.GetInt32(1);
            var recordKey = $"{definitionKey}::{versionNumber}";
            if (!definitions.TryGetValue(recordKey, out var definition))
            {
                definition = new PublishedWorkflowDefinitionStartupValidationRecord(
                    definitionKey,
                    versionNumber,
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    new List<WorkflowDefinitionNodeDto>(),
                    new List<WorkflowDefinitionEdgeDto>(),
                    new List<WorkflowDefinitionValidationIssue>());
                definitions.Add(recordKey, definition);

                if (string.IsNullOrWhiteSpace(definition.PrimaryLegacyProcessTypeKey))
                {
                    definition.ReferenceIssues.Add(new WorkflowDefinitionValidationIssue
                    {
                        Code = "missing_primary_legacy_process_type",
                        Severity = "error",
                        Scope = "workflow_definition_version",
                        Message = $"Workflow definition '{definition.DefinitionKey}' version {definition.VersionNumber} requires a primaryLegacyProcessTypeKey before it can be published.",
                        ReferenceKey = definition.DefinitionKey
                    });
                }
            }

            if (!reader.IsDBNull(3)
                && !definition.Nodes.Any(node => string.Equals(node.NodeKey, reader.GetString(3), StringComparison.Ordinal)))
            {
                definition.Nodes.Add(new WorkflowDefinitionNodeDto
                {
                    NodeKey = reader.GetString(3),
                    NodeType = reader.GetString(4),
                    Title = reader.IsDBNull(5) ? null : reader.GetString(5),
                    SortOrder = reader.GetInt32(6),
                    Config = reader.IsDBNull(7) ? null : ParseJsonElement(reader.GetString(7))
                });
            }

            if (!reader.IsDBNull(8))
            {
                var sourceNodeKey = reader.GetString(8);
                var targetNodeKey = reader.GetString(9);
                var priority = reader.GetInt32(10);
                if (!definition.Edges.Any(edge =>
                        string.Equals(edge.SourceNodeKey, sourceNodeKey, StringComparison.Ordinal)
                        && string.Equals(edge.TargetNodeKey, targetNodeKey, StringComparison.Ordinal)
                        && edge.Priority == priority))
                {
                    definition.Edges.Add(new WorkflowDefinitionEdgeDto
                    {
                        SourceNodeKey = sourceNodeKey,
                        TargetNodeKey = targetNodeKey,
                        Priority = priority,
                        ConditionExpression = reader.IsDBNull(11) ? null : reader.GetString(11)
                    });
                }
            }
        }

        await reader.CloseAsync();

        foreach (var definition in definitions.Values)
        {
            if (!string.IsNullOrWhiteSpace(definition.PrimaryLegacyProcessTypeKey))
            {
                if (!await LegacyProcessTypeExists(connection, definition.PrimaryLegacyProcessTypeKey, requireActive: true))
                {
                    definition.ReferenceIssues.Add(new WorkflowDefinitionValidationIssue
                    {
                        Code = "unknown_primary_legacy_process_type",
                        Severity = "error",
                        Scope = "workflow_definition_version",
                        Message = $"Workflow definition '{definition.DefinitionKey}' version {definition.VersionNumber} references unknown or inactive primaryLegacyProcessTypeKey '{definition.PrimaryLegacyProcessTypeKey}'.",
                        ReferenceKey = definition.PrimaryLegacyProcessTypeKey
                    });
                }
            }

            foreach (var node in definition.Nodes)
            {
                var configValue = TryGetNodeConfigValue(node, "legacyProcessTypeKey");
                if (string.Equals(node.NodeType, "form", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(configValue)
                    && !await LegacyProcessTypeExists(connection, configValue, requireActive: true))
                {
                    definition.ReferenceIssues.Add(new WorkflowDefinitionValidationIssue
                    {
                        Code = "unknown_form_legacy_process_type",
                        Severity = "error",
                        Scope = "workflow_node",
                        Message = $"Node '{node.NodeKey}' references unknown or inactive legacyProcessTypeKey '{configValue}'.",
                        ReferenceKey = node.NodeKey
                    });
                }

                var templateKey = TryGetNodeConfigValue(node, "legacyTemplateKey");
                if ((string.Equals(node.NodeType, "task", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.NodeType, "approval", StringComparison.OrdinalIgnoreCase))
                    && !string.IsNullOrWhiteSpace(templateKey)
                    && !await LegacyTaskTemplateExists(connection, templateKey, requireActive: true))
                {
                    definition.ReferenceIssues.Add(new WorkflowDefinitionValidationIssue
                    {
                        Code = "unknown_legacy_template",
                        Severity = "error",
                        Scope = "workflow_node",
                        Message = $"Node '{node.NodeKey}' references unknown or inactive legacyTemplateKey '{templateKey}'.",
                        ReferenceKey = node.NodeKey
                    });
                }
            }
        }

        ValidatePublishedWorkflowDefinitionsConfiguration(definitions.Values.ToList(), validationService, logger);
    }

    private static string? TryGetNodeConfigValue(WorkflowDefinitionNodeDto node, string propertyName)
    {
        if (!node.Config.HasValue || node.Config.Value.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return null;
        }

        if (!node.Config.Value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != System.Text.Json.JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            return null;
        }

        return property.GetString()!.Trim().ToLowerInvariant();
    }

    private static System.Text.Json.JsonElement ParseJsonElement(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static async Task<bool> LegacyProcessTypeExists(
        NpgsqlConnection connection,
        string processTypeKey,
        bool requireActive)
    {
        const string sql = """
SELECT 1
FROM process_types
WHERE key = @key
  AND (@requireActive = FALSE OR is_active = TRUE)
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("key", processTypeKey.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("requireActive", requireActive);
        return await command.ExecuteScalarAsync() is not null;
    }

    private static async Task<bool> LegacyTaskTemplateExists(
        NpgsqlConnection connection,
        string templateKey,
        bool requireActive)
    {
        const string sql = """
SELECT 1
FROM task_templates
WHERE template_key = @templateKey
  AND (@requireActive = FALSE OR is_active = TRUE)
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("templateKey", templateKey.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("requireActive", requireActive);
        return await command.ExecuteScalarAsync() is not null;
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
