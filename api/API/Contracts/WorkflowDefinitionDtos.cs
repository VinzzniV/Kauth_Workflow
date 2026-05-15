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
    // Slice 2 (Admin-Gated-Automation, Task-Automation-Binding): bei task-Nodes mit
    // Actions Pflicht; bestimmt welche Admin-Rolle den Plan im Approval-Schritt
    // (Slice 3) bestaetigen darf. NULL fuer alle Nicht-task-Nodes und task-Nodes
    // ohne Actions.
    public string? AutomationAdminRole { get; init; }
    // FE-9: Task-Specs reisen mit der Version-DTO. Vorher hingen Specs implizit
    // am workflow_node_id der published Version; bei Versions-Wechseln gingen sie
    // verloren. Jetzt: jede Version traegt ihre Specs explizit, EnsureWorkingDraft
    // klont sie automatisch, Replace persistiert sie diff-basiert.
    public List<WorkflowDefinitionNodeSpecDto> Specs { get; init; } = new();
}

public sealed class WorkflowDefinitionNodeSpecDto
{
    public string? SpecKey { get; init; }
    public string? Title { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
    public string? IconKey { get; init; }
    public int? DefaultResponsibilityId { get; init; }
    public string? ProcessAreaLabel { get; init; }
    public bool IsDepartmentPhaseTask { get; init; }
    public bool IsRequired { get; init; }
    public int? DueInDays { get; init; }
    public int SortOrder { get; init; }
    public List<WorkflowDefinitionNodeSpecConditionDto> Conditions { get; init; } = new();
    public List<WorkflowDefinitionNodeSpecDependencyDto> Dependencies { get; init; } = new();
}

public sealed class WorkflowDefinitionNodeSpecConditionDto
{
    public string? AnswerKey { get; init; }
    public string? Operator { get; init; }
    public string? ExpectedValueText { get; init; }
    public bool? ExpectedValueBoolean { get; init; }
    public decimal? ExpectedValueNumber { get; init; }
}

public sealed class WorkflowDefinitionNodeSpecDependencyDto
{
    // Same-node-only: zeigt auf einen anderen Spec-Key am SELBEN node
    public string? DependsOnSpecKey { get; init; }
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
    public List<WorkflowDefinitionNodeDto> Nodes { get; init; } = new();
    public List<WorkflowDefinitionEdgeDto> Edges { get; init; } = new();

    // FE-13: Optimistic-Concurrency-Token. Wenn gesetzt, muss er zum aktuellen
    // updated_at der Version passen — sonst lehnt der Server den Replace mit
    // 409 Conflict ab. Nicht gesetzt = kein Stale-Check (Backwards-Compat).
    public DateTime? ExpectedUpdatedAt { get; init; }
}

public sealed class WorkflowDefinitionVersionConflictDto
{
    public required string Message { get; init; }
    public required DateTime CurrentUpdatedAt { get; init; }
}

public sealed class WorkflowDefinitionVersionStaleException : Exception
{
    public WorkflowDefinitionVersionStaleException(DateTime currentUpdatedAt, string? message = null)
        : base(message ?? "Workflow definition version was modified by another writer.")
    {
        CurrentUpdatedAt = currentUpdatedAt;
    }

    public DateTime CurrentUpdatedAt { get; }
}
