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

            // LA5: task/approval-Nodes referenzieren Specs ueber workflow_node_task_specs.workflow_node_id
            // (statt frueher per legacyTemplateKey-String). Spec-Existenz wird beim Persistieren der
            // Version sichergestellt (siehe ClonePreviousVersionTaskSpecs in PersistWorkflowDefinitionVersionGraph).

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
