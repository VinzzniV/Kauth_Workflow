using Microsoft.AspNetCore.Builder;
using Npgsql;

namespace API;

internal static class LifecycleStartupValidationExtensions
{
    private const string OnboardingProcessTypeKey = "onboarding";

    public static WebApplication ValidateLifecycleStartup(this WebApplication app)
    {
        ValidateRequiredSupervisorConfigurationAsync().GetAwaiter().GetResult();
        return app;
    }

    private static async Task ValidateRequiredSupervisorConfigurationAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("CONNECTION_STRING is not configured.");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

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
    }
}
