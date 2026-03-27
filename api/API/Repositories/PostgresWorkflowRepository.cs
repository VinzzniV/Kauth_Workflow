using Npgsql;
using NpgsqlTypes;

namespace API;

// Kapselt den kompletten PostgreSQL-Zugriff fuer Workflows, Anforderungen, Aufgaben und Benachrichtigungen.
internal sealed partial class PostgresWorkflowRepository : IWorkflowRepository
{
    private sealed class ProcessTypeCreateRecord
    {
        public required int Id { get; init; }
        public required string Key { get; init; }
        public required string Name { get; init; }
        public required bool RequiresSupervisorStep { get; init; }
        public string? ApprovalTaskTemplateKey { get; init; }
        public required bool RequiresTargetPerson { get; init; }
        public required bool IsActive { get; init; }
    }

    private sealed class TargetPersonRecord
    {
        public required long PersonId { get; init; }
        public required string DisplayName { get; init; }
        public int? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public int? RoleId { get; init; }
        public string? RoleName { get; init; }
        public int? EmployeeNumber { get; init; }
        public int? BadgeNumber { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
    }

    // Diese Regeln definieren den erlaubten Lebenszyklus einzelner Aufgaben.
    private static readonly HashSet<string> AllowedTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "open",
        "ready",
        "in_progress",
        "blocked",
        "done"
    };

    private static readonly HashSet<string> TerminalTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "done"
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedTaskTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["open"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ready", "in_progress", "blocked", "done" },
        ["ready"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "in_progress", "blocked", "done" },
        ["in_progress"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "done", "blocked" },
        ["blocked"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ready" },
        ["done"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
    };

    private enum TaskGenerationStage
    {
        Initial,
        AfterSupervisor
    }

    private static readonly string[] LegacyManagerCreatableProcessTypeKeys =
    [
        "department_change",
        "name_change",
        "position_change",
        "role_change"
    ];

    // Die Verbindung wird bewusst direkt aus der Umgebung gelesen, damit API und Container identisch konfiguriert bleiben.
    private static string GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("CONNECTION_STRING is not configured.");
        }

        return connectionString;
    }
}
