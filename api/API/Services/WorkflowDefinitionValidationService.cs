using System.Text.Json;

namespace API;

// Z7-3.4: Duenne Facade — delegiert an Draft- und Snapshot-Validator.
internal sealed class WorkflowDefinitionValidationService : IWorkflowDefinitionValidationService
{
    public string NormalizeDefinitionKey(string? definitionKey)
        => WorkflowDefinitionDraftValidator.NormalizeDefinitionKey(definitionKey);

    public WorkflowDefinitionDraftValidationResult ValidateAndNormalize(ReplaceWorkflowDefinitionVersionRequest request)
        => WorkflowDefinitionDraftValidator.ValidateAndNormalize(request);

    public WorkflowDefinitionValidationSnapshot ValidateSnapshot(WorkflowDefinitionValidationContext context)
        => WorkflowDefinitionSnapshotValidator.ValidateSnapshot(context);
}

internal sealed class WorkflowDefinitionValidationContext
{
    public required IReadOnlyList<WorkflowDefinitionNodeDto> Nodes { get; init; }
    public required IReadOnlyList<WorkflowDefinitionEdgeDto> Edges { get; init; }
    public string? WorkflowDefinitionKey { get; init; }
    public bool RequiresSupervisorStep { get; init; }
    public IReadOnlyList<WorkflowDefinitionValidationIssue> ReferenceIssues { get; init; } = [];
}

internal sealed class WorkflowDefinitionValidationSnapshot
{
    public required bool CanSaveDraft { get; init; }
    public required bool CanPublish { get; init; }
    public required List<WorkflowDefinitionValidationIssue> Issues { get; init; }
}

internal sealed class WorkflowDefinitionValidationIssue
{
    public required string Code { get; init; }
    public required string Severity { get; init; }
    public required string Scope { get; init; }
    public required string Message { get; init; }
    public string? ReferenceKey { get; init; }
}

internal sealed class WorkflowDefinitionDraftValidationResult
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public required List<WorkflowDefinitionDraftNode> Nodes { get; init; }
    public required List<WorkflowDefinitionDraftEdge> Edges { get; init; }
}

internal sealed class WorkflowDefinitionDraftNode
{
    public required string NodeKey { get; init; }
    public required string NodeType { get; init; }
    public string? Title { get; init; }
    public int SortOrder { get; init; }
    public int? PositionX { get; init; }
    public int? PositionY { get; init; }
    public JsonElement? Config { get; init; }
    public required List<WorkflowDefinitionDraftNodeAction> Actions { get; init; }
    // FE-9: Specs reisen mit der Version. Bei `task`/`approval`-Nodes 0..1, bei
    // `measure_*` 0..N. Andere Node-Typen sollten leer bleiben (Validation).
    public required List<WorkflowDefinitionDraftNodeSpec> Specs { get; init; }
    // Slice 2: kanonisch lowercase (NormalizeAutomationAdminRole). Bei task-Nodes
    // mit Actions Pflicht; Validierung gegen WorkflowDefinitionValidationCatalog
    // .AllowedAutomationAdminRoles.
    public string? AutomationAdminRole { get; init; }
}

internal sealed class WorkflowDefinitionDraftNodeSpec
{
    public required string SpecKey { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string Description { get; init; }
    public string? IconKey { get; init; }
    public int? DefaultResponsibilityId { get; init; }
    public string? ProcessAreaLabel { get; init; }
    public bool IsDepartmentPhaseTask { get; init; }
    public bool IsRequired { get; init; }
    public int? DueInDays { get; init; }
    public int SortOrder { get; init; }
    public required List<WorkflowDefinitionDraftNodeSpecCondition> Conditions { get; init; }
    public required List<WorkflowDefinitionDraftNodeSpecDependency> Dependencies { get; init; }
}

internal sealed class WorkflowDefinitionDraftNodeSpecCondition
{
    public required string AnswerKey { get; init; }
    public required string Operator { get; init; }
    public string? ExpectedValueText { get; init; }
    public bool? ExpectedValueBoolean { get; init; }
    public decimal? ExpectedValueNumber { get; init; }
}

internal sealed class WorkflowDefinitionDraftNodeSpecDependency
{
    public required string DependsOnSpecKey { get; init; }
}

internal sealed class WorkflowDefinitionDraftEdge
{
    public required string SourceNodeKey { get; init; }
    public required string TargetNodeKey { get; init; }
    public int Priority { get; init; }
    public string? ConditionExpression { get; init; }
}

internal sealed class WorkflowDefinitionDraftNodeAction
{
    public string? ActionKey { get; init; }
    public int ExecutionOrder { get; init; }
    public required string OnErrorBehavior { get; init; }
    public JsonElement? InputMapping { get; init; }
}
