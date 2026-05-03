using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace API;

// Graph- und Validation-Mapping fuer Workflow-Definition-Versionen, ausgelagert
// aus PostgresWorkflowRepository.WorkflowDefinitionAdminOperations.cs (HQ3-Z3).
// Enthaelt: Validation-Snapshot-Build, Issue-Mapping, Draft-Mapping (DTO->Draft),
// Persist-Graph (Nodes + Configs + Actions + Edges), Position-Column-Detection.
//
// Cross-File-Helper (bleiben in WorkflowDefinitionAdminOperations.cs):
//   LoadDefinitionRequiresSupervisorStep, LegacyProcessTypeExists,
//   NormalizeWorkflowDefinitionOptionalText
internal sealed partial class PostgresWorkflowRepository
{
    internal static async Task<WorkflowDefinitionValidationSnapshot> BuildWorkflowDefinitionValidationSnapshot(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        WorkflowDefinitionVersionDetailDto detail,
        IWorkflowAutomationOperations automation,
        IWorkflowDefinitionValidationService validationService)
    {
        var referenceIssues = new List<WorkflowDefinitionValidationIssue>();
        var requiresSupervisorStep = await LoadDefinitionRequiresSupervisorStep(
            connection,
            transaction,
            detail.DefinitionKey);

        foreach (var node in detail.Nodes)
        {
            if (string.Equals(node.NodeType, "form", StringComparison.OrdinalIgnoreCase))
            {
                var processTypeKey = TryGetNodeConfigValue(node, "legacyProcessTypeKey");
                if (!string.IsNullOrWhiteSpace(processTypeKey)
                    && !await LegacyProcessTypeExists(connection, transaction, processTypeKey, requireActive: true))
                {
                    referenceIssues.Add(CreateDefinitionReferenceIssue(
                        "unknown_form_legacy_process_type",
                        $"Node '{node.NodeKey}' references unknown or inactive legacyProcessTypeKey '{processTypeKey}'.",
                        "workflow_node",
                        node.NodeKey));
                }
            }

            // LA5 + FE-9: task/approval-Nodes referenzieren Specs ueber workflow_node_task_specs.workflow_node_id
            // (statt frueher per legacyTemplateKey-String). Specs reisen seit FE-9 mit der Version-DTO und werden
            // in PersistWorkflowDefinitionVersionGraph persistiert. Der frueher in einem Plan-Kommentar referenzierte
            // ClonePreviousVersionTaskSpecs-Helper ist nicht mehr noetig — EnsureAdminWorkflowDefinitionWorkingDraft
            // klont Specs automatisch via ToDraftNode -> Persist.

            if (string.Equals(node.NodeType, "automation", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var action in node.Actions)
                {
                    if (string.IsNullOrWhiteSpace(action.ActionKey))
                    {
                        continue;
                    }

                    if (!await automation.ActionDefinitionExists(connection, transaction, action.ActionKey, requireActive: true))
                    {
                        referenceIssues.Add(CreateDefinitionReferenceIssue(
                            "unknown_action_definition",
                            $"Node '{node.NodeKey}' references unknown or inactive action '{action.ActionKey}'.",
                            "workflow_node",
                            node.NodeKey));
                    }
                }
            }
        }

        return validationService.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes = detail.Nodes,
            Edges = detail.Edges,
            WorkflowDefinitionKey = detail.DefinitionKey,
            RequiresSupervisorStep = requiresSupervisorStep,
            ReferenceIssues = referenceIssues
        });
    }

    private static List<ValidationIssueDto> MapValidationIssues(IEnumerable<WorkflowDefinitionValidationIssue> issues)
    {
        return issues
            .Select(issue => new ValidationIssueDto
            {
                Code = issue.Code,
                Severity = issue.Severity,
                Scope = issue.Scope,
                Message = issue.Message,
                ReferenceKey = issue.ReferenceKey
            })
            .ToList();
    }

    private static WorkflowDefinitionValidationIssue CreateDefinitionReferenceIssue(
        string code,
        string message,
        string scope,
        string? referenceKey = null)
    {
        return new WorkflowDefinitionValidationIssue
        {
            Code = code,
            Severity = "error",
            Scope = scope,
            Message = message,
            ReferenceKey = referenceKey
        };
    }

    private static string? TryGetNodeConfigValue(WorkflowDefinitionNodeDto node, string propertyName)
    {
        if (!node.Config.HasValue || node.Config.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!node.Config.Value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            return null;
        }

        return property.GetString()!.Trim().ToLowerInvariant();
    }

    // FE-9: Konvertiert eine NodeDto in eine DraftNode-Struktur fuer Persistierung.
    // Wird von EnsureAdminWorkflowDefinitionWorkingDraft genutzt — dadurch klonen
    // Specs jetzt automatisch von source -> draft, ohne Sondermethoden.
    private static WorkflowDefinitionDraftNode ToDraftNode(WorkflowDefinitionNodeDto node)
    {
        return new WorkflowDefinitionDraftNode
        {
            NodeKey = node.NodeKey ?? string.Empty,
            NodeType = node.NodeType ?? string.Empty,
            Title = NormalizeWorkflowDefinitionOptionalText(node.Title),
            SortOrder = node.SortOrder,
            PositionX = node.PositionX,
            PositionY = node.PositionY,
            Config = node.Config?.Clone(),
            Actions = (node.Actions ?? [])
                .Select(action => new WorkflowDefinitionDraftNodeAction
                {
                    ActionKey = NormalizeWorkflowDefinitionOptionalText(action.ActionKey),
                    ExecutionOrder = action.ExecutionOrder,
                    OnErrorBehavior = NormalizeWorkflowDefinitionOptionalText(action.OnErrorBehavior)?.ToLowerInvariant() ?? "fail_workflow",
                    InputMapping = action.InputMapping?.Clone()
                })
                .ToList(),
            Specs = (node.Specs ?? [])
                .Select(spec => new WorkflowDefinitionDraftNodeSpec
                {
                    SpecKey = spec.SpecKey ?? string.Empty,
                    Title = spec.Title ?? string.Empty,
                    Category = spec.Category ?? "general",
                    Description = spec.Description ?? string.Empty,
                    IconKey = NormalizeWorkflowDefinitionOptionalText(spec.IconKey),
                    DefaultResponsibilityId = spec.DefaultResponsibilityId,
                    ProcessAreaLabel = NormalizeWorkflowDefinitionOptionalText(spec.ProcessAreaLabel),
                    IsDepartmentPhaseTask = spec.IsDepartmentPhaseTask,
                    IsRequired = spec.IsRequired,
                    DueInDays = spec.DueInDays,
                    SortOrder = spec.SortOrder,
                    Conditions = (spec.Conditions ?? [])
                        .Select(c => new WorkflowDefinitionDraftNodeSpecCondition
                        {
                            AnswerKey = c.AnswerKey ?? string.Empty,
                            Operator = c.Operator ?? string.Empty,
                            ExpectedValueText = NormalizeWorkflowDefinitionOptionalText(c.ExpectedValueText),
                            ExpectedValueBoolean = c.ExpectedValueBoolean,
                            ExpectedValueNumber = c.ExpectedValueNumber,
                        })
                        .ToList(),
                    Dependencies = (spec.Dependencies ?? [])
                        .Select(d => new WorkflowDefinitionDraftNodeSpecDependency
                        {
                            DependsOnSpecKey = d.DependsOnSpecKey ?? string.Empty,
                        })
                        .ToList(),
                })
                .ToList()
        };
    }

    private static WorkflowDefinitionDraftEdge ToDraftEdge(WorkflowDefinitionEdgeDto edge)
    {
        return new WorkflowDefinitionDraftEdge
        {
            SourceNodeKey = edge.SourceNodeKey ?? string.Empty,
            TargetNodeKey = edge.TargetNodeKey ?? string.Empty,
            Priority = edge.Priority,
            ConditionExpression = NormalizeWorkflowDefinitionOptionalText(edge.ConditionExpression)
        };
    }

    private async Task PersistWorkflowDefinitionVersionGraph(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long versionId,
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        IReadOnlyList<WorkflowDefinitionDraftEdge> edges)
    {
        const string clearSql = """
DELETE FROM workflow_edges
WHERE workflow_definition_version_id = @versionId;

DELETE FROM workflow_node_configs
WHERE workflow_node_id IN (
    SELECT id
    FROM workflow_nodes
    WHERE workflow_definition_version_id = @versionId
);

DELETE FROM workflow_nodes
WHERE workflow_definition_version_id = @versionId;
""";

        await using (var clearCommand = new NpgsqlCommand(clearSql, connection, transaction))
        {
            clearCommand.Parameters.AddWithValue("versionId", versionId);
            await clearCommand.ExecuteNonQueryAsync();
        }

        var nodeIdByKey = new Dictionary<string, long>(StringComparer.Ordinal);
        const string insertNodeSql = """
INSERT INTO workflow_nodes (
    workflow_definition_version_id,
    node_key,
    node_type,
    title,
    sort_order,
    position_x,
    position_y
)
VALUES (
    @versionId,
    @nodeKey,
    @nodeType,
    @title,
    @sortOrder,
    @positionX,
    @positionY
)
RETURNING id;
""";

        const string insertConfigSql = """
INSERT INTO workflow_node_configs (
    workflow_node_id,
    config_json
)
VALUES (
    @workflowNodeId,
    @configJson
);
""";

        const string insertNodeActionSql = """
INSERT INTO workflow_node_actions (
    workflow_node_id,
    action_definition_id,
    input_mapping_json,
    execution_order,
    on_error_behavior
)
VALUES (
    @workflowNodeId,
    @actionDefinitionId,
    @inputMappingJson,
    @executionOrder,
    @onErrorBehavior
);
""";

        foreach (var node in nodes)
        {
            await using var insertNodeCommand = new NpgsqlCommand(insertNodeSql, connection, transaction);
            insertNodeCommand.Parameters.AddWithValue("versionId", versionId);
            insertNodeCommand.Parameters.AddWithValue("nodeKey", node.NodeKey);
            insertNodeCommand.Parameters.AddWithValue("nodeType", node.NodeType);
            insertNodeCommand.Parameters.AddWithValue("title", (object?)node.Title ?? DBNull.Value);
            insertNodeCommand.Parameters.AddWithValue("sortOrder", node.SortOrder);
            insertNodeCommand.Parameters.AddWithValue("positionX", (object?)node.PositionX ?? DBNull.Value);
            insertNodeCommand.Parameters.AddWithValue("positionY", (object?)node.PositionY ?? DBNull.Value);

            var createdNodeId = await insertNodeCommand.ExecuteScalarAsync();
            if (createdNodeId is not long workflowNodeId)
            {
                throw new InvalidOperationException($"Workflow node '{node.NodeKey}' could not be created.");
            }

            nodeIdByKey.Add(node.NodeKey, workflowNodeId);

            if (node.Config.HasValue)
            {
                await using var insertConfigCommand = new NpgsqlCommand(insertConfigSql, connection, transaction);
                insertConfigCommand.Parameters.AddWithValue("workflowNodeId", workflowNodeId);
                insertConfigCommand.Parameters.Add(
                    new NpgsqlParameter("configJson", NpgsqlDbType.Jsonb)
                    {
                        Value = JsonSerializer.Serialize(node.Config.Value)
                    });
                await insertConfigCommand.ExecuteNonQueryAsync();
            }

            foreach (var action in node.Actions)
            {
                var actionDefinitionId = await _automation.ResolveActionDefinitionIdByKey(
                    connection,
                    transaction,
                    action.ActionKey!,
                    requireActive: false);

                await using var insertActionCommand = new NpgsqlCommand(insertNodeActionSql, connection, transaction);
                insertActionCommand.Parameters.AddWithValue("workflowNodeId", workflowNodeId);
                insertActionCommand.Parameters.AddWithValue("actionDefinitionId", actionDefinitionId);
                insertActionCommand.Parameters.Add(
                    new NpgsqlParameter("inputMappingJson", NpgsqlDbType.Jsonb)
                    {
                        Value = action.InputMapping.HasValue ? JsonSerializer.Serialize(action.InputMapping.Value) : DBNull.Value
                    });
                insertActionCommand.Parameters.AddWithValue("executionOrder", action.ExecutionOrder);
                insertActionCommand.Parameters.AddWithValue("onErrorBehavior", action.OnErrorBehavior);
                await insertActionCommand.ExecuteNonQueryAsync();
            }
        }

        // FE-9: Specs (+ Conditions + Dependencies) pro Node persistieren.
        // Reihenfolge: erst alle Specs aller Nodes anlegen (Sammeln der neuen IDs),
        // dann Conditions, dann Dependencies (mit Spec-Key-Lookup auf NEUE IDs).
        // Composite-FK auf workflow_node_task_specs (id, workflow_node_id) erzwingt
        // Same-Node-Scope; Validation hat das vorher schon gepruft.
        var specIdByNodeAndKey = new Dictionary<(long NodeId, string SpecKey), long>();

        const string insertSpecSql = """
INSERT INTO workflow_node_task_specs (
    workflow_node_id,
    spec_key,
    title,
    category,
    description,
    icon_key,
    default_responsibility_id,
    process_area_label,
    is_department_phase_task,
    is_required,
    due_in_days,
    sort_order
)
VALUES (
    @workflowNodeId,
    @specKey,
    @title,
    @category,
    @description,
    @iconKey,
    @defaultResponsibilityId,
    @processAreaLabel,
    @isDepartmentPhaseTask,
    @isRequired,
    @dueInDays,
    @sortOrder
)
RETURNING id;
""";

        foreach (var node in nodes)
        {
            if (node.Specs.Count == 0)
            {
                continue;
            }

            var workflowNodeId = nodeIdByKey[node.NodeKey];
            foreach (var spec in node.Specs)
            {
                await using var insertSpecCommand = new NpgsqlCommand(insertSpecSql, connection, transaction);
                insertSpecCommand.Parameters.AddWithValue("workflowNodeId", workflowNodeId);
                insertSpecCommand.Parameters.AddWithValue("specKey", spec.SpecKey);
                insertSpecCommand.Parameters.AddWithValue("title", spec.Title);
                insertSpecCommand.Parameters.AddWithValue("category", spec.Category);
                insertSpecCommand.Parameters.AddWithValue("description", spec.Description);
                insertSpecCommand.Parameters.AddWithValue("iconKey", PostgresRepositorySharedHelpers.NormalizeAdminTaskTemplateIconKey(spec.IconKey));
                insertSpecCommand.Parameters.AddWithValue("defaultResponsibilityId", (object?)spec.DefaultResponsibilityId ?? DBNull.Value);
                insertSpecCommand.Parameters.AddWithValue("processAreaLabel", (object?)spec.ProcessAreaLabel ?? DBNull.Value);
                insertSpecCommand.Parameters.AddWithValue("isDepartmentPhaseTask", spec.IsDepartmentPhaseTask);
                insertSpecCommand.Parameters.AddWithValue("isRequired", spec.IsRequired);
                insertSpecCommand.Parameters.AddWithValue("dueInDays", (object?)spec.DueInDays ?? DBNull.Value);
                insertSpecCommand.Parameters.AddWithValue("sortOrder", spec.SortOrder);

                var specIdResult = await insertSpecCommand.ExecuteScalarAsync();
                if (specIdResult is not long specDbId)
                {
                    throw new InvalidOperationException($"Spec '{spec.SpecKey}' for node '{node.NodeKey}' could not be created.");
                }

                specIdByNodeAndKey.Add((workflowNodeId, spec.SpecKey), specDbId);
            }
        }

        const string insertConditionSql = """
INSERT INTO workflow_node_task_spec_conditions (
    workflow_node_task_spec_id,
    answer_key,
    operator,
    expected_value_text,
    expected_value_boolean,
    expected_value_number
)
VALUES (
    @workflowNodeTaskSpecId,
    @answerKey,
    @operator,
    @expectedValueText,
    @expectedValueBoolean,
    @expectedValueNumber
);
""";

        const string insertDependencySql = """
INSERT INTO workflow_node_task_spec_dependencies (
    workflow_node_task_spec_id,
    depends_on_workflow_node_task_spec_id,
    workflow_node_id
)
VALUES (
    @workflowNodeTaskSpecId,
    @dependsOnWorkflowNodeTaskSpecId,
    @workflowNodeId
);
""";

        foreach (var node in nodes)
        {
            if (node.Specs.Count == 0)
            {
                continue;
            }

            var workflowNodeId = nodeIdByKey[node.NodeKey];
            foreach (var spec in node.Specs)
            {
                var specDbId = specIdByNodeAndKey[(workflowNodeId, spec.SpecKey)];

                foreach (var condition in spec.Conditions)
                {
                    await using var insertConditionCommand = new NpgsqlCommand(insertConditionSql, connection, transaction);
                    insertConditionCommand.Parameters.AddWithValue("workflowNodeTaskSpecId", specDbId);
                    insertConditionCommand.Parameters.AddWithValue("answerKey", condition.AnswerKey);
                    insertConditionCommand.Parameters.AddWithValue("operator", condition.Operator);
                    insertConditionCommand.Parameters.AddWithValue("expectedValueText", (object?)condition.ExpectedValueText ?? DBNull.Value);
                    insertConditionCommand.Parameters.AddWithValue("expectedValueBoolean", (object?)condition.ExpectedValueBoolean ?? DBNull.Value);
                    insertConditionCommand.Parameters.AddWithValue("expectedValueNumber", (object?)condition.ExpectedValueNumber ?? DBNull.Value);
                    await insertConditionCommand.ExecuteNonQueryAsync();
                }

                foreach (var dependency in spec.Dependencies)
                {
                    if (!specIdByNodeAndKey.TryGetValue((workflowNodeId, dependency.DependsOnSpecKey), out var dependsOnSpecDbId))
                    {
                        // Validation hat das eigentlich schon abgefangen — hier defensiv.
                        throw new InvalidOperationException(
                            $"Spec '{spec.SpecKey}' for node '{node.NodeKey}' depends on '{dependency.DependsOnSpecKey}' which is not a sibling spec.");
                    }

                    await using var insertDependencyCommand = new NpgsqlCommand(insertDependencySql, connection, transaction);
                    insertDependencyCommand.Parameters.AddWithValue("workflowNodeTaskSpecId", specDbId);
                    insertDependencyCommand.Parameters.AddWithValue("dependsOnWorkflowNodeTaskSpecId", dependsOnSpecDbId);
                    insertDependencyCommand.Parameters.AddWithValue("workflowNodeId", workflowNodeId);
                    await insertDependencyCommand.ExecuteNonQueryAsync();
                }
            }
        }

        const string insertEdgeSql = """
INSERT INTO workflow_edges (
    workflow_definition_version_id,
    source_workflow_node_id,
    target_workflow_node_id,
    priority,
    condition_expression
)
VALUES (
    @versionId,
    @sourceNodeId,
    @targetNodeId,
    @priority,
    @conditionExpression
);
""";

        foreach (var edge in edges)
        {
            await using var insertEdgeCommand = new NpgsqlCommand(insertEdgeSql, connection, transaction);
            insertEdgeCommand.Parameters.AddWithValue("versionId", versionId);
            insertEdgeCommand.Parameters.AddWithValue("sourceNodeId", nodeIdByKey[edge.SourceNodeKey]);
            insertEdgeCommand.Parameters.AddWithValue("targetNodeId", nodeIdByKey[edge.TargetNodeKey]);
            insertEdgeCommand.Parameters.AddWithValue("priority", edge.Priority);
            insertEdgeCommand.Parameters.AddWithValue("conditionExpression", (object?)edge.ConditionExpression ?? DBNull.Value);
            await insertEdgeCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task<bool> HasWorkflowNodePositionColumns(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        const string sql = """
SELECT COUNT(*)
FROM information_schema.columns
WHERE table_schema = current_schema()
  AND table_name = 'workflow_nodes'
  AND column_name IN ('position_x', 'position_y');
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var scalar = await command.ExecuteScalarAsync();
        return scalar is long count && count >= 2;
    }
}
