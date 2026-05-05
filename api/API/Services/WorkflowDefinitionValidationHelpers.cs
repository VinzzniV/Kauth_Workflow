using System.Text.Json;

namespace API;

internal static class WorkflowDefinitionValidationHelpers
{
    public static string NormalizeRequiredKey(string? value, string fieldName)
    {
        var normalized = NormalizeOptionalText(value)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return normalized;
    }

    public static string? TryNormalizeRequiredKey(string? value, string fieldName, List<string> errors)
    {
        var normalized = NormalizeOptionalText(value)?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        errors.Add($"{fieldName} is required.");
        return null;
    }

    public static string? TryNormalizeRequiredKey(
        string? value,
        string fieldName,
        List<WorkflowDefinitionValidationIssue> issues,
        string scope)
    {
        var normalized = NormalizeOptionalText(value)?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        issues.Add(CreateIssue(
            "required_value_missing",
            $"{fieldName} is required.",
            scope));
        return null;
    }

    public static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static bool HasConfig(JsonElement? config)
    {
        return config.HasValue
               && config.Value.ValueKind is not JsonValueKind.Null
               && config.Value.ValueKind is not JsonValueKind.Undefined;
    }

    public static WorkflowDefinitionValidationIssue CreateIssue(
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

    public static void ValidateRequiredStringConfig(
        WorkflowDefinitionDraftNode node,
        string propertyName,
        List<string> errors)
    {
        if (!HasConfig(node.Config))
        {
            errors.Add($"Node '{node.NodeKey}' of type '{node.NodeType}' requires a config object with '{propertyName}'.");
            return;
        }

        var config = node.Config!.Value;
        if (config.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"Node '{node.NodeKey}' of type '{node.NodeType}' requires a JSON object config.");
            return;
        }

        if (!config.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            errors.Add($"Node '{node.NodeKey}' of type '{node.NodeType}' requires config property '{propertyName}' as non-empty string.");
        }
    }

    public static void ValidateRequiredStringConfig(
        WorkflowDefinitionDraftNode node,
        string propertyName,
        List<WorkflowDefinitionValidationIssue> issues)
    {
        if (!HasConfig(node.Config))
        {
            issues.Add(CreateIssue(
                "missing_node_config",
                $"Node '{node.NodeKey}' of type '{node.NodeType}' requires a config object with '{propertyName}'.",
                "workflow_node",
                node.NodeKey));
            return;
        }

        var config = node.Config!.Value;
        if (config.ValueKind != JsonValueKind.Object)
        {
            issues.Add(CreateIssue(
                "invalid_node_config_kind",
                $"Node '{node.NodeKey}' of type '{node.NodeType}' requires a JSON object config.",
                "workflow_node",
                node.NodeKey));
            return;
        }

        if (!config.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            issues.Add(CreateIssue(
                "missing_required_node_config_property",
                $"Node '{node.NodeKey}' of type '{node.NodeType}' requires config property '{propertyName}' as non-empty string.",
                "workflow_node",
                node.NodeKey));
        }
    }

    public static void ValidateOptionalObjectConfig(
        WorkflowDefinitionDraftNode node,
        List<string> errors)
    {
        if (!HasConfig(node.Config))
        {
            return;
        }

        if (node.Config!.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"Node '{node.NodeKey}' of type '{node.NodeType}' requires a JSON object config.");
        }
    }

    public static void ValidateOptionalObjectConfig(
        WorkflowDefinitionDraftNode node,
        List<WorkflowDefinitionValidationIssue> issues)
    {
        if (!HasConfig(node.Config))
        {
            return;
        }

        if (node.Config!.Value.ValueKind != JsonValueKind.Object)
        {
            issues.Add(CreateIssue(
                "invalid_node_config",
                $"Node '{node.NodeKey}' of type '{node.NodeType}' requires a JSON object config.",
                "workflow_node",
                node.NodeKey));
        }
    }

    public static JsonElement? CloneConfig(JsonElement? config)
    {
        if (!HasConfig(config))
        {
            return null;
        }

        return config!.Value.Clone();
    }

    public static IReadOnlyList<string> FindUnreachableNodeKeys(
        IReadOnlyList<WorkflowDefinitionDraftNode> nodes,
        IReadOnlyList<WorkflowDefinitionDraftEdge> edges)
    {
        var startNode = nodes.FirstOrDefault(n => string.Equals(n.NodeType, "start", StringComparison.Ordinal));
        if (startNode is null)
        {
            return [];
        }

        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            adjacency[node.NodeKey] = [];
        }

        foreach (var edge in edges)
        {
            if (adjacency.TryGetValue(edge.SourceNodeKey, out var targets) && adjacency.ContainsKey(edge.TargetNodeKey))
            {
                targets.Add(edge.TargetNodeKey);
            }
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(startNode.NodeKey);
        visited.Add(startNode.NodeKey);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var neighbor in neighbors)
            {
                if (visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return nodes.Select(n => n.NodeKey).Where(key => !visited.Contains(key)).ToList();
    }

    public static bool HasAnyExpectedValue(JsonElement root)
    {
        if (root.TryGetProperty("expectedValueText", out var textProperty)
            && textProperty.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(textProperty.GetString()))
        {
            return true;
        }
        if (root.TryGetProperty("expectedValueBoolean", out var boolProperty)
            && boolProperty.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return true;
        }
        if (root.TryGetProperty("expectedValueNumber", out var numberProperty)
            && numberProperty.ValueKind == JsonValueKind.Number)
        {
            return true;
        }
        return false;
    }

    public static bool IsMeasureGenerationNodeType(string? nodeType)
    {
        return !string.IsNullOrWhiteSpace(nodeType)
               && WorkflowDefinitionValidationCatalog.MeasureGenerationNodeTypes.Contains(nodeType.Trim());
    }

    // Definition-Key bestimmt, welcher Maßnahmen-Node-Type fachlich erwartet wird
    // (siehe Zielarchitektur — measure_provision für onboarding, measure_deprovision für offboarding etc.).
    public static string? GetExpectedMeasureNodeTypeForDefinitionKey(string? workflowDefinitionKey)
    {
        if (string.IsNullOrWhiteSpace(workflowDefinitionKey))
        {
            return null;
        }

        return WorkflowDefinitionValidationCatalog.ExpectedMeasureNodeTypeByDefinitionKey.TryGetValue(workflowDefinitionKey.Trim(), out var nodeType)
            ? nodeType
            : null;
    }

    public static bool NodeTypeCanHaveSpecs(string nodeType) =>
        nodeType.StartsWith("measure_", StringComparison.OrdinalIgnoreCase)
        || string.Equals(nodeType, "task", StringComparison.OrdinalIgnoreCase)
        || string.Equals(nodeType, "approval", StringComparison.OrdinalIgnoreCase);

    public static bool NodeTypeAllowsAtMostOneSpec(string nodeType) =>
        string.Equals(nodeType, "task", StringComparison.OrdinalIgnoreCase)
        || string.Equals(nodeType, "approval", StringComparison.OrdinalIgnoreCase);

    public static string? TryGetNodeConfigValue(WorkflowDefinitionDraftNode node, string propertyName)
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
