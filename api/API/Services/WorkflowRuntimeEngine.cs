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

    // ====== Plan(...) — Slice 1.4 ====================================
    //
    // Pure Domain-Logik: nimmt einen Snapshot + completedNode, gibt einen
    // RuntimePlan zurueck. Spiegelt AdvanceRuntimeUntilWaitOrTerminal 1:1
    // (Reihenfolge der Steps, Failure-Verhalten, Decision-/Bridge-/Measure-
    // Spezialfaelle), aber ohne SQL.
    //
    // Failure: kein Throw — die Engine produziert WorkflowFailureOutcome.
    // Apply schreibt runtime_status=failed (Q4: Transaction ist die Grenze,
    // keine Saga).
    //
    // Q5: Engine bleibt frei von Spec-Inhalten. Spec-Lookup passiert im
    // Apply (Slice 1.5). Engine kennt nur den TemplateKey-Hint aus dem
    // Snapshot fuer den Bridge-Vergleich.
    //
    // Q6: Rekursiver Loop-Pfad aus TryCompleteRuntimeSetupNodeIfReady wird
    // nicht in der Engine modelliert. Apply signalisiert per ApplyResult,
    // ob ein Re-Plan noetig ist (Apply-seitige Iteration, Option a).
    public static WorkflowRuntimePlan Plan(
        WorkflowRuntimeSnapshot snapshot,
        WorkflowDefinitionNodeRecord completedNode)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(completedNode);

        var nodeSteps = new List<RuntimeNodeStep>();
        var pendingNodes = new Queue<WorkflowDefinitionNodeRecord>();
        var scheduledNodeIds = new HashSet<long>();

        IReadOnlyList<WorkflowDefinitionNodeRecord> initialResolved;
        try
        {
            initialResolved = ResolveNextNodes(snapshot.Graph, completedNode, snapshot.AnswersByKey);
        }
        catch (InvalidOperationException ex)
        {
            return BuildFailurePlan(nodeSteps, ex.Message);
        }

        foreach (var nextNode in initialResolved)
        {
            EnqueueIfNeeded(pendingNodes, scheduledNodeIds, nextNode);
        }

        while (pendingNodes.Count > 0)
        {
            var nextNode = pendingNodes.Dequeue();
            scheduledNodeIds.Remove(nextNode.NodeId);

            // Idempotenz-Guard: NodeInstance existiert bereits in DB.
            if (snapshot.NodeInstanceStatusByWorkflowNodeId.ContainsKey(nextNode.NodeId))
            {
                continue;
            }

            // parallel_join: nur aktivieren, wenn alle eingehenden Branches done.
            if (!CanActivateNodePure(snapshot, nextNode))
            {
                continue;
            }

            // Supervisor-Approval-Bridge: approval-Knoten direkt als done
            // markieren, wenn der vorgelagerte Form-Knoten bereits Gatekeeper-
            // Form-Antworten erfasst hat und der TemplateKey passt.
            if (ShouldAutoCompleteBridgePure(snapshot, completedNode, nextNode))
            {
                nodeSteps.Add(new SupervisorApprovalBridgeSkipStep
                {
                    NodeId = nextNode.NodeId,
                    NodeKey = nextNode.NodeKey,
                    NodeType = nextNode.NodeType
                });

                try
                {
                    foreach (var resolved in ResolveNextNodes(snapshot.Graph, nextNode, snapshot.AnswersByKey))
                    {
                        EnqueueIfNeeded(pendingNodes, scheduledNodeIds, resolved);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    return BuildFailurePlan(nodeSteps, ex.Message);
                }
                continue;
            }

            switch (nextNode.NodeType)
            {
                case "start":
                case "parallel_split":
                case "parallel_join":
                case "end":
                {
                    nodeSteps.Add(new AutoCompleteStep
                    {
                        NodeId = nextNode.NodeId,
                        NodeKey = nextNode.NodeKey,
                        NodeType = nextNode.NodeType
                    });

                    try
                    {
                        foreach (var resolved in ResolveNextNodes(snapshot.Graph, nextNode, snapshot.AnswersByKey))
                        {
                            EnqueueIfNeeded(pendingNodes, scheduledNodeIds, resolved);
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        return BuildFailurePlan(nodeSteps, ex.Message);
                    }
                    break;
                }

                case "decision":
                {
                    WorkflowDefinitionNodeRecord? decisionTarget;
                    WorkflowDefinitionEdgeRecord? selectedEdge;
                    try
                    {
                        decisionTarget = ResolveDecisionTarget(snapshot.Graph, nextNode, snapshot.AnswersByKey, out selectedEdge);
                    }
                    catch (InvalidOperationException ex)
                    {
                        return BuildFailurePlan(nodeSteps, ex.Message);
                    }

                    if (decisionTarget is null || selectedEdge is null)
                    {
                        return BuildFailurePlan(nodeSteps,
                            $"Decision node '{nextNode.NodeKey}' has no matching outgoing edge.");
                    }

                    nodeSteps.Add(new DecisionStep
                    {
                        NodeId = nextNode.NodeId,
                        NodeKey = nextNode.NodeKey,
                        NodeType = nextNode.NodeType,
                        SelectedTargetNodeId = decisionTarget.NodeId,
                        SelectedTargetNodeKey = decisionTarget.NodeKey,
                        SelectedEdgeId = selectedEdge.EdgeId,
                        EdgePriority = selectedEdge.Priority
                    });

                    EnqueueIfNeeded(pendingNodes, scheduledNodeIds, decisionTarget);
                    break;
                }

                case "measure_provision":
                case "measure_deprovision":
                case "measure_change":
                case "measure_rename":
                {
                    nodeSteps.Add(new MeasureNodeActivationStep
                    {
                        NodeId = nextNode.NodeId,
                        NodeKey = nextNode.NodeKey,
                        NodeType = nextNode.NodeType
                    });
                    // Q6: ggf. sofortige Completion, wenn 0 Pflicht-Tasks generiert
                    // werden — wird im Apply-Pfad ueber ApplyResult signalisiert
                    // und vom Lifecycle-Service als Re-Plan getriggert.
                    break;
                }

                case "form":
                case "approval":
                case "task":
                {
                    nodeSteps.Add(new WaitNodeActivationStep
                    {
                        NodeId = nextNode.NodeId,
                        NodeKey = nextNode.NodeKey,
                        NodeType = nextNode.NodeType
                    });
                    break;
                }

                case "automation":
                {
                    if (!snapshot.Graph.NodeActionsByNodeId.TryGetValue(nextNode.NodeId, out var actions)
                        || actions.Count == 0)
                    {
                        return BuildFailurePlan(nodeSteps,
                            $"Automation node '{nextNode.NodeKey}' has no configured actions.");
                    }
                    nodeSteps.Add(new WaitNodeActivationStep
                    {
                        NodeId = nextNode.NodeId,
                        NodeKey = nextNode.NodeKey,
                        NodeType = nextNode.NodeType
                    });
                    break;
                }

                default:
                    return BuildFailurePlan(nodeSteps,
                        $"Node type '{nextNode.NodeType}' is not supported by the runtime.");
            }
        }

        return BuildOutcomePlan(snapshot, nodeSteps);
    }

    private static WorkflowRuntimePlan BuildFailurePlan(IReadOnlyList<RuntimeNodeStep> nodeSteps, string reason)
    {
        return new WorkflowRuntimePlan
        {
            NodeSteps = nodeSteps,
            Outcome = new WorkflowFailureOutcome { Reason = reason }
        };
    }

    private static WorkflowRuntimePlan BuildOutcomePlan(
        WorkflowRuntimeSnapshot snapshot,
        IReadOnlyList<RuntimeNodeStep> nodeSteps)
    {
        var postActiveNodes = ComputePostPlanActiveNodes(snapshot, nodeSteps);

        if (postActiveNodes.Count == 0)
        {
            return new WorkflowRuntimePlan
            {
                NodeSteps = nodeSteps,
                Outcome = new WorkflowCompletionOutcome()
            };
        }

        var hasMeasureActive = postActiveNodes.Any(n => IsMeasureGenerationNodeType(n.NodeType));

        var legacyStatus = MapLegacyStatusForActiveNodes(
            snapshot.Graph,
            postActiveNodes,
            snapshot.PrimaryLegacyProcessTypeKey,
            snapshot.RequiresSupervisorStep);

        return new WorkflowRuntimePlan
        {
            NodeSteps = nodeSteps,
            Outcome = new WorkflowWaitOutcome
            {
                LegacyStatus = legacyStatus,
                RequiresStatusRecalc = hasMeasureActive
            }
        };
    }

    // Berechnet die nach Plan-Apply aktiven Nodes:
    // - bestehende active-Nodes aus dem Snapshot (bleiben unveraendert)
    // - neue active-Nodes aus MeasureNodeActivationStep + WaitNodeActivationStep
    // Auto-Completes + Bridge-Skips schliessen NEUE Nodes ab — sie tauchen
    // im Snapshot noch nicht auf und gehen im Plan direkt auf done. Sie
    // tragen also nichts zur post-Plan-Active-Liste bei.
    private static IReadOnlyList<ActiveRuntimeNodeRecord> ComputePostPlanActiveNodes(
        WorkflowRuntimeSnapshot snapshot,
        IReadOnlyList<RuntimeNodeStep> nodeSteps)
    {
        var result = new List<ActiveRuntimeNodeRecord>();

        foreach (var (workflowNodeId, status) in snapshot.NodeInstanceStatusByWorkflowNodeId)
        {
            if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!snapshot.Graph.NodeById.TryGetValue(workflowNodeId, out var node))
            {
                continue;
            }
            result.Add(new ActiveRuntimeNodeRecord
            {
                NodeInstanceId = 0,
                NodeKey = node.NodeKey,
                NodeType = node.NodeType
            });
        }

        foreach (var step in nodeSteps)
        {
            if (step is MeasureNodeActivationStep or WaitNodeActivationStep)
            {
                result.Add(new ActiveRuntimeNodeRecord
                {
                    NodeInstanceId = 0,
                    NodeKey = step.NodeKey,
                    NodeType = step.NodeType
                });
            }
        }

        return result;
    }

    private static bool CanActivateNodePure(WorkflowRuntimeSnapshot snapshot, WorkflowDefinitionNodeRecord node)
    {
        if (!string.Equals(node.NodeType, "parallel_join", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!snapshot.Graph.IncomingEdgesByTargetNodeId.TryGetValue(node.NodeId, out var incomingEdges)
            || incomingEdges.Count == 0)
        {
            return true;
        }

        return incomingEdges
            .Select(edge => edge.SourceNodeId)
            .Distinct()
            .All(sourceNodeId =>
                snapshot.NodeInstanceStatusByWorkflowNodeId.TryGetValue(sourceNodeId, out var status)
                && string.Equals(status, "done", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldAutoCompleteBridgePure(
        WorkflowRuntimeSnapshot snapshot,
        WorkflowDefinitionNodeRecord completedNode,
        WorkflowDefinitionNodeRecord candidateNode)
    {
        if (!string.Equals(candidateNode.NodeType, "approval", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!snapshot.RequiresSupervisorStep
            || string.IsNullOrWhiteSpace(snapshot.ApprovalTaskTemplateKey))
        {
            return false;
        }

        var gatekeeperProcessTypeKey = TryGetNodeConfigString(completedNode, "legacyProcessTypeKey");
        if (!IsSupervisorGatekeeperNode(
                snapshot.Graph,
                completedNode.NodeKey,
                gatekeeperProcessTypeKey,
                snapshot.RequiresSupervisorStep))
        {
            return false;
        }

        if (!snapshot.ApprovalSpecByNodeId.TryGetValue(candidateNode.NodeId, out var approvalSpec))
        {
            return false;
        }

        return string.Equals(
            approvalSpec.TemplateKey,
            snapshot.ApprovalTaskTemplateKey,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMeasureGenerationNodeType(string? nodeType)
    {
        return !string.IsNullOrWhiteSpace(nodeType)
            && MeasureGenerationNodeTypes.Contains(nodeType.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static readonly string[] MeasureGenerationNodeTypes =
    [
        "measure_provision",
        "measure_deprovision",
        "measure_change",
        "measure_rename"
    ];
}

internal sealed class ActiveRuntimeNodeRecord
{
    public required long NodeInstanceId { get; init; }
    public required string NodeKey { get; init; }
    public required string NodeType { get; init; }
}
