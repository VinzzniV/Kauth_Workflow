using Npgsql;

namespace API;

// Schritt 7 Slice 1.5 — Adapter zwischen Pure-Engine (WorkflowRuntimeEngine.Plan)
// und Repository-SQL.
//
// LoadRuntimeSnapshot konsolidiert alle DB-Reads, die der Engine-Plan-Lauf
// braucht, in einem Aufruf (vorher verteilt mitten im Loop).
// ApplyRuntimePlan fuehrt die Plan-NodeSteps + das Outcome via SQL aus und
// signalisiert ueber WorkflowRuntimeApplyResult, ob ein Re-Plan noetig ist
// (Q6, Apply-seitige Iteration).
//
// Beide Methoden bleiben partial-class-Mitglieder von
// PostgresWorkflowRuntimeRepository, damit sie auf die existierenden
// private-static-Helper zugreifen koennen, ohne deren Sichtbarkeit zu
// erweitern.
//
// Slice 1.5 ist additiv: die neuen Adapter-Methoden sind noch nicht im
// Loop verdrahtet. Slice 1.6 stellt AdvanceRuntimeUntilWaitOrTerminal auf
// LoadRuntimeSnapshot + Engine.Plan + ApplyRuntimePlan um.

internal sealed partial class PostgresWorkflowRuntimeRepository
{
    internal static async Task<WorkflowRuntimeSnapshot> LoadRuntimeSnapshot(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        WorkflowDefinitionGraphRecord graph,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        var nodeInstanceStates = await LoadAllWorkflowNodeInstanceStates(connection, transaction, workflowId);
        var statusContext = await LoadRuntimeWorkflowStatusContext(connection, transaction, workflowId);
        var taskGenContext = await PostgresWorkflowTaskGenerationService.LoadWorkflowTaskGenerationContextAsync(
            connection,
            transaction,
            workflowId);
        var approvalSpecHints = await LoadApprovalSpecHintsForGraph(connection, transaction, graph);

        // Etappe 9a Schritt 8: succeeded Automation-Outputs fuer automation_output-Decision-
        // Bedingungen. Aktuell nur CreateAdUserLdaps whitelisted (siehe
        // WorkflowRuntimeEngine.AllowedConditionProperties). Weitere Producer kommen mit dem
        // jeweiligen Use-Case dazu und werden hier in dasselbe Dictionary gemerged.
        var automationOutputsByNodeKey = await PostgresWorkflowAutomationOperations
            .LoadCreatedAdUserOutputsForWorkflowInScope(connection, transaction, workflowId, CancellationToken.None);

        return new WorkflowRuntimeSnapshot
        {
            WorkflowId = workflowId,
            Graph = graph,
            AnswersByKey = answersByKey,
            AutomationOutputsByNodeKey = automationOutputsByNodeKey,
            NodeInstanceStatusByWorkflowNodeId = nodeInstanceStates,
            WorkflowDefinitionKey = statusContext.WorkflowDefinitionKey,
            RequiresSupervisorStep = statusContext.RequiresSupervisorStep,
            ApprovalSpecKey = taskGenContext.ApprovalSpecKey,
            ApprovalSpecByNodeId = approvalSpecHints
        };
    }

