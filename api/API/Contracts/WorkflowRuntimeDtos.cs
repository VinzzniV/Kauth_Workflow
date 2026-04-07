using System.Text.Json;

namespace API;

public sealed class CreateWorkflowDefinitionInstanceRequest
{
    public string? WorkflowDefinitionKey { get; init; }
    public int? DepartmentId { get; init; }
    public int? RoleId { get; init; }
    public long? TargetPersonId { get; init; }
    public Guid? SourceWorkflowUid { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public int? EmployeeNumber { get; init; }
    public int? BadgeNumber { get; init; }
    public DateOnly? DeadlineDate { get; init; }
}

public sealed class WorkflowDefinitionRuntimeDetailDto
{
    public required long WorkflowId { get; init; }
    public required Guid WorkflowUid { get; init; }
    public required string WorkflowDefinitionKey { get; init; }
    public required string WorkflowDefinitionName { get; init; }
    public required long WorkflowDefinitionVersionId { get; init; }
    public required int WorkflowDefinitionVersionNumber { get; init; }
    public required string CurrentRuntimeStatus { get; init; }
    public required string LegacyWorkflowStatus { get; init; }
    public required string PrimaryLegacyProcessTypeKey { get; init; }
    public required string PrimaryLegacyProcessTypeName { get; init; }
    public required int DepartmentId { get; init; }
    public required int RoleId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public int? EmployeeNumber { get; init; }
    public int? BadgeNumber { get; init; }
    public long? TargetPersonId { get; init; }
    public DateOnly? DeadlineDate { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public required List<WorkflowNodeInstanceDto> NodeInstances { get; init; }
}

public sealed class WorkflowNodeInstanceDto
{
    public required long Id { get; init; }
    public required string NodeKey { get; init; }
    public required string NodeType { get; init; }
    public string? Title { get; init; }
    public required string Status { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public JsonElement? Result { get; init; }
}

public sealed class WorkflowRuntimeEventDto
{
    public required long Id { get; init; }
    public long? WorkflowNodeInstanceId { get; init; }
    public required string EventType { get; init; }
    public JsonElement? Payload { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed class CompleteRuntimeFormNodeRequest
{
    public required List<RequirementSelectionInputDto> RequirementSelections { get; init; }
}

public sealed class CompleteRuntimeApprovalNodeRequest
{
    public required bool Approved { get; init; }
}

public sealed class CompleteRuntimeTaskNodeRequest
{
    public string? Comment { get; init; }
}
