using Microsoft.AspNetCore.Builder;
using Npgsql;

namespace API;

internal static class OnboardingStartupValidationExtensions
{
    private const string SupervisorRequirementTaskKey = "supervisor_fills_document";

    public static WebApplication ValidateOnboardingStartup(this WebApplication app)
    {
        ValidateRequiredTaskTemplatesAsync().GetAwaiter().GetResult();
        return app;
    }

    private static async Task ValidateRequiredTaskTemplatesAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("CONNECTION_STRING is not configured.");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM task_templates
    WHERE template_key = @taskKey
);";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskKey", SupervisorRequirementTaskKey);

        var exists = (bool)(await command.ExecuteScalarAsync() ?? false);
        if (!exists)
        {
            throw new InvalidOperationException(
                "Required task template 'supervisor_fills_document' is missing in task_templates. Startup aborted.");
        }
    }
}
