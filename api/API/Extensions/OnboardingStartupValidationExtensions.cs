using Microsoft.AspNetCore.Builder;
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
        ValidateRequiredSupervisorConfigurationAsync(logger).GetAwaiter().GetResult();
        return app;
    }

    private static async Task ValidateRequiredSupervisorConfigurationAsync(ILogger logger)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("CONNECTION_STRING is not configured.");
        }

        NpgsqlConnection connection;
        try
        {
            connection = new NpgsqlConnection(connectionString);
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
}