    internal static async Task<WorkflowRuntimeApplyResult> ApplyRuntimePlan(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkflowRuntimeSnapshot snapshot,
        WorkflowRuntimePlan plan,
        long? actorUserId)
    {
        var workflowId = snapshot.WorkflowId;
        var graph = snapshot.Graph;
        var answersByKey = snapshot.AnswersByKey;

        await SetWorkflowRuntimeState(connection, transaction, workflowId, RuntimeStatusRunning, "in_progress", null);

        var immediatelyCompletedMeasures = new List<long>();

        foreach (var step in plan.NodeSteps)
        {
            switch (step)
            {
                case AutoCompleteStep auto:
                    await ApplyAutoCompleteStep(connection, transaction, workflowId, auto);
                    break;

                case DecisionStep decision:
                    await ApplyDecisionStep(connection, transaction, workflowId, decision);
                    break;

                case SupervisorApprovalBridgeSkipStep bridge:
                    await ApplyBridgeStep(connection, transaction, workflowId, bridge, graph, actorUserId);
                    break;

                case MeasureNodeActivationStep measure:
                {
                    var node = graph.NodeById[measure.NodeId];
                    var completedImmediately = await ApplyMeasureActivationStep(
                        connection,
                        transaction,
                        workflowId,
                        node,
                        answersByKey,
                        actorUserId);
                    if (completedImmediately)
                    {
                        immediatelyCompletedMeasures.Add(measure.NodeId);
                    }
                    break;
                }

                case WaitNodeActivationStep wait:
                {
                    var node = graph.NodeById[wait.NodeId];
                    var actions = graph.NodeActionsByNodeId.TryGetValue(node.NodeId, out var actionList)
                        ? actionList
                        : null;
                    await ApplyWaitActivationStep(
                        connection,
                        transaction,
                        workflowId,
                        node,
                        actions,
                        answersByKey,
                        actorUserId);
                    break;
                }

                default:
                    throw new InvalidOperationException(
                        $"Unsupported runtime plan step '{step.GetType().Name}' for node '{step.NodeKey}'.");
            }
        }

        await ApplyOutcome(connection, transaction, workflowId, plan.Outcome, actorUserId);

        return new WorkflowRuntimeApplyResult
        {
            ImmediatelyCompletedMeasureNodeIds = immediatelyCompletedMeasures
        };
    }

    // ---- Snapshot-Loader-Helpers ----------------------------------------

