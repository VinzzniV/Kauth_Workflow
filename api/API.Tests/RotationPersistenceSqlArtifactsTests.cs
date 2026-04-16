using Xunit;

namespace API.Tests;

public sealed class RotationPersistenceSqlArtifactsTests
{
    [Theory]
    [InlineData("db/01_schema.sql")]
    [InlineData("db/54_rotation_phase1_persistence.sql")]
    [InlineData("db/56_rotation_task_generation_sync.sql")]
    public async Task SqlArtifacts_DefineRotationPersistenceArtifacts(string relativePath)
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile(relativePath));

        if (!relativePath.EndsWith("56_rotation_task_generation_sync.sql", StringComparison.Ordinal))
        {
            Assert.Contains("rotation_plans", content);
            Assert.Contains("rotation_stations", content);
            Assert.Contains("department_action_templates", content);
            Assert.Contains("rotation_generated_tasks", content);
            Assert.Contains("rotation_notifications", content);
            Assert.Contains("rotation_audit_log", content);
        }

        if (relativePath.EndsWith("54_rotation_phase1_persistence.sql", StringComparison.Ordinal))
        {
            Assert.Contains("uq_rotation_plans_active_per_person", content);
            Assert.Contains("uq_rotation_plans_open_source_workflow", content);
            Assert.Contains("chk_rotation_stations_date_range", content);
            Assert.Contains("ensure_rotation_plan_source_workflow_completed", content);
        }

        if (relativePath.EndsWith("01_schema.sql", StringComparison.Ordinal)
            || relativePath.EndsWith("56_rotation_task_generation_sync.sql", StringComparison.Ordinal))
        {
            Assert.Contains("rotation_task_assignments", content);
            Assert.Contains("rotation_task_comments", content);
            Assert.Contains("trigger_type", content);
            Assert.Contains("anchor_date", content);
            Assert.Contains("uq_rotation_generated_tasks_station_template", content);
        }
    }

    [Theory]
    [InlineData("db/init/dev/00_init.sql")]
    [InlineData("db/init/prod/00_init.sql")]
    public async Task InitScripts_IncludeRotationPersistenceMigration(string relativePath)
    {
        var content = await File.ReadAllTextAsync(FindRepositoryFile(relativePath));

        Assert.Contains("/docker-entrypoint-sql/54_rotation_phase1_persistence.sql", content);
        Assert.Contains("/docker-entrypoint-sql/56_rotation_task_generation_sync.sql", content);
    }

    [Fact]
    public async Task DevelopmentInit_AndSeedArtifacts_IncludeRotationTemplateExamples()
    {
        var initContent = await File.ReadAllTextAsync(FindRepositoryFile("db/init/dev/00_init.sql"));
        var seedContent = await File.ReadAllTextAsync(FindRepositoryFile("db/55_rotation_dev_template_examples.sql"));

        Assert.Contains("/docker-entrypoint-sql/55_rotation_dev_template_examples.sql", initContent);
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
