using Npgsql;
using NpgsqlTypes;

namespace API;

// Kapselt den kompletten PostgreSQL-Zugriff fuer Workflows, Anforderungen, Aufgaben und Benachrichtigungen.
internal sealed partial class PostgresWorkflowRepository : IWorkflowRepository, IWorkflowAutomationRepository
{
    private readonly IWorkflowDefinitionValidationService _workflowDefinitionValidationService;
    private readonly IRotationRepository _rotationRepository;
    private readonly IWorkflowAuditWriteOperations _auditWrite;
    private readonly IWorkflowTaskGenerationService _taskGeneration;
    private readonly IWorkflowStatusCalculationService _statusCalculation;
    private readonly IWorkflowNotificationDispatchOperations _notificationDispatch;
    private readonly IWorkflowAutomationOperations _automation;

    public PostgresWorkflowRepository()
        : this(new WorkflowDefinitionValidationService(), new PostgresRotationRepository(), new PostgresWorkflowAuditWriteOperations(), new PostgresWorkflowTaskGenerationService(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations(), new PostgresWorkflowAutomationOperations())
    {
    }

    internal PostgresWorkflowRepository(IWorkflowDefinitionValidationService workflowDefinitionValidationService)
        : this(workflowDefinitionValidationService, new PostgresRotationRepository(), new PostgresWorkflowAuditWriteOperations(), new PostgresWorkflowTaskGenerationService(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations(), new PostgresWorkflowAutomationOperations())
    {
    }

    internal PostgresWorkflowRepository(
        IWorkflowDefinitionValidationService workflowDefinitionValidationService,
        IRotationRepository rotationRepository)
        : this(workflowDefinitionValidationService, rotationRepository, new PostgresWorkflowAuditWriteOperations(), new PostgresWorkflowTaskGenerationService(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations(), new PostgresWorkflowAutomationOperations())
    {
    }

    internal PostgresWorkflowRepository(
        IWorkflowDefinitionValidationService workflowDefinitionValidationService,
        IRotationRepository rotationRepository,
        IWorkflowAuditWriteOperations auditWrite)
        : this(workflowDefinitionValidationService, rotationRepository, auditWrite, new PostgresWorkflowTaskGenerationService(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations(), new PostgresWorkflowAutomationOperations())
    {
    }

    internal PostgresWorkflowRepository(
        IWorkflowDefinitionValidationService workflowDefinitionValidationService,
        IRotationRepository rotationRepository,
        IWorkflowAuditWriteOperations auditWrite,
        IWorkflowTaskGenerationService taskGeneration)
        : this(workflowDefinitionValidationService, rotationRepository, auditWrite, taskGeneration, new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations(), new PostgresWorkflowAutomationOperations())
    {
    }

    internal PostgresWorkflowRepository(
        IWorkflowDefinitionValidationService workflowDefinitionValidationService,
        IRotationRepository rotationRepository,
        IWorkflowAuditWriteOperations auditWrite,
        IWorkflowTaskGenerationService taskGeneration,
        IWorkflowStatusCalculationService statusCalculation)
        : this(workflowDefinitionValidationService, rotationRepository, auditWrite, taskGeneration, statusCalculation, new PostgresWorkflowNotificationDispatchOperations(), new PostgresWorkflowAutomationOperations())
    {
    }

    internal PostgresWorkflowRepository(
        IWorkflowDefinitionValidationService workflowDefinitionValidationService,
        IRotationRepository rotationRepository,
        IWorkflowAuditWriteOperations auditWrite,
        IWorkflowTaskGenerationService taskGeneration,
        IWorkflowStatusCalculationService statusCalculation,
        IWorkflowNotificationDispatchOperations notificationDispatch)
        : this(workflowDefinitionValidationService, rotationRepository, auditWrite, taskGeneration, statusCalculation, notificationDispatch, new PostgresWorkflowAutomationOperations())
    {
    }

    internal PostgresWorkflowRepository(
        IWorkflowDefinitionValidationService workflowDefinitionValidationService,
        IRotationRepository rotationRepository,
        IWorkflowAuditWriteOperations auditWrite,
        IWorkflowTaskGenerationService taskGeneration,
        IWorkflowStatusCalculationService statusCalculation,
        IWorkflowNotificationDispatchOperations notificationDispatch,
        IWorkflowAutomationOperations automation)
    {
        _workflowDefinitionValidationService = workflowDefinitionValidationService;
        _rotationRepository = rotationRepository;
        _auditWrite = auditWrite;
        _taskGeneration = taskGeneration;
        _statusCalculation = statusCalculation;
        _notificationDispatch = notificationDispatch;
        _automation = automation;
    }

    // Die Verbindung wird bewusst direkt aus der Umgebung gelesen, damit API und Container identisch konfiguriert bleiben.
    private static string GetConnectionString()
    {
        return LifecycleRuntimeSettingsResolver.GetRequiredConnectionString();
    }
}

internal sealed class ProcessTypeCreateRecord
{
    public required int Id { get; init; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required bool RequiresSupervisorStep { get; init; }
    public string? ApprovalTaskTemplateKey { get; init; }
    public required bool RequiresTargetPerson { get; init; }
}
