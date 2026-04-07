using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
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

    [Fact]
    public void ValidateSupervisorProcessConfiguration_AllowsNoSupervisorProcesses()
    {
        LifecycleStartupValidationExtensions.ValidateSupervisorProcessConfiguration(
            [],
            NullLogger.Instance);
    }

    [Fact]
    public void ValidateSupervisorProcessConfiguration_AllowsMultipleValidSupervisorProcesses()
    {
        var processTypes = new[]
        {
            new LifecycleStartupValidationExtensions.SupervisorProcessValidationRecord(
                "department_change",
                true,
                "department_change_approval",
                true),
            new LifecycleStartupValidationExtensions.SupervisorProcessValidationRecord(
                "offboarding",
                true,
                "offboarding_approval",
                true)
        };

        LifecycleStartupValidationExtensions.ValidateSupervisorProcessConfiguration(
            processTypes,
            NullLogger.Instance);
    }

    [Fact]
    public void ValidateSupervisorProcessConfiguration_RejectsMissingApprovalTaskTemplateForConcreteProcessType()
    {
        var processTypes = new[]
        {
            new LifecycleStartupValidationExtensions.SupervisorProcessValidationRecord(
                "role_change",
                true,
                "role_change_approval",
                false)
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            LifecycleStartupValidationExtensions.ValidateSupervisorProcessConfiguration(
                processTypes,
                NullLogger.Instance));

        Assert.Contains("role_change", exception.Message);
        Assert.Contains("role_change_approval", exception.Message);
    }

    [Fact]
    public void ValidatePublishedWorkflowDefinitionsConfiguration_AllowsConsistentPublishedDefinitions()
    {
        var validationService = new WorkflowDefinitionValidationService();
        var definitions = new[]
        {
            new LifecycleStartupValidationExtensions.PublishedWorkflowDefinitionStartupValidationRecord(
                "department_change",
                3,
                "department_change",
                new List<WorkflowDefinitionNodeDto>
                {
                    CreateNode("start", "start"),
                    CreateNode("form", "form", """{"legacyProcessTypeKey":"department_change"}"""),
                    CreateNode("end", "end")
                },
                new List<WorkflowDefinitionEdgeDto>
                {
                    CreateEdge("start", "form", 0),
                    CreateEdge("form", "end", 0)
                },
                new List<WorkflowDefinitionValidationIssue>())
        };

        LifecycleStartupValidationExtensions.ValidatePublishedWorkflowDefinitionsConfiguration(
            definitions,
            validationService,
            NullLogger.Instance);
    }

    [Fact]
    public void ValidatePublishedWorkflowDefinitionsConfiguration_RejectsInvalidPublishedDefinition()
    {
        var validationService = new WorkflowDefinitionValidationService();
        var definitions = new[]
        {
            new LifecycleStartupValidationExtensions.PublishedWorkflowDefinitionStartupValidationRecord(
                "hr-onboarding",
                2,
                "onboarding",
                new List<WorkflowDefinitionNodeDto>
                {
                    CreateNode("start", "start"),
                    CreateNode("decision", "decision"),
                    CreateNode("end", "end")
                },
                new List<WorkflowDefinitionEdgeDto>
                {
                    CreateEdge("start", "decision", 0),
                    CreateEdge("decision", "end", 0, """{"answerKey":"approved","operator":"gt"}""")
                },
                new List<WorkflowDefinitionValidationIssue>())
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            LifecycleStartupValidationExtensions.ValidatePublishedWorkflowDefinitionsConfiguration(
                definitions,
                validationService,
                NullLogger.Instance));

        Assert.Contains("hr-onboarding", exception.Message);
        Assert.Contains("unsupported operator", exception.Message);
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

    private static WorkflowDefinitionNodeDto CreateNode(
        string nodeKey,
        string nodeType,
        string? configJson = null)
    {
        return new WorkflowDefinitionNodeDto
        {
            NodeKey = nodeKey,
            NodeType = nodeType,
            SortOrder = 0,
            Config = configJson is null ? null : JsonDocument.Parse(configJson).RootElement.Clone()
        };
    }

    private static WorkflowDefinitionEdgeDto CreateEdge(
        string sourceNodeKey,
        string targetNodeKey,
        int priority,
        string? conditionExpression = null)
    {
        return new WorkflowDefinitionEdgeDto
        {
            SourceNodeKey = sourceNodeKey,
            TargetNodeKey = targetNodeKey,
            Priority = priority,
            ConditionExpression = conditionExpression
        };
    }
}
