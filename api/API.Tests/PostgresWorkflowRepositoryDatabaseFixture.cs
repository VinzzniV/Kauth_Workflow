using Npgsql;
using Xunit;

namespace API.Tests;

public sealed class PostgresWorkflowRepositoryDatabaseFixture : IAsyncLifetime
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=26432;Database=appdb;Username=app;Password=app_pw";

    private static readonly string[] InitializationScriptPaths =
    [
        "db/01_schema.sql",
        "db/02_dev_seed.sql"
    ];

    private const string SupplementalTestSeedSql =
        """
        WITH it_department AS (
            INSERT INTO public.departments (name)
            VALUES ('IT')
            ON CONFLICT (name) DO UPDATE SET name = EXCLUDED.name
            RETURNING id
        )
        INSERT INTO public.app_roles (department_id, role_key, name, role_kind, is_active)
        SELECT id, 'position_developer', 'Developer', 'position', TRUE
        FROM it_department
        ON CONFLICT (role_key) DO UPDATE
        SET department_id = EXCLUDED.department_id,
            name = EXCLUDED.name,
            role_kind = EXCLUDED.role_kind,
            is_active = EXCLUDED.is_active;

        INSERT INTO public.app_users (department_id, display_name, email, is_active)
        VALUES (
            (SELECT id FROM public.departments WHERE name = 'IT'),
            'Integration Test Actor',
            'integration.test.actor@kauth.local',
            TRUE
        );

        INSERT INTO public.people (
            app_user_id,
            department_id,
            first_name,
            last_name,
            employee_number,
            badge_number,
            employment_status,
            current_position_role_id
        )
        VALUES (
            (SELECT id FROM public.app_users WHERE email = 'integration.test.actor@kauth.local'),
            (SELECT id FROM public.departments WHERE name = 'IT'),
            'Integration',
            'Lead',
            900001,
            900001,
            'active',
            (SELECT id FROM public.app_roles WHERE role_key = 'position_developer')
        );

        INSERT INTO public.app_responsibilities (department_id, responsibility_key, system_key, name, responsibility_type, description, is_active)
        VALUES (
            (SELECT id FROM public.departments WHERE name = 'IT'),
            'leadership_it_integration',
            NULL,
            'Abteilungsleitung IT',
            'department_lead',
            'Temporäre Test-Abteilungsleitung für Integrationstests.',
            TRUE
        );

        INSERT INTO public.department_settings (
            department_id,
            department_lead_person_id,
            requirement_approver_person_id
        )
        VALUES (
            (SELECT id FROM public.departments WHERE name = 'IT'),
            (SELECT id FROM public.people WHERE app_user_id = (SELECT id FROM public.app_users WHERE email = 'integration.test.actor@kauth.local')),
            (SELECT id FROM public.people WHERE app_user_id = (SELECT id FROM public.app_users WHERE email = 'integration.test.actor@kauth.local'))
        );
        """;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ONBOARDING_TEST_CONNECTION_STRING")
                               ?? DefaultTestConnectionString;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var resetCommand = new NpgsqlCommand(
                         """
                         DROP SCHEMA IF EXISTS public CASCADE;
                         CREATE SCHEMA public;
                         GRANT ALL ON SCHEMA public TO public;
                         """,
                         connection))
        {
            await resetCommand.ExecuteNonQueryAsync();
        }

        foreach (var scriptPath in InitializationScriptPaths)
        {
            var sql = await File.ReadAllTextAsync(FindRepositoryFile(scriptPath));
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        await using (var searchPathCommand = new NpgsqlCommand("SET search_path TO public;", connection))
        {
            await searchPathCommand.ExecuteNonQueryAsync();
        }

        await using (var supportSeedCommand = new NpgsqlCommand(SupplementalTestSeedSql, connection))
        {
            await supportSeedCommand.ExecuteNonQueryAsync();
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
