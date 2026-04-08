using System.Text.Json;

namespace API;

public sealed class WorkflowDefinitionSummaryDto
{
    public required int Id { get; init; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required List<WorkflowDefinitionVersionSummaryDto> Versions { get; init; }
}

public sealed class WorkflowStartableDefinitionDto
{
    public required string DefinitionKey { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required bool RequiresTargetPerson { get; init; }
    public required string PrimaryLegacyProcessTypeKey { get; init; }
    public required int LatestPublishedVersionNumber { get; init; }
}

public sealed class WorkflowDefinitionVersionSummaryDto
{
    public required long Id { get; init; }
    public required int WorkflowDefinitionId { get; init; }
    public required int VersionNumber { get; init; }
    public required string Status { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? PrimaryLegacyProcessTypeKey { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public DateTime? PublishedAt { get; init; }
    public required bool CanPublish { get; set; }
    public required List<ValidationIssueDto> ValidationIssues { get; set; }
}

public sealed class WorkflowDefinitionVersionDetailDto
{
    public required long Id { get; init; }
    public required int WorkflowDefinitionId { get; init; }
    public required string DefinitionKey { get; init; }
    public required string DefinitionName { get; init; }
    public string? DefinitionDescription { get; init; }
    public required int VersionNumber { get; init; }
    public required string Status { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? PrimaryLegacyProcessTypeKey { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public DateTime? PublishedAt { get; init; }
    public required bool CanPublish { get; set; }
    public required List<ValidationIssueDto> ValidationIssues { get; set; }
    public required List<WorkflowDefinitionNodeDto> Nodes { get; init; }
    public required List<WorkflowDefinitionEdgeDto> Edges { get; init; }
}

public sealed class ValidationIssueDto
{
    public required string Code { get; init; }
    public required string Severity { get; init; }
    public required string Scope { get; init; }
    public required string Message { get; init; }
    public string? ReferenceKey { get; init; }
}

public sealed class WorkflowDefinitionNodeDto
{
    public string? NodeKey { get; init; }
    public string? NodeType { get; init; }
    public string? Title { get; init; }
    public int SortOrder { get; init; }
    public int? PositionX { get; init; }
    public int? PositionY { get; init; }
    public JsonElement? Config { get; init; }
    public List<WorkflowNodeActionDto> Actions { get; init; } = new();
}

public sealed class WorkflowDefinitionEdgeDto
{
    public string? SourceNodeKey { get; init; }
    public string? TargetNodeKey { get; init; }
    public int Priority { get; init; }
    public string? ConditionExpression { get; init; }
}

public sealed class WorkflowNodeActionDto
{
    public string? ActionKey { get; init; }
    public JsonElement? InputMapping { get; init; }
    public int ExecutionOrder { get; init; }
    public string? OnErrorBehavior { get; init; }
}

public sealed class CreateWorkflowDefinitionRequest
{
    public string? Key { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
}

public sealed class UpdateWorkflowDefinitionRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
}

public sealed class CreateWorkflowDefinitionVersionRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
}

public sealed class ReplaceWorkflowDefinitionVersionRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? PrimaryLegacyProcessTypeKey { get; init; }
    public List<WorkflowDefinitionNodeDto> Nodes { get; init; } = new();
    public List<WorkflowDefinitionEdgeDto> Edges { get; init; } = new();
}
