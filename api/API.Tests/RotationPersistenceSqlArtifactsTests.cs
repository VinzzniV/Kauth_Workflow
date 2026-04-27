using Xunit;

namespace API.Tests;

public sealed class RotationPersistenceSqlArtifactsTests
{
    [Fact]
    public async Task SqlArtifacts_DefineRotationPersistenceArtifacts()
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile("db/01_schema.sql"));

        Assert.Contains("rotation_plans", content);
        Assert.Contains("rotation_stations", content);
        Assert.Contains("department_action_templates", content);
        Assert.Contains("rotation_generated_tasks", content);
        Assert.Contains("rotation_notifications", content);
        Assert.Contains("rotation_audit_log", content);
        Assert.Contains("uq_rotation_plans_active_per_person", content);
        Assert.Contains("uq_rotation_plans_open_source_workflow", content);
        Assert.Contains("chk_rotation_stations_date_range", content);
        Assert.Contains("ensure_rotation_plan_source_workflow_completed", content);
        Assert.Contains("rotation_task_assignments", content);
        Assert.Contains("rotation_task_comments", content);
        Assert.Contains("trigger_type", content);
        Assert.Contains("anchor_date", content);
        Assert.Contains("uq_rotation_generated_tasks_station_template", content);
    }

    [Theory]
    [InlineData("db/init/dev/00_init.sql")]
    [InlineData("db/init/prod/00_init.sql")]
    public async Task InitScripts_IncludeRotationPersistenceMigration(string relativePath)
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

    [Fact]
    public async Task DevelopmentInit_AndSeedArtifacts_IncludeRotationTemplateExamples()
    {
        var initContent = await File.ReadAllTextAsync(FindRepositoryFile("db/init/dev/00_init.sql"));
        var seedContent = await File.ReadAllTextAsync(FindRepositoryFile("db/02_dev_seed.sql"));

        Assert.Contains("/docker-entrypoint-sql/02_dev_seed.sql", initContent);
        Assert.Contains("Einkauf", seedContent);
        Assert.Contains("Produktion", seedContent);
        Assert.Contains("IT", seedContent);
        Assert.Contains("department_action_templates", seedContent);
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
