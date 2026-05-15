using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class WorkflowRuntimeEngineTests
{
    // ---- Test helpers --------------------------------------------------

    private static WorkflowDefinitionNodeRecord Node(long id, string key, string type, string? configJson = null)
    {
        JsonElement? config = configJson is not null
            ? JsonSerializer.Deserialize<JsonElement>(configJson)
            : null;
        return new WorkflowDefinitionNodeRecord { NodeId = id, NodeKey = key, NodeType = type, SortOrder = 0, Config = config };
    }

    private static WorkflowDefinitionEdgeRecord Edge(long edgeId, long sourceId, long targetId, int priority = 0, string? condition = null)
        => new() { EdgeId = edgeId, SourceNodeId = sourceId, TargetNodeId = targetId, Priority = priority, ConditionExpression = condition };

    private static WorkflowDefinitionGraphRecord BuildGraph(
        IReadOnlyList<WorkflowDefinitionNodeRecord> nodes,
        IReadOnlyList<WorkflowDefinitionEdgeRecord> edges,
        Dictionary<long, List<WorkflowNodeActionRecord>>? actions = null)
    {
        var nodeById = nodes.ToDictionary(n => n.NodeId);
        var outgoing = nodes.ToDictionary(n => n.NodeId, _ => new List<WorkflowDefinitionEdgeRecord>());
        var incoming = nodes.ToDictionary(n => n.NodeId, _ => new List<WorkflowDefinitionEdgeRecord>());
        foreach (var e in edges)
        {
            outgoing[e.SourceNodeId].Add(e);
            incoming[e.TargetNodeId].Add(e);
        }
        return new WorkflowDefinitionGraphRecord
        {
            Nodes = nodes.ToList(),
            Edges = edges.ToList(),
            NodeById = nodeById,
            NodeActionsByNodeId = actions ?? [],
            OutgoingEdgesBySourceNodeId = outgoing,
            IncomingEdgesByTargetNodeId = incoming
        };
    }

    private static WorkflowRuntimeSnapshot Snapshot(
        WorkflowDefinitionGraphRecord graph,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord>? answers = null,
        IReadOnlyDictionary<long, string>? nodeStatuses = null,
        bool requiresSupervisorStep = false,
        string? workflowDefinitionKey = null,
        string? approvalTaskTemplateKey = null,
        IReadOnlyDictionary<long, RuntimeApprovalNodeHint>? approvalSpecs = null,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement>? automationOutputs = null)
        => new()
        {
            WorkflowId = 1,
            Graph = graph,
            AnswersByKey = answers ?? new Dictionary<string, StoredWorkflowAnswerRecord>(),
            AutomationOutputsByNodeKey = automationOutputs ?? new Dictionary<string, System.Text.Json.JsonElement>(),
            NodeInstanceStatusByWorkflowNodeId = nodeStatuses ?? new Dictionary<long, string>(),
            WorkflowDefinitionKey = workflowDefinitionKey,
            RequiresSupervisorStep = requiresSupervisorStep,
            ApprovalSpecKey = approvalTaskTemplateKey,
            ApprovalSpecByNodeId = approvalSpecs ?? new Dictionary<long, RuntimeApprovalNodeHint>()
        };

    private static StoredWorkflowAnswerRecord BoolAnswer(string key, bool value)
        => new()
        {
            WorkflowAnswerId = 1,
            AnswerDefinitionId = 1,
            AnswerKey = key,
            InputType = "boolean",
            ValueBoolean = value,
            SelectedOptionIds = [],
            SelectedOptionValues = []
        };

    // ---- Completion paths ----------------------------------------------

    [Fact]
    public void Plan_CompletedNodeHasNoOutgoingEdges_NoSteps_CompletionOutcome()
    {
        var end = Node(1, "end", "end");
        var graph = BuildGraph([end], []);
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), end);

        Assert.Empty(plan.NodeSteps);
        Assert.IsType<WorkflowCompletionOutcome>(plan.Outcome);
    }

    [Fact]
    public void Plan_StartToEnd_AutoCompleteEnd_CompletionOutcome()
    {
        var start = Node(1, "start", "start");
        var end = Node(2, "end", "end");
        var graph = BuildGraph([start, end], [Edge(10, 1, 2)]);
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), start);

        var step = Assert.Single(plan.NodeSteps);
        Assert.IsType<AutoCompleteStep>(step);
        Assert.Equal("end", step.NodeKey);
        Assert.IsType<WorkflowCompletionOutcome>(plan.Outcome);
    }

    // ---- Wait paths ----------------------------------------------------

    [Fact]
    public void Plan_StartToFormNode_EmitsWaitActivation_WaitOutcome_InProgress()
    {
        var start = Node(1, "start", "start");
        var form = Node(2, "form_a", "form");
        var graph = BuildGraph([start, form], [Edge(10, 1, 2)]);
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), start);

        var step = Assert.Single(plan.NodeSteps);
        Assert.IsType<WaitNodeActivationStep>(step);
        Assert.Equal(2, step.NodeId);
        var outcome = Assert.IsType<WorkflowWaitOutcome>(plan.Outcome);
        Assert.Equal("in_progress", outcome.ComputedStatus);
        Assert.False(outcome.RequiresStatusRecalc);
    }

    [Fact]
    public void Plan_TaskNode_WaitOutcome_WaitingForDepartment()
    {
        var form = Node(1, "form_a", "form");
        var task = Node(2, "task_a", "task");
        var graph = BuildGraph([form, task], [Edge(10, 1, 2)]);
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), form);

        Assert.Single(plan.NodeSteps);
        var outcome = Assert.IsType<WorkflowWaitOutcome>(plan.Outcome);
        Assert.Equal("waiting_for_department", outcome.ComputedStatus);
    }

    [Fact]
    public void Plan_ApprovalNode_WaitOutcome_WaitingForSupervisor()
    {
        var form = Node(1, "form_a", "form");
        var approval = Node(2, "approval_a", "approval");
        var graph = BuildGraph([form, approval], [Edge(10, 1, 2)]);
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), form);

        Assert.Single(plan.NodeSteps);
        var outcome = Assert.IsType<WorkflowWaitOutcome>(plan.Outcome);
        Assert.Equal("waiting_for_supervisor", outcome.ComputedStatus);
    }

    // ---- Decision branching --------------------------------------------

    [Fact]
    public void Plan_DecisionNode_ConditionMatches_EmitsDecisionAndFollower()
    {
        var start = Node(1, "start", "start");
        var decision = Node(2, "decision", "decision");
        var formYes = Node(3, "form_yes", "form");
        var formNo = Node(4, "form_no", "form");
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0, condition: """{"answerKey":"approved","operator":"is_true"}"""),
            Edge(12, 2, 4, priority: 1)
        };
        var graph = BuildGraph([start, decision, formYes, formNo], edges);
        var answers = new Dictionary<string, StoredWorkflowAnswerRecord> { ["approved"] = BoolAnswer("approved", true) };
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph, answers: answers), start);

        Assert.Equal(2, plan.NodeSteps.Count);
        var decisionStep = Assert.IsType<DecisionStep>(plan.NodeSteps[0]);
        Assert.Equal(2, decisionStep.NodeId);
        Assert.Equal(3, decisionStep.SelectedTargetNodeId);
        Assert.Equal("form_yes", decisionStep.SelectedTargetNodeKey);
        Assert.Equal(11, decisionStep.SelectedEdgeId);
        var waitStep = Assert.IsType<WaitNodeActivationStep>(plan.NodeSteps[1]);
        Assert.Equal("form_yes", waitStep.NodeKey);
        Assert.IsType<WorkflowWaitOutcome>(plan.Outcome);
    }

    [Fact]
    public void Plan_DecisionNode_FallbackEdge_EmitsDecisionStep()
    {
        var start = Node(1, "start", "start");
        var decision = Node(2, "decision", "decision");
        var fallback = Node(3, "form_fallback", "form");
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0)  // no condition = fallback
        };
        var graph = BuildGraph([start, decision, fallback], edges);
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), start);

        Assert.Equal(2, plan.NodeSteps.Count);
        var decisionStep = Assert.IsType<DecisionStep>(plan.NodeSteps[0]);
        Assert.Equal(11, decisionStep.SelectedEdgeId);
        Assert.IsType<WaitNodeActivationStep>(plan.NodeSteps[1]);
    }

    [Fact]
    public void Plan_DecisionNode_NoMatchingEdge_NoFallback_FailureOutcome()
    {
        var start = Node(1, "start", "start");
        var decision = Node(2, "decision", "decision");
        var form = Node(3, "form_a", "form");
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0, condition: """{"answerKey":"approved","operator":"is_true"}""")
        };
        var graph = BuildGraph([start, decision, form], edges);
        // no answer → condition false, no fallback
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), start);

        Assert.IsType<WorkflowFailureOutcome>(plan.Outcome);
    }

    [Fact]
    public void Plan_DecisionNode_InvalidConditionJson_FailureOutcome()
    {
        var start = Node(1, "start", "start");
        var decision = Node(2, "decision", "decision");
        var form = Node(3, "form_a", "form");
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0, condition: "not-valid-json{{{")
        };
        var graph = BuildGraph([start, decision, form], edges);
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), start);

        Assert.IsType<WorkflowFailureOutcome>(plan.Outcome);
    }

    // ---- Parallel split / join -----------------------------------------

    [Fact]
    public void Plan_ParallelSplit_AutoCompletesAndEnqueuesBothBranches()
    {
        var start = Node(1, "start", "start");
        var split = Node(2, "split", "parallel_split");
        var branchA = Node(3, "branch_a", "task");
        var branchB = Node(4, "branch_b", "task");
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3),
            Edge(12, 2, 4)
        };
        var graph = BuildGraph([start, split, branchA, branchB], edges);
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), start);

        Assert.Equal(3, plan.NodeSteps.Count);
        Assert.IsType<AutoCompleteStep>(plan.NodeSteps[0]);
        Assert.Equal("split", plan.NodeSteps[0].NodeKey);
        Assert.IsType<WaitNodeActivationStep>(plan.NodeSteps[1]);
        Assert.IsType<WaitNodeActivationStep>(plan.NodeSteps[2]);
        var branchKeys = new[] { plan.NodeSteps[1].NodeKey, plan.NodeSteps[2].NodeKey };
        Assert.Contains("branch_a", branchKeys);
        Assert.Contains("branch_b", branchKeys);
    }

    [Fact]
    public void Plan_ParallelJoin_NotAllBranchesDone_JoinSkipped_WaitOutcome()
    {
        var branchA = Node(2, "branch_a", "form");
        var branchB = Node(3, "branch_b", "form");
        var join = Node(4, "join", "parallel_join");
        var edges = new[]
        {
            Edge(12, 2, 4),
            Edge(13, 3, 4)
        };
        var graph = BuildGraph([branchA, branchB, join], edges);
        // branch_b still active — join cannot fire yet
        var statuses = new Dictionary<long, string> { [2] = "done", [3] = "active" };
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph, nodeStatuses: statuses), branchA);

        Assert.Empty(plan.NodeSteps);
        var outcome = Assert.IsType<WorkflowWaitOutcome>(plan.Outcome);
        Assert.Equal("in_progress", outcome.ComputedStatus);
    }

    [Fact]
    public void Plan_ParallelJoin_AllBranchesDone_EmitsAutoCompleteForJoin()
    {
        var branchA = Node(2, "branch_a", "form");
        var branchB = Node(3, "branch_b", "form");
        var join = Node(4, "join", "parallel_join");
        var end = Node(5, "end", "end");
        var edges = new[]
        {
            Edge(12, 2, 4),
            Edge(13, 3, 4),
            Edge(14, 4, 5)
        };
        var graph = BuildGraph([branchA, branchB, join, end], edges);
        var statuses = new Dictionary<long, string> { [2] = "done", [3] = "done" };
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph, nodeStatuses: statuses), branchB);

        Assert.Equal(2, plan.NodeSteps.Count);
        Assert.IsType<AutoCompleteStep>(plan.NodeSteps[0]);
        Assert.Equal("join", plan.NodeSteps[0].NodeKey);
        Assert.IsType<AutoCompleteStep>(plan.NodeSteps[1]);
        Assert.Equal("end", plan.NodeSteps[1].NodeKey);
        Assert.IsType<WorkflowCompletionOutcome>(plan.Outcome);
    }

    // ---- Measure nodes -------------------------------------------------

    [Fact]
    public void Plan_MeasureProvisionNode_EmitsMeasureActivation_RequiresStatusRecalc()
    {
        var start = Node(1, "start", "start");
        var measure = Node(2, "measure_a", "measure_provision");
        var graph = BuildGraph([start, measure], [Edge(10, 1, 2)]);
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), start);

        var step = Assert.Single(plan.NodeSteps);
        Assert.IsType<MeasureNodeActivationStep>(step);
        Assert.Equal(2, step.NodeId);
        var outcome = Assert.IsType<WorkflowWaitOutcome>(plan.Outcome);
        Assert.True(outcome.RequiresStatusRecalc);
    }

    [Theory]
    [InlineData("measure_provision")]
    [InlineData("measure_deprovision")]
    [InlineData("measure_change")]
    [InlineData("measure_rename")]
    public void Plan_AllMeasureNodeTypes_RequiresStatusRecalc(string measureType)
    {
        var start = Node(1, "start", "start");
        var measure = Node(2, "measure_node", measureType);
        var graph = BuildGraph([start, measure], [Edge(10, 1, 2)]);
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), start);

        Assert.IsType<MeasureNodeActivationStep>(Assert.Single(plan.NodeSteps));
        Assert.True(((WorkflowWaitOutcome)plan.Outcome).RequiresStatusRecalc);
    }

    // ---- Automation node -----------------------------------------------

    [Fact]
    public void Plan_AutomationNode_NoActions_FailureOutcome()
    {
        var start = Node(1, "start", "start");
        var automation = Node(2, "automation_a", "automation");
        var graph = BuildGraph([start, automation], [Edge(10, 1, 2)], actions: []);
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), start);

        var failure = Assert.IsType<WorkflowFailureOutcome>(plan.Outcome);
        Assert.Contains("automation_a", failure.Reason);
    }

    [Fact]
    public void Plan_AutomationNode_WithActions_EmitsWaitActivation()
    {
        var start = Node(1, "start", "start");
        var automation = Node(2, "automation_a", "automation");
        var graph = BuildGraph(
            [start, automation],
            [Edge(10, 1, 2)],
            actions: new Dictionary<long, List<WorkflowNodeActionRecord>>
            {
                [2] = [new WorkflowNodeActionRecord
                {
                    Id = 100, ActionDefinitionId = 1, ExecutionOrder = 0,
                    OnErrorBehavior = "fail", ActionKey = "send_email",
                    ActionName = "Send Email", HandlerType = "EmailHandler", IsIdempotent = false
                }]
            });
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), start);

        var step = Assert.Single(plan.NodeSteps);
        Assert.IsType<WaitNodeActivationStep>(step);
        Assert.Equal("automation_a", step.NodeKey);
    }

    // ---- Failure paths -------------------------------------------------

    [Fact]
    public void Plan_UnsupportedNodeType_FailureOutcome()
    {
        var start = Node(1, "start", "start");
        var unknown = Node(2, "unknown_node", "custom_unsupported_type");
        var graph = BuildGraph([start, unknown], [Edge(10, 1, 2)]);
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), start);

        var failure = Assert.IsType<WorkflowFailureOutcome>(plan.Outcome);
        Assert.Contains("custom_unsupported_type", failure.Reason);
    }

    // ---- Idempotency guard ---------------------------------------------

    [Fact]
    public void Plan_NextNodeAlreadyHasInstance_Skipped_ExistingActiveKeptInStatus()
    {
        var start = Node(1, "start", "start");
        var form = Node(2, "form_a", "form");
        var graph = BuildGraph([start, form], [Edge(10, 1, 2)]);
        var statuses = new Dictionary<long, string> { [2] = "active" };
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph, nodeStatuses: statuses), start);

        Assert.Empty(plan.NodeSteps);
        var outcome = Assert.IsType<WorkflowWaitOutcome>(plan.Outcome);
        Assert.Equal("in_progress", outcome.ComputedStatus);
    }

    // ---- Supervisor approval bridge ------------------------------------

    [Fact]
    public void Plan_ApprovalAfterGatekeeperForm_EmitsBridgeSkipAndContinues()
    {
        // Graph: start(1) → gatekeeper_form(2) → approval(3) → end(4)
        // gatekeeper_form has workflowDefinitionKey="onboarding" in config
        var start = Node(1, "start", "start");
        var gatekeeper = Node(2, "gatekeeper_form", "form", configJson: """{"workflowDefinitionKey":"onboarding"}""");
        var approval = Node(3, "supervisor_approval", "approval");
        var end = Node(4, "end", "end");
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3),
            Edge(12, 3, 4)
        };
        var graph = BuildGraph([start, gatekeeper, approval, end], edges);
        var approvalSpecs = new Dictionary<long, RuntimeApprovalNodeHint>
        {
            [3] = new RuntimeApprovalNodeHint { TemplateKey = "supervisor_approval_template" }
        };
        var snapshot = Snapshot(
            graph,
            requiresSupervisorStep: true,
            workflowDefinitionKey: "onboarding",
            approvalTaskTemplateKey: "supervisor_approval_template",
            approvalSpecs: approvalSpecs);

        var plan = WorkflowRuntimeEngine.Plan(snapshot, gatekeeper);

        Assert.Equal(2, plan.NodeSteps.Count);
        var bridgeStep = Assert.IsType<SupervisorApprovalBridgeSkipStep>(plan.NodeSteps[0]);
        Assert.Equal("supervisor_approval", bridgeStep.NodeKey);
        Assert.IsType<AutoCompleteStep>(plan.NodeSteps[1]);
        Assert.Equal("end", plan.NodeSteps[1].NodeKey);
        Assert.IsType<WorkflowCompletionOutcome>(plan.Outcome);
    }

    [Fact]
    public void Plan_ApprovalAfterGatekeeperForm_TemplateKeyMismatch_NoBridge_WaitsOnApproval()
    {
        var start = Node(1, "start", "start");
        var gatekeeper = Node(2, "gatekeeper_form", "form", configJson: """{"workflowDefinitionKey":"onboarding"}""");
        var approval = Node(3, "supervisor_approval", "approval");
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3)
        };
        var graph = BuildGraph([start, gatekeeper, approval], edges);
        var approvalSpecs = new Dictionary<long, RuntimeApprovalNodeHint>
        {
            [3] = new RuntimeApprovalNodeHint { TemplateKey = "different_template" }  // mismatch
        };
        var snapshot = Snapshot(
            graph,
            requiresSupervisorStep: true,
            workflowDefinitionKey: "onboarding",
            approvalTaskTemplateKey: "supervisor_approval_template",
            approvalSpecs: approvalSpecs);

        var plan = WorkflowRuntimeEngine.Plan(snapshot, gatekeeper);

        var step = Assert.Single(plan.NodeSteps);
        Assert.IsType<WaitNodeActivationStep>(step);
        Assert.Equal("supervisor_approval", step.NodeKey);
    }

    // ---- Post-plan active node composition ----------------------------

    [Fact]
    public void Plan_ExistingActiveTaskInSnapshot_IncludedInStatusComputation()
    {
        // form_a → end; existingTask is active from a prior plan
        var form = Node(1, "form_a", "form");
        var end = Node(2, "end", "end");
        var existingTask = Node(3, "existing_task", "task");
        var graph = BuildGraph([form, end, existingTask], [Edge(10, 1, 2)]);
        var statuses = new Dictionary<long, string> { [3] = "active" };
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph, nodeStatuses: statuses), form);

        Assert.Single(plan.NodeSteps);
        Assert.IsType<AutoCompleteStep>(plan.NodeSteps[0]);
        // end auto-completes but existingTask still active → WaitOutcome
        var outcome = Assert.IsType<WorkflowWaitOutcome>(plan.Outcome);
        Assert.Equal("waiting_for_department", outcome.ComputedStatus);
    }

    [Fact]
    public void Plan_FormActive_RequiresStatusRecalc_False()
    {
        var start = Node(1, "start", "start");
        var form = Node(2, "form_a", "form");
        var graph = BuildGraph([start, form], [Edge(10, 1, 2)]);
        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), start);

        var outcome = Assert.IsType<WorkflowWaitOutcome>(plan.Outcome);
        Assert.False(outcome.RequiresStatusRecalc);
    }

    // ---- Decision condition parser (Z21-S6b: AND/OR-Mehrbedingungen) -----

    [Fact]
    public void ParseDecisionConditionExpression_SingleForm_ReturnsOneConditionWithAndLogic()
    {
        const string json = """{"answerKey":"x","operator":"is_true"}""";
        var expr = WorkflowRuntimeEngine.ParseDecisionConditionExpression(json);
        Assert.Equal(DecisionConditionLogic.And, expr.Logic);
        Assert.Single(expr.Conditions);
        var answer = Assert.IsType<DecisionConditionRecord.AnswerBased>(expr.Conditions[0]);
        Assert.Equal("x", answer.Condition.AnswerKey);
        Assert.Equal("is_true", answer.Condition.Operator);
    }

    [Fact]
    public void ParseDecisionConditionExpression_MultiFormWithAnd_ReturnsAllConditions()
    {
        const string json = """{"logic":"AND","conditions":[{"answerKey":"a","operator":"is_true"},{"answerKey":"b","operator":"is_false"}]}""";
        var expr = WorkflowRuntimeEngine.ParseDecisionConditionExpression(json);
        Assert.Equal(DecisionConditionLogic.And, expr.Logic);
        Assert.Equal(2, expr.Conditions.Count);
    }

    [Fact]
    public void ParseDecisionConditionExpression_MultiFormWithOr_ReturnsOrLogic()
    {
        const string json = """{"logic":"or","conditions":[{"answerKey":"a","operator":"is_true"}]}""";
        var expr = WorkflowRuntimeEngine.ParseDecisionConditionExpression(json);
        Assert.Equal(DecisionConditionLogic.Or, expr.Logic);
    }

    [Fact]
    public void ParseDecisionConditionExpression_MultiFormWithoutLogic_DefaultsToAnd()
    {
        const string json = """{"conditions":[{"answerKey":"a","operator":"is_true"}]}""";
        var expr = WorkflowRuntimeEngine.ParseDecisionConditionExpression(json);
        Assert.Equal(DecisionConditionLogic.And, expr.Logic);
    }

    [Fact]
    public void ParseDecisionConditionExpression_MultiFormWithEmptyConditions_Throws()
    {
        const string json = """{"logic":"AND","conditions":[]}""";
        Assert.Throws<InvalidOperationException>(
            () => WorkflowRuntimeEngine.ParseDecisionConditionExpression(json));
    }

    [Fact]
    public void ParseDecisionConditionExpression_UnknownLogic_Throws()
    {
        const string json = """{"logic":"XOR","conditions":[{"answerKey":"a","operator":"is_true"}]}""";
        Assert.Throws<InvalidOperationException>(
            () => WorkflowRuntimeEngine.ParseDecisionConditionExpression(json));
    }

    [Fact]
    public void EvaluateDecisionConditionExpression_AndAllTrue_ReturnsTrue()
    {
        var expr = new DecisionConditionExpressionRecord
        {
            Logic = DecisionConditionLogic.And,
            Conditions = new DecisionConditionRecord[]
            {
                new DecisionConditionRecord.AnswerBased(ConditionRecord("a", "is_true")),
                new DecisionConditionRecord.AnswerBased(ConditionRecord("b", "is_true"))
            }
        };
        var answers = new Dictionary<string, StoredWorkflowAnswerRecord>
        {
            ["a"] = BoolAnswer("a", true),
            ["b"] = BoolAnswer("b", true)
        };
        Assert.True(WorkflowRuntimeEngine.EvaluateDecisionConditionExpression(expr, answers));
    }

    [Fact]
    public void EvaluateDecisionConditionExpression_AndOneFalse_ReturnsFalse()
    {
        var expr = new DecisionConditionExpressionRecord
        {
            Logic = DecisionConditionLogic.And,
            Conditions = new DecisionConditionRecord[]
            {
                new DecisionConditionRecord.AnswerBased(ConditionRecord("a", "is_true")),
                new DecisionConditionRecord.AnswerBased(ConditionRecord("b", "is_true"))
            }
        };
        var answers = new Dictionary<string, StoredWorkflowAnswerRecord>
        {
            ["a"] = BoolAnswer("a", true),
            ["b"] = BoolAnswer("b", false)
        };
        Assert.False(WorkflowRuntimeEngine.EvaluateDecisionConditionExpression(expr, answers));
    }

    [Fact]
    public void EvaluateDecisionConditionExpression_OrOneTrue_ReturnsTrue()
    {
        var expr = new DecisionConditionExpressionRecord
        {
            Logic = DecisionConditionLogic.Or,
            Conditions = new DecisionConditionRecord[]
            {
                new DecisionConditionRecord.AnswerBased(ConditionRecord("a", "is_true")),
                new DecisionConditionRecord.AnswerBased(ConditionRecord("b", "is_true"))
            }
        };
        var answers = new Dictionary<string, StoredWorkflowAnswerRecord>
        {
            ["a"] = BoolAnswer("a", false),
            ["b"] = BoolAnswer("b", true)
        };
        Assert.True(WorkflowRuntimeEngine.EvaluateDecisionConditionExpression(expr, answers));
    }

    [Fact]
    public void EvaluateDecisionConditionExpression_OrAllFalse_ReturnsFalse()
    {
        var expr = new DecisionConditionExpressionRecord
        {
            Logic = DecisionConditionLogic.Or,
            Conditions = new DecisionConditionRecord[] { new DecisionConditionRecord.AnswerBased(ConditionRecord("a", "is_true")) }
        };
        var answers = new Dictionary<string, StoredWorkflowAnswerRecord>
        {
            ["a"] = BoolAnswer("a", false)
        };
        Assert.False(WorkflowRuntimeEngine.EvaluateDecisionConditionExpression(expr, answers));
    }

    private static TaskTemplateConditionRecord ConditionRecord(string answerKey, string op)
        => new()
        {
            TaskTemplateId = 0,
            ConditionGroup = 0,
            AnswerKey = answerKey,
            Operator = op,
            ExpectedValueText = null,
            ExpectedValueBoolean = null,
            ExpectedValueNumber = null
        };

    // ---- Etappe 9a Schritt 8: automation_output Decision-Conditions ----

    private static WorkflowNodeActionRecord ActionRecord(long id, string actionKey, string handlerType = "AdLdaps")
        => new()
        {
            Id = id,
            ActionDefinitionId = id,
            ExecutionOrder = 0,
            OnErrorBehavior = "fail",
            ActionKey = actionKey,
            ActionName = actionKey,
            HandlerType = handlerType,
            IsIdempotent = true
        };

    private static IReadOnlyDictionary<string, JsonElement> AutomationOutputs(string nodeKey, string json)
    {
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        return new Dictionary<string, JsonElement> { [nodeKey] = element };
    }

    private const string AutomationOutputAlreadyExistsCondition =
        """{"referenceKind":"automation_output","sourceNodeKey":"create-ad-user","property":"alreadyExisted","operator":"is_true"}""";

    [Fact]
    public void Plan_AutomationOutput_AlreadyExistedTrue_NimmtAlreadyExistsEdge()
    {
        var createAdUser = Node(1, "create-ad-user", "automation");
        var decision = Node(2, "decision_already_exists", "decision");
        var skipEnd = Node(3, "end_skip", "end");
        var freshEnd = Node(4, "end_fresh", "end");
        var actions = new Dictionary<long, List<WorkflowNodeActionRecord>>
        {
            [1] = new() { ActionRecord(100, "CreateAdUserLdaps") }
        };
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0, condition: AutomationOutputAlreadyExistsCondition),
            Edge(12, 2, 4, priority: 1)
        };
        var graph = BuildGraph([createAdUser, decision, skipEnd, freshEnd], edges, actions);
        var outputs = AutomationOutputs("create-ad-user", """{"alreadyExisted":true,"distinguishedName":"CN=x"}""");

        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph, automationOutputs: outputs), createAdUser);

        Assert.IsType<WorkflowCompletionOutcome>(plan.Outcome);
        var decisionStep = plan.NodeSteps.OfType<DecisionStep>().Single();
        Assert.Equal("decision_already_exists", decisionStep.NodeKey);
        Assert.Equal("end_skip", plan.NodeSteps.OfType<AutoCompleteStep>().Last().NodeKey);
    }

    [Fact]
    public void Plan_AutomationOutput_AlreadyExistedFalse_NimmtFreshCreateEdge()
    {
        var createAdUser = Node(1, "create-ad-user", "automation");
        var decision = Node(2, "decision_already_exists", "decision");
        var skipEnd = Node(3, "end_skip", "end");
        var freshEnd = Node(4, "end_fresh", "end");
        var actions = new Dictionary<long, List<WorkflowNodeActionRecord>>
        {
            [1] = new() { ActionRecord(100, "CreateAdUserLdaps") }
        };
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0, condition: AutomationOutputAlreadyExistsCondition),
            Edge(12, 2, 4, priority: 1)
        };
        var graph = BuildGraph([createAdUser, decision, skipEnd, freshEnd], edges, actions);
        var outputs = AutomationOutputs("create-ad-user", """{"alreadyExisted":false,"distinguishedName":"CN=x"}""");

        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph, automationOutputs: outputs), createAdUser);

        Assert.IsType<WorkflowCompletionOutcome>(plan.Outcome);
        Assert.Equal("end_fresh", plan.NodeSteps.OfType<AutoCompleteStep>().Last().NodeKey);
    }

    [Fact]
    public void Plan_AutomationOutput_NodeFehltImSnapshot_LiefertFailurePlan()
    {
        var createAdUser = Node(1, "create-ad-user", "automation");
        var decision = Node(2, "decision_already_exists", "decision");
        var skipEnd = Node(3, "end_skip", "end");
        var freshEnd = Node(4, "end_fresh", "end");
        var actions = new Dictionary<long, List<WorkflowNodeActionRecord>>
        {
            [1] = new() { ActionRecord(100, "CreateAdUserLdaps") }
        };
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0, condition: AutomationOutputAlreadyExistsCondition),
            Edge(12, 2, 4, priority: 1)
        };
        var graph = BuildGraph([createAdUser, decision, skipEnd, freshEnd], edges, actions);
        // Snapshot ohne automationOutputs -> Lookup auf "create-ad-user" schlaegt fehl.

        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), createAdUser);

        var failure = Assert.IsType<WorkflowFailureOutcome>(plan.Outcome);
        Assert.Contains("no succeeded automation output", failure.Reason);
    }

    [Fact]
    public void Plan_AutomationOutput_PropertyKeinBoolean_LiefertFailurePlan()
    {
        var createAdUser = Node(1, "create-ad-user", "automation");
        var decision = Node(2, "decision_already_exists", "decision");
        var skipEnd = Node(3, "end_skip", "end");
        var freshEnd = Node(4, "end_fresh", "end");
        var actions = new Dictionary<long, List<WorkflowNodeActionRecord>>
        {
            [1] = new() { ActionRecord(100, "CreateAdUserLdaps") }
        };
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0, condition: AutomationOutputAlreadyExistsCondition),
            Edge(12, 2, 4, priority: 1)
        };
        var graph = BuildGraph([createAdUser, decision, skipEnd, freshEnd], edges, actions);
        var outputs = AutomationOutputs("create-ad-user", """{"alreadyExisted":"true"}""");

        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph, automationOutputs: outputs), createAdUser);

        var failure = Assert.IsType<WorkflowFailureOutcome>(plan.Outcome);
        Assert.Contains("Expected boolean property", failure.Reason);
    }

    [Fact]
    public void Plan_AutomationOutput_OperatorNichtIsTrueOderIsFalse_LiefertFailurePlan()
    {
        var createAdUser = Node(1, "create-ad-user", "automation");
        var decision = Node(2, "decision_already_exists", "decision");
        var skipEnd = Node(3, "end_skip", "end");
        var freshEnd = Node(4, "end_fresh", "end");
        var actions = new Dictionary<long, List<WorkflowNodeActionRecord>>
        {
            [1] = new() { ActionRecord(100, "CreateAdUserLdaps") }
        };
        const string invalidOperator = """{"referenceKind":"automation_output","sourceNodeKey":"create-ad-user","property":"alreadyExisted","operator":"equals"}""";
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0, condition: invalidOperator),
            Edge(12, 2, 4, priority: 1)
        };
        var graph = BuildGraph([createAdUser, decision, skipEnd, freshEnd], edges, actions);
        var outputs = AutomationOutputs("create-ad-user", """{"alreadyExisted":true}""");

        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph, automationOutputs: outputs), createAdUser);

        var failure = Assert.IsType<WorkflowFailureOutcome>(plan.Outcome);
        Assert.Contains("operator", failure.Reason);
    }

    [Fact]
    public void Plan_AutomationOutput_PropertyNichtInWhitelist_LiefertFailurePlan()
    {
        var createAdUser = Node(1, "create-ad-user", "automation");
        var decision = Node(2, "decision_already_exists", "decision");
        var skipEnd = Node(3, "end_skip", "end");
        var freshEnd = Node(4, "end_fresh", "end");
        var actions = new Dictionary<long, List<WorkflowNodeActionRecord>>
        {
            [1] = new() { ActionRecord(100, "CreateAdUserLdaps") }
        };
        const string forbiddenProperty = """{"referenceKind":"automation_output","sourceNodeKey":"create-ad-user","property":"distinguishedName","operator":"is_true"}""";
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0, condition: forbiddenProperty),
            Edge(12, 2, 4, priority: 1)
        };
        var graph = BuildGraph([createAdUser, decision, skipEnd, freshEnd], edges, actions);

        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), createAdUser);

        var failure = Assert.IsType<WorkflowFailureOutcome>(plan.Outcome);
        Assert.Contains("whitelist", failure.Reason);
    }

    [Fact]
    public void Plan_AutomationOutput_NichtDirekterPredecessor_LiefertFailurePlan()
    {
        // Graph: create-ad-user(1) -> assign-groups(2) -> decision(3) -> ...
        // Decision referenziert create-ad-user, das aber kein DIRECT Predecessor mehr ist.
        var createAdUser = Node(1, "create-ad-user", "automation");
        var assignGroups = Node(2, "assign-groups", "automation");
        var decision = Node(3, "decision_already_exists", "decision");
        var skipEnd = Node(4, "end_skip", "end");
        var freshEnd = Node(5, "end_fresh", "end");
        var actions = new Dictionary<long, List<WorkflowNodeActionRecord>>
        {
            [1] = new() { ActionRecord(100, "CreateAdUserLdaps") },
            [2] = new() { ActionRecord(101, "AssignGroupsLdaps") }
        };
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3),
            Edge(12, 3, 4, priority: 0, condition: AutomationOutputAlreadyExistsCondition),
            Edge(13, 3, 5, priority: 1)
        };
        var graph = BuildGraph([createAdUser, assignGroups, decision, skipEnd, freshEnd], edges, actions);
        var outputs = AutomationOutputs("create-ad-user", """{"alreadyExisted":true}""");

        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph, automationOutputs: outputs), createAdUser);

        Assert.IsType<WorkflowWaitOutcome>(plan.Outcome);
    }

    [Fact]
    public void Plan_AutomationOutput_PredecessorOhneActions_LiefertFailurePlan()
    {
        var task = Node(1, "task_a", "task");
        var decision = Node(2, "decision_after_task", "decision");
        var endA = Node(3, "end_a", "end");
        var endB = Node(4, "end_b", "end");
        // Source-Node hat KEINE Action -> Schranke 3a wirft.
        const string condition = """{"referenceKind":"automation_output","sourceNodeKey":"task_a","property":"alreadyExisted","operator":"is_true"}""";
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0, condition: condition),
            Edge(12, 2, 4, priority: 1)
        };
        var graph = BuildGraph([task, decision, endA, endB], edges);

        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), task);

        var failure = Assert.IsType<WorkflowFailureOutcome>(plan.Outcome);
        Assert.Contains("no automation actions", failure.Reason);
    }

    [Fact]
    public void Plan_AutomationOutput_PredecessorMitMehrerenActions_LiefertFailurePlan()
    {
        var createAdUser = Node(1, "multi-action-node", "automation");
        var decision = Node(2, "decision_already_exists", "decision");
        var skipEnd = Node(3, "end_skip", "end");
        var freshEnd = Node(4, "end_fresh", "end");
        // Source-Node mit zwei Actions -> Schranke 3b wirft.
        var actions = new Dictionary<long, List<WorkflowNodeActionRecord>>
        {
            [1] = new()
            {
                ActionRecord(100, "CreateAdUserLdaps"),
                ActionRecord(101, "AssignGroupsLdaps")
            }
        };
        const string condition = """{"referenceKind":"automation_output","sourceNodeKey":"multi-action-node","property":"alreadyExisted","operator":"is_true"}""";
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0, condition: condition),
            Edge(12, 2, 4, priority: 1)
        };
        var graph = BuildGraph([createAdUser, decision, skipEnd, freshEnd], edges, actions);

        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), createAdUser);

        var failure = Assert.IsType<WorkflowFailureOutcome>(plan.Outcome);
        Assert.Contains("multiple actions", failure.Reason);
    }

    [Fact]
    public void Plan_AutomationOutput_PredecessorActionNichtWhitelisted_LiefertFailurePlan()
    {
        var sendMail = Node(1, "send-mail", "automation");
        var decision = Node(2, "decision_already_exists", "decision");
        var skipEnd = Node(3, "end_skip", "end");
        var freshEnd = Node(4, "end_fresh", "end");
        var actions = new Dictionary<long, List<WorkflowNodeActionRecord>>
        {
            [1] = new() { ActionRecord(100, "SendWelcomeMailGraph") }
        };
        const string condition = """{"referenceKind":"automation_output","sourceNodeKey":"send-mail","property":"alreadyExisted","operator":"is_true"}""";
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0, condition: condition),
            Edge(12, 2, 4, priority: 1)
        };
        var graph = BuildGraph([sendMail, decision, skipEnd, freshEnd], edges, actions);

        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph), sendMail);

        var failure = Assert.IsType<WorkflowFailureOutcome>(plan.Outcome);
        Assert.Contains("not a supported producer", failure.Reason);
    }

    [Fact]
    public void Plan_AutomationOutput_MixedMultiForm_AndLogic_NimmtPath()
    {
        var createAdUser = Node(1, "create-ad-user", "automation");
        var decision = Node(2, "decision_multi", "decision");
        var endA = Node(3, "end_a", "end");
        var endB = Node(4, "end_b", "end");
        var actions = new Dictionary<long, List<WorkflowNodeActionRecord>>
        {
            [1] = new() { ActionRecord(100, "CreateAdUserLdaps") }
        };
        const string mixed = """{"logic":"AND","conditions":[{"answerKey":"flag","operator":"is_true"},{"referenceKind":"automation_output","sourceNodeKey":"create-ad-user","property":"alreadyExisted","operator":"is_true"}]}""";
        var edges = new[]
        {
            Edge(10, 1, 2),
            Edge(11, 2, 3, priority: 0, condition: mixed),
            Edge(12, 2, 4, priority: 1)
        };
        var graph = BuildGraph([createAdUser, decision, endA, endB], edges, actions);
        var outputs = AutomationOutputs("create-ad-user", """{"alreadyExisted":true}""");
        var answers = new Dictionary<string, StoredWorkflowAnswerRecord> { ["flag"] = BoolAnswer("flag", true) };

        var plan = WorkflowRuntimeEngine.Plan(Snapshot(graph, answers: answers, automationOutputs: outputs), createAdUser);

        Assert.IsType<WorkflowCompletionOutcome>(plan.Outcome);
        Assert.Equal("end_a", plan.NodeSteps.OfType<AutoCompleteStep>().Last().NodeKey);
    }
}
