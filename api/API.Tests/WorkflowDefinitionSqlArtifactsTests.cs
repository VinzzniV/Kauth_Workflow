using Xunit;

namespace API.Tests;

public sealed class WorkflowDefinitionSqlArtifactsTests
{
    [Theory]
    [InlineData("db/01_schema.sql")]
    [InlineData("db/41_workflow_definition_layer.sql")]
    public async Task SqlArtifacts_DefineWorkflowDefinitionLayerTables(string relativePath)
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile(relativePath));

        Assert.Contains("workflow_definitions", content);
        Assert.Contains("workflow_definition_versions", content);
        Assert.Contains("workflow_nodes", content);
        Assert.Contains("workflow_edges", content);
        Assert.Contains("workflow_node_configs", content);
    }

    [Theory]
    [InlineData("db/01_schema.sql")]
    [InlineData("db/41_workflow_definition_layer.sql")]
    [InlineData("db/46_workflow_builder_positions.sql")]
    public async Task SqlArtifacts_DefineWorkflowBuilderPositionArtifacts(string relativePath)
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile(relativePath));

        Assert.Contains("position_x", content);
        Assert.Contains("position_y", content);
    }

    [Theory]
    [InlineData("db/01_schema.sql")]
    [InlineData("db/42_workflow_runtime_layer.sql")]
    public async Task SqlArtifacts_DefineWorkflowRuntimeLayerArtifacts(string relativePath)
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile(relativePath));

        Assert.Contains("workflow_definition_version_id", content);
        Assert.Contains("current_runtime_status", content);
        Assert.Contains("published_at", content);
        Assert.Contains("primary_legacy_process_type_id", content);
        Assert.Contains("workflow_node_instances", content);
        Assert.Contains("workflow_runtime_events", content);
    }

    [Theory]
    [InlineData("db/01_schema.sql")]
    [InlineData("db/45_automation_layer.sql")]
    public async Task SqlArtifacts_DefineAutomationLayerArtifacts(string relativePath)
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile(relativePath));

        Assert.Contains("action_definitions", content);
        Assert.Contains("workflow_node_actions", content);
        Assert.Contains("automation_jobs", content);
        Assert.Contains("automation_job_attempts", content);
        Assert.Contains("automation_job_logs", content);
        Assert.Contains("CreateAdUser", content);
        Assert.Contains("SendWelcomeMail", content);
    }

    [Theory]
    [InlineData("db/01_schema.sql")]
    [InlineData("db/44_runtime_task_bridge.sql")]
    public async Task SqlArtifacts_DefineRuntimeTaskBridgeArtifacts(string relativePath)
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile(relativePath));

        Assert.Contains("node_instance_id", content);
        Assert.Contains("ux_workflow_tasks_node_instance_id", content);
    }

    [Fact]
    public async Task SqlArtifacts_DefineSeededWorkflowMappings()
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile("db/43_workflow_definition_mappings.sql"));

        Assert.Contains("upsert_linearized_workflow_definition", content);
        Assert.Contains("'onboarding'", content);
        Assert.Contains("'offboarding'", content);
        Assert.Contains("'department_change'", content);
        Assert.Contains("'published'", content);
    }

    [Theory]
    [InlineData("db/init/dev/00_init.sql")]
    [InlineData("db/init/prod/00_init.sql")]
    public async Task InitScripts_IncludeWorkflowDefinitionLayerMigration(string relativePath)
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile(relativePath));

        Assert.Contains("/docker-entrypoint-sql/41_workflow_definition_layer.sql", content);
        Assert.Contains("/docker-entrypoint-sql/42_workflow_runtime_layer.sql", content);
        Assert.Contains("/docker-entrypoint-sql/43_workflow_definition_mappings.sql", content);
        Assert.Contains("/docker-entrypoint-sql/44_runtime_task_bridge.sql", content);
        Assert.Contains("/docker-entrypoint-sql/45_automation_layer.sql", content);
        Assert.Contains("/docker-entrypoint-sql/46_workflow_builder_positions.sql", content);
        Assert.Contains("/docker-entrypoint-sql/48_measure_generation_node_types.sql", content);
        Assert.Contains("/docker-entrypoint-sql/49_measure_generation_phase_c.sql", content);
        Assert.Contains("/docker-entrypoint-sql/50_onboarding_gatekeeper_measure_flow.sql", content);
        Assert.Contains("/docker-entrypoint-sql/51_remove_seeded_demo_departments.sql", content);
        Assert.Contains("/docker-entrypoint-sql/52_restore_core_responsibilities.sql", content);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var candidate = Path.Combine(currentDirectory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException($"Repository file '{relativePath}' could not be found.");
    }
}