    private static async Task<IReadOnlyDictionary<long, string>> LoadAllWorkflowNodeInstanceStates(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = """
SELECT workflow_node_id, status
FROM workflow_node_instances
WHERE workflow_id = @workflowId;
""";

        var result = new Dictionary<long, string>();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetInt64(0)] = reader.GetString(1);
        }
        return result;
    }

    private static async Task<IReadOnlyDictionary<long, RuntimeApprovalNodeHint>> LoadApprovalSpecHintsForGraph(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkflowDefinitionGraphRecord graph)
    {
        var result = new Dictionary<long, RuntimeApprovalNodeHint>();
        foreach (var node in graph.Nodes)
        {
            if (!string.Equals(node.NodeType, "approval", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var spec = await LoadTaskSpecForNode(connection, transaction, node.NodeId);
            if (spec is not null)
            {
                result[node.NodeId] = new RuntimeApprovalNodeHint { TemplateKey = spec.TemplateKey };
            }
        }
        return result;
    }

    // ---- Plan-Step-Apply-Helpers ---------------------------------------

    private static async Task ApplyAutoCompleteStep(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        AutoCompleteStep step)
    {
        var node = new WorkflowDefinitionNodeRecord
        {
            NodeId = step.NodeId,
            NodeKey = step.NodeKey,
            NodeType = step.NodeType,
            SortOrder = 0
        };

        var nodeInstanceId = await CreateWorkflowNodeInstance(
            connection,
            transaction,
            workflowId,
            node,
            NodeInstanceStatusDone,
            CreateJsonbPayload(new { auto = true }));

        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflowId,
            nodeInstanceId,
            "node_completed",
            CreateJsonbPayload(new { nodeKey = step.NodeKey, nodeType = step.NodeType, auto = true }));
    }

    private static async Task ApplyDecisionStep(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        DecisionStep step)
    {
        var node = new WorkflowDefinitionNodeRecord
        {
            NodeId = step.NodeId,
            NodeKey = step.NodeKey,
            NodeType = step.NodeType,
            SortOrder = 0
        };

        var nodeInstanceId = await CreateWorkflowNodeInstance(
            connection,
            transaction,
            workflowId,
            node,
            NodeInstanceStatusDone,
            CreateJsonbPayload(new { auto = true }));

        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflowId,
            nodeInstanceId,
            "node_completed",
            CreateJsonbPayload(new { nodeKey = step.NodeKey, nodeType = step.NodeType, auto = true }));

        await UpdateNodeInstanceStatus(
            connection,
            transaction,
            nodeInstanceId,
            NodeInstanceStatusDone,
            CreateJsonbPayload(new
            {
                auto = true,
                selectedTargetNodeKey = step.SelectedTargetNodeKey,
                selectedEdgeId = step.SelectedEdgeId
            }));

        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflowId,
            nodeInstanceId,
            "decision_branch_selected",
            CreateJsonbPayload(new
            {
                nodeKey = step.NodeKey,
                targetNodeKey = step.SelectedTargetNodeKey,
                edgePriority = step.EdgePriority
            }));
    }

    private static async Task ApplyBridgeStep(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        SupervisorApprovalBridgeSkipStep step,
        WorkflowDefinitionGraphRecord graph,
        long? actorUserId)
    {
        var node = graph.NodeById[step.NodeId];

        var nodeInstanceId = await CreateWorkflowNodeInstance(
            connection,
            transaction,
            workflowId,
            node,
            NodeInstanceStatusDone,
            CreateJsonbPayload(new
            {
                auto = true,
                reason = "supervisor_gatekeeper_form_already_completed"
            }));

        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflowId,
            nodeInstanceId,
            "node_completed",
            CreateJsonbPayload(new
            {
                nodeKey = step.NodeKey,
                nodeType = step.NodeType,
                auto = true,
                reason = "supervisor_gatekeeper_form_already_completed"
            }));

        await PostgresRepositorySharedHelpers.InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            null,
            actorUserId,
            "runtime_node_auto_completed",
            null,
            step.NodeType,
            step.NodeKey);
    }

    // Aktiviert measure_*-Node, generiert Setup-Tasks, prueft sofortige
    // Auto-Completion (Q6 Option a). Bei sofortiger Completion wird der
    // Node-Instance als done markiert + Audit geschrieben; der Lifecycle-
    // Service triggert per ApplyResult einen Re-Plan.
    private static async Task<bool> ApplyMeasureActivationStep(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        WorkflowDefinitionNodeRecord node,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        long? actorUserId)
    {
        var nodeInstanceId = await CreateWorkflowNodeInstance(
            connection,
            transaction,
            workflowId,
            node,
            NodeInstanceStatusActive,
            null);

        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflowId,
            nodeInstanceId,
            "node_activated",
            CreateJsonbPayload(new { nodeKey = node.NodeKey, nodeType = node.NodeType }));

        await EnsureRuntimeSetupTasksGenerated(connection, transaction, workflowId, node, answersByKey, actorUserId);

        await PostgresRepositorySharedHelpers.InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            null,
            actorUserId,
            "runtime_node_waiting",
            null,
            node.NodeType,
            node.NodeKey);

        // Q6: sofortige Auto-Completion erkennen — wenn alle Pflicht-Tasks
        // bereits done sind (z. B. 0 Tasks generiert), Node sofort als done
        // markieren und an den Lifecycle-Service zurueckgeben fuer Re-Plan.
        if (await AreRuntimeSetupTasksCompleted(connection, transaction, workflowId))
        {
            await UpdateNodeInstanceStatus(
                connection,
                transaction,
                nodeInstanceId,
                NodeInstanceStatusDone,
                CreateJsonbPayload(new { completedBy = "task_completion" }));

            await InsertWorkflowRuntimeEvent(
                connection,
                transaction,
                workflowId,
                nodeInstanceId,
                "setup_completed",
                CreateJsonbPayload(new { nodeKey = node.NodeKey }));

            await PostgresRepositorySharedHelpers.InsertAuditEntry(
                connection,
                transaction,
                workflowId,
                null,
                actorUserId,
                "runtime_setup_completed",
                null,
                NodeInstanceStatusDone,
                node.NodeKey);

            return true;
        }

        return false;
    }

    private static async Task ApplyWaitActivationStep(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        WorkflowDefinitionNodeRecord node,
        IReadOnlyList<WorkflowNodeActionRecord>? actions,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        long? actorUserId)
    {
        var nodeInstanceId = await CreateWorkflowNodeInstance(
            connection,
            transaction,
            workflowId,
            node,
            NodeInstanceStatusActive,
            null);

        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            workflowId,
            nodeInstanceId,
            "node_activated",
            CreateJsonbPayload(new { nodeKey = node.NodeKey, nodeType = node.NodeType }));

        // Slice 2 (Task-Automation-Binding): task-Nodes koennen optional Actions in
        // workflow_node_actions tragen. Diese werden NICHT engine-driven ausgefuehrt
        // — sie sind Admin-Gated und werden ausschliesslich ueber WorkflowAutomation-
        // PlanService (Plan-Preview) + (Slice 3) Admin-Approval-Endpoint mit Re-Auth-
        // Gate orchestriert. Der Engine-Branch hier erzeugt deshalb bewusst nur den
        // Human Task; etwaige Actions am Node bleiben fuer den Approval-Pfad reserviert.
        if (string.Equals(node.NodeType, "task", StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.NodeType, "approval", StringComparison.OrdinalIgnoreCase))
        {
            await CreateRuntimeWorkflowTask(connection, transaction, workflowId, nodeInstanceId, node);
        }
        else if (string.Equals(node.NodeType, "automation", StringComparison.OrdinalIgnoreCase))
        {
            // Engine.Plan hat bereits sichergestellt, dass mindestens eine
            // Action vorhanden ist (sonst Plan-Failure). Defensive-Check hier
            // bleibt, falls der Caller den Snapshot inkonsistent uebergibt.
            if (actions is null || actions.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Automation node '{node.NodeKey}' has no configured actions.");
            }

            var firstAction = actions
                .OrderBy(action => action.ExecutionOrder)
                .ThenBy(action => action.Id)
                .First();

            var createdAdUserOutputsByNodeKey = await PostgresWorkflowAutomationOperations.LoadCreatedAdUserOutputsForWorkflowInScope(
                connection, transaction, workflowId, CancellationToken.None);
            var createdMailboxOutputsByNodeKey = await PostgresWorkflowAutomationOperations.LoadCreatedMailboxOutputsForWorkflowInScope(
                connection, transaction, workflowId, CancellationToken.None);
            var payload = await PostgresWorkflowAutomationOperations.BuildAutomationJobPayloadAsync(
                connection,
                transaction,
                workflowId,
                firstAction.InputMapping,
                answersByKey,
                CancellationToken.None,
                createdAdUserOutputsByNodeKey,
                createdMailboxOutputsByNodeKey);

            await PostgresWorkflowAutomationOperations.CreateAutomationJobAsync(
                connection,
                transaction,
                workflowId,
                nodeInstanceId,
                firstAction.Id,
                firstAction.ActionDefinitionId,
                payload,
                CancellationToken.None);
        }

        await PostgresRepositorySharedHelpers.InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            null,
            actorUserId,
            "runtime_node_waiting",
            null,
            node.NodeType,
            node.NodeKey);
    }

    // ---- Outcome-Apply -------------------------------------------------

    private static async Task ApplyOutcome(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        RuntimeWorkflowOutcome outcome,
        long? actorUserId)
    {
        switch (outcome)
        {
            case WorkflowFailureOutcome failure:
                await FailRuntimeWorkflow(connection, transaction, workflowId, actorUserId, failure.Reason);
                break;

            case WorkflowCompletionOutcome:
                await CompleteRuntimeWorkflow(
                    connection,
                    transaction,
                    workflowId,
                    actorUserId,
                    RuntimeStatusCompleted,
                    "workflow_completed");
                break;

            case WorkflowWaitOutcome wait:
            {
                string finalStatus;
                if (wait.RequiresStatusRecalc)
                {
                    await PostgresWorkflowStatusCalculationService.RecalculateAndPersistWorkflowStatusAsync(
                        connection,
                        transaction,
                        workflowId,
                        actorUserId);
                    finalStatus = await PostgresWorkflowRepository.LoadWorkflowStatusForUpdate(
                        connection,
                        transaction,
                        workflowId) ?? "in_progress";
                }
                else
                {
                    finalStatus = wait.ComputedStatus;
                }

                await SetWorkflowRuntimeState(
                    connection,
                    transaction,
                    workflowId,
                    RuntimeStatusWaitingOnNode,
                    finalStatus,
                    null);
                break;
            }

            default:
                throw new InvalidOperationException(
                    $"Unsupported runtime workflow outcome '{outcome.GetType().Name}'.");
        }
    }
}
