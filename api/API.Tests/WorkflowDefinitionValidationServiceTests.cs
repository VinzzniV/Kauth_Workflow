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
                CreateNode("Start", "start", positionX: 60, positionY: 40),
                CreateNode("Form_A", "form", configJson: """{"legacyProcessTypeKey":"onboarding"}""", positionX: 320, positionY: 40),
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
        Assert.Contains(result.Nodes, node => node.NodeKey == "form_a" && node.PositionX == 320 && node.PositionY == 40);
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

    [Fact]
    public void ValidateAndNormalize_AllowsAutomationNodeWithActions()
    {
        var result = _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode(
                    "Auto",
                    "automation",
                    actions:
                    [
                        CreateAction("CreateAdUser", 10, """{"employeeNumber":{"source":"workflow","property":"employeeNumber"}}"""),
                        CreateAction("AssignGroups", 20, """{"groups":{"source":"static","value":["grp-a","grp-b"]}}""")
                    ]),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Auto", 0),
                CreateEdge("Auto", "End", 0)
            ]
        });

        var automationNode = Assert.Single(result.Nodes, node => node.NodeType == "automation");
        Assert.Equal(2, automationNode.Actions.Count);
    }

    [Fact]
    public void ValidateAndNormalize_AllowsExplicitParallelSplitAndJoinGraph()
    {
        var result = _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Split", "parallel_split"),
                CreateNode("Task_A", "task", configJson: """{"legacyTemplateKey":"collect_equipment"}"""),
                CreateNode("Task_B", "task", configJson: """{"legacyTemplateKey":"collect_equipment"}"""),
                CreateNode("Join", "parallel_join"),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Split", 0),
                CreateEdge("Split", "Task_A", 0),
                CreateEdge("Split", "Task_B", 1),
                CreateEdge("Task_A", "Join", 0),
                CreateEdge("Task_B", "Join", 0),
                CreateEdge("Join", "End", 0)
            ]
        });

        Assert.Equal(6, result.Nodes.Count);
        Assert.Contains(result.Nodes, node => node.NodeType == "parallel_split");
        Assert.Contains(result.Nodes, node => node.NodeType == "parallel_join");
    }

    [Fact]
    public void ValidateAndNormalize_AllowsBusinessPhaseMeasureGraph()
    {
        var result = _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Requirements", "form", configJson: """{"legacyProcessTypeKey":"offboarding"}"""),
                CreateNode("Department_Setup", "measure_deprovision", configJson: """{"summaryText":"Entzieht bereichsbezogene Maßnahmen."}"""),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Requirements", 0),
                CreateEdge("Requirements", "Department_Setup", 0),
                CreateEdge("Department_Setup", "End", 0)
            ]
        });

        Assert.Equal(4, result.Nodes.Count);
        Assert.Contains(result.Nodes, node => node.NodeType == "measure_deprovision");
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
    [InlineData("missing_automation_actions")]
    [InlineData("duplicate_action_order")]
    [InlineData("invalid_parallel_split")]
    [InlineData("invalid_parallel_join")]
    [InlineData("invalid_multi_outgoing_task")]
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
            "missing_automation_actions" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Auto", "automation"), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Auto", 0), CreateEdge("Auto", "End", 0)]
            },
            "duplicate_action_order" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes =
                [
                    CreateNode(
                        "Start",
                        "start"),
                    CreateNode(
                        "Auto",
                        "automation",
                        actions:
                        [
                            CreateAction("CreateAdUser", 10),
                            CreateAction("AssignGroups", 10)
                        ]),
                    CreateNode("End", "end")
                ],
                Edges = [CreateEdge("Start", "Auto", 0), CreateEdge("Auto", "End", 0)]
            },
            "invalid_parallel_split" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Split", "parallel_split"), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Split", 0), CreateEdge("Split", "End", 0)]
            },
            "invalid_parallel_join" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Join", "parallel_join"), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Join", 0), CreateEdge("Join", "End", 0)]
            },
            "invalid_multi_outgoing_task" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes =
                [
                    CreateNode("Start", "start"),
                    CreateNode("Task", "task", configJson: """{"legacyTemplateKey":"collect_equipment"}"""),
                    CreateNode("End_A", "end"),
                    CreateNode("End_B", "end")
                ],
                Edges =
                [
                    CreateEdge("Start", "Task", 0),
                    CreateEdge("Task", "End_A", 0),
                    CreateEdge("Task", "End_B", 1)
                ]
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

    [Fact]
    public void ValidateSnapshot_RejectsSupervisorRequiredDefinitionWithoutStartGatekeeper()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Task_A", "task", configJson: """{"legacyTemplateKey":"collect_equipment"}"""),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Task_A", 0),
                CreateEdge("Task_A", "End", 0)
            ],
            PrimaryLegacyProcessTypeKey = "onboarding",
            RequiresSupervisorStep = true
        });

        Assert.False(snapshot.CanPublish);
        Assert.Contains(snapshot.Issues, issue => issue.Code == "supervisor_gatekeeper_must_be_form");
    }

    [Fact]
    public void ValidateSnapshot_RejectsSupervisorRequiredDefinitionWithMismatchedGatekeeperProcessType()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Gatekeeper", "form", configJson: """{"legacyProcessTypeKey":"offboarding"}"""),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Gatekeeper", 0),
                CreateEdge("Gatekeeper", "End", 0)
            ],
            PrimaryLegacyProcessTypeKey = "onboarding",
            RequiresSupervisorStep = true
        });

        Assert.False(snapshot.CanPublish);
        Assert.Contains(snapshot.Issues, issue => issue.Code == "supervisor_gatekeeper_process_type_mismatch");
    }

    [Fact]
    public void ValidateSnapshot_AllowsSupervisorRequiredDefinitionWithMatchingStartGatekeeper()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Gatekeeper", "form", configJson: """{"legacyProcessTypeKey":"onboarding"}"""),
                CreateNode("Task_A", "task", configJson: """{"legacyTemplateKey":"collect_equipment"}"""),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Gatekeeper", 0),
                CreateEdge("Gatekeeper", "Task_A", 0),
                CreateEdge("Task_A", "End", 0)
            ],
            PrimaryLegacyProcessTypeKey = "onboarding",
            RequiresSupervisorStep = true
        });

        Assert.True(snapshot.CanPublish);
        Assert.DoesNotContain(snapshot.Issues, issue => issue.Scope == "workflow_definition");
    }

    [Fact]
    public void ValidateSnapshot_RejectsMeasureFlowWithTechnicalMainNodes()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Requirements", "form", configJson: """{"legacyProcessTypeKey":"offboarding"}"""),
                CreateNode("Setup", "measure_deprovision"),
                CreateNode("HiddenTask", "task", configJson: """{"legacyTemplateKey":"collect_equipment"}"""),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Requirements", 0),
                CreateEdge("Requirements", "Setup", 0),
                CreateEdge("Setup", "End", 0),
                CreateEdge("HiddenTask", "End", 1)
            ],
            PrimaryLegacyProcessTypeKey = "offboarding",
            RequiresSupervisorStep = false
        });

        Assert.False(snapshot.CanPublish);
        Assert.Contains(snapshot.Issues, issue => issue.Code == "technical_nodes_not_allowed_in_measure_flow");
    }

    [Fact]
    public void ValidateSnapshot_AllowsSupervisorRequiredMeasureFlowWithFormGatekeeperWithoutApprovalPhase()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Requirements", "form", configJson: """{"legacyProcessTypeKey":"onboarding"}"""),
                CreateNode("Setup", "measure_provision"),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Requirements", 0),
                CreateEdge("Requirements", "Setup", 0),
                CreateEdge("Setup", "End", 0)
            ],
            PrimaryLegacyProcessTypeKey = "onboarding",
            RequiresSupervisorStep = true
        });

        Assert.True(snapshot.CanPublish);
        Assert.DoesNotContain(snapshot.Issues, issue => issue.Code == "measure_flow_requires_approval");
    }

    [Fact]
    public void ValidateSnapshot_RejectsUnexpectedApprovalInNonSupervisorMeasureFlow()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Requirements", "form", configJson: """{"legacyProcessTypeKey":"offboarding"}"""),
                CreateNode("Approval", "approval", configJson: """{"legacyTemplateKey":"manager_approval"}"""),
                CreateNode("Setup", "measure_deprovision"),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Requirements", 0),
                CreateEdge("Requirements", "Approval", 0),
                CreateEdge("Approval", "Setup", 0),
                CreateEdge("Setup", "End", 0)
            ],
            PrimaryLegacyProcessTypeKey = "offboarding",
            RequiresSupervisorStep = false
        });

        Assert.False(snapshot.CanPublish);
        Assert.Contains(snapshot.Issues, issue => issue.Code == "measure_flow_unexpected_approval");
    }

    [Fact]
    public void ValidateSnapshot_AllowsLegacySetupAliasForMigratedProcess()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Requirements", "form", configJson: """{"legacyProcessTypeKey":"offboarding"}"""),
                CreateNode("Setup", "setup"),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Requirements", 0),
                CreateEdge("Requirements", "Setup", 0),
                CreateEdge("Setup", "End", 0)
            ],
            PrimaryLegacyProcessTypeKey = "offboarding",
            RequiresSupervisorStep = false
        });

        Assert.True(snapshot.CanPublish);
        Assert.DoesNotContain(snapshot.Issues, issue => issue.Code == "measure_flow_process_type_mismatch");
    }

    [Theory]
    [InlineData("name_change", "measure_rename")]
    [InlineData("position_change", "measure_change")]
    [InlineData("role_change", "measure_change")]
    public void ValidateSnapshot_AllowsPhaseCMeasureTypeForProcess(string processTypeKey, string measureNodeType)
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Requirements", "form", configJson: $$"""{"legacyProcessTypeKey":"{{processTypeKey}}"}"""),
                CreateNode("Setup", measureNodeType),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Requirements", 0),
                CreateEdge("Requirements", "Setup", 0),
                CreateEdge("Setup", "End", 0)
            ],
            PrimaryLegacyProcessTypeKey = processTypeKey,
            RequiresSupervisorStep = false
        });

        Assert.True(snapshot.CanPublish);
        Assert.DoesNotContain(snapshot.Issues, issue => issue.Code == "measure_flow_process_type_mismatch");
    }

    [Fact]
    public void ValidateSnapshot_RejectsWrongPhaseCMeasureTypeForProcess()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Requirements", "form", configJson: """{"legacyProcessTypeKey":"role_change"}"""),
                CreateNode("Setup", "measure_rename"),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Requirements", 0),
                CreateEdge("Requirements", "Setup", 0),
                CreateEdge("Setup", "End", 0)
            ],
            PrimaryLegacyProcessTypeKey = "role_change",
            RequiresSupervisorStep = false
        });

        Assert.False(snapshot.CanPublish);
        Assert.Contains(snapshot.Issues, issue => issue.Code == "measure_flow_process_type_mismatch");
    }

    private static WorkflowDefinitionNodeDto CreateNode(
        string nodeKey,
        string nodeType,
        int sortOrder = 0,
        string? configJson = null,
        int? positionX = null,
        int? positionY = null,
        params WorkflowNodeActionDto[] actions)
    {
        return new WorkflowDefinitionNodeDto
        {
            NodeKey = nodeKey,
            NodeType = nodeType,
            SortOrder = sortOrder,
            PositionX = positionX,
            PositionY = positionY,
            Config = configJson is null ? null : JsonDocument.Parse(configJson).RootElement.Clone(),
            Actions = actions.ToList()
        };
    }

    private static WorkflowNodeActionDto CreateAction(
        string actionKey,
        int executionOrder,
        string? inputMappingJson = null,
        string onErrorBehavior = "fail_workflow")
    {
        return new WorkflowNodeActionDto
        {
            ActionKey = actionKey,
            ExecutionOrder = executionOrder,
            OnErrorBehavior = onErrorBehavior,
            InputMapping = inputMappingJson is null ? null : JsonDocument.Parse(inputMappingJson).RootElement.Clone()
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
