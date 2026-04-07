using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class WorkflowDefinitionValidationServiceTests
{
    private readonly WorkflowDefinitionValidationService _sut = new();

    [Fact]
    public void ValidateAndNormalize_AllowsSimpleStartFormTaskEndGraph()
    {
        var result = _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Form_A", "form", configJson: """{"legacyProcessTypeKey":"onboarding"}"""),
                CreateNode("Task_A", "task", configJson: """{"legacyTemplateKey":"collect_equipment"}"""),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Form_A", 0),
                CreateEdge("Form_A", "Task_A", 0),
                CreateEdge("Task_A", "End", 0)
            ]
        });

        Assert.Equal(["end", "form_a", "start", "task_a"], result.Nodes.Select(node => node.NodeKey).OrderBy(key => key).ToArray());
        Assert.Equal("form_a", result.Edges[1].SourceNodeKey);
    }

    [Fact]
    public void ValidateAndNormalize_AllowsApprovalDecisionEndGraph()
    {
        var result = _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Approval", "approval", configJson: """{"legacyTemplateKey":"manager_approval"}"""),
                CreateNode("Decision", "decision"),
                CreateNode("Done", "end"),
                CreateNode("Rejected", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Approval", 0),
                CreateEdge("Approval", "Decision", 0),
                CreateEdge("Decision", "Done", 0, """{"answerKey":"approved","operator":"is_true"}"""),
                CreateEdge("Decision", "Rejected", 1, """{"answerKey":"approved","operator":"is_false"}""")
            ]
        });

        Assert.Equal(5, result.Nodes.Count);
        Assert.Equal(4, result.Edges.Count);
    }

    [Theory]
    [InlineData("missing_start")]
    [InlineData("multiple_starts")]
    [InlineData("missing_end")]
    [InlineData("missing_config")]
    [InlineData("invalid_config")]
    [InlineData("self_loop")]
    [InlineData("missing_node_reference")]
    [InlineData("start_with_incoming")]
    [InlineData("end_with_outgoing")]
    [InlineData("duplicate_priority")]
    [InlineData("unsupported_type")]
    [InlineData("invalid_decision_json")]
    [InlineData("unsupported_decision_operator")]
    public void ValidateAndNormalize_RejectsInvalidDrafts(string scenario)
    {
        var request = scenario switch
        {
            "missing_start" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Form", "form", configJson: """{"legacyProcessTypeKey":"onboarding"}"""), CreateNode("End", "end")],
                Edges = [CreateEdge("Form", "End", 0)]
            },
            "multiple_starts" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("StartA", "start"), CreateNode("StartB", "start"), CreateNode("End", "end")],
                Edges = [CreateEdge("StartA", "End", 0), CreateEdge("StartB", "End", 0)]
            },
            "missing_end" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Task", "task", configJson: """{"legacyTemplateKey":"t"}""")],
                Edges = [CreateEdge("Start", "Task", 0)]
            },
            "missing_config" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Form", "form"), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Form", 0), CreateEdge("Form", "End", 0)]
            },
            "invalid_config" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes =
                [
                    CreateNode("Start", "start"),
                    CreateNode("Approval", "approval", configJson: """{"wrong":"value"}"""),
                    CreateNode("End", "end")
                ],
                Edges = [CreateEdge("Start", "Approval", 0), CreateEdge("Approval", "End", 0)]
            },
            "self_loop" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Task", "task", configJson: """{"legacyTemplateKey":"t"}"""), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Task", 0), CreateEdge("Task", "Task", 0), CreateEdge("Task", "End", 1)]
            },
            "missing_node_reference" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Ghost", 0)]
            },
            "start_with_incoming" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Task", "task", configJson: """{"legacyTemplateKey":"t"}"""), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Task", 0), CreateEdge("Task", "Start", 1), CreateEdge("Task", "End", 2)]
            },
            "end_with_outgoing" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("End", "end"), CreateNode("Task", "task", configJson: """{"legacyTemplateKey":"t"}""")],
                Edges = [CreateEdge("Start", "End", 0), CreateEdge("End", "Task", 0), CreateEdge("Task", "End", 1)]
            },
            "duplicate_priority" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Decision", "decision"), CreateNode("EndA", "end"), CreateNode("EndB", "end")],
                Edges = [CreateEdge("Start", "Decision", 0), CreateEdge("Decision", "EndA", 0), CreateEdge("Decision", "EndB", 0)]
            },
            "unsupported_type" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Magic", "script"), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Magic", 0), CreateEdge("Magic", "End", 0)]
            },
            "invalid_decision_json" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Decision", "decision"), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Decision", 0), CreateEdge("Decision", "End", 0, "{bad-json}")]
            },
            "unsupported_decision_operator" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Decision", "decision"), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Decision", 0), CreateEdge("Decision", "End", 0, """{"answerKey":"mailbox_requested","operator":"gt"}""")]
            },
            _ => throw new InvalidOperationException($"Unknown scenario '{scenario}'.")
        };

        var exception = Assert.Throws<InvalidOperationException>(() => _sut.ValidateAndNormalize(request));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Fact]
    public void NormalizeDefinitionKey_TrimsAndLowercases()
    {
        var normalized = _sut.NormalizeDefinitionKey("  OffBoarding  ");

        Assert.Equal("offboarding", normalized);
    }

    [Fact]
    public void ValidateSnapshot_ReturnsStructuredIssues()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("TaskA", "task", configJson: """{"legacyTemplateKey":"missing_template"}"""),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "TaskA", 0),
                CreateEdge("TaskA", "End", 0)
            ],
            ReferenceIssues =
            [
                new WorkflowDefinitionValidationIssue
                {
                    Code = "unknown_legacy_template",
                    Severity = "error",
                    Scope = "workflow_node",
                    Message = "Node 'taska' references unknown or inactive legacyTemplateKey 'missing_template'.",
                    ReferenceKey = "taska"
                }
            ]
        });

        Assert.False(snapshot.CanPublish);
        Assert.Contains(snapshot.Issues, issue => issue.Code == "unknown_legacy_template");
    }

    private static WorkflowDefinitionNodeDto CreateNode(
        string nodeKey,
        string nodeType,
        int sortOrder = 0,
        string? configJson = null)
    {
        return new WorkflowDefinitionNodeDto
        {
            NodeKey = nodeKey,
            NodeType = nodeType,
            SortOrder = sortOrder,
            Config = configJson is null ? null : JsonDocument.Parse(configJson).RootElement.Clone()
        };
    }

    private static WorkflowDefinitionEdgeDto CreateEdge(
        string sourceNodeKey,
        string targetNodeKey,
        int priority,
        string? conditionExpression = null)
    {
        return new WorkflowDefinitionEdgeDto
        {
            SourceNodeKey = sourceNodeKey,
            TargetNodeKey = targetNodeKey,
            Priority = priority,
            ConditionExpression = conditionExpression
        };
    }
}
