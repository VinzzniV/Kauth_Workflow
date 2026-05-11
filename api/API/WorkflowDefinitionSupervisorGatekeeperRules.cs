namespace API;

internal static class WorkflowDefinitionSupervisorGatekeeperRules
{
    // Der workflowDefinitionKey steuert, gegen welchen Wert der Gatekeeper-Node-Config
    // (`workflowDefinitionKey`, mit Fallback `legacyProcessTypeKey` fuer Alt-Daten)
    // gematcht werden muss.
    public static WorkflowDefinitionSupervisorGatekeeperEvaluation Evaluate(
        IReadOnlyList<WorkflowDefinitionSupervisorGatekeeperNode> nodes,
        IReadOnlyList<WorkflowDefinitionSupervisorGatekeeperEdge> edges,
        string? workflowDefinitionKey,
        bool requiresSupervisorStep)
    {
        if (!requiresSupervisorStep)
        {
            return WorkflowDefinitionSupervisorGatekeeperEvaluation.Satisfied();
        }

        if (string.IsNullOrWhiteSpace(workflowDefinitionKey))
        {
            // FailureCode-String bleibt aus Audit-Log-Kompatibilität "primary_process_type" — interne Bedeutung ist jetzt definitionKey.
            return WorkflowDefinitionSupervisorGatekeeperEvaluation.Failed(
                "missing_supervisor_gatekeeper_primary_process_type",
                "Supervisor-pflichtige Workflow-Definitionen benötigen einen Definition-Key, bevor der Gatekeeper geprüft werden kann.");
        }

        var normalizedDefinitionKey = workflowDefinitionKey.Trim().ToLowerInvariant();
        var nodeByKey = nodes.ToDictionary(node => node.NodeKey, StringComparer.OrdinalIgnoreCase);
        var startNodes = nodes
            .Where(node => string.Equals(node.NodeType, "start", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (startNodes.Count != 1)
        {
            return WorkflowDefinitionSupervisorGatekeeperEvaluation.Failed(
                "missing_supervisor_gatekeeper_start_node",
                "Supervisor-pflichtige Workflow-Definitionen müssen genau einen Start-Node besitzen.");
        }

        var startNode = startNodes[0];
        var startOutgoingEdges = edges
            .Where(edge => string.Equals(edge.SourceNodeKey, startNode.NodeKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(edge => edge.Priority)
            .ToList();

        if (startOutgoingEdges.Count != 1)
        {
            return WorkflowDefinitionSupervisorGatekeeperEvaluation.Failed(
                "missing_supervisor_gatekeeper_start_edge",
                "Supervisor-pflichtige Workflow-Definitionen müssen direkt nach dem Start genau in einen Form-Gatekeeper verzweigen.");
        }

        var gatekeeperTargetKey = startOutgoingEdges[0].TargetNodeKey;
        if (!nodeByKey.TryGetValue(gatekeeperTargetKey, out var gatekeeperNode))
        {
            return WorkflowDefinitionSupervisorGatekeeperEvaluation.Failed(
                "missing_supervisor_gatekeeper_node",
                $"Supervisor-Gatekeeper-Zielnode '{gatekeeperTargetKey}' wurde nicht gefunden.");
        }

        if (!string.Equals(gatekeeperNode.NodeType, "form", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowDefinitionSupervisorGatekeeperEvaluation.Failed(
                "supervisor_gatekeeper_must_be_form",
                $"Supervisor-pflichtige Workflow-Definitionen müssen mit einer Form-Node starten, gefunden wurde '{gatekeeperNode.NodeType}'.");
        }

        var gatekeeperIncomingEdges = edges
            .Where(edge => string.Equals(edge.TargetNodeKey, gatekeeperNode.NodeKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (gatekeeperIncomingEdges.Count != 1
            || !string.Equals(gatekeeperIncomingEdges[0].SourceNodeKey, startNode.NodeKey, StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowDefinitionSupervisorGatekeeperEvaluation.Failed(
                "invalid_supervisor_gatekeeper_incoming_path",
                $"Supervisor-Gatekeeper-Node '{gatekeeperNode.NodeKey}' darf nur den direkten Startpfad als Eingang haben.");
        }

        if (!string.Equals(
                gatekeeperNode.WorkflowDefinitionKey,
                normalizedDefinitionKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowDefinitionSupervisorGatekeeperEvaluation.Failed(
                "supervisor_gatekeeper_process_type_mismatch",
                $"Supervisor-Gatekeeper-Node '{gatekeeperNode.NodeKey}' muss workflowDefinitionKey '{normalizedDefinitionKey}' verwenden.");
        }

        return WorkflowDefinitionSupervisorGatekeeperEvaluation.Satisfied(gatekeeperNode.NodeKey);
    }
}

internal sealed class WorkflowDefinitionSupervisorGatekeeperEvaluation
{
    private WorkflowDefinitionSupervisorGatekeeperEvaluation()
    {
    }

    public bool IsSatisfied { get; init; }
    public string? GatekeeperNodeKey { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }

    public static WorkflowDefinitionSupervisorGatekeeperEvaluation Satisfied(string? gatekeeperNodeKey = null)
    {
        return new WorkflowDefinitionSupervisorGatekeeperEvaluation
        {
            IsSatisfied = true,
            GatekeeperNodeKey = gatekeeperNodeKey
        };
    }

    public static WorkflowDefinitionSupervisorGatekeeperEvaluation Failed(string code, string message)
    {
        return new WorkflowDefinitionSupervisorGatekeeperEvaluation
        {
            IsSatisfied = false,
            FailureCode = code,
            FailureMessage = message
        };
    }
}

internal sealed class WorkflowDefinitionSupervisorGatekeeperNode
{
    public required string NodeKey { get; init; }
    public required string NodeType { get; init; }
    public string? WorkflowDefinitionKey { get; init; }
}

internal sealed class WorkflowDefinitionSupervisorGatekeeperEdge
{
    public required string SourceNodeKey { get; init; }
    public required string TargetNodeKey { get; init; }
    public int Priority { get; init; }
}
