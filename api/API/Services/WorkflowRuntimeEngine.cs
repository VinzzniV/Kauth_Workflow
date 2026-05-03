using System.Text.Json;

namespace API;

// Pure Domain-Engine fuer den Workflow-Runtime-Loop (Schritt 7, Slice 1.1).
//
// Diese Klasse enthaelt die DB-freien Bausteine, die der Runtime-Loop in
// PostgresWorkflowRuntimeRepository nutzt: Graph-Traversal, Decision-
// Auswertung, Status-Mapping und Config-Lookups. Alle Methoden arbeiten
// rein auf In-Memory-Records und sind deterministisch testbar.
//
// Slice 1.1 verschiebt nur bestehende Helper aus dem Repo hierher; das
// Verhalten bleibt 1:1 unveraendert. Die anschliessenden Slices fuegen
// Plan-Records + Engine.Plan(...) als zusaetzliche Eintrittspunkte hinzu.
internal static class WorkflowRuntimeEngine
{
    public static void EnqueueIfNeeded(
        Queue<WorkflowDefinitionNodeRecord> queue,
        ISet<long> scheduledNodeIds,
        WorkflowDefinitionNodeRecord node)
    {
        if (scheduledNodeIds.Add(node.NodeId))
        {
            queue.Enqueue(node);
        }
    }

    public static IReadOnlyList<WorkflowDefinitionNodeRecord> ResolveNextNodes(
        WorkflowDefinitionGraphRecord graph,
        WorkflowDefinitionNodeRecord currentNode,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        if (!graph.OutgoingEdgesBySourceNodeId.TryGetValue(currentNode.NodeId, out var outgoingEdges)
            || outgoingEdges.Count == 0)
        {
            return [];
        }

        if (string.Equals(currentNode.NodeType, "decision", StringComparison.OrdinalIgnoreCase))
        {
            var decisionTarget = ResolveDecisionTarget(graph, currentNode, answersByKey, out _);
            return decisionTarget is null ? [] : [decisionTarget];
        }

        if (string.Equals(currentNode.NodeType, "parallel_split", StringComparison.OrdinalIgnoreCase))
        {
            return outgoingEdges
                .Select(edge => graph.NodeById.GetValueOrDefault(edge.TargetNodeId))
                .Where(node => node is not null)
                .Cast<WorkflowDefinitionNodeRecord>()
                .ToList();
        }

        if (outgoingEdges.Count != 1)
        {
            throw new InvalidOperationException(
                $"Node '{currentNode.NodeKey}' uses {outgoingEdges.Count} outgoing edges, but only decision and parallel_split nodes may branch.");
        }

        var nextNode = graph.NodeById.GetValueOrDefault(outgoingEdges[0].TargetNodeId);
        return nextNode is null ? [] : [nextNode];
    }

    public static WorkflowDefinitionNodeRecord? ResolveDecisionTarget(
        WorkflowDefinitionGraphRecord graph,
        WorkflowDefinitionNodeRecord decisionNode,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        out WorkflowDefinitionEdgeRecord? selectedEdge)
    {
        selectedEdge = null;
        if (!graph.OutgoingEdgesBySourceNodeId.TryGetValue(decisionNode.NodeId, out var outgoingEdges)
            || outgoingEdges.Count == 0)
        {
            return null;
        }

        WorkflowDefinitionEdgeRecord? fallbackEdge = null;
        foreach (var edge in outgoingEdges.OrderBy(edge => edge.Priority).ThenBy(edge => edge.EdgeId))
        {
            if (string.IsNullOrWhiteSpace(edge.ConditionExpression))
            {
                fallbackEdge ??= edge;
                continue;
            }

            TaskTemplateConditionRecord condition;
            try
            {
                condition = ParseDecisionCondition(edge.ConditionExpression);
            }
            catch (InvalidOperationException ex)
            {
                var sourceKey = graph.NodeById.TryGetValue(edge.SourceNodeId, out var src) ? src.NodeKey : edge.SourceNodeId.ToString();
                var targetKey = graph.NodeById.TryGetValue(edge.TargetNodeId, out var tgt) ? tgt.NodeKey : edge.TargetNodeId.ToString();
                throw new InvalidOperationException(
                    $"Decision condition on edge '{sourceKey}' → '{targetKey}' is invalid: {ex.Message}", ex);
            }

            if (TaskConditionEvaluator.EvaluateCondition(condition, answersByKey))
            {
                selectedEdge = edge;
                return graph.NodeById.GetValueOrDefault(edge.TargetNodeId);
            }
        }

        if (fallbackEdge is not null)
        {
            selectedEdge = fallbackEdge;
            return graph.NodeById.GetValueOrDefault(fallbackEdge.TargetNodeId);
        }

        return null;
    }

