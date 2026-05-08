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
        string? ApprovalSpecKey,
        bool ApprovalTaskTemplateExists);

    internal sealed record PublishedWorkflowDefinitionStartupValidationRecord(
        string DefinitionKey,
        int VersionNumber,
        string? PrimaryLegacyProcessTypeKey,
        bool RequiresSupervisorStep,
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
        using var scope = app.Services.CreateScope();
        var validationService = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionValidationService>();
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

            if (string.IsNullOrWhiteSpace(processType.ApprovalSpecKey))
            {
                throw new InvalidOperationException(
                    $"Process type '{processType.ProcessTypeKey}' requires a supervisor step but has no approval_spec_key configured. Startup aborted.");
            }

            if (!processType.ApprovalTaskTemplateExists)
            {
                throw new InvalidOperationException(
                    $"Configured approval spec key '{processType.ApprovalSpecKey}' for process type '{processType.ProcessTypeKey}' is missing in workflow_node_task_specs. Startup aborted.");
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
                ReferenceIssues = definition.ReferenceIssues,
                WorkflowDefinitionKey = definition.DefinitionKey,
                RequiresSupervisorStep = definition.RequiresSupervisorStep
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
        // LA5: Approval-Task-Spec haengt am measure-Node der published Version.
        const string sql = @"
SELECT
    pt.definition_key,
    pt.requires_supervisor_step,
    pt.approval_spec_key,
    EXISTS(
        SELECT 1
        FROM workflow_node_task_specs s
        JOIN workflow_nodes n ON n.id = s.workflow_node_id
        JOIN workflow_definition_versions v ON v.id = n.workflow_definition_version_id
        WHERE v.workflow_definition_id = pt.id
          AND v.published_at IS NOT NULL
          AND n.node_type LIKE 'measure_%'
          AND s.spec_key = pt.approval_spec_key
    ) AS approval_task_exists
FROM workflow_definitions pt
WHERE pt.requires_supervisor_step = TRUE
ORDER BY pt.name, pt.definition_key;";

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
    d.definition_key AS primary_legacy_process_type_key,
    d.requires_supervisor_step,
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
                    reader.GetBoolean(3),
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

            if (!reader.IsDBNull(4)
                && !definition.Nodes.Any(node => string.Equals(node.NodeKey, reader.GetString(4), StringComparison.Ordinal)))
            {
                definition.Nodes.Add(new WorkflowDefinitionNodeDto
                {
                    NodeKey = reader.GetString(4),
                    NodeType = reader.GetString(5),
                    Title = reader.IsDBNull(6) ? null : reader.GetString(6),
                    SortOrder = reader.GetInt32(7),
                    Config = reader.IsDBNull(8) ? null : ParseJsonElement(reader.GetString(8)),
                    Actions = new List<WorkflowNodeActionDto>()
                });
            }

            if (!reader.IsDBNull(9))
            {
                var sourceNodeKey = reader.GetString(9);
                var targetNodeKey = reader.GetString(10);
                var priority = reader.GetInt32(11);
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
                        ConditionExpression = reader.IsDBNull(12) ? null : reader.GetString(12)
                    });
                }
            }
        }

        await reader.CloseAsync();

        const string actionSql = """
SELECT
    d.definition_key,
    v.version_number,
    n.node_key,
    ad.action_key,
    wna.input_mapping_json::text,
    wna.execution_order,
    wna.on_error_behavior
FROM workflow_definition_versions v
INNER JOIN workflow_definitions d
    ON d.id = v.workflow_definition_id
INNER JOIN workflow_nodes n
    ON n.workflow_definition_version_id = v.id
INNER JOIN workflow_node_actions wna
    ON wna.workflow_node_id = n.id
INNER JOIN action_definitions ad
    ON ad.id = wna.action_definition_id
WHERE v.status = 'published'
ORDER BY d.definition_key, v.version_number, n.sort_order, n.node_key, wna.execution_order, wna.id;
""";

        await using (var actionCommand = new NpgsqlCommand(actionSql, connection))
        await using (var actionReader = await actionCommand.ExecuteReaderAsync())
        {
            while (await actionReader.ReadAsync())
            {
                var recordKey = $"{actionReader.GetString(0)}::{actionReader.GetInt32(1)}";
                if (!definitions.TryGetValue(recordKey, out var definition))
                {
                    continue;
                }

                var nodeKey = actionReader.GetString(2);
                var node = definition.Nodes.FirstOrDefault(existing =>
                    string.Equals(existing.NodeKey, nodeKey, StringComparison.Ordinal));
                if (node is null)
                {
                    continue;
                }

                node.Actions.Add(new WorkflowNodeActionDto
                {
                    ActionKey = actionReader.GetString(3),
                    InputMapping = actionReader.IsDBNull(4) ? null : ParseJsonElement(actionReader.GetString(4)),
                    ExecutionOrder = actionReader.GetInt32(5),
                    OnErrorBehavior = actionReader.GetString(6)
                });
            }
        }

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

                // LA5: task/approval-Nodes binden ueber workflow_node_task_specs.workflow_node_id
                // an ihre Spec; Drift-Check beim Startup entfaellt — Runtime-Resolver wirft, falls Spec fehlt.

                if (string.Equals(node.NodeType, "automation", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var action in node.Actions)
                    {
                        if (string.IsNullOrWhiteSpace(action.ActionKey))
                        {
                            continue;
                        }

                        if (!await ActionDefinitionExists(connection, action.ActionKey, requireActive: true))
                        {
                            definition.ReferenceIssues.Add(new WorkflowDefinitionValidationIssue
                            {
                                Code = "unknown_action_definition",
                                Severity = "error",
                                Scope = "workflow_node",
                                Message = $"Node '{node.NodeKey}' references unknown or inactive action '{action.ActionKey}'.",
                                ReferenceKey = node.NodeKey
                            });
                        }
                    }
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
        _ = requireActive;
        const string sql = """
SELECT 1
FROM workflow_definitions
WHERE definition_key = @key
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("key", processTypeKey.Trim().ToLowerInvariant());
        return await command.ExecuteScalarAsync() is not null;
    }


    private static async Task<bool> ActionDefinitionExists(
        NpgsqlConnection connection,
        string actionKey,
        bool requireActive)
    {
        const string sql = """
SELECT 1
FROM action_definitions
WHERE LOWER(action_key) = LOWER(@actionKey)
  AND (@requireActive = FALSE OR is_active = TRUE)
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("actionKey", actionKey.Trim());
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
