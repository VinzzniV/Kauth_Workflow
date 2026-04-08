using Npgsql;
using NpgsqlTypes;

namespace API;

// Kapselt den kompletten PostgreSQL-Zugriff fuer Workflows, Anforderungen, Aufgaben und Benachrichtigungen.
internal sealed partial class PostgresWorkflowRepository : IWorkflowRepository, IWorkflowDefinitionRuntimeRepository, IWorkflowAutomationRepository
{
    private readonly IWorkflowDefinitionValidationService _workflowDefinitionValidationService;

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

    public PostgresWorkflowRepository()
        : this(new WorkflowDefinitionValidationService())
    {
    }

    internal PostgresWorkflowRepository(IWorkflowDefinitionValidationService workflowDefinitionValidationService)
    {
        _workflowDefinitionValidationService = workflowDefinitionValidationService;
    }

    // Die Verbindung wird bewusst direkt aus der Umgebung gelesen, damit API und Container identisch konfiguriert bleiben.
    private static string GetConnectionString()
    {
        return LifecycleRuntimeSettingsResolver.GetRequiredConnectionString();
    }
}
