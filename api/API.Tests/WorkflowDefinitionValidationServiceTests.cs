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
                CreateNode("Form_A", "form", configJson: """{"workflowDefinitionKey":"onboarding"}""", positionX: 320, positionY: 40),
                CreateNode("Task_A", "task"),
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
                CreateNode("Approval", "approval"),
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
                CreateNode("Task_A", "task"),
                CreateNode("Task_B", "task"),
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
                CreateNode("Requirements", "form", configJson: """{"workflowDefinitionKey":"offboarding"}"""),
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
    [InlineData("missing_decision_expected_value_eq")]
    [InlineData("missing_decision_expected_value_neq")]
    [InlineData("missing_automation_actions")]
    [InlineData("duplicate_action_order")]
    [InlineData("invalid_parallel_split")]
    [InlineData("invalid_parallel_join")]
    [InlineData("invalid_multi_outgoing_task")]
    [InlineData("unreachable_node")]
    public void ValidateAndNormalize_RejectsInvalidDrafts(string scenario)
    {
        var request = scenario switch
        {
            "missing_start" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Form", "form", configJson: """{"workflowDefinitionKey":"onboarding"}"""), CreateNode("End", "end")],
                Edges = [CreateEdge("Form", "End", 0)]
            },
            "multiple_starts" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("StartA", "start"), CreateNode("StartB", "start"), CreateNode("End", "end")],
                Edges = [CreateEdge("StartA", "End", 0), CreateEdge("StartB", "End", 0)]
            },
            "missing_end" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Task", "task")],
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
                Nodes = [CreateNode("Start", "start"), CreateNode("Task", "task"), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Task", 0), CreateEdge("Task", "Task", 0), CreateEdge("Task", "End", 1)]
            },
            "missing_node_reference" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Ghost", 0)]
            },
            "start_with_incoming" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Task", "task"), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Task", 0), CreateEdge("Task", "Start", 1), CreateEdge("Task", "End", 2)]
            },
            "end_with_outgoing" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("End", "end"), CreateNode("Task", "task")],
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
            "missing_decision_expected_value_eq" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Decision", "decision"), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Decision", 0), CreateEdge("Decision", "End", 0, """{"answerKey":"mailbox_requested","operator":"eq"}""")]
            },
            "missing_decision_expected_value_neq" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes = [CreateNode("Start", "start"), CreateNode("Decision", "decision"), CreateNode("End", "end")],
                Edges = [CreateEdge("Start", "Decision", 0), CreateEdge("Decision", "End", 0, """{"answerKey":"mailbox_requested","operator":"neq"}""")]
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
                    CreateNode("Task", "task"),
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
            "unreachable_node" => new ReplaceWorkflowDefinitionVersionRequest
            {
                Nodes =
                [
                    CreateNode("Start", "start"),
                    CreateNode("Main", "task"),
                    CreateNode("Orphan", "task"),
                    CreateNode("End", "end")
                ],
                Edges =
                [
                    CreateEdge("Start", "Main", 0),
                    CreateEdge("Main", "End", 0),
                    CreateEdge("Orphan", "End", 1)
                ]
            },
            _ => throw new InvalidOperationException($"Unknown scenario '{scenario}'.")
        };

        var exception = Assert.Throws<InvalidOperationException>(() => _sut.ValidateAndNormalize(request));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Fact]
    public void ValidateSnapshot_RejectsUnreachableNode()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Main", "task"),
                CreateNode("Orphan", "task"),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Main", 0),
                CreateEdge("Main", "End", 0),
                CreateEdge("Orphan", "End", 1)
            ]
        });

        Assert.False(snapshot.CanPublish);
        Assert.Contains(snapshot.Issues, issue => issue.Code == "node_not_reachable" && issue.ReferenceKey == "orphan");
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
                CreateNode("TaskA", "task"),
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
                CreateNode("Task_A", "task"),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Task_A", 0),
                CreateEdge("Task_A", "End", 0)
            ],
            WorkflowDefinitionKey ="onboarding",
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
                CreateNode("Gatekeeper", "form", configJson: """{"workflowDefinitionKey":"offboarding"}"""),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Gatekeeper", 0),
                CreateEdge("Gatekeeper", "End", 0)
            ],
            WorkflowDefinitionKey ="onboarding",
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
                CreateNode("Gatekeeper", "form", configJson: """{"workflowDefinitionKey":"onboarding"}"""),
                CreateNode("Task_A", "task"),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Gatekeeper", 0),
                CreateEdge("Gatekeeper", "Task_A", 0),
                CreateEdge("Task_A", "End", 0)
            ],
            WorkflowDefinitionKey ="onboarding",
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
                CreateNode("Requirements", "form", configJson: """{"workflowDefinitionKey":"offboarding"}"""),
                CreateNode("Setup", "measure_deprovision"),
                CreateNode("HiddenTask", "task"),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Requirements", 0),
                CreateEdge("Requirements", "Setup", 0),
                CreateEdge("Setup", "End", 0),
                CreateEdge("HiddenTask", "End", 1)
            ],
            WorkflowDefinitionKey ="offboarding",
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
                CreateNode("Requirements", "form", configJson: """{"workflowDefinitionKey":"onboarding"}"""),
                CreateNode("Setup", "measure_provision"),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Requirements", 0),
                CreateEdge("Requirements", "Setup", 0),
                CreateEdge("Setup", "End", 0)
            ],
            WorkflowDefinitionKey ="onboarding",
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
                CreateNode("Requirements", "form", configJson: """{"workflowDefinitionKey":"offboarding"}"""),
                CreateNode("Approval", "approval"),
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
            WorkflowDefinitionKey ="offboarding",
            RequiresSupervisorStep = false
        });

        Assert.False(snapshot.CanPublish);
        Assert.Contains(snapshot.Issues, issue => issue.Code == "measure_flow_unexpected_approval");
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
                CreateNode("Requirements", "form", configJson: $$"""{"workflowDefinitionKey":"{{processTypeKey}}"}"""),
                CreateNode("Setup", measureNodeType),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Requirements", 0),
                CreateEdge("Requirements", "Setup", 0),
                CreateEdge("Setup", "End", 0)
            ],
            WorkflowDefinitionKey =processTypeKey,
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
                CreateNode("Requirements", "form", configJson: """{"workflowDefinitionKey":"role_change"}"""),
                CreateNode("Setup", "measure_rename"),
                CreateNode("End", "end")
            ],
            Edges =
            [
                CreateEdge("Start", "Requirements", 0),
                CreateEdge("Requirements", "Setup", 0),
                CreateEdge("Setup", "End", 0)
            ],
            WorkflowDefinitionKey ="role_change",
            RequiresSupervisorStep = false
        });

        Assert.False(snapshot.CanPublish);
        Assert.Contains(snapshot.Issues, issue => issue.Code == "measure_flow_process_type_mismatch");
    }

    // FE-9: Spec-Validierung am Write-Pfad. Specs reisen jetzt im DTO mit;
    // Validation muss Eindeutigkeit, Sibling-Dependencies und Node-Type-Compat pruefen.

    [Fact]
    public void ValidateAndNormalize_RejectsSpecsOnNonSpecNodeType()
    {
        var formNodeWithSpecs = new WorkflowDefinitionNodeDto
        {
            NodeKey = "form",
            NodeType = "form",
            SortOrder = 10,
            Config = JsonDocument.Parse("""{"workflowDefinitionKey":"onboarding"}""").RootElement.Clone(),
            Specs = new List<WorkflowDefinitionNodeSpecDto>
            {
                new() { SpecKey = "should_not_be_here", Title = "Spec on form" }
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes = [CreateNode("Start", "start"), formNodeWithSpecs, CreateNode("End", "end", sortOrder: 20)],
            Edges = [CreateEdge("Start", "form", 0), CreateEdge("form", "End", 0)]
        }));

        Assert.Contains("'form' darf keine Specs", ex.Message);
    }

    [Fact]
    public void ValidateAndNormalize_RejectsDuplicateSpecKeyOnSameNode()
    {
        var measureNode = new WorkflowDefinitionNodeDto
        {
            NodeKey = "measure",
            NodeType = "measure_provision",
            SortOrder = 10,
            Specs = new List<WorkflowDefinitionNodeSpecDto>
            {
                new() { SpecKey = "dup", Title = "First" },
                new() { SpecKey = "dup", Title = "Second" },
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Form", "form", sortOrder: 5, configJson: """{"workflowDefinitionKey":"onboarding"}"""),
                measureNode,
                CreateNode("End", "end", sortOrder: 20)
            ],
            Edges = [CreateEdge("Start", "Form", 0), CreateEdge("Form", "measure", 0), CreateEdge("measure", "End", 0)]
        }));

        Assert.Contains("'dup' ist innerhalb des Nodes nicht eindeutig", ex.Message);
    }

    [Fact]
    public void ValidateAndNormalize_RejectsSpecDependencyOnNonSibling()
    {
        var measureNode = new WorkflowDefinitionNodeDto
        {
            NodeKey = "measure",
            NodeType = "measure_provision",
            SortOrder = 10,
            Specs = new List<WorkflowDefinitionNodeSpecDto>
            {
                new()
                {
                    SpecKey = "spec_a",
                    Title = "Spec A",
                    Dependencies = new List<WorkflowDefinitionNodeSpecDependencyDto>
                    {
                        new() { DependsOnSpecKey = "ghost_sibling" }
                    }
                }
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Form", "form", sortOrder: 5, configJson: """{"workflowDefinitionKey":"onboarding"}"""),
                measureNode,
                CreateNode("End", "end", sortOrder: 20)
            ],
            Edges = [CreateEdge("Start", "Form", 0), CreateEdge("Form", "measure", 0), CreateEdge("measure", "End", 0)]
        }));

        Assert.Contains("'ghost_sibling'", ex.Message);
        Assert.Contains("nicht am selben Node", ex.Message);
    }

    [Fact]
    public void ValidateAndNormalize_RejectsSpecSelfDependency()
    {
        var measureNode = new WorkflowDefinitionNodeDto
        {
            NodeKey = "measure",
            NodeType = "measure_provision",
            SortOrder = 10,
            Specs = new List<WorkflowDefinitionNodeSpecDto>
            {
                new()
                {
                    SpecKey = "spec_a",
                    Title = "Spec A",
                    Dependencies = new List<WorkflowDefinitionNodeSpecDependencyDto>
                    {
                        new() { DependsOnSpecKey = "spec_a" }
                    }
                }
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Form", "form", sortOrder: 5, configJson: """{"workflowDefinitionKey":"onboarding"}"""),
                measureNode,
                CreateNode("End", "end", sortOrder: 20)
            ],
            Edges = [CreateEdge("Start", "Form", 0), CreateEdge("Form", "measure", 0), CreateEdge("measure", "End", 0)]
        }));

        Assert.Contains("darf nicht von sich selbst abhaengen", ex.Message);
    }

    [Fact]
    public void ValidateAndNormalize_RejectsTaskNodeWithMultipleSpecs()
    {
        var taskNode = new WorkflowDefinitionNodeDto
        {
            NodeKey = "task1",
            NodeType = "task",
            SortOrder = 10,
            Specs = new List<WorkflowDefinitionNodeSpecDto>
            {
                new() { SpecKey = "first", Title = "First" },
                new() { SpecKey = "second", Title = "Second" },
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                taskNode,
                CreateNode("End", "end", sortOrder: 20)
            ],
            Edges = [CreateEdge("Start", "task1", 0), CreateEdge("task1", "End", 0)]
        }));

        Assert.Contains("'task' darf maximal 1 Spec haben", ex.Message);
    }

    [Fact]
    public void ValidateAndNormalize_AcceptsValidMeasureNodeSpecs()
    {
        var measureNode = new WorkflowDefinitionNodeDto
        {
            NodeKey = "measure",
            NodeType = "measure_provision",
            SortOrder = 10,
            Specs = new List<WorkflowDefinitionNodeSpecDto>
            {
                new()
                {
                    SpecKey = "spec_a",
                    Title = "Spec A",
                    Conditions = new List<WorkflowDefinitionNodeSpecConditionDto>
                    {
                        new() { AnswerKey = "needs_a", Operator = "is_true" }
                    }
                },
                new()
                {
                    SpecKey = "spec_b",
                    Title = "Spec B",
                    Dependencies = new List<WorkflowDefinitionNodeSpecDependencyDto>
                    {
                        new() { DependsOnSpecKey = "spec_a" }
                    }
                },
            }
        };

        var result = _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("Form", "form", sortOrder: 5, configJson: """{"workflowDefinitionKey":"onboarding"}"""),
                measureNode,
                CreateNode("End", "end", sortOrder: 20)
            ],
            Edges = [CreateEdge("Start", "Form", 0), CreateEdge("Form", "measure", 0), CreateEdge("measure", "End", 0)]
        });

        var measureDraft = result.Nodes.Single(n => n.NodeKey == "measure");
        Assert.Equal(2, measureDraft.Specs.Count);
        Assert.Single(measureDraft.Specs.Single(s => s.SpecKey == "spec_a").Conditions);
        Assert.Single(measureDraft.Specs.Single(s => s.SpecKey == "spec_b").Dependencies);
    }

    // Slice 2 (Admin-Gated-Automation, Task-Automation-Binding): task-Nodes
    // duerfen optional Actions tragen, brauchen dann aber einen Role-Slug.
    [Fact]
    public void ValidateAndNormalize_AllowsTaskNodeWithActionsAndRole()
    {
        var result = _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode(
                    "TaskWithPlan",
                    "task",
                    automationAdminRole: "auth_admin",
                    actions: CreateAction("CreateAdUser", 10)),
                CreateNode("End", "end")
            ],
            Edges = [CreateEdge("Start", "TaskWithPlan", 0), CreateEdge("TaskWithPlan", "End", 0)]
        });

        var taskNode = Assert.Single(result.Nodes, node => node.NodeKey == "TaskWithPlan");
        Assert.Equal("task", taskNode.NodeType);
        Assert.Single(taskNode.Actions);
        Assert.Equal("auth_admin", taskNode.AutomationAdminRole);
    }

    [Fact]
    public void ValidateAndNormalize_AllowsTaskNodeWithoutActionsOrRole()
    {
        var result = _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("PlainTask", "task"),
                CreateNode("End", "end")
            ],
            Edges = [CreateEdge("Start", "PlainTask", 0), CreateEdge("PlainTask", "End", 0)]
        });

        var taskNode = Assert.Single(result.Nodes, node => node.NodeKey == "PlainTask");
        Assert.Empty(taskNode.Actions);
        Assert.Null(taskNode.AutomationAdminRole);
    }

    [Fact]
    public void ValidateAndNormalize_RejectsTaskNodeWithActionsButNoRole()
    {
        var request = new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("TaskNoRole", "task", actions: CreateAction("CreateAdUser", 10)),
                CreateNode("End", "end")
            ],
            Edges = [CreateEdge("Start", "TaskNoRole", 0), CreateEdge("TaskNoRole", "End", 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => _sut.ValidateAndNormalize(request));
        Assert.Contains("automationAdminRole", exception.Message);
    }

    [Fact]
    public void ValidateAndNormalize_RejectsTaskNodeWithRoleButNoActions()
    {
        var request = new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("RoleOnly", "task", automationAdminRole: "auth_admin"),
                CreateNode("End", "end")
            ],
            Edges = [CreateEdge("Start", "RoleOnly", 0), CreateEdge("RoleOnly", "End", 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => _sut.ValidateAndNormalize(request));
        Assert.Contains("automationAdminRole", exception.Message);
        Assert.Contains("no actions", exception.Message);
    }

    [Fact]
    public void ValidateAndNormalize_RejectsTaskNodeWithUnsupportedRole()
    {
        var request = new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode(
                    "WorkerTask",
                    "task",
                    automationAdminRole: "auth_worker",
                    actions: CreateAction("CreateAdUser", 10)),
                CreateNode("End", "end")
            ],
            Edges = [CreateEdge("Start", "WorkerTask", 0), CreateEdge("WorkerTask", "End", 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => _sut.ValidateAndNormalize(request));
        Assert.Contains("unsupported automationAdminRole", exception.Message);
    }

    [Fact]
    public void ValidateAndNormalize_RejectsAutomationAdminRoleOnNonActionNode()
    {
        var request = new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode(
                    "FormA",
                    "form",
                    configJson: """{"workflowDefinitionKey":"onboarding"}""",
                    automationAdminRole: "auth_admin"),
                CreateNode("End", "end")
            ],
            Edges = [CreateEdge("Start", "FormA", 0), CreateEdge("FormA", "End", 0)]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => _sut.ValidateAndNormalize(request));
        Assert.Contains("automationAdminRole", exception.Message);
    }

    [Theory]
    [InlineData("AUTH_ADMIN")]
    [InlineData("  Auth_Admin  ")]
    [InlineData("auth_admin")]
    public void ValidateAndNormalize_NormalizesAutomationAdminRoleCasing(string rawRole)
    {
        var result = _sut.ValidateAndNormalize(new ReplaceWorkflowDefinitionVersionRequest
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode(
                    "TaskCasing",
                    "task",
                    automationAdminRole: rawRole,
                    actions: CreateAction("CreateAdUser", 10)),
                CreateNode("End", "end")
            ],
            Edges = [CreateEdge("Start", "TaskCasing", 0), CreateEdge("TaskCasing", "End", 0)]
        });

        var taskNode = Assert.Single(result.Nodes, node => node.NodeKey == "TaskCasing");
        Assert.Equal("auth_admin", taskNode.AutomationAdminRole);
    }

    [Fact]
    public void ValidateSnapshot_TaskWithActionsAndRole_ProducesNoIssues()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode(
                    "TaskSnap",
                    "task",
                    automationAdminRole: "auth_admin",
                    actions: CreateAction("CreateAdUser", 10)),
                CreateNode("End", "end")
            ],
            Edges = [CreateEdge("Start", "TaskSnap", 0), CreateEdge("TaskSnap", "End", 0)]
        });

        Assert.DoesNotContain(snapshot.Issues, i => i.Code == "missing_automation_admin_role_for_task_with_actions");
        Assert.DoesNotContain(snapshot.Issues, i => i.Code == "invalid_automation_admin_role");
        Assert.DoesNotContain(snapshot.Issues, i => i.Code == "task_admin_role_without_actions");
        Assert.DoesNotContain(snapshot.Issues, i => i.Code == "actions_not_allowed");
    }

    [Fact]
    public void ValidateSnapshot_TaskWithActionsWithoutRole_EmitsStructuredIssue()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("TaskNoRole", "task", actions: CreateAction("CreateAdUser", 10)),
                CreateNode("End", "end")
            ],
            Edges = [CreateEdge("Start", "TaskNoRole", 0), CreateEdge("TaskNoRole", "End", 0)]
        });

        Assert.False(snapshot.CanPublish);
        Assert.Contains(
            snapshot.Issues,
            i => i.Code == "missing_automation_admin_role_for_task_with_actions" && i.ReferenceKey == "TaskNoRole");
    }

    [Fact]
    public void ValidateSnapshot_TaskWithUnsupportedRole_EmitsInvalidRoleIssue()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode(
                    "WorkerTask",
                    "task",
                    automationAdminRole: "auth_worker",
                    actions: CreateAction("CreateAdUser", 10)),
                CreateNode("End", "end")
            ],
            Edges = [CreateEdge("Start", "WorkerTask", 0), CreateEdge("WorkerTask", "End", 0)]
        });

        Assert.False(snapshot.CanPublish);
        Assert.Contains(snapshot.Issues, i => i.Code == "invalid_automation_admin_role");
    }

    [Fact]
    public void ValidateSnapshot_TaskWithRoleWithoutActions_EmitsTaskRoleWithoutActionsIssue()
    {
        var snapshot = _sut.ValidateSnapshot(new WorkflowDefinitionValidationContext
        {
            Nodes =
            [
                CreateNode("Start", "start"),
                CreateNode("RoleOnly", "task", automationAdminRole: "auth_admin"),
                CreateNode("End", "end")
            ],
            Edges = [CreateEdge("Start", "RoleOnly", 0), CreateEdge("RoleOnly", "End", 0)]
        });

        Assert.False(snapshot.CanPublish);
        Assert.Contains(snapshot.Issues, i => i.Code == "task_admin_role_without_actions");
    }

    private static WorkflowDefinitionNodeDto CreateNode(
        string nodeKey,
        string nodeType,
        int sortOrder = 0,
        string? configJson = null,
        int? positionX = null,
        int? positionY = null,
        string? automationAdminRole = null,
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
            AutomationAdminRole = automationAdminRole,
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
