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
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        IReadOnlyDictionary<string, JsonElement>? automationOutputsByNodeKey = null)
    {
        if (!graph.OutgoingEdgesBySourceNodeId.TryGetValue(currentNode.NodeId, out var outgoingEdges)
            || outgoingEdges.Count == 0)
        {
            return [];
        }

        if (string.Equals(currentNode.NodeType, "decision", StringComparison.OrdinalIgnoreCase))
        {
            var decisionTarget = ResolveDecisionTarget(graph, currentNode, answersByKey, out _, automationOutputsByNodeKey);
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
        out WorkflowDefinitionEdgeRecord? selectedEdge,
        IReadOnlyDictionary<string, JsonElement>? automationOutputsByNodeKey = null)
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

            DecisionConditionExpressionRecord expression;
            try
            {
                expression = ParseDecisionConditionExpression(edge.ConditionExpression);
            }
            catch (InvalidOperationException ex)
            {
                var sourceKey = graph.NodeById.TryGetValue(edge.SourceNodeId, out var src) ? src.NodeKey : edge.SourceNodeId.ToString();
                var targetKey = graph.NodeById.TryGetValue(edge.TargetNodeId, out var tgt) ? tgt.NodeKey : edge.TargetNodeId.ToString();
                throw new InvalidOperationException(
                    $"Decision condition on edge '{sourceKey}' → '{targetKey}' is invalid: {ex.Message}", ex);
            }

            // Schranke 2+3 (Plan-Zeit): Direct-Predecessor + Action-Key gegen den
            // Graph-Kontext pruefen, bevor evaluiert wird.
            ValidateAutomationOutputReferences(graph, decisionNode, expression);

            if (EvaluateDecisionConditionExpression(expression, answersByKey, automationOutputsByNodeKey))
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

    // Schranken 2+3 fuer automation_output-Bedingungen. Wirft InvalidOperationException,
    // die der Plan(...)-try/catch in BuildFailurePlan ueberfuehrt.
    //   Schranke 2: sourceNodeKey muss ein direkter eingehender Predecessor der Decision-Node sein.
    //   Schranke 3: Predecessor-Node muss genau eine Action tragen, deren action_key in
    //               AllowedConditionProperties whitelisted ist; Property muss in der
    //               action_key-spezifischen Whitelist liegen.
    private static void ValidateAutomationOutputReferences(
        WorkflowDefinitionGraphRecord graph,
        WorkflowDefinitionNodeRecord decisionNode,
        DecisionConditionExpressionRecord expression)
    {
        foreach (var condition in expression.Conditions)
        {
            if (condition is not DecisionConditionRecord.AutomationOutputBased automationOutput)
            {
                continue;
            }

            var sourceNodeKey = automationOutput.Condition.SourceNodeKey;

            // Schranke 2: Direct-Predecessor.
            var incomingEdges = graph.IncomingEdgesByTargetNodeId.TryGetValue(decisionNode.NodeId, out var edges)
                ? edges
                : (IReadOnlyList<WorkflowDefinitionEdgeRecord>)[];
            WorkflowDefinitionNodeRecord? predecessorNode = null;
            foreach (var incomingEdge in incomingEdges)
            {
                if (!graph.NodeById.TryGetValue(incomingEdge.SourceNodeId, out var candidate))
                {
                    continue;
                }
                if (string.Equals(candidate.NodeKey, sourceNodeKey, StringComparison.OrdinalIgnoreCase))
                {
                    predecessorNode = candidate;
                    break;
                }
            }

            if (predecessorNode is null)
            {
                throw new InvalidOperationException(
                    $"Decision condition on node '{decisionNode.NodeKey}' references node '{sourceNodeKey}', which is not a direct predecessor. Only direct incoming-edge sources are allowed.");
            }

            // Schranke 3: Action-Key + Property-Whitelist.
            if (!graph.NodeActionsByNodeId.TryGetValue(predecessorNode.NodeId, out var actions)
                || actions.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Decision condition references node '{sourceNodeKey}' which has no automation actions; automation_output conditions require a producer node.");
            }

            if (actions.Count > 1)
            {
                var actionKeys = string.Join(", ", actions.Select(a => a.ActionKey));
                throw new InvalidOperationException(
                    $"Decision condition references node '{sourceNodeKey}', which carries multiple actions [{actionKeys}]. automation_output conditions require an unambiguous single producer action; refactor the workflow so the referenced node has exactly one whitelisted action.");
            }

            var actionKey = actions[0].ActionKey;
            if (!AllowedConditionProperties.TryGetValue(actionKey, out var allowedProperties))
            {
                var whitelistedProducers = string.Join(", ", AllowedConditionProperties.Keys);
                throw new InvalidOperationException(
                    $"Decision condition references node '{sourceNodeKey}' with action_key '{actionKey}', but this action is not a supported producer for automation_output conditions. Whitelisted producers: [{whitelistedProducers}].");
            }

            if (!allowedProperties.Contains(automationOutput.Condition.Property))
            {
                var allowed = string.Join(", ", allowedProperties);
                throw new InvalidOperationException(
                    $"Decision condition references property '{automationOutput.Condition.Property}' on action '{actionKey}', which is not in the whitelist [{allowed}].");
            }
        }
    }

    // Action-Key -> erlaubte Output-Properties in workflow_edges.condition_expression.
    // Bewusst minimal: nur Outcome-Flags, keine Identifier (kein distinguishedName,
    // kein UPN, ...). Erweiterung erfordert auch eine Eintragung in
    // AutomationPropertyCatalog.CreatedAdUserConditionProperties (Drift-Schutz via Test).
    internal static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedConditionProperties
        = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["CreateAdUserLdaps"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "alreadyExisted" }
        };

    // Operator-Set fuer automation_output-Bedingungen. Boolean-only im ersten Aufschlag;
    // String-/Number-Compares folgen mit dem jeweiligen Use-Case.
    internal static readonly IReadOnlySet<string> AllowedAutomationOutputOperators
        = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "is_true", "is_false" };

    private static readonly IReadOnlySet<string> AllAllowedConditionPropertiesUnion
        = AllowedConditionProperties.Values
            .SelectMany(set => set)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Erlaubt zwei Formen:
    //   Single (legacy):  { answerKey, operator, expectedValue* }  oder  { referenceKind, ... }
    //   Multi:            { logic: "AND" | "OR", conditions: [ ... ] }
    // Single-Form ohne `referenceKind` bleibt als `answer`-Default fuer Rueckwaertskompat.
    public static DecisionConditionExpressionRecord ParseDecisionConditionExpression(string conditionExpression)
    {
        try
        {
            using var document = JsonDocument.Parse(conditionExpression);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Decision condition must be a JSON object.");
            }

            if (root.TryGetProperty("conditions", out var conditionsProperty))
            {
                return ParseMultiForm(root, conditionsProperty);
            }

            return new DecisionConditionExpressionRecord
            {
                Logic = DecisionConditionLogic.And,
                Conditions = new List<DecisionConditionRecord> { ParseConditionElement(root) }
            };
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Decision condition is not valid JSON: {ex.Message}", ex);
        }
    }

    // Evaluiert eine Decision-Bedingung gegen Answer- und Automation-Output-Quellen.
    // `automationOutputsByNodeKey`: Output-JSON pro `workflow_nodes.node_key`. Default leer
    // fuer reine Answer-Bedingungen (Backwards-Compat).
    public static bool EvaluateDecisionConditionExpression(
        DecisionConditionExpressionRecord expression,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        IReadOnlyDictionary<string, JsonElement>? automationOutputsByNodeKey = null)
    {
        if (expression.Conditions.Count == 0)
        {
            return false;
        }

        return expression.Logic == DecisionConditionLogic.Or
            ? expression.Conditions.Any(c => EvaluateDecisionCondition(c, answersByKey, automationOutputsByNodeKey))
            : expression.Conditions.All(c => EvaluateDecisionCondition(c, answersByKey, automationOutputsByNodeKey));
    }

    private static bool EvaluateDecisionCondition(
        DecisionConditionRecord condition,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        IReadOnlyDictionary<string, JsonElement>? automationOutputsByNodeKey)
    {
        return condition switch
        {
            DecisionConditionRecord.AnswerBased a => TaskConditionEvaluator.EvaluateCondition(a.Condition, answersByKey),
            DecisionConditionRecord.AutomationOutputBased o => EvaluateAutomationOutputCondition(o.Condition, automationOutputsByNodeKey),
            _ => throw new InvalidOperationException($"Unknown decision condition variant: {condition.GetType().Name}.")
        };
    }

    private static bool EvaluateAutomationOutputCondition(
        AutomationOutputConditionRecord condition,
        IReadOnlyDictionary<string, JsonElement>? automationOutputsByNodeKey)
    {
        if (automationOutputsByNodeKey is null
            || !automationOutputsByNodeKey.TryGetValue(condition.SourceNodeKey, out var outputJson))
        {
            throw new InvalidOperationException(
                $"Decision condition references node '{condition.SourceNodeKey}' but no succeeded automation output was found for it in this workflow run.");
        }

        if (outputJson.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Automation output for node '{condition.SourceNodeKey}' is not a JSON object (kind: {outputJson.ValueKind}).");
        }

        if (!outputJson.TryGetProperty(condition.Property, out var propertyValue))
        {
            throw new InvalidOperationException(
                $"Expected boolean property '{condition.Property}' on node '{condition.SourceNodeKey}' output, but the property is missing.");
        }

        var booleanValue = propertyValue.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidOperationException(
                $"Expected boolean property '{condition.Property}' on node '{condition.SourceNodeKey}' output, found {propertyValue.ValueKind}.")
        };

        return condition.Operator switch
        {
            "is_true" => booleanValue,
            "is_false" => !booleanValue,
            _ => throw new InvalidOperationException(
                $"Unsupported operator '{condition.Operator}' for automation_output condition on node '{condition.SourceNodeKey}'.")
        };
    }

    private static DecisionConditionExpressionRecord ParseMultiForm(JsonElement root, JsonElement conditionsProperty)
    {
        if (conditionsProperty.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Decision condition 'conditions' must be an array.");
        }

        var logic = DecisionConditionLogic.And;
        if (root.TryGetProperty("logic", out var logicProperty)
            && logicProperty.ValueKind == JsonValueKind.String)
        {
            var raw = logicProperty.GetString()!.Trim().ToUpperInvariant();
            logic = raw switch
            {
                "AND" => DecisionConditionLogic.And,
                "OR" => DecisionConditionLogic.Or,
                _ => throw new InvalidOperationException($"Decision condition 'logic' must be AND or OR, got '{raw}'.")
            };
        }

        var conditions = new List<DecisionConditionRecord>();
        foreach (var item in conditionsProperty.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Decision condition entry must be a JSON object.");
            }
            conditions.Add(ParseConditionElement(item));
        }

        if (conditions.Count == 0)
        {
            throw new InvalidOperationException("Decision condition 'conditions' must contain at least one entry.");
        }

        return new DecisionConditionExpressionRecord
        {
            Logic = logic,
            Conditions = conditions
        };
    }

    private static DecisionConditionRecord ParseConditionElement(JsonElement element)
    {
        var referenceKind = "answer";
        if (element.TryGetProperty("referenceKind", out var referenceKindProperty)
            && referenceKindProperty.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(referenceKindProperty.GetString()))
        {
            referenceKind = referenceKindProperty.GetString()!.Trim().ToLowerInvariant();
        }

        return referenceKind switch
        {
            "answer" => new DecisionConditionRecord.AnswerBased(ParseAnswerConditionElement(element)),
            "automation_output" => new DecisionConditionRecord.AutomationOutputBased(ParseAutomationOutputConditionElement(element)),
            _ => throw new InvalidOperationException(
                $"Decision condition 'referenceKind' must be one of [answer, automation_output], got '{referenceKind}'.")
        };
    }

    private static TaskTemplateConditionRecord ParseAnswerConditionElement(JsonElement element)
    {
        if (!element.TryGetProperty("answerKey", out var answerKeyProperty)
            || answerKeyProperty.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(answerKeyProperty.GetString()))
        {
            throw new InvalidOperationException("Decision condition requires answerKey.");
        }

        if (!element.TryGetProperty("operator", out var operatorProperty)
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
            ExpectedValueText = element.TryGetProperty("expectedValueText", out var expectedTextProperty)
                && expectedTextProperty.ValueKind == JsonValueKind.String
                ? expectedTextProperty.GetString()
                : null,
            ExpectedValueBoolean = element.TryGetProperty("expectedValueBoolean", out var expectedBooleanProperty)
                && expectedBooleanProperty.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? expectedBooleanProperty.GetBoolean()
                : null,
            ExpectedValueNumber = element.TryGetProperty("expectedValueNumber", out var expectedNumberProperty)
                && expectedNumberProperty.ValueKind == JsonValueKind.Number
                && expectedNumberProperty.TryGetDecimal(out var expectedNumber)
                    ? expectedNumber
                    : null
        };
    }

    private static AutomationOutputConditionRecord ParseAutomationOutputConditionElement(JsonElement element)
    {
        if (!element.TryGetProperty("sourceNodeKey", out var sourceNodeKeyProperty)
            || sourceNodeKeyProperty.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(sourceNodeKeyProperty.GetString()))
        {
            throw new InvalidOperationException("Automation-output decision condition requires sourceNodeKey.");
        }

        if (!element.TryGetProperty("property", out var propertyProperty)
            || propertyProperty.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(propertyProperty.GetString()))
        {
            throw new InvalidOperationException("Automation-output decision condition requires property.");
        }

        if (!element.TryGetProperty("operator", out var operatorProperty)
            || operatorProperty.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(operatorProperty.GetString()))
        {
            throw new InvalidOperationException("Automation-output decision condition requires operator.");
        }

        var property = propertyProperty.GetString()!.Trim();
        var op = operatorProperty.GetString()!.Trim().ToLowerInvariant();

        // Property-Union-Schnellcheck (Schranke 1): Action-Key-spezifische Whitelist greift erst
        // in der Engine-Stufe mit Graph-Kontext (Schranke 3).
        if (!AllAllowedConditionPropertiesUnion.Contains(property))
        {
            throw new InvalidOperationException(
                $"Automation-output decision condition references property '{property}', which is not in the whitelist [{string.Join(", ", AllAllowedConditionPropertiesUnion)}].");
        }

        if (!AllowedAutomationOutputOperators.Contains(op))
        {
            throw new InvalidOperationException(
                $"Automation-output decision condition operator '{op}' is not supported. Allowed: [{string.Join(", ", AllowedAutomationOutputOperators)}].");
        }

        return new AutomationOutputConditionRecord(
            SourceNodeKey: sourceNodeKeyProperty.GetString()!.Trim(),
            Property: property,
            Operator: op);
    }

    public static WorkflowDefinitionSupervisorGatekeeperEvaluation EvaluateSupervisorGatekeeper(
        WorkflowDefinitionGraphRecord graph,
        string? workflowDefinitionKey,
        bool requiresSupervisorStep)
    {
        return WorkflowDefinitionSupervisorGatekeeperRules.Evaluate(
            graph.Nodes.Select(node => new WorkflowDefinitionSupervisorGatekeeperNode
            {
                NodeKey = node.NodeKey,
                NodeType = node.NodeType,
                WorkflowDefinitionKey = TryGetWorkflowDefinitionKeyFromNodeConfig(node)
            }).ToList(),
            graph.Edges.Select(edge => new WorkflowDefinitionSupervisorGatekeeperEdge
            {
                SourceNodeKey = graph.NodeById[edge.SourceNodeId].NodeKey,
                TargetNodeKey = graph.NodeById[edge.TargetNodeId].NodeKey,
                Priority = edge.Priority
            }).ToList(),
            workflowDefinitionKey,
            requiresSupervisorStep);
    }

    public static bool IsSupervisorGatekeeperNode(
        WorkflowDefinitionGraphRecord graph,
        string nodeKey,
        string? workflowDefinitionKey,
        bool requiresSupervisorStep)
    {
        var evaluation = EvaluateSupervisorGatekeeper(graph, workflowDefinitionKey, requiresSupervisorStep);
        return evaluation.IsSatisfied
            && string.Equals(evaluation.GatekeeperNodeKey, nodeKey, StringComparison.OrdinalIgnoreCase);
    }

    public static string ComputeWorkflowStatusFromActiveNodes(
        WorkflowDefinitionGraphRecord graph,
        IReadOnlyCollection<ActiveRuntimeNodeRecord> activeNodes,
        string? workflowDefinitionKey,
        bool requiresSupervisorStep)
    {
        if (activeNodes.Any(node =>
                IsSupervisorGatekeeperNode(
                    graph,
                    node.NodeKey,
                    workflowDefinitionKey,
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

    // Liest den fachlichen Workflow-Definition-Schluessel aus der Node-Config.
    // Aktiver Pfad nutzt "workflowDefinitionKey"; "legacyProcessTypeKey" bleibt
    // ein read-only Fallback fuer persistierte Definitionen aus der Alt-Welt.
    public static string? TryGetWorkflowDefinitionKeyFromNodeConfig(WorkflowDefinitionNodeRecord node)
    {
        return TryGetNodeConfigString(node, "workflowDefinitionKey")
            ?? TryGetNodeConfigString(node, "legacyProcessTypeKey");
    }

    public static string GetRequiredWorkflowDefinitionKeyFromNodeConfig(WorkflowDefinitionNodeRecord node)
    {
        var value = TryGetWorkflowDefinitionKeyFromNodeConfig(node);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Node '{node.NodeKey}' requires config property 'workflowDefinitionKey' as non-empty string.");
        }

        return value;
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
            initialResolved = ResolveNextNodes(snapshot.Graph, completedNode, snapshot.AnswersByKey, snapshot.AutomationOutputsByNodeKey);
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
                    foreach (var resolved in ResolveNextNodes(snapshot.Graph, nextNode, snapshot.AnswersByKey, snapshot.AutomationOutputsByNodeKey))
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
                        foreach (var resolved in ResolveNextNodes(snapshot.Graph, nextNode, snapshot.AnswersByKey, snapshot.AutomationOutputsByNodeKey))
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
                        decisionTarget = ResolveDecisionTarget(snapshot.Graph, nextNode, snapshot.AnswersByKey, out selectedEdge, snapshot.AutomationOutputsByNodeKey);
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

        var computedStatus = ComputeWorkflowStatusFromActiveNodes(
            snapshot.Graph,
            postActiveNodes,
            snapshot.WorkflowDefinitionKey,
            snapshot.RequiresSupervisorStep);

        return new WorkflowRuntimePlan
        {
            NodeSteps = nodeSteps,
            Outcome = new WorkflowWaitOutcome
            {
                ComputedStatus = computedStatus,
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
            || string.IsNullOrWhiteSpace(snapshot.ApprovalSpecKey))
        {
            return false;
        }

        var gatekeeperWorkflowDefinitionKey = TryGetWorkflowDefinitionKeyFromNodeConfig(completedNode);
        if (!IsSupervisorGatekeeperNode(
                snapshot.Graph,
                completedNode.NodeKey,
                gatekeeperWorkflowDefinitionKey,
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
            snapshot.ApprovalSpecKey,
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

internal enum DecisionConditionLogic
{
    And,
    Or
}

internal sealed class DecisionConditionExpressionRecord
{
    public required DecisionConditionLogic Logic { get; init; }
    public required IReadOnlyList<DecisionConditionRecord> Conditions { get; init; }
}

// Discriminated Union der Decision-Bedingungs-Varianten (Etappe 9a Schritt 8).
// AnswerBased: bestehender Pfad, evaluiert ueber StoredWorkflowAnswerRecord.
// AutomationOutputBased: neuer Pfad, evaluiert ueber das Output-JSON des
// direkten Predecessor-Automation-Nodes.
internal abstract record DecisionConditionRecord
{
    public sealed record AnswerBased(TaskTemplateConditionRecord Condition) : DecisionConditionRecord;
    public sealed record AutomationOutputBased(AutomationOutputConditionRecord Condition) : DecisionConditionRecord;
}

internal sealed record AutomationOutputConditionRecord(
    string SourceNodeKey,
    string Property,
    string Operator);