    public static TaskTemplateConditionRecord ParseDecisionCondition(string conditionExpression)
    {
        try
        {
            using var document = JsonDocument.Parse(conditionExpression);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Decision condition must be a JSON object.");
            }

            if (!root.TryGetProperty("answerKey", out var answerKeyProperty)
                || answerKeyProperty.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(answerKeyProperty.GetString()))
            {
                throw new InvalidOperationException("Decision condition requires answerKey.");
            }

            if (!root.TryGetProperty("operator", out var operatorProperty)
                || operatorProperty.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(operatorProperty.GetString()))
            {
                throw new InvalidOperationException("Decision condition requires operator.");
            }

            return new TaskTemplateConditionRecord
            {
                TaskTemplateId = 0,
                ConditionGroup = 0,
                AnswerKey = answerKeyProperty.GetString()!.Trim(),
                Operator = operatorProperty.GetString()!.Trim().ToLowerInvariant(),
                ExpectedValueText = root.TryGetProperty("expectedValueText", out var expectedTextProperty)
                    && expectedTextProperty.ValueKind == JsonValueKind.String
                    ? expectedTextProperty.GetString()
                    : null,
                ExpectedValueBoolean = root.TryGetProperty("expectedValueBoolean", out var expectedBooleanProperty)
                    && expectedBooleanProperty.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? expectedBooleanProperty.GetBoolean()
                    : null,
                ExpectedValueNumber = root.TryGetProperty("expectedValueNumber", out var expectedNumberProperty)
                    && expectedNumberProperty.ValueKind == JsonValueKind.Number
                    && expectedNumberProperty.TryGetDecimal(out var expectedNumber)
                        ? expectedNumber
                        : null
            };
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Decision condition is not valid JSON: {ex.Message}", ex);
        }
    }

    public static WorkflowDefinitionSupervisorGatekeeperEvaluation EvaluateSupervisorGatekeeper(
        WorkflowDefinitionGraphRecord graph,
        string? primaryLegacyProcessTypeKey,
        bool requiresSupervisorStep)
    {
        return WorkflowDefinitionSupervisorGatekeeperRules.Evaluate(
            graph.Nodes.Select(node => new WorkflowDefinitionSupervisorGatekeeperNode
            {
                NodeKey = node.NodeKey,
                NodeType = node.NodeType,
                LegacyProcessTypeKey = TryGetNodeConfigString(node, "legacyProcessTypeKey")
            }).ToList(),
            graph.Edges.Select(edge => new WorkflowDefinitionSupervisorGatekeeperEdge
            {
                SourceNodeKey = graph.NodeById[edge.SourceNodeId].NodeKey,
                TargetNodeKey = graph.NodeById[edge.TargetNodeId].NodeKey,
                Priority = edge.Priority
            }).ToList(),
            primaryLegacyProcessTypeKey,
            requiresSupervisorStep);
    }

    public static bool IsSupervisorGatekeeperNode(
        WorkflowDefinitionGraphRecord graph,
        string nodeKey,
        string? primaryLegacyProcessTypeKey,
        bool requiresSupervisorStep)
    {
        var evaluation = EvaluateSupervisorGatekeeper(graph, primaryLegacyProcessTypeKey, requiresSupervisorStep);
        return evaluation.IsSatisfied
            && string.Equals(evaluation.GatekeeperNodeKey, nodeKey, StringComparison.OrdinalIgnoreCase);
    }

    public static string MapLegacyStatusForActiveNodes(
        WorkflowDefinitionGraphRecord graph,
        IReadOnlyCollection<ActiveRuntimeNodeRecord> activeNodes,
        string? primaryLegacyProcessTypeKey,
        bool requiresSupervisorStep)
    {
        if (activeNodes.Any(node =>
                IsSupervisorGatekeeperNode(
                    graph,
                    node.NodeKey,
                    primaryLegacyProcessTypeKey,
                    requiresSupervisorStep)))
        {
            return "waiting_for_supervisor";
        }

        if (activeNodes.Any(node => string.Equals(node.NodeType, "approval", StringComparison.OrdinalIgnoreCase)))
        {
            return "waiting_for_supervisor";
        }

        if (activeNodes.Any(node => string.Equals(node.NodeType, "task", StringComparison.OrdinalIgnoreCase)))
        {
            return "waiting_for_department";
        }

        return "in_progress";
    }

    public static string GetRequiredNodeConfigString(WorkflowDefinitionNodeRecord node, string propertyName)
    {
        if (!node.Config.HasValue || node.Config.Value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Node '{node.NodeKey}' requires config property '{propertyName}'.");
        }

        if (!node.Config.Value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidOperationException(
                $"Node '{node.NodeKey}' requires config property '{propertyName}' as non-empty string.");
        }

        return property.GetString()!.Trim();
    }

    public static string? TryGetNodeConfigString(WorkflowDefinitionNodeRecord node, string propertyName)
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

        return property.GetString()!.Trim();
    }
}

internal sealed class ActiveRuntimeNodeRecord
{
    public required long NodeInstanceId { get; init; }
    public required string NodeKey { get; init; }
    public required string NodeType { get; init; }
}
