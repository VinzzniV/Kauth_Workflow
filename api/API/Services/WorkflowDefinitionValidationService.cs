using System.Text.Json;

namespace API;

internal sealed class WorkflowDefinitionValidationService : IWorkflowDefinitionValidationService
{
    private static HashSet<string> SupportedDecisionOperators => WorkflowDefinitionValidationCatalog.SupportedDecisionOperators;
    private static HashSet<string> AllowedNodeTypes => WorkflowDefinitionValidationCatalog.AllowedNodeTypes;

    public string NormalizeDefinitionKey(string? definitionKey)
    {
        var normalized = NormalizeRequiredKey(definitionKey, "Definition key");
        return normalized;
    }

    public WorkflowDefinitionDraftValidationResult ValidateAndNormalize(ReplaceWorkflowDefinitionVersionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nodes = request.Nodes ?? [];
        var edges = request.Edges ?? [];

        var normalizedNodes = new List<WorkflowDefinitionDraftNode>(nodes.Count);
        var nodeByKey = new Dictionary<string, WorkflowDefinitionDraftNode>(StringComparer.Ordinal);
        var errors = new List<string>();

        for (var index = 0; index < nodes.Count; index += 1)
        {
            var node = nodes[index];
            var nodeKey = TryNormalizeRequiredKey(node.NodeKey, $"Node[{index}].nodeKey", errors);
            var nodeType = TryNormalizeRequiredKey(node.NodeType, $"Node[{index}].nodeType", errors);
            if (nodeKey is null || nodeType is null)
            {
                continue;
            }

            if (!AllowedNodeTypes.Contains(nodeType))
            {
                errors.Add($"Node '{nodeKey}' uses unsupported node_type '{nodeType}'.");
                continue;
            }

            if (!nodeByKey.TryAdd(nodeKey, new WorkflowDefinitionDraftNode
                {
                    NodeKey = nodeKey,
                    NodeType = nodeType,
                    Title = NormalizeOptionalText(node.Title),
                    SortOrder = node.SortOrder,
                    PositionX = node.PositionX,
                    PositionY = node.PositionY,
                    Config = CloneConfig(node.Config),
                    Actions = NormalizeNodeActions(node, errors, $"Node[{index}]"),
                    Specs = NormalizeNodeSpecs(node, nodeType, errors, $"Node[{index}]")
                }))
            {
                errors.Add($"Duplicate node key '{nodeKey}'.");
            }
        }

        normalizedNodes.AddRange(nodeByKey.Values.OrderBy(node => node.SortOrder).ThenBy(node => node.NodeKey, StringComparer.Ordinal));

        var normalizedEdges = new List<WorkflowDefinitionDraftEdge>(edges.Count);
        var prioritiesBySource = new HashSet<(string SourceNodeKey, int Priority)>();
        for (var index = 0; index < edges.Count; index += 1)
        {
            var edge = edges[index];
            var sourceNodeKey = TryNormalizeRequiredKey(edge.SourceNodeKey, $"Edge[{index}].sourceNodeKey", errors);
            var targetNodeKey = TryNormalizeRequiredKey(edge.TargetNodeKey, $"Edge[{index}].targetNodeKey", errors);
            if (sourceNodeKey is null || targetNodeKey is null)
            {
                continue;
            }

            if (string.Equals(sourceNodeKey, targetNodeKey, StringComparison.Ordinal))
            {
                errors.Add($"Edge '{sourceNodeKey}' -> '{targetNodeKey}' is not allowed because self-loops are not supported.");
            }

            if (!prioritiesBySource.Add((sourceNodeKey, edge.Priority)))
            {
                errors.Add($"Source node '{sourceNodeKey}' uses duplicate edge priority '{edge.Priority}'.");
            }

            normalizedEdges.Add(new WorkflowDefinitionDraftEdge
            {
                SourceNodeKey = sourceNodeKey,
                TargetNodeKey = targetNodeKey,
                Priority = edge.Priority,
                ConditionExpression = NormalizeOptionalText(edge.ConditionExpression)
            });
        }

        ValidateGraphStructure(normalizedNodes, normalizedEdges, nodeByKey, errors);
        ValidateReachability(normalizedNodes, normalizedEdges, errors);
        ValidateNodeConfigurations(normalizedNodes, errors);
        ValidateNodeActions(normalizedNodes, errors);
        ValidateDecisionConditions(normalizedNodes, normalizedEdges, nodeByKey, errors);
        ValidateGatewayTopology(normalizedNodes, normalizedEdges, errors);
        ValidateMeasurePhaseTopology(normalizedNodes, normalizedEdges, errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        return new WorkflowDefinitionDraftValidationResult
        {
            Name = NormalizeOptionalText(request.Name),
            Description = NormalizeOptionalText(request.Description),
            Nodes = normalizedNodes,
            Edges = normalizedEdges
        };
    }

    public WorkflowDefinitionValidationSnapshot ValidateSnapshot(WorkflowDefinitionValidationContext context)
        => WorkflowDefinitionSnapshotValidator.ValidateSnapshot(context);

    private static void ValidateGraphStructure(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        IReadOnlyList<WorkflowDefinitionDraftEdge> edges,
        IReadOnlyDictionary<string, WorkflowDefinitionDraftNode> nodeByKey,
        List<string> errors)
    {
        var startNodes = nodes.Where(node => string.Equals(node.NodeType, "start", StringComparison.Ordinal)).ToList();
        if (startNodes.Count != 1)
        {
            errors.Add(startNodes.Count == 0
                ? "A workflow definition draft must contain exactly one start node."
                : "A workflow definition draft must not contain more than one start node.");
        }

        var endNodeCount = nodes.Count(node => string.Equals(node.NodeType, "end", StringComparison.Ordinal));
        if (endNodeCount == 0)
        {
            errors.Add("A workflow definition draft must contain at least one end node.");
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
                errors.Add($"Edge source node '{edge.SourceNodeKey}' does not exist in this definition version.");
                continue;
            }

            if (!nodeByKey.ContainsKey(edge.TargetNodeKey))
            {
                errors.Add($"Edge target node '{edge.TargetNodeKey}' does not exist in this definition version.");
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
                    errors.Add($"Start node '{node.NodeKey}' must not have incoming edges.");
                }

                continue;
            }

            if (string.Equals(node.NodeType, "end", StringComparison.Ordinal))
            {
                if (outgoing > 0)
                {
                    errors.Add($"End node '{node.NodeKey}' must not have outgoing edges.");
                }

                continue;
            }

            if (incoming == 0)
            {
                errors.Add($"Node '{node.NodeKey}' must have at least one incoming edge.");
            }

            if (outgoing == 0)
            {
                errors.Add($"Node '{node.NodeKey}' must have at least one outgoing edge.");
            }
        }
    }

    private static void ValidateReachability(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        IReadOnlyList<WorkflowDefinitionDraftEdge> edges,
        List<string> errors)
    {
        foreach (var unreachableKey in WorkflowDefinitionValidationHelpers.FindUnreachableNodeKeys(nodes, edges))
        {
            errors.Add($"Node '{unreachableKey}' is not reachable from the start node.");
        }
    }

    private static void ValidateNodeConfigurations(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        List<string> errors)
    {
        foreach (var node in nodes)
        {
            switch (node.NodeType)
            {
                case "start":
                case "end":
                    if (HasConfig(node.Config))
                    {
                        errors.Add($"Node '{node.NodeKey}' of type '{node.NodeType}' must not define a config.");
                    }
                    break;
                case "form":
                    ValidateRequiredStringConfig(node, "legacyProcessTypeKey", errors);
                    break;
                case "task":
                case "approval":
                    // LA5: Spec wird ueber workflow_node_task_specs.workflow_node_id aufgeloest;
                    // kein Config-Pflichtfeld mehr.
                    if (HasConfig(node.Config))
                    {
                        errors.Add($"Node '{node.NodeKey}' of type '{node.NodeType}' must not define a config.");
                    }
                    break;
                case "measure_provision":
                case "measure_deprovision":
                case "measure_change":
                case "measure_rename":
                    ValidateOptionalObjectConfig(node, errors);
                    break;
                case "automation":
                case "parallel_split":
                case "parallel_join":
                    if (HasConfig(node.Config))
                    {
                        errors.Add($"Node '{node.NodeKey}' of type '{node.NodeType}' must not define a config.");
                    }
                    break;
            }
        }
    }

    private static void ValidateRequiredStringConfig(
        WorkflowDefinitionDraftNode node,
        string propertyName,
        List<string> errors)
        => WorkflowDefinitionValidationHelpers.ValidateRequiredStringConfig(node, propertyName, errors);

    private static void ValidateOptionalObjectConfig(
        WorkflowDefinitionDraftNode node,
        List<string> errors)
        => WorkflowDefinitionValidationHelpers.ValidateOptionalObjectConfig(node, errors);

    private static void ValidateNodeActions(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        List<string> errors)
    {
        foreach (var node in nodes)
        {
            if (!string.Equals(node.NodeType, "automation", StringComparison.Ordinal))
            {
                if (node.Actions.Count > 0)
                {
                    errors.Add($"Node '{node.NodeKey}' of type '{node.NodeType}' must not define actions.");
                }

                continue;
            }

            if (node.Actions.Count == 0)
            {
                errors.Add($"Node '{node.NodeKey}' of type 'automation' requires at least one action.");
                continue;
            }

            var seenExecutionOrders = new HashSet<int>();
            foreach (var action in node.Actions)
            {
                if (!seenExecutionOrders.Add(action.ExecutionOrder))
                {
                    errors.Add($"Node '{node.NodeKey}' uses duplicate action execution_order '{action.ExecutionOrder}'.");
                }

                if (string.IsNullOrWhiteSpace(action.ActionKey))
                {
                    errors.Add($"Node '{node.NodeKey}' contains an action without actionKey.");
                }

                if (!string.Equals(action.OnErrorBehavior, "fail_workflow", StringComparison.Ordinal))
                {
                    errors.Add($"Node '{node.NodeKey}' action '{action.ActionKey}' uses unsupported onErrorBehavior '{action.OnErrorBehavior}'.");
                }

                if (HasConfig(action.InputMapping) && action.InputMapping!.Value.ValueKind != JsonValueKind.Object)
                {
                    errors.Add($"Node '{node.NodeKey}' action '{action.ActionKey}' requires inputMapping to be a JSON object.");
                }
            }
        }
    }

    private static void ValidateDecisionConditions(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        IReadOnlyList<WorkflowDefinitionDraftEdge> edges,
        IReadOnlyDictionary<string, WorkflowDefinitionDraftNode> nodeByKey,
        List<string> errors)
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
                    errors.Add($"Decision edge from '{edge.SourceNodeKey}' to '{edge.TargetNodeKey}' must use a JSON object condition.");
                    continue;
                }

                if (!root.TryGetProperty("answerKey", out var answerKeyProperty)
                    || answerKeyProperty.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(answerKeyProperty.GetString()))
                {
                    errors.Add($"Decision edge from '{edge.SourceNodeKey}' to '{edge.TargetNodeKey}' requires answerKey.");
                }

                if (!root.TryGetProperty("operator", out var operatorProperty)
                    || operatorProperty.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(operatorProperty.GetString()))
                {
                    errors.Add($"Decision edge from '{edge.SourceNodeKey}' to '{edge.TargetNodeKey}' requires operator.");
                    continue;
                }

                var @operator = operatorProperty.GetString()!.Trim().ToLowerInvariant();
                if (!SupportedDecisionOperators.Contains(@operator))
                {
                    errors.Add($"Decision edge from '{edge.SourceNodeKey}' to '{edge.TargetNodeKey}' uses unsupported operator '{@operator}'.");
                }
                else if ((@operator == "eq" || @operator == "neq") && !WorkflowDefinitionValidationHelpers.HasAnyExpectedValue(root))
                {
                    errors.Add($"Decision edge from '{edge.SourceNodeKey}' to '{edge.TargetNodeKey}' uses operator '{@operator}' but is missing expectedValueText/Boolean/Number.");
                }
            }
            catch (JsonException ex)
            {
                errors.Add($"Decision edge from '{edge.SourceNodeKey}' to '{edge.TargetNodeKey}' is not valid JSON: {ex.Message}");
            }
        }
    }

    private static string NormalizeRequiredKey(string? value, string fieldName)
        => WorkflowDefinitionValidationHelpers.NormalizeRequiredKey(value, fieldName);

    private static string? TryNormalizeRequiredKey(string? value, string fieldName, List<string> errors)
        => WorkflowDefinitionValidationHelpers.TryNormalizeRequiredKey(value, fieldName, errors);

    private static string? NormalizeOptionalText(string? value)
        => WorkflowDefinitionValidationHelpers.NormalizeOptionalText(value);

    private static bool HasConfig(JsonElement? config)
        => WorkflowDefinitionValidationHelpers.HasConfig(config);

    private static JsonElement? CloneConfig(JsonElement? config)
        => WorkflowDefinitionValidationHelpers.CloneConfig(config);

    private static void ValidateGatewayTopology(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        IReadOnlyList<WorkflowDefinitionDraftEdge> edges,
        List<string> errors)
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
                errors.Add($"Decision node '{node.NodeKey}' requires at least two outgoing edges.");
            }

            if (string.Equals(node.NodeType, "parallel_split", StringComparison.OrdinalIgnoreCase) && outgoing < 2)
            {
                errors.Add($"Parallel split node '{node.NodeKey}' requires at least two outgoing edges.");
            }

            if (string.Equals(node.NodeType, "parallel_join", StringComparison.OrdinalIgnoreCase) && incoming < 2)
            {
                errors.Add($"Parallel join node '{node.NodeKey}' requires at least two incoming edges.");
            }

            if (!string.Equals(node.NodeType, "decision", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(node.NodeType, "parallel_split", StringComparison.OrdinalIgnoreCase)
                && outgoing > 1)
            {
                errors.Add($"Node '{node.NodeKey}' uses {outgoing} outgoing edges, but only decision and parallel_split nodes may branch.");
            }
        }
    }

    private static void ValidateMeasurePhaseTopology(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        IReadOnlyList<WorkflowDefinitionDraftEdge> edges,
        List<string> errors)
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
            errors.Add("A measure-based workflow definition must contain exactly one measure generation node.");
            return;
        }

        var formNodes = nodes
            .Where(node => string.Equals(node.NodeType, "form", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (formNodes.Count != 1)
        {
            errors.Add("A measure-based workflow definition must contain exactly one form node.");
        }

        var approvalNodes = nodes
            .Where(node => string.Equals(node.NodeType, "approval", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (approvalNodes.Count > 1)
        {
            errors.Add("A measure-based workflow definition may contain at most one approval node.");
        }

        var endNodes = nodes
            .Where(node => string.Equals(node.NodeType, "end", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (endNodes.Count != 1)
        {
            errors.Add("A measure-based workflow definition must contain exactly one end node.");
        }

        foreach (var node in nodes.Where(node =>
                     string.Equals(node.NodeType, "task", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(node.NodeType, "decision", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(node.NodeType, "parallel_split", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(node.NodeType, "parallel_join", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(node.NodeType, "automation", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"Measure-based workflow definitions must not contain technical node type '{node.NodeType}' ('{node.NodeKey}').");
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
            errors.Add("A measure-based workflow definition must only expose the phase path Start -> Formular -> optionale Freigabe -> Maßnahmen -> Ende.");
            return;
        }

        foreach (var (sourceNodeKey, targetNodeKey) in expectedEdges)
        {
            if (!edges.Any(edge =>
                    string.Equals(edge.SourceNodeKey, sourceNodeKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(edge.TargetNodeKey, targetNodeKey, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"A measure-based workflow definition is missing the phase edge '{sourceNodeKey}' -> '{targetNodeKey}'.");
            }
        }
    }

    private static List<WorkflowDefinitionDraftNodeAction> NormalizeNodeActions(
        WorkflowDefinitionNodeDto node,
        List<string> errors,
        string fieldPrefix)
    {
        var actions = node.Actions ?? [];
        var normalized = new List<WorkflowDefinitionDraftNodeAction>(actions.Count);
        for (var index = 0; index < actions.Count; index += 1)
        {
            var action = actions[index];
            var actionKey = NormalizeOptionalText(action.ActionKey);
            var onErrorBehavior = NormalizeOptionalText(action.OnErrorBehavior)?.ToLowerInvariant() ?? "fail_workflow";

            if (string.IsNullOrWhiteSpace(actionKey))
            {
                errors.Add($"{fieldPrefix}.actions[{index}].actionKey is required.");
            }

            normalized.Add(new WorkflowDefinitionDraftNodeAction
            {
                ActionKey = actionKey,
                ExecutionOrder = action.ExecutionOrder,
                OnErrorBehavior = onErrorBehavior,
                InputMapping = CloneConfig(action.InputMapping)
            });
        }

        return normalized
            .OrderBy(action => action.ExecutionOrder)
            .ThenBy(action => action.ActionKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // FE-9: Specs normalisieren + validieren — Write-Pfad (errors-Liste, wirft InvalidOperationException am Ende).
    // Validiert Spec-Key-Eindeutigkeit pro Node, Same-Node-Dependencies, gueltige Operatoren, Node-Typ-Compatibilitaet.
    private static List<WorkflowDefinitionDraftNodeSpec> NormalizeNodeSpecs(
        WorkflowDefinitionNodeDto node,
        string nodeType,
        List<string> errors,
        string fieldPrefix)
    {
        var specs = node.Specs ?? [];
        var canHaveSpecs = WorkflowDefinitionValidationHelpers.NodeTypeCanHaveSpecs(nodeType);
        if (specs.Count > 0 && !canHaveSpecs)
        {
            errors.Add($"{fieldPrefix}: Node-Type '{nodeType}' darf keine Specs tragen.");
            return new List<WorkflowDefinitionDraftNodeSpec>();
        }

        if (WorkflowDefinitionValidationHelpers.NodeTypeAllowsAtMostOneSpec(nodeType) && specs.Count > 1)
        {
            errors.Add($"{fieldPrefix}: Node-Type '{nodeType}' darf maximal 1 Spec haben (gefunden: {specs.Count}).");
            return new List<WorkflowDefinitionDraftNodeSpec>();
        }

        var normalized = new List<WorkflowDefinitionDraftNodeSpec>(specs.Count);
        var specKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < specs.Count; index += 1)
        {
            var spec = specs[index];
            var specPrefix = $"{fieldPrefix}.specs[{index}]";
            var specKey = NormalizeOptionalText(spec.SpecKey);
            if (string.IsNullOrWhiteSpace(specKey))
            {
                errors.Add($"{specPrefix}.specKey is required.");
                continue;
            }
            if (!specKeys.Add(specKey))
            {
                errors.Add($"{specPrefix}.specKey '{specKey}' ist innerhalb des Nodes nicht eindeutig.");
                continue;
            }

            var title = NormalizeOptionalText(spec.Title);
            if (string.IsNullOrWhiteSpace(title))
            {
                errors.Add($"{specPrefix}.title is required.");
                continue;
            }

            normalized.Add(new WorkflowDefinitionDraftNodeSpec
            {
                SpecKey = specKey,
                Title = title,
                Category = NormalizeOptionalText(spec.Category) ?? "general",
                Description = NormalizeOptionalText(spec.Description) ?? string.Empty,
                IconKey = NormalizeOptionalText(spec.IconKey),
                DefaultResponsibilityId = spec.DefaultResponsibilityId,
                ProcessAreaLabel = NormalizeOptionalText(spec.ProcessAreaLabel),
                IsDepartmentPhaseTask = spec.IsDepartmentPhaseTask,
                IsRequired = spec.IsRequired,
                DueInDays = spec.DueInDays,
                SortOrder = spec.SortOrder,
                Conditions = NormalizeSpecConditions(spec, errors, specPrefix),
                Dependencies = NormalizeSpecDependencies(spec, errors, specPrefix),
            });
        }

        ValidateSpecDependenciesPointToSiblings(normalized, errors, fieldPrefix);

        return normalized
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.SpecKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<WorkflowDefinitionDraftNodeSpecCondition> NormalizeSpecConditions(
        WorkflowDefinitionNodeSpecDto spec,
        List<string> errors,
        string fieldPrefix)
    {
        var conditions = spec.Conditions ?? [];
        var normalized = new List<WorkflowDefinitionDraftNodeSpecCondition>(conditions.Count);
        for (var index = 0; index < conditions.Count; index += 1)
        {
            var c = conditions[index];
            var answerKey = NormalizeOptionalText(c.AnswerKey);
            var op = NormalizeOptionalText(c.Operator)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(answerKey))
            {
                errors.Add($"{fieldPrefix}.conditions[{index}].answerKey is required.");
                continue;
            }
            if (op is null || !WorkflowDefinitionValidationCatalog.AllowedSpecConditionOperators.Contains(op))
            {
                errors.Add($"{fieldPrefix}.conditions[{index}].operator '{c.Operator}' ist ungueltig.");
                continue;
            }
            normalized.Add(new WorkflowDefinitionDraftNodeSpecCondition
            {
                AnswerKey = answerKey,
                Operator = op,
                ExpectedValueText = NormalizeOptionalText(c.ExpectedValueText),
                ExpectedValueBoolean = c.ExpectedValueBoolean,
                ExpectedValueNumber = c.ExpectedValueNumber,
            });
        }
        return normalized;
    }

    private static List<WorkflowDefinitionDraftNodeSpecDependency> NormalizeSpecDependencies(
        WorkflowDefinitionNodeSpecDto spec,
        List<string> errors,
        string fieldPrefix)
    {
        var dependencies = spec.Dependencies ?? [];
        var normalized = new List<WorkflowDefinitionDraftNodeSpecDependency>(dependencies.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < dependencies.Count; index += 1)
        {
            var dep = dependencies[index];
            var dependsOn = NormalizeOptionalText(dep.DependsOnSpecKey);
            if (string.IsNullOrWhiteSpace(dependsOn))
            {
                errors.Add($"{fieldPrefix}.dependencies[{index}].dependsOnSpecKey is required.");
                continue;
            }
            if (string.Equals(dependsOn, spec.SpecKey, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{fieldPrefix}.dependencies[{index}]: Spec '{spec.SpecKey}' darf nicht von sich selbst abhaengen.");
                continue;
            }
            if (!seen.Add(dependsOn))
            {
                errors.Add($"{fieldPrefix}.dependencies[{index}]: Doppelte Abhaengigkeit auf '{dependsOn}'.");
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
        List<string> errors,
        string fieldPrefix)
    {
        var siblingKeys = new HashSet<string>(specs.Select(s => s.SpecKey), StringComparer.OrdinalIgnoreCase);
        foreach (var spec in specs)
        {
            foreach (var dep in spec.Dependencies)
            {
                if (!siblingKeys.Contains(dep.DependsOnSpecKey))
                {
                    errors.Add($"{fieldPrefix}: Spec '{spec.SpecKey}' haengt von '{dep.DependsOnSpecKey}' ab, aber dieser Spec existiert nicht am selben Node.");
                }
            }
        }
    }
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
