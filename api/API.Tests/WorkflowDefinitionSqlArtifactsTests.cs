using Xunit;

namespace API.Tests;

public sealed class WorkflowDefinitionSqlArtifactsTests
{
    [Fact]
    public async Task SqlArtifacts_DefineWorkflowDefinitionLayerTables()
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile("db/01_schema.sql"));

        Assert.Contains("workflow_definitions", content);
        Assert.Contains("workflow_definition_versions", content);
        Assert.Contains("workflow_nodes", content);
        Assert.Contains("workflow_edges", content);
        Assert.Contains("workflow_node_configs", content);
    }

    [Fact]
    public async Task SqlArtifacts_DefineWorkflowBuilderPositionArtifacts()
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile("db/01_schema.sql"));

        Assert.Contains("position_x", content);
        Assert.Contains("position_y", content);
    }

    [Fact]
    public async Task SqlArtifacts_DefineWorkflowRuntimeLayerArtifacts()
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile("db/01_schema.sql"));

        Assert.Contains("workflow_definition_version_id", content);
        Assert.Contains("current_runtime_status", content);
        Assert.Contains("published_at", content);
        Assert.Contains("workflow_node_instances", content);
        Assert.Contains("workflow_runtime_events", content);
    }

    [Fact]
    public async Task SqlArtifacts_DefineAutomationLayerArtifacts()
    {
        var schemaContent = await File.ReadAllTextAsync(FindRepositoryFile("db/01_schema.sql"));
        var seedContent = await File.ReadAllTextAsync(FindRepositoryFile("db/02_bootstrap.sql"));

        Assert.Contains("action_definitions", schemaContent);
        Assert.Contains("workflow_node_actions", schemaContent);
        Assert.Contains("automation_jobs", schemaContent);
        Assert.Contains("automation_job_attempts", schemaContent);
        Assert.Contains("automation_job_logs", schemaContent);
        Assert.Contains("CreateAdUser", seedContent);
        Assert.Contains("SendWelcomeMail", seedContent);
    }

    [Fact]
    public async Task SqlArtifacts_DefineRuntimeTaskBridgeArtifacts()
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile("db/01_schema.sql"));

        Assert.Contains("node_instance_id", content);
        Assert.Contains("ux_workflow_tasks_node_instance_id", content);
    }

    [Fact]
    public async Task SqlArtifacts_DefineSeededWorkflowMappings()
    {
        var schemaContent = await File.ReadAllTextAsync(FindRepositoryFile("db/01_schema.sql"));
        var seedContent = await File.ReadAllTextAsync(FindRepositoryFile("db/02_bootstrap.sql"));

        Assert.Contains("upsert_linearized_workflow_definition", schemaContent);
        Assert.Contains("'onboarding'", seedContent);
        Assert.Contains("'offboarding'", seedContent);
        Assert.Contains("'department_change'", seedContent);
        Assert.Contains("'published'", seedContent);
    }

    [Theory]
    [InlineData("db/init/dev/00_init.sql")]
    [InlineData("db/init/prod/00_init.sql")]
    public async Task InitScripts_IncludeWorkflowDefinitionLayerMigration(string relativePath)
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile(relativePath));

        Assert.Contains("/docker-entrypoint-sql/01_schema.sql", content);

        if (relativePath.EndsWith("dev/00_init.sql", StringComparison.Ordinal))
        {
            Assert.Contains("/docker-entrypoint-sql/02_dev_seed.sql", content);
            return;
        }

        Assert.Contains("/docker-entrypoint-sql/02_bootstrap.sql", content);
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
