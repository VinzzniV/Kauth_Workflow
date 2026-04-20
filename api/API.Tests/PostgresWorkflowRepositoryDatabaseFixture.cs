using Npgsql;
using Xunit;

namespace API.Tests;

public sealed class PostgresWorkflowRepositoryDatabaseFixture : IAsyncLifetime
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=26432;Database=appdb;Username=app;Password=app_pw";

    private static readonly string[] EnsureScriptPaths =
    [
        "db/02_bootstrap.sql",
        "db/47_responsibility_plain_names.sql",
        "db/59_people_lifecycle_anchor.sql"
    ];

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ONBOARDING_TEST_CONNECTION_STRING")
                               ?? DefaultTestConnectionString;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var scriptPath in EnsureScriptPaths)
        {
            var sql = await File.ReadAllTextAsync(FindRepositoryFile(scriptPath));
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string FindRepositoryFile(string relativePath)
    {
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var candidate = Path.Combine(currentDirectory.FullName, normalizedRelativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException($"Repository file '{relativePath}' could not be found.");
    }
}
