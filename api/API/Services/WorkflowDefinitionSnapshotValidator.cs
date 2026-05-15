using System.Text.Json;

namespace API;

// Z7-3.3: Snapshot-Validierungspfad — issues-basiert.
// Wird vom WorkflowDefinitionValidationService ueber ValidateSnapshot delegiert.
internal static class WorkflowDefinitionSnapshotValidator
{
    private static HashSet<string> SupportedDecisionOperators => WorkflowDefinitionValidationCatalog.SupportedDecisionOperators;
    private static HashSet<string> AllowedNodeTypes => WorkflowDefinitionValidationCatalog.AllowedNodeTypes;
    private static HashSet<string> AllowedSpecConditionOperators => WorkflowDefinitionValidationCatalog.AllowedSpecConditionOperators;

    public static WorkflowDefinitionValidationSnapshot ValidateSnapshot(WorkflowDefinitionValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var issues = new List<WorkflowDefinitionValidationIssue>();
        var normalizedNodes = new List<WorkflowDefinitionDraftNode>(context.Nodes.Count);
        var nodeByKey = new Dictionary<string, WorkflowDefinitionDraftNode>(StringComparer.Ordinal);

        for (var index = 0; index < context.Nodes.Count; index += 1)
        {
            var node = context.Nodes[index];
            var nodeKey = WorkflowDefinitionValidationHelpers.TryNormalizeRequiredKey(node.NodeKey, $"Node[{index}].nodeKey", issues, "workflow_node");
            var nodeType = WorkflowDefinitionValidationHelpers.TryNormalizeRequiredKey(node.NodeType, $"Node[{index}].nodeType", issues, "workflow_node");
            if (nodeKey is null || nodeType is null)
            {
                continue;
            }

            if (!AllowedNodeTypes.Contains(nodeType))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "unsupported_node_type",
                    $"Node '{nodeKey}' uses unsupported node_type '{nodeType}'.",
                    "workflow_node",
                    nodeKey));
                continue;
            }

            if (!nodeByKey.TryAdd(nodeKey, new WorkflowDefinitionDraftNode
                {
                    NodeKey = nodeKey,
                    NodeType = nodeType,
                    Title = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(node.Title),
                    SortOrder = node.SortOrder,
                    PositionX = node.PositionX,
                    PositionY = node.PositionY,
                    Config = WorkflowDefinitionValidationHelpers.CloneConfig(node.Config),
                    Actions = NormalizeNodeActions(node, issues, $"Node[{index}]", nodeKey),
                    Specs = NormalizeNodeSpecs(node, nodeType, issues, $"Node[{index}]", nodeKey),
                    AutomationAdminRole = WorkflowDefinitionValidationHelpers.NormalizeAutomationAdminRole(node.AutomationAdminRole)
                }))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "duplicate_node_key",
                    $"Duplicate node key '{nodeKey}'.",
                    "workflow_node",
                    nodeKey));
            }
        }

        normalizedNodes.AddRange(nodeByKey.Values.OrderBy(node => node.SortOrder).ThenBy(node => node.NodeKey, StringComparer.Ordinal));

        var normalizedEdges = new List<WorkflowDefinitionDraftEdge>(context.Edges.Count);
        var prioritiesBySource = new HashSet<(string SourceNodeKey, int Priority)>();
        for (var index = 0; index < context.Edges.Count; index += 1)
        {
            var edge = context.Edges[index];
            var sourceNodeKey = WorkflowDefinitionValidationHelpers.TryNormalizeRequiredKey(edge.SourceNodeKey, $"Edge[{index}].sourceNodeKey", issues, "workflow_edge");
            var targetNodeKey = WorkflowDefinitionValidationHelpers.TryNormalizeRequiredKey(edge.TargetNodeKey, $"Edge[{index}].targetNodeKey", issues, "workflow_edge");
            if (sourceNodeKey is null || targetNodeKey is null)
            {
                continue;
            }

            if (string.Equals(sourceNodeKey, targetNodeKey, StringComparison.Ordinal))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "self_loop_not_supported",
                    $"Edge '{sourceNodeKey}' -> '{targetNodeKey}' is not allowed because self-loops are not supported.",
                    "workflow_edge",
                    sourceNodeKey));
            }

            if (!prioritiesBySource.Add((sourceNodeKey, edge.Priority)))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "duplicate_edge_priority",
                    $"Source node '{sourceNodeKey}' uses duplicate edge priority '{edge.Priority}'.",
                    "workflow_edge",
                    sourceNodeKey));
            }

            normalizedEdges.Add(new WorkflowDefinitionDraftEdge
            {
                SourceNodeKey = sourceNodeKey,
                TargetNodeKey = targetNodeKey,
                Priority = edge.Priority,
                ConditionExpression = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(edge.ConditionExpression)
            });
        }

        ValidateGraphStructure(normalizedNodes, normalizedEdges, nodeByKey, issues);
        ValidateReachability(normalizedNodes, normalizedEdges, issues);
        ValidateNodeConfigurations(normalizedNodes, issues);
        ValidateNodeActions(normalizedNodes, issues);
        ValidateDecisionConditions(normalizedNodes, normalizedEdges, nodeByKey, issues);
        ValidateGatewayTopology(normalizedNodes, normalizedEdges, issues);
        ValidateMeasurePhaseTopology(normalizedNodes, normalizedEdges, issues);
        ValidateSupervisorGatekeeper(normalizedNodes, normalizedEdges, context, issues);
        ValidateMeasurePhaseProcessConsistency(
            normalizedNodes,
            context.WorkflowDefinitionKey,
            context.RequiresSupervisorStep,
            issues);

        foreach (var referenceIssue in context.ReferenceIssues ?? [])
        {
            issues.Add(referenceIssue);
        }

        return new WorkflowDefinitionValidationSnapshot
        {
            CanSaveDraft = !issues.Any(issue => string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase)),
            CanPublish = !issues.Any(issue => string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase)),
            Issues = issues
        };
    }

    private static void ValidateGraphStructure(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        IReadOnlyList<WorkflowDefinitionDraftEdge> edges,
        IReadOnlyDictionary<string, WorkflowDefinitionDraftNode> nodeByKey,
        List<WorkflowDefinitionValidationIssue> issues)
    {
        var startNodes = nodes.Where(node => string.Equals(node.NodeType, "start", StringComparison.Ordinal)).ToList();
        if (startNodes.Count != 1)
        {
            issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                startNodes.Count == 0 ? "missing_start_node" : "multiple_start_nodes",
                startNodes.Count == 0
                    ? "A workflow definition draft must contain exactly one start node."
                    : "A workflow definition draft must not contain more than one start node.",
                "workflow_definition"));
        }

        var endNodeCount = nodes.Count(node => string.Equals(node.NodeType, "end", StringComparison.Ordinal));
        if (endNodeCount == 0)
        {
            issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                "missing_end_node",
                "A workflow definition draft must contain at least one end node.",
                "workflow_definition"));
        }

        var incomingCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var outgoingCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            incomingCounts[node.NodeKey] = 0;
            outgoingCounts[node.NodeKey] = 0;
        }

        foreach (var edge in edges)
        {
            if (!nodeByKey.ContainsKey(edge.SourceNodeKey))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "missing_edge_source",
                    $"Edge source node '{edge.SourceNodeKey}' does not exist in this definition version.",
                    "workflow_edge",
                    edge.SourceNodeKey));
                continue;
            }

            if (!nodeByKey.ContainsKey(edge.TargetNodeKey))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "missing_edge_target",
                    $"Edge target node '{edge.TargetNodeKey}' does not exist in this definition version.",
                    "workflow_edge",
                    edge.TargetNodeKey));
                continue;
            }

            outgoingCounts[edge.SourceNodeKey] += 1;
            incomingCounts[edge.TargetNodeKey] += 1;
        }

        foreach (var node in nodes)
        {
            var incoming = incomingCounts[node.NodeKey];
            var outgoing = outgoingCounts[node.NodeKey];

            if (string.Equals(node.NodeType, "start", StringComparison.Ordinal))
            {
                if (incoming > 0)
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "start_node_incoming_edge",
                        $"Start node '{node.NodeKey}' must not have incoming edges.",
                        "workflow_node",
                        node.NodeKey));
                }

                continue;
            }

            if (string.Equals(node.NodeType, "end", StringComparison.Ordinal))
            {
                if (outgoing > 0)
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "end_node_outgoing_edge",
                        $"End node '{node.NodeKey}' must not have outgoing edges.",
                        "workflow_node",
                        node.NodeKey));
                }

                continue;
            }

            if (incoming == 0)
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "missing_incoming_edge",
                    $"Node '{node.NodeKey}' must have at least one incoming edge.",
                    "workflow_node",
                    node.NodeKey));
            }

            if (outgoing == 0)
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "missing_outgoing_edge",
                    $"Node '{node.NodeKey}' must have at least one outgoing edge.",
                    "workflow_node",
                    node.NodeKey));
            }
        }
    }

    private static void ValidateReachability(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        IReadOnlyList<WorkflowDefinitionDraftEdge> edges,
        List<WorkflowDefinitionValidationIssue> issues)
    {
        foreach (var unreachableKey in WorkflowDefinitionValidationHelpers.FindUnreachableNodeKeys(nodes, edges))
        {
            issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                "node_not_reachable",
                $"Node '{unreachableKey}' is not reachable from the start node.",
                "workflow_node",
                unreachableKey));
        }
    }

    private static void ValidateNodeConfigurations(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        List<WorkflowDefinitionValidationIssue> issues)
    {
        foreach (var node in nodes)
        {
            switch (node.NodeType)
            {
                case "start":
                case "end":
                    if (WorkflowDefinitionValidationHelpers.HasConfig(node.Config))
                    {
                        issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                            "config_not_allowed",
                            $"Node '{node.NodeKey}' of type '{node.NodeType}' must not define a config.",
                            "workflow_node",
                            node.NodeKey));
                    }
                    break;
                case "form":
                    WorkflowDefinitionValidationHelpers.ValidateRequiredWorkflowDefinitionKeyConfig(node, issues);
                    break;
                case "task":
                case "approval":
                    if (WorkflowDefinitionValidationHelpers.HasConfig(node.Config))
                    {
                        issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                            "config_not_allowed",
                            $"Node '{node.NodeKey}' of type '{node.NodeType}' must not define a config.",
                            "workflow_node",
                            node.NodeKey));
                    }
                    break;
                case "measure_provision":
                case "measure_deprovision":
                case "measure_change":
                case "measure_rename":
                    WorkflowDefinitionValidationHelpers.ValidateOptionalObjectConfig(node, issues);
                    break;
                case "automation":
                case "parallel_split":
                case "parallel_join":
                    if (WorkflowDefinitionValidationHelpers.HasConfig(node.Config))
                    {
                        issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                            "config_not_allowed",
                            $"Node '{node.NodeKey}' of type '{node.NodeType}' must not define a config.",
                            "workflow_node",
                            node.NodeKey));
                    }
                    break;
            }
        }
    }

    private static void ValidateNodeActions(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        List<WorkflowDefinitionValidationIssue> issues)
    {
        foreach (var node in nodes)
        {
            var isAutomationNode = string.Equals(node.NodeType, "automation", StringComparison.Ordinal);
            var isTaskNode = string.Equals(node.NodeType, "task", StringComparison.Ordinal);
            var hasRole = !string.IsNullOrEmpty(node.AutomationAdminRole);

            // Slice 2: Actions sind ausser bei 'automation' nur noch bei 'task' erlaubt.
            // Bei jedem anderen Typ ist sowohl Actions als auch automation_admin_role verboten.
            if (!isAutomationNode && !isTaskNode)
            {
                if (node.Actions.Count > 0)
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "actions_not_allowed",
                        $"Node '{node.NodeKey}' of type '{node.NodeType}' must not define actions.",
                        "workflow_node",
                        node.NodeKey));
                }

                if (hasRole)
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "automation_admin_role_not_allowed",
                        $"Node '{node.NodeKey}' of type '{node.NodeType}' must not define an automationAdminRole.",
                        "workflow_node",
                        node.NodeKey));
                }

                continue;
            }

            // automation-Pfad: bestehende Pflicht "mindestens eine Action" bleibt;
            // automation_admin_role ist hier irrelevant (engine-driven, kein Admin-Gate).
            if (isAutomationNode && node.Actions.Count == 0)
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "missing_automation_actions",
                    $"Node '{node.NodeKey}' of type 'automation' requires at least one action.",
                    "workflow_node",
                    node.NodeKey));
                continue;
            }

            // task-Pfad: drei Branches je nach (Actions, Role).
            if (isTaskNode)
            {
                if (node.Actions.Count == 0 && hasRole)
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "task_admin_role_without_actions",
                        $"Node '{node.NodeKey}' defines an automationAdminRole but no actions.",
                        "workflow_node",
                        node.NodeKey));
                    continue;
                }

                if (node.Actions.Count > 0 && !hasRole)
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "missing_automation_admin_role_for_task_with_actions",
                        $"Node '{node.NodeKey}' of type 'task' defines actions and therefore requires an 'automationAdminRole'.",
                        "workflow_node",
                        node.NodeKey));
                    // Kein continue: action-interne Validierung soll trotzdem laufen.
                }
                else if (node.Actions.Count > 0
                    && !WorkflowDefinitionValidationCatalog.AllowedAutomationAdminRoles.Contains(node.AutomationAdminRole!))
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "invalid_automation_admin_role",
                        $"Node '{node.NodeKey}' uses unsupported automationAdminRole '{node.AutomationAdminRole}'.",
                        "workflow_node",
                        node.NodeKey));
                }

                if (node.Actions.Count == 0)
                {
                    // task-Node ohne Actions + ohne Role: klassischer Human Task. OK.
                    continue;
                }
            }

            var seenExecutionOrders = new HashSet<int>();
            foreach (var action in node.Actions)
            {
                if (!seenExecutionOrders.Add(action.ExecutionOrder))
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "duplicate_action_execution_order",
                        $"Node '{node.NodeKey}' uses duplicate action execution_order '{action.ExecutionOrder}'.",
                        "workflow_node",
                        node.NodeKey));
                }

                if (string.IsNullOrWhiteSpace(action.ActionKey))
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "missing_action_key",
                        $"Node '{node.NodeKey}' contains an action without actionKey.",
                        "workflow_node",
                        node.NodeKey));
                }

                if (!string.Equals(action.OnErrorBehavior, "fail_workflow", StringComparison.Ordinal))
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "unsupported_action_error_behavior",
                        $"Node '{node.NodeKey}' action '{action.ActionKey}' uses unsupported onErrorBehavior '{action.OnErrorBehavior}'.",
                        "workflow_node",
                        node.NodeKey));
                }

                if (WorkflowDefinitionValidationHelpers.HasConfig(action.InputMapping) && action.InputMapping!.Value.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "invalid_action_input_mapping",
                        $"Node '{node.NodeKey}' action '{action.ActionKey}' requires inputMapping to be a JSON object.",
                        "workflow_node",
                        node.NodeKey));
                }
            }
        }
    }

    private static void ValidateDecisionConditions(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        IReadOnlyList<WorkflowDefinitionDraftEdge> edges,
        IReadOnlyDictionary<string, WorkflowDefinitionDraftNode> nodeByKey,
        List<WorkflowDefinitionValidationIssue> issues)
    {
        foreach (var edge in edges)
        {
            if (!nodeByKey.TryGetValue(edge.SourceNodeKey, out var sourceNode)
                || !string.Equals(sourceNode.NodeType, "decision", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(edge.ConditionExpression))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(edge.ConditionExpression);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "invalid_decision_condition",
                        $"Decision edge from '{edge.SourceNodeKey}' to '{edge.TargetNodeKey}' must use a JSON object condition.",
                        "workflow_edge",
                        edge.SourceNodeKey));
                    continue;
                }

                if (!root.TryGetProperty("answerKey", out var answerKeyProperty)
                    || answerKeyProperty.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(answerKeyProperty.GetString()))
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "missing_decision_answer_key",
                        $"Decision edge from '{edge.SourceNodeKey}' to '{edge.TargetNodeKey}' requires answerKey.",
                        "workflow_edge",
                        edge.SourceNodeKey));
                }

                if (!root.TryGetProperty("operator", out var operatorProperty)
                    || operatorProperty.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(operatorProperty.GetString()))
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "missing_decision_operator",
                        $"Decision edge from '{edge.SourceNodeKey}' to '{edge.TargetNodeKey}' requires operator.",
                        "workflow_edge",
                        edge.SourceNodeKey));
                    continue;
                }

                var @operator = operatorProperty.GetString()!.Trim().ToLowerInvariant();
                if (!SupportedDecisionOperators.Contains(@operator))
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "unsupported_decision_operator",
                        $"Decision edge from '{edge.SourceNodeKey}' to '{edge.TargetNodeKey}' uses unsupported operator '{@operator}'.",
                        "workflow_edge",
                        edge.SourceNodeKey));
                }
                else if ((@operator == "eq" || @operator == "neq") && !WorkflowDefinitionValidationHelpers.HasAnyExpectedValue(root))
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "missing_decision_expected_value",
                        $"Decision edge from '{edge.SourceNodeKey}' to '{edge.TargetNodeKey}' uses operator '{@operator}' but is missing expectedValueText/Boolean/Number.",
                        "workflow_edge",
                        edge.SourceNodeKey));
                }
            }
            catch (JsonException ex)
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "invalid_decision_condition_json",
                    $"Decision edge from '{edge.SourceNodeKey}' to '{edge.TargetNodeKey}' is not valid JSON: {ex.Message}",
                    "workflow_edge",
                    edge.SourceNodeKey));
            }
        }
    }

    private static void ValidateGatewayTopology(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        IReadOnlyList<WorkflowDefinitionDraftEdge> edges,
        List<WorkflowDefinitionValidationIssue> issues)
    {
        var incomingCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var outgoingCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            incomingCounts[node.NodeKey] = 0;
            outgoingCounts[node.NodeKey] = 0;
        }

        foreach (var edge in edges)
        {
            if (outgoingCounts.ContainsKey(edge.SourceNodeKey))
            {
                outgoingCounts[edge.SourceNodeKey] += 1;
            }

            if (incomingCounts.ContainsKey(edge.TargetNodeKey))
            {
                incomingCounts[edge.TargetNodeKey] += 1;
            }
        }

        foreach (var node in nodes)
        {
            var incoming = incomingCounts.GetValueOrDefault(node.NodeKey);
            var outgoing = outgoingCounts.GetValueOrDefault(node.NodeKey);

            if (string.Equals(node.NodeType, "decision", StringComparison.OrdinalIgnoreCase) && outgoing < 2)
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "decision_requires_multiple_outgoing_edges",
                    $"Decision node '{node.NodeKey}' requires at least two outgoing edges.",
                    "workflow_node",
                    node.NodeKey));
            }

            if (string.Equals(node.NodeType, "parallel_split", StringComparison.OrdinalIgnoreCase) && outgoing < 2)
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "parallel_split_requires_multiple_outgoing_edges",
                    $"Parallel split node '{node.NodeKey}' requires at least two outgoing edges.",
                    "workflow_node",
                    node.NodeKey));
            }

            if (string.Equals(node.NodeType, "parallel_join", StringComparison.OrdinalIgnoreCase) && incoming < 2)
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "parallel_join_requires_multiple_incoming_edges",
                    $"Parallel join node '{node.NodeKey}' requires at least two incoming edges.",
                    "workflow_node",
                    node.NodeKey));
            }

            if (!string.Equals(node.NodeType, "decision", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(node.NodeType, "parallel_split", StringComparison.OrdinalIgnoreCase)
                && outgoing > 1)
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "multiple_outgoing_edges_not_supported",
                    $"Node '{node.NodeKey}' uses {outgoing} outgoing edges, but only decision and parallel_split nodes may branch.",
                    "workflow_node",
                    node.NodeKey));
            }
        }
    }

    private static void ValidateMeasurePhaseTopology(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        IReadOnlyList<WorkflowDefinitionDraftEdge> edges,
        List<WorkflowDefinitionValidationIssue> issues)
    {
        var measureNodes = nodes
            .Where(node => WorkflowDefinitionValidationHelpers.IsMeasureGenerationNodeType(node.NodeType))
            .ToList();
        if (measureNodes.Count == 0)
        {
            return;
        }

        if (measureNodes.Count != 1)
        {
            issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                "invalid_measure_node_count",
                "A measure-based workflow definition must contain exactly one measure generation node.",
                "workflow_definition"));
            return;
        }

        var formNodes = nodes
            .Where(node => string.Equals(node.NodeType, "form", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (formNodes.Count != 1)
        {
            issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                "invalid_measure_form_count",
                "A measure-based workflow definition must contain exactly one form node.",
                "workflow_definition"));
        }

        var approvalNodes = nodes
            .Where(node => string.Equals(node.NodeType, "approval", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (approvalNodes.Count > 1)
        {
            issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                "invalid_measure_approval_count",
                "A measure-based workflow definition may contain at most one approval node.",
                "workflow_definition"));
        }

        var endNodes = nodes
            .Where(node => string.Equals(node.NodeType, "end", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (endNodes.Count != 1)
        {
            issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                "invalid_measure_end_count",
                "A measure-based workflow definition must contain exactly one end node.",
                "workflow_definition"));
        }

        foreach (var node in nodes.Where(node =>
                     string.Equals(node.NodeType, "task", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(node.NodeType, "decision", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(node.NodeType, "parallel_split", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(node.NodeType, "parallel_join", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(node.NodeType, "automation", StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                "technical_nodes_not_allowed_in_measure_flow",
                $"Measure-based workflow definitions must not contain technical node type '{node.NodeType}' ('{node.NodeKey}').",
                "workflow_node",
                node.NodeKey));
        }

        if (formNodes.Count != 1 || endNodes.Count != 1)
        {
            return;
        }

        var startNode = nodes.SingleOrDefault(node => string.Equals(node.NodeType, "start", StringComparison.OrdinalIgnoreCase));
        if (startNode is null)
        {
            return;
        }

        var measureNode = measureNodes[0];
        var formNode = formNodes[0];
        var endNode = endNodes[0];
        var approvalNode = approvalNodes.SingleOrDefault();

        var expectedEdges = approvalNode is null
            ? new[]
            {
                (startNode.NodeKey, formNode.NodeKey),
                (formNode.NodeKey, measureNode.NodeKey),
                (measureNode.NodeKey, endNode.NodeKey)
            }
            : new[]
            {
                (startNode.NodeKey, formNode.NodeKey),
                (formNode.NodeKey, approvalNode.NodeKey),
                (approvalNode.NodeKey, measureNode.NodeKey),
                (measureNode.NodeKey, endNode.NodeKey)
            };

        if (edges.Count != expectedEdges.Length)
        {
            issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                "invalid_measure_phase_edges",
                "A measure-based workflow definition must only expose the phase path Start -> Formular -> optionale Freigabe -> Maßnahmen -> Ende.",
                "workflow_definition"));
            return;
        }

        foreach (var (sourceNodeKey, targetNodeKey) in expectedEdges)
        {
            if (!edges.Any(edge =>
                    string.Equals(edge.SourceNodeKey, sourceNodeKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(edge.TargetNodeKey, targetNodeKey, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "missing_measure_phase_edge",
                    $"A measure-based workflow definition is missing the phase edge '{sourceNodeKey}' -> '{targetNodeKey}'.",
                    "workflow_edge",
                    sourceNodeKey));
            }
        }
    }

    private static void ValidateSupervisorGatekeeper(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        IReadOnlyList<WorkflowDefinitionDraftEdge> edges,
        WorkflowDefinitionValidationContext context,
        List<WorkflowDefinitionValidationIssue> issues)
    {
        if (!context.RequiresSupervisorStep)
        {
            return;
        }

        var evaluation = WorkflowDefinitionSupervisorGatekeeperRules.Evaluate(
            nodes.Select(node => new WorkflowDefinitionSupervisorGatekeeperNode
            {
                NodeKey = node.NodeKey,
                NodeType = node.NodeType,
                WorkflowDefinitionKey = WorkflowDefinitionValidationHelpers.TryGetWorkflowDefinitionKeyFromNodeConfig(node)
            }).ToList(),
            edges.Select(edge => new WorkflowDefinitionSupervisorGatekeeperEdge
            {
                SourceNodeKey = edge.SourceNodeKey,
                TargetNodeKey = edge.TargetNodeKey,
                Priority = edge.Priority
            }).ToList(),
            context.WorkflowDefinitionKey,
            context.RequiresSupervisorStep);

        if (!evaluation.IsSatisfied)
        {
            issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                evaluation.FailureCode ?? "missing_supervisor_gatekeeper",
                evaluation.FailureMessage ?? "Supervisor-pflichtige Workflow-Definitionen benötigen einen gültigen Gatekeeper.",
                "workflow_definition"));
        }
    }

    private static void ValidateMeasurePhaseProcessConsistency(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        string? workflowDefinitionKey,
        bool requiresSupervisorStep,
        List<WorkflowDefinitionValidationIssue> issues)
    {
        var measureNodes = nodes
            .Where(node => WorkflowDefinitionValidationHelpers.IsMeasureGenerationNodeType(node.NodeType))
            .ToList();
        if (measureNodes.Count == 0)
        {
            return;
        }

        var approvalCount = nodes.Count(node => string.Equals(node.NodeType, "approval", StringComparison.OrdinalIgnoreCase));
        if (!requiresSupervisorStep && approvalCount > 0)
        {
            issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                "measure_flow_unexpected_approval",
                "Maßnahmen-Workflows ohne Supervisor-Pflicht dürfen keine Freigabe-Phase enthalten.",
                "workflow_definition"));
        }

        var expectedMeasureNodeType = WorkflowDefinitionValidationHelpers.GetExpectedMeasureNodeTypeForDefinitionKey(workflowDefinitionKey);
        if (expectedMeasureNodeType is null || measureNodes.Count != 1)
        {
            return;
        }

        var measureNode = measureNodes[0];
        if (string.Equals(measureNode.NodeType, expectedMeasureNodeType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // FailureCode-String bleibt aus Audit-Log-Kompatibilität "process_type_mismatch" — interne Bedeutung ist jetzt definitionKey.
        issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
            "measure_flow_process_type_mismatch",
            $"Workflow-Definitionen für '{workflowDefinitionKey}' müssen den Maßnahmen-Typ '{expectedMeasureNodeType}' verwenden.",
            "workflow_node",
            measureNode.NodeKey));
    }

    private static List<WorkflowDefinitionDraftNodeAction> NormalizeNodeActions(
        WorkflowDefinitionNodeDto node,
        List<WorkflowDefinitionValidationIssue> issues,
        string fieldPrefix,
        string nodeKey)
    {
        var actions = node.Actions ?? [];
        var normalized = new List<WorkflowDefinitionDraftNodeAction>(actions.Count);
        for (var index = 0; index < actions.Count; index += 1)
        {
            var action = actions[index];
            var actionKey = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(action.ActionKey);
            var onErrorBehavior = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(action.OnErrorBehavior)?.ToLowerInvariant() ?? "fail_workflow";

            if (string.IsNullOrWhiteSpace(actionKey))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "required_value_missing",
                    $"{fieldPrefix}.actions[{index}].actionKey is required.",
                    "workflow_node",
                    nodeKey));
            }

            normalized.Add(new WorkflowDefinitionDraftNodeAction
            {
                ActionKey = actionKey,
                ExecutionOrder = action.ExecutionOrder,
                OnErrorBehavior = onErrorBehavior,
                InputMapping = WorkflowDefinitionValidationHelpers.CloneConfig(action.InputMapping)
            });
        }

        return normalized
            .OrderBy(action => action.ExecutionOrder)
            .ThenBy(action => action.ActionKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // FE-9: Read-Pfad — Issue-Liste statt errors. Verhalten parallel zur Write-Variante.
    private static List<WorkflowDefinitionDraftNodeSpec> NormalizeNodeSpecs(
        WorkflowDefinitionNodeDto node,
        string nodeType,
        List<WorkflowDefinitionValidationIssue> issues,
        string fieldPrefix,
        string nodeKey)
    {
        var specs = node.Specs ?? [];
        var canHaveSpecs = WorkflowDefinitionValidationHelpers.NodeTypeCanHaveSpecs(nodeType);
        if (specs.Count > 0 && !canHaveSpecs)
        {
            issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                "specs_not_allowed_for_node_type",
                $"{fieldPrefix}: Node-Type '{nodeType}' darf keine Specs tragen.",
                "workflow_node",
                nodeKey));
            return new List<WorkflowDefinitionDraftNodeSpec>();
        }

        if (WorkflowDefinitionValidationHelpers.NodeTypeAllowsAtMostOneSpec(nodeType) && specs.Count > 1)
        {
            issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                "too_many_specs_for_node_type",
                $"{fieldPrefix}: Node-Type '{nodeType}' darf maximal 1 Spec haben (gefunden: {specs.Count}).",
                "workflow_node",
                nodeKey));
            return new List<WorkflowDefinitionDraftNodeSpec>();
        }

        var normalized = new List<WorkflowDefinitionDraftNodeSpec>(specs.Count);
        var specKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < specs.Count; index += 1)
        {
            var spec = specs[index];
            var specKey = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(spec.SpecKey);
            if (string.IsNullOrWhiteSpace(specKey))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "spec_key_missing",
                    $"{fieldPrefix}.specs[{index}].specKey is required.",
                    "workflow_node",
                    nodeKey));
                continue;
            }
            if (!specKeys.Add(specKey))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "duplicate_spec_key",
                    $"{fieldPrefix}.specs[{index}].specKey '{specKey}' ist innerhalb des Nodes nicht eindeutig.",
                    "workflow_node",
                    nodeKey));
                continue;
            }

            var title = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(spec.Title);
            if (string.IsNullOrWhiteSpace(title))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "spec_title_missing",
                    $"{fieldPrefix}.specs[{index}].title is required.",
                    "workflow_node",
                    nodeKey));
                continue;
            }

            normalized.Add(new WorkflowDefinitionDraftNodeSpec
            {
                SpecKey = specKey,
                Title = title,
                Category = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(spec.Category) ?? "general",
                Description = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(spec.Description) ?? string.Empty,
                IconKey = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(spec.IconKey),
                DefaultResponsibilityId = spec.DefaultResponsibilityId,
                ProcessAreaLabel = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(spec.ProcessAreaLabel),
                IsDepartmentPhaseTask = spec.IsDepartmentPhaseTask,
                IsRequired = spec.IsRequired,
                DueInDays = spec.DueInDays,
                SortOrder = spec.SortOrder,
                Conditions = NormalizeSpecConditions(spec, issues, $"{fieldPrefix}.specs[{index}]", nodeKey),
                Dependencies = NormalizeSpecDependencies(spec, issues, $"{fieldPrefix}.specs[{index}]", nodeKey),
            });
        }

        ValidateSpecDependenciesPointToSiblings(normalized, issues, fieldPrefix, nodeKey);

        return normalized
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.SpecKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<WorkflowDefinitionDraftNodeSpecCondition> NormalizeSpecConditions(
        WorkflowDefinitionNodeSpecDto spec,
        List<WorkflowDefinitionValidationIssue> issues,
        string fieldPrefix,
        string nodeKey)
    {
        var conditions = spec.Conditions ?? [];
        var normalized = new List<WorkflowDefinitionDraftNodeSpecCondition>(conditions.Count);
        for (var index = 0; index < conditions.Count; index += 1)
        {
            var c = conditions[index];
            var answerKey = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(c.AnswerKey);
            var op = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(c.Operator)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(answerKey))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "spec_condition_answer_key_missing",
                    $"{fieldPrefix}.conditions[{index}].answerKey is required.",
                    "workflow_node",
                    nodeKey));
                continue;
            }
            if (op is null || !AllowedSpecConditionOperators.Contains(op))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "spec_condition_operator_invalid",
                    $"{fieldPrefix}.conditions[{index}].operator '{c.Operator}' ist ungueltig.",
                    "workflow_node",
                    nodeKey));
                continue;
            }
            normalized.Add(new WorkflowDefinitionDraftNodeSpecCondition
            {
                AnswerKey = answerKey,
                Operator = op,
                ExpectedValueText = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(c.ExpectedValueText),
                ExpectedValueBoolean = c.ExpectedValueBoolean,
                ExpectedValueNumber = c.ExpectedValueNumber,
            });
        }
        return normalized;
    }

    private static List<WorkflowDefinitionDraftNodeSpecDependency> NormalizeSpecDependencies(
        WorkflowDefinitionNodeSpecDto spec,
        List<WorkflowDefinitionValidationIssue> issues,
        string fieldPrefix,
        string nodeKey)
    {
        var dependencies = spec.Dependencies ?? [];
        var normalized = new List<WorkflowDefinitionDraftNodeSpecDependency>(dependencies.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < dependencies.Count; index += 1)
        {
            var dep = dependencies[index];
            var dependsOn = WorkflowDefinitionValidationHelpers.NormalizeOptionalText(dep.DependsOnSpecKey);
            if (string.IsNullOrWhiteSpace(dependsOn))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "spec_dependency_target_missing",
                    $"{fieldPrefix}.dependencies[{index}].dependsOnSpecKey is required.",
                    "workflow_node",
                    nodeKey));
                continue;
            }
            if (string.Equals(dependsOn, spec.SpecKey, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "spec_dependency_self_loop",
                    $"{fieldPrefix}.dependencies[{index}]: Spec '{spec.SpecKey}' darf nicht von sich selbst abhaengen.",
                    "workflow_node",
                    nodeKey));
                continue;
            }
            if (!seen.Add(dependsOn))
            {
                issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                    "spec_dependency_duplicate",
                    $"{fieldPrefix}.dependencies[{index}]: Doppelte Abhaengigkeit auf '{dependsOn}'.",
                    "workflow_node",
                    nodeKey));
                continue;
            }
            normalized.Add(new WorkflowDefinitionDraftNodeSpecDependency
            {
                DependsOnSpecKey = dependsOn,
            });
        }
        return normalized;
    }

    private static void ValidateSpecDependenciesPointToSiblings(
        IReadOnlyList<WorkflowDefinitionDraftNodeSpec> specs,
        List<WorkflowDefinitionValidationIssue> issues,
        string fieldPrefix,
        string nodeKey)
    {
        var siblingKeys = new HashSet<string>(specs.Select(s => s.SpecKey), StringComparer.OrdinalIgnoreCase);
        foreach (var spec in specs)
        {
            foreach (var dep in spec.Dependencies)
            {
                if (!siblingKeys.Contains(dep.DependsOnSpecKey))
                {
                    issues.Add(WorkflowDefinitionValidationHelpers.CreateIssue(
                        "spec_dependency_target_missing",
                        $"{fieldPrefix}: Spec '{spec.SpecKey}' haengt von '{dep.DependsOnSpecKey}' ab, aber dieser Spec existiert nicht am selben Node.",
                        "workflow_node",
                        nodeKey));
                }
            }
        }
    }
}
