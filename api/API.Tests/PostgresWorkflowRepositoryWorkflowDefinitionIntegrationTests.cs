using Npgsql;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class PostgresWorkflowRepositoryWorkflowDefinitionIntegrationTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=26432;Database=appdb;Username=app;Password=app_pw";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateAdminWorkflowDefinition_CreatesInitialDraftAndGeneratedKey()
    {
        var connectionString = GetTestConnectionString();
        if (!await EnsureWorkflowDefinitionLayerAsync(connectionString))
        {
            return;
        }

        WorkflowDefinitionSummaryDto? definition = null;
        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        try
        {
            var repository = new PostgresWorkflowRepository();
            var runtimeRepository = new PostgresWorkflowRuntimeRepository();
            var lifecycleService = new WorkflowLifecycleService(repository, repository, new PostgresWorkflowAuditWriteOperations(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations(), repository, new WorkflowAutomationRetrySettings());
            var uniqueName = $"Builder Smoke {Guid.NewGuid():N}";

            definition = await repository.CreateAdminWorkflowDefinition(new CreateWorkflowDefinitionRequest
            {
                Name = uniqueName,
                Description = "Initial draft should be created automatically"
            });

            Assert.StartsWith("builder_smoke_", definition.Key, StringComparison.Ordinal);
            Assert.Single(definition.Versions);
            Assert.Equal("draft", definition.Versions[0].Status);
            Assert.Equal(1, definition.Versions[0].VersionNumber);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);

            if (definition is not null)
            {
                await CleanupWorkflowDefinitionAsync(connectionString, definition.Id);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WorkflowDefinitionDraft_Roundtrip_CreateReplaceAndReload()
    {
        var connectionString = GetTestConnectionString();
        if (!await EnsureWorkflowDefinitionLayerAsync(connectionString))
        {
            return;
        }

        WorkflowDefinitionSummaryDto? definition = null;
        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        try
        {
            var repository = new PostgresWorkflowRepository();
            var runtimeRepository = new PostgresWorkflowRuntimeRepository();
            var lifecycleService = new WorkflowLifecycleService(repository, repository, new PostgresWorkflowAuditWriteOperations(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations(), repository, new WorkflowAutomationRetrySettings());

            definition = await repository.CreateAdminWorkflowDefinition(new CreateWorkflowDefinitionRequest
            {
                Key = $"Definition_{Guid.NewGuid():N}",
                Name = "Employee Lifecycle",
                Description = "Draft-only integration test definition"
            });

            var createdVersion = await repository.CreateAdminWorkflowDefinitionVersion(
                definition.Id,
                new CreateWorkflowDefinitionVersionRequest
                {
                    Name = "Initial Draft",
                    Description = "Roundtrip version"
                });

            Assert.NotNull(createdVersion);

            var updatedVersion = await repository.ReplaceAdminWorkflowDefinitionVersion(
                createdVersion!.Id,
                new ReplaceWorkflowDefinitionVersionRequest
                {
                    Name = "Initial Draft Updated",
                    Description = "Roundtrip graph",
                    Nodes =
                    [
                        WorkflowDefinitionTestData.FormNode("Start", "start"),
                        WorkflowDefinitionTestData.FormNode("Collect_Data", "form", """{"workflowDefinitionKey":"onboarding"}""", 10, 160, 80),
                        WorkflowDefinitionTestData.FormNode("Approve_Manager", "approval", null, 20, 480, 80),
                        WorkflowDefinitionTestData.FormNode("Finish", "end", null, 30, 820, 80)
                    ],
                    Edges =
                    [
                        WorkflowDefinitionTestData.Edge("Start", "Collect_Data", 0),
                        WorkflowDefinitionTestData.Edge("Collect_Data", "Approve_Manager", 0),
                        WorkflowDefinitionTestData.Edge("Approve_Manager", "Finish", 0)
                    ]
                });

            Assert.NotNull(updatedVersion);
            Assert.Equal(definition.Key.ToLowerInvariant(), definition.Key);
            Assert.Equal(4, updatedVersion!.Nodes.Count);
            Assert.Equal(3, updatedVersion.Edges.Count);
            Assert.Contains(updatedVersion.Nodes, node =>
                node.NodeKey == "collect_data"
                && node.Config.HasValue
                && node.PositionX == 160
                && node.PositionY == 80);

            var reloadedVersion = await repository.GetAdminWorkflowDefinitionVersion(createdVersion.Id);

            Assert.NotNull(reloadedVersion);
            Assert.Equal(updatedVersion.Id, reloadedVersion!.Id);
            Assert.Equal("Initial Draft Updated", reloadedVersion.Name);
            Assert.Equal(definition.Key, reloadedVersion.DefinitionKey);
            Assert.Equal("Employee Lifecycle", reloadedVersion.DefinitionName);
            Assert.Contains(reloadedVersion.Nodes, node =>
                node.NodeType == "approval"
                && node.PositionX == 480
                && node.PositionY == 80);
            Assert.Contains(reloadedVersion.Edges, edge => edge.SourceNodeKey == "approve_manager" && edge.TargetNodeKey == "finish");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);

            if (definition is not null)
            {
                await CleanupWorkflowDefinitionAsync(connectionString, definition.Id);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeededWorkflowMappings_ArePublishedValidAndRuntimeStartable()
    {
        var connectionString = GetTestConnectionString();
        if (!await EnsureWorkflowDefinitionMappingsAsync(connectionString))
        {
            return;
        }

        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        SeededRuntimeTargetPerson? targetPerson = null;
        WorkflowDefinitionRuntimeDetailDto? onboardingRuntime = null;
        WorkflowDefinitionRuntimeDetailDto? offboardingRuntime = null;
        WorkflowDefinitionRuntimeDetailDto? departmentChangeRuntime = null;
        WorkflowDefinitionRuntimeDetailDto? nameChangeRuntime = null;
        WorkflowDefinitionRuntimeDetailDto? positionChangeRuntime = null;
        WorkflowDefinitionRuntimeDetailDto? roleChangeRuntime = null;

        try
        {
            var repository = new PostgresWorkflowRepository();
            var runtimeRepository = new PostgresWorkflowRuntimeRepository();
            var lifecycleService = new WorkflowLifecycleService(repository, repository, new PostgresWorkflowAuditWriteOperations(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations(), repository, new WorkflowAutomationRetrySettings());

            var definitionsPage = await repository.GetAdminWorkflowDefinitions(new AdminListQuery { Limit = AdminListQuery.MaxLimit });
            var definitions = definitionsPage.Items;
            foreach (var definitionKey in new[] { "onboarding", "offboarding", "department_change", "name_change", "position_change", "role_change" })
            {
                var definition = definitions.SingleOrDefault(item =>
                    string.Equals(item.Key, definitionKey, StringComparison.OrdinalIgnoreCase));

                Assert.NotNull(definition);
                var publishedVersion = definition!.Versions.SingleOrDefault(version =>
                    string.Equals(version.Status, "published", StringComparison.OrdinalIgnoreCase));

                Assert.NotNull(publishedVersion);
                Assert.True(publishedVersion!.CanPublish);
                Assert.Empty(publishedVersion.ValidationIssues);
            }

            targetPerson = await CreateSeededRuntimeTargetPersonAsync(connectionString);

            onboardingRuntime = await lifecycleService.CreateWorkflowInstanceAsync(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = "onboarding",
                    TargetPersonId = targetPerson.PersonId,
                    DepartmentId = targetPerson.DepartmentId,
                    RoleId = targetPerson.RoleId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = targetPerson.EmployeeNumber + 1000,
                    BadgeNumber = targetPerson.BadgeNumber + 1000
                },
                targetPerson.ActorUserId);

            offboardingRuntime = await lifecycleService.CreateWorkflowInstanceAsync(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = "offboarding",
                    TargetPersonId = targetPerson.PersonId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = targetPerson.EmployeeNumber,
                    BadgeNumber = targetPerson.BadgeNumber
                },
                targetPerson.ActorUserId);

            departmentChangeRuntime = await lifecycleService.CreateWorkflowInstanceAsync(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = "department_change",
                    TargetPersonId = targetPerson.PersonId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = targetPerson.EmployeeNumber,
                    BadgeNumber = targetPerson.BadgeNumber
                },
                targetPerson.ActorUserId);

            nameChangeRuntime = await lifecycleService.CreateWorkflowInstanceAsync(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = "name_change",
                    TargetPersonId = targetPerson.PersonId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = targetPerson.EmployeeNumber,
                    BadgeNumber = targetPerson.BadgeNumber
                },
                targetPerson.ActorUserId);

            positionChangeRuntime = await lifecycleService.CreateWorkflowInstanceAsync(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = "position_change",
                    TargetPersonId = targetPerson.PersonId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = targetPerson.EmployeeNumber,
                    BadgeNumber = targetPerson.BadgeNumber
                },
                targetPerson.ActorUserId);

            roleChangeRuntime = await lifecycleService.CreateWorkflowInstanceAsync(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = "role_change",
                    TargetPersonId = targetPerson.PersonId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = targetPerson.EmployeeNumber,
                    BadgeNumber = targetPerson.BadgeNumber
                },
                targetPerson.ActorUserId);

            Assert.Equal("onboarding", onboardingRuntime.WorkflowDefinitionKey);
            Assert.Equal("waiting_on_node", onboardingRuntime.CurrentRuntimeStatus);
            Assert.Contains(onboardingRuntime.NodeInstances, node => node.NodeKey == "collect_requirements" && node.Status == "active");
            Assert.DoesNotContain(onboardingRuntime.NodeInstances, node => node.NodeType == "task" && node.Status == "active");
            Assert.Equal("offboarding", offboardingRuntime.WorkflowDefinitionKey);
            Assert.Equal("waiting_on_node", offboardingRuntime.CurrentRuntimeStatus);
            Assert.Contains(offboardingRuntime.NodeInstances, node => node.NodeKey == "collect_requirements" && node.Status == "active");
            Assert.DoesNotContain(offboardingRuntime.NodeInstances, node => node.NodeType == "task" && node.Status == "active");
            Assert.Equal("department_change", departmentChangeRuntime.WorkflowDefinitionKey);
            Assert.Equal("waiting_on_node", departmentChangeRuntime.CurrentRuntimeStatus);
            Assert.Contains(departmentChangeRuntime.NodeInstances, node => node.NodeKey == "collect_requirements" && node.Status == "active");
            Assert.DoesNotContain(departmentChangeRuntime.NodeInstances, node => node.NodeType == "task" && node.Status == "active");
            Assert.Equal("name_change", nameChangeRuntime.WorkflowDefinitionKey);
            Assert.Equal("waiting_on_node", nameChangeRuntime.CurrentRuntimeStatus);
            Assert.Contains(nameChangeRuntime.NodeInstances, node => node.NodeKey == "collect_requirements" && node.Status == "active");
            Assert.DoesNotContain(nameChangeRuntime.NodeInstances, node => node.NodeType == "task" && node.Status == "active");
            Assert.Equal("position_change", positionChangeRuntime.WorkflowDefinitionKey);
            Assert.Equal("waiting_on_node", positionChangeRuntime.CurrentRuntimeStatus);
            Assert.Contains(positionChangeRuntime.NodeInstances, node => node.NodeKey == "collect_requirements" && node.Status == "active");
            Assert.DoesNotContain(positionChangeRuntime.NodeInstances, node => node.NodeType == "task" && node.Status == "active");
            Assert.Equal("role_change", roleChangeRuntime.WorkflowDefinitionKey);
            Assert.Equal("waiting_on_node", roleChangeRuntime.CurrentRuntimeStatus);
            Assert.Contains(roleChangeRuntime.NodeInstances, node => node.NodeKey == "collect_requirements" && node.Status == "active");
            Assert.DoesNotContain(roleChangeRuntime.NodeInstances, node => node.NodeType == "task" && node.Status == "active");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);

            if (onboardingRuntime is not null)
            {
                await CleanupWorkflowAsync(connectionString, onboardingRuntime.WorkflowId);
            }

            if (offboardingRuntime is not null)
            {
                await CleanupWorkflowAsync(connectionString, offboardingRuntime.WorkflowId);
            }

            if (departmentChangeRuntime is not null)
            {
                await CleanupWorkflowAsync(connectionString, departmentChangeRuntime.WorkflowId);
            }

            if (nameChangeRuntime is not null)
            {
                await CleanupWorkflowAsync(connectionString, nameChangeRuntime.WorkflowId);
            }

            if (positionChangeRuntime is not null)
            {
                await CleanupWorkflowAsync(connectionString, positionChangeRuntime.WorkflowId);
            }

            if (roleChangeRuntime is not null)
            {
                await CleanupWorkflowAsync(connectionString, roleChangeRuntime.WorkflowId);
            }

            if (targetPerson is not null)
            {
                await CleanupSeededRuntimeTargetPersonAsync(connectionString, targetPerson);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EnsureAdminWorkflowDefinitionWorkingDraft_ClonesLatestPublishedVersion()
    {
        var connectionString = GetTestConnectionString();
        if (!await EnsureWorkflowDefinitionMappingsAsync(connectionString))
        {
            return;
        }

        WorkflowDefinitionSummaryDto? definition = null;
        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        try
        {
            var repository = new PostgresWorkflowRepository();
            var runtimeRepository = new PostgresWorkflowRuntimeRepository();
            var lifecycleService = new WorkflowLifecycleService(repository, repository, new PostgresWorkflowAuditWriteOperations(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations(), repository, new WorkflowAutomationRetrySettings());

            definition = await repository.CreateAdminWorkflowDefinition(new CreateWorkflowDefinitionRequest
            {
                Name = $"Working Draft Clone {Guid.NewGuid():N}",
                Description = "Working draft clone integration test"
            });

            var initialVersionId = definition.Versions.Single().Id;
            var replacedVersion = await repository.ReplaceAdminWorkflowDefinitionVersion(
                initialVersionId,
                new ReplaceWorkflowDefinitionVersionRequest
                {
                    Name = "Published baseline",
                    Description = "Baseline graph",
                    Nodes =
                    [
                        WorkflowDefinitionTestData.FormNode("Start", "start"),
                        WorkflowDefinitionTestData.FormNode("Collect_Data", "form", """{"workflowDefinitionKey":"offboarding"}""", 10, 160, 80),
                        WorkflowDefinitionTestData.FormNode("Finish", "end", null, 20, 460, 80)
                    ],
                    Edges =
                    [
                        WorkflowDefinitionTestData.Edge("Start", "Collect_Data", 0),
                        WorkflowDefinitionTestData.Edge("Collect_Data", "Finish", 0)
                    ]
                });

            Assert.NotNull(replacedVersion);
            var publishedVersion = await runtimeRepository.PublishWorkflowDefinitionVersion(initialVersionId);
            Assert.NotNull(publishedVersion);

            var workingDraft = await repository.EnsureAdminWorkflowDefinitionWorkingDraft(definition.Id);

            Assert.NotNull(workingDraft);
            Assert.Equal("draft", workingDraft!.Status);
            Assert.Equal(2, workingDraft.VersionNumber);
            Assert.Equal(3, workingDraft.Nodes.Count);
            Assert.Equal(2, workingDraft.Edges.Count);
            Assert.Contains(workingDraft.Nodes, node => node.NodeKey == "collect_data" && node.PositionX == 160 && node.PositionY == 80);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);

            if (definition is not null)
            {
                await CleanupWorkflowDefinitionAsync(connectionString, definition.Id);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EnsureAdminWorkflowDefinitionWorkingDraft_ClonesSpecsFromPublishedMeasureNode()
    {
        // FE-9 Regression: Specs hingen frueher am workflow_node_id der published Version.
        // Bei EnsureAdminWorkflowDefinitionWorkingDraft wurden Nodes geklont, Specs aber nicht.
        // Nach FE-9 reisen Specs in der Version-DTO mit, weshalb die Klon-Methode (ToDraftNode)
        // sie automatisch ueberfuehrt. Test: published Version mit measure-Node + Specs anlegen,
        // working draft erzeugen, asserten dass Specs am neuen measure-Node landen.
        var connectionString = GetTestConnectionString();
        if (!await EnsureWorkflowDefinitionMappingsAsync(connectionString))
        {
            return;
        }

        WorkflowDefinitionSummaryDto? definition = null;
        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        try
        {
            var repository = new PostgresWorkflowRepository();
            var runtimeRepository = new PostgresWorkflowRuntimeRepository();
            var lifecycleService = new WorkflowLifecycleService(repository, repository, new PostgresWorkflowAuditWriteOperations(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations(), repository, new WorkflowAutomationRetrySettings());

            definition = await repository.CreateAdminWorkflowDefinition(new CreateWorkflowDefinitionRequest
            {
                Name = $"Spec Carry Over {Guid.NewGuid():N}",
                Description = "FE-9 spec carry-over integration test"
            });

            var initialVersionId = definition.Versions.Single().Id;

            var measureNode = new WorkflowDefinitionNodeDto
            {
                NodeKey = "department_setup",
                NodeType = "measure_provision",
                Title = "Massnahmen",
                SortOrder = 20,
                Specs = new List<WorkflowDefinitionNodeSpecDto>
                {
                    new()
                    {
                        SpecKey = "spec_a",
                        Title = "Spec A — Account anlegen",
                        Category = "general",
                        Description = "AD-Account fuer den neuen Mitarbeiter",
                        IsDepartmentPhaseTask = true,
                        IsRequired = true,
                        DueInDays = 3,
                        SortOrder = 0,
                        Conditions = new List<WorkflowDefinitionNodeSpecConditionDto>
                        {
                            new()
                            {
                                AnswerKey = "needs_account",
                                Operator = "is_true",
                            }
                        },
                        Dependencies = new List<WorkflowDefinitionNodeSpecDependencyDto>(),
                    },
                    new()
                    {
                        SpecKey = "spec_b",
                        Title = "Spec B — Schulung buchen",
                        Category = "general",
                        Description = "Onboarding-Schulung",
                        IsDepartmentPhaseTask = true,
                        IsRequired = false,
                        SortOrder = 10,
                        Conditions = new List<WorkflowDefinitionNodeSpecConditionDto>(),
                        Dependencies = new List<WorkflowDefinitionNodeSpecDependencyDto>
                        {
                            new() { DependsOnSpecKey = "spec_a" },
                        },
                    },
                }
            };

            var replacedVersion = await repository.ReplaceAdminWorkflowDefinitionVersion(
                initialVersionId,
                new ReplaceWorkflowDefinitionVersionRequest
                {
                    Name = "Published baseline",
                    Description = "Baseline graph",
                    Nodes =
                    [
                        WorkflowDefinitionTestData.FormNode("start", "start"),
                        WorkflowDefinitionTestData.FormNode("collect_requirements", "form", """{"workflowDefinitionKey":"onboarding"}""", 10),
                        measureNode,
                        WorkflowDefinitionTestData.FormNode("end", "end", null, 30)
                    ],
                    Edges =
                    [
                        WorkflowDefinitionTestData.Edge("start", "collect_requirements", 0),
                        WorkflowDefinitionTestData.Edge("collect_requirements", "department_setup", 0),
                        WorkflowDefinitionTestData.Edge("department_setup", "end", 0),
                    ]
                });

            Assert.NotNull(replacedVersion);
            var publishedMeasureNode = replacedVersion!.Nodes.Single(n => n.NodeKey == "department_setup");
            Assert.Equal(2, publishedMeasureNode.Specs.Count);
            Assert.Contains(publishedMeasureNode.Specs, s => s.SpecKey == "spec_a" && s.Conditions.Count == 1);
            Assert.Contains(publishedMeasureNode.Specs, s => s.SpecKey == "spec_b" && s.Dependencies.Count == 1 && s.Dependencies[0].DependsOnSpecKey == "spec_a");

            var publishedVersion = await runtimeRepository.PublishWorkflowDefinitionVersion(initialVersionId);
            Assert.NotNull(publishedVersion);

            var workingDraft = await repository.EnsureAdminWorkflowDefinitionWorkingDraft(definition.Id);

            Assert.NotNull(workingDraft);
            Assert.Equal("draft", workingDraft!.Status);
            Assert.Equal(2, workingDraft.VersionNumber);

            var draftMeasureNode = workingDraft.Nodes.Single(n => n.NodeKey == "department_setup");
            Assert.Equal(2, draftMeasureNode.Specs.Count);

            var draftSpecA = draftMeasureNode.Specs.Single(s => s.SpecKey == "spec_a");
            Assert.Equal("Spec A — Account anlegen", draftSpecA.Title);
            Assert.True(draftSpecA.IsRequired);
            Assert.Equal(3, draftSpecA.DueInDays);
            Assert.Single(draftSpecA.Conditions);
            Assert.Equal("needs_account", draftSpecA.Conditions[0].AnswerKey);
            Assert.Equal("is_true", draftSpecA.Conditions[0].Operator);
            Assert.Empty(draftSpecA.Dependencies);

            var draftSpecB = draftMeasureNode.Specs.Single(s => s.SpecKey == "spec_b");
            Assert.False(draftSpecB.IsRequired);
            Assert.Single(draftSpecB.Dependencies);
            Assert.Equal("spec_a", draftSpecB.Dependencies[0].DependsOnSpecKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);

            if (definition is not null)
            {
                await CleanupWorkflowDefinitionAsync(connectionString, definition.Id);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DefinitionRuntime_SupervisorStepCompletesGatekeeperBeforeActivatingTasks()
    {
        var connectionString = GetTestConnectionString();
        if (!await EnsureWorkflowDefinitionMappingsAsync(connectionString))
        {
            return;
        }

        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        SeededRuntimeTargetPerson? targetPerson = null;
        WorkflowDefinitionRuntimeDetailDto? onboardingRuntime = null;

        try
        {
            var repository = new PostgresWorkflowRepository();
            var runtimeRepository = new PostgresWorkflowRuntimeRepository();
            var lifecycleService = new WorkflowLifecycleService(repository, repository, new PostgresWorkflowAuditWriteOperations(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations(), repository, new WorkflowAutomationRetrySettings());
            targetPerson = await CreateSeededRuntimeTargetPersonAsync(connectionString);

            onboardingRuntime = await lifecycleService.CreateWorkflowInstanceAsync(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = "onboarding",
                    TargetPersonId = targetPerson.PersonId,
                    DepartmentId = targetPerson.DepartmentId,
                    RoleId = targetPerson.RoleId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = targetPerson.EmployeeNumber + 3000,
                    BadgeNumber = targetPerson.BadgeNumber + 3000
                },
                targetPerson.ActorUserId);

            Assert.Equal("waiting_on_node", onboardingRuntime.CurrentRuntimeStatus);
            Assert.Contains(onboardingRuntime.NodeInstances, node => node.NodeKey == "collect_requirements" && node.Status == "active");
            Assert.DoesNotContain(onboardingRuntime.NodeInstances, node => node.NodeType == "task" && node.Status == "active");

            var updatedWorkflow = await repository.CompleteSupervisorStep(
                onboardingRuntime.WorkflowUid,
                await BuildPositiveSupervisorSelectionsAsync(connectionString),
                targetPerson.ActorUserId);

            Assert.NotNull(updatedWorkflow);
            Assert.Equal("waiting_for_department", updatedWorkflow!.WorkflowStatus);
            Assert.Contains(updatedWorkflow.Requirements, requirement =>
                requirement.Value.ValueBoolean.HasValue
                || requirement.Value.SelectedOptionId.HasValue);
            Assert.NotEmpty(updatedWorkflow.Tasks);
            Assert.Contains(updatedWorkflow.Tasks, task => task.Status == "ready");

            var runtimeDetail = await runtimeRepository.GetWorkflowDefinitionRuntimeDetail(onboardingRuntime.WorkflowUid);

            Assert.NotNull(runtimeDetail);
            Assert.Equal("waiting_on_node", runtimeDetail!.CurrentRuntimeStatus);
            Assert.Contains(runtimeDetail.NodeInstances, node => node.NodeKey == "collect_requirements" && node.Status == "done");
            Assert.Contains(runtimeDetail.NodeInstances, node =>
                node.NodeKey == "department_setup"
                && node.NodeType == "measure_provision"
                && node.Status == "active");
            Assert.DoesNotContain(runtimeDetail.NodeInstances, node => node.NodeType == "task" && node.Status == "active");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);

            if (onboardingRuntime is not null)
            {
                await CleanupWorkflowAsync(connectionString, onboardingRuntime.WorkflowId);
            }

            if (targetPerson is not null)
            {
                await CleanupSeededRuntimeTargetPersonAsync(connectionString, targetPerson);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DefinitionRuntime_MeasurePhaseCompletesAfterRequiredGeneratedTasksAreDone()
    {
        var connectionString = GetTestConnectionString();
        if (!await EnsureWorkflowDefinitionMappingsAsync(connectionString))
        {
            return;
        }

        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        SeededRuntimeTargetPerson? targetPerson = null;
        WorkflowDefinitionRuntimeDetailDto? onboardingRuntime = null;

        try
        {
            var repository = new PostgresWorkflowRepository();
            var runtimeRepository = new PostgresWorkflowRuntimeRepository();
            var lifecycleService = new WorkflowLifecycleService(repository, repository, new PostgresWorkflowAuditWriteOperations(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations(), repository, new WorkflowAutomationRetrySettings());
            targetPerson = await CreateSeededRuntimeTargetPersonAsync(connectionString);

            onboardingRuntime = await lifecycleService.CreateWorkflowInstanceAsync(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = "onboarding",
                    TargetPersonId = targetPerson.PersonId,
                    DepartmentId = targetPerson.DepartmentId,
                    RoleId = targetPerson.RoleId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = targetPerson.EmployeeNumber + 4000,
                    BadgeNumber = targetPerson.BadgeNumber + 4000
                },
                targetPerson.ActorUserId);

            var updatedWorkflow = await repository.CompleteSupervisorStep(
                onboardingRuntime.WorkflowUid,
                await BuildPositiveSupervisorSelectionsAsync(connectionString),
                targetPerson.ActorUserId);

            Assert.NotNull(updatedWorkflow);
            Assert.NotEmpty(updatedWorkflow!.Tasks);

            foreach (var task in updatedWorkflow.Tasks.Where(task => task.IsRequired))
            {
                var updateResult = await lifecycleService.UpdateTaskStatusAsync(task.Id, "done", targetPerson.ActorUserId);
                Assert.NotNull(updateResult);
            }

            var workflowDetail = await repository.GetWorkflowByUid(onboardingRuntime.WorkflowUid);
            var runtimeDetail = await runtimeRepository.GetWorkflowDefinitionRuntimeDetail(onboardingRuntime.WorkflowUid);

            Assert.NotNull(workflowDetail);
            Assert.NotNull(runtimeDetail);
            Assert.Equal("completed", workflowDetail!.WorkflowStatus);
            Assert.Equal("completed", runtimeDetail!.CurrentRuntimeStatus);
            Assert.Contains(runtimeDetail.NodeInstances, node =>
                node.NodeKey == "department_setup"
                && node.NodeType == "measure_provision"
                && node.Status == "done");
            Assert.Contains(runtimeDetail.NodeInstances, node => node.NodeKey == "end" && node.Status == "done");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);

            if (onboardingRuntime is not null)
            {
                await CleanupWorkflowAsync(connectionString, onboardingRuntime.WorkflowId);
            }

            if (targetPerson is not null)
            {
                await CleanupSeededRuntimeTargetPersonAsync(connectionString, targetPerson);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Runtime_WaitsForParallelJoinUntilAllBranchesAreDone()
    {
        var connectionString = GetTestConnectionString();
        if (!await EnsureWorkflowDefinitionMappingsAsync(connectionString))
        {
            return;
        }

        WorkflowDefinitionSummaryDto? definition = null;
        WorkflowDefinitionRuntimeDetailDto? runtime = null;
        SeededRuntimeTargetPerson? targetPerson = null;
        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        try
        {
            var repository = new PostgresWorkflowRepository();
            var runtimeRepository = new PostgresWorkflowRuntimeRepository();
            var lifecycleService = new WorkflowLifecycleService(repository, repository, new PostgresWorkflowAuditWriteOperations(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations(), repository, new WorkflowAutomationRetrySettings());
            targetPerson = await CreateSeededRuntimeTargetPersonAsync(connectionString);

            definition = await repository.CreateAdminWorkflowDefinition(new CreateWorkflowDefinitionRequest
            {
                Key = $"parallel_join_{Guid.NewGuid():N}",
                Name = "Parallel Join Runtime",
                Description = "Runtime integration test for parallel split and join"
            });

            var createdVersion = await repository.CreateAdminWorkflowDefinitionVersion(
                definition.Id,
                new CreateWorkflowDefinitionVersionRequest
                {
                    Name = "Parallel Draft",
                    Description = "Parallel runtime draft"
                });

            var replacedVersion = await repository.ReplaceAdminWorkflowDefinitionVersion(
                createdVersion!.Id,
                new ReplaceWorkflowDefinitionVersionRequest
                {
                    Name = "Parallel Draft",
                    Description = "Parallel runtime draft",
                    Nodes =
                    [
                        WorkflowDefinitionTestData.FormNode("start", "start"),
                        WorkflowDefinitionTestData.FormNode("split", "parallel_split", null, 10),
                        WorkflowDefinitionTestData.FormNode("task_a", "task", null, 20),
                        WorkflowDefinitionTestData.FormNode("task_b", "task", null, 30),
                        WorkflowDefinitionTestData.FormNode("join", "parallel_join", null, 40),
                        WorkflowDefinitionTestData.FormNode("end", "end", null, 50)
                    ],
                    Edges =
                    [
                        WorkflowDefinitionTestData.Edge("start", "split", 0),
                        WorkflowDefinitionTestData.Edge("split", "task_a", 0),
                        WorkflowDefinitionTestData.Edge("split", "task_b", 1),
                        WorkflowDefinitionTestData.Edge("task_a", "join", 0),
                        WorkflowDefinitionTestData.Edge("task_b", "join", 0),
                        WorkflowDefinitionTestData.Edge("join", "end", 0)
                    ]
                });

            Assert.NotNull(replacedVersion);

            var publishedVersion = await runtimeRepository.PublishWorkflowDefinitionVersion(createdVersion.Id);
            Assert.NotNull(publishedVersion);
            Assert.True(publishedVersion!.CanPublish);

            // LA5: task/approval-Nodes brauchen je 1 Eintrag in workflow_node_task_specs.
            await SeedTaskNodeSpecsAsync(connectionString, createdVersion.Id, new[] { "task_a", "task_b" });

            runtime = await lifecycleService.CreateWorkflowInstanceAsync(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = definition.Key,
                    TargetPersonId = targetPerson.PersonId,
                    DepartmentId = targetPerson.DepartmentId,
                    RoleId = targetPerson.RoleId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = targetPerson.EmployeeNumber + 2000,
                    BadgeNumber = targetPerson.BadgeNumber + 2000
                },
                targetPerson.ActorUserId);

            Assert.Equal("waiting_on_node", runtime.CurrentRuntimeStatus);
            Assert.Contains(runtime.NodeInstances, node => node.NodeKey == "task_a" && node.Status == "active");
            Assert.Contains(runtime.NodeInstances, node => node.NodeKey == "task_b" && node.Status == "active");
            Assert.DoesNotContain(runtime.NodeInstances, node => node.NodeKey == "join");

            var taskANodeInstanceId = runtime.NodeInstances.Single(node => node.NodeKey == "task_a").Id;
            runtime = await lifecycleService.CompleteTaskNodeAsync(
                runtime.WorkflowUid,
                taskANodeInstanceId,
                new CompleteRuntimeTaskNodeRequest
                {
                    Comment = "Task A completed"
                },
                targetPerson.ActorUserId);

            Assert.NotNull(runtime);
            Assert.Equal("waiting_on_node", runtime!.CurrentRuntimeStatus);
            Assert.DoesNotContain(runtime.NodeInstances, node => node.NodeKey == "join");
            Assert.Contains(runtime.NodeInstances, node => node.NodeKey == "task_b" && node.Status == "active");

            var taskBNodeInstanceId = runtime.NodeInstances.Single(node => node.NodeKey == "task_b").Id;
            runtime = await lifecycleService.CompleteTaskNodeAsync(
                runtime.WorkflowUid,
                taskBNodeInstanceId,
                new CompleteRuntimeTaskNodeRequest
                {
                    Comment = "Task B completed"
                },
                targetPerson.ActorUserId);

            Assert.NotNull(runtime);
            Assert.Equal("completed", runtime!.CurrentRuntimeStatus);
            Assert.Contains(runtime.NodeInstances, node => node.NodeKey == "join" && node.Status == "done");
            Assert.Contains(runtime.NodeInstances, node => node.NodeKey == "end" && node.Status == "done");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);

            if (runtime is not null)
            {
                await CleanupWorkflowAsync(connectionString, runtime.WorkflowId);
            }

            if (definition is not null)
            {
                await CleanupWorkflowDefinitionAsync(connectionString, definition.Id);
            }

            if (targetPerson is not null)
            {
                await CleanupSeededRuntimeTargetPersonAsync(connectionString, targetPerson);
            }
        }
    }

    private static string GetTestConnectionString()
    {
        return Environment.GetEnvironmentVariable("ONBOARDING_TEST_CONNECTION_STRING")
               ?? DefaultTestConnectionString;
    }

    private static async Task<bool> EnsureWorkflowDefinitionLayerAsync(string connectionString)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(
                """
                SELECT to_regclass('public.workflow_definitions') IS NOT NULL
                   AND to_regclass('public.workflow_definition_versions') IS NOT NULL
                   AND to_regclass('public.workflow_nodes') IS NOT NULL
                   AND to_regclass('public.workflow_edges') IS NOT NULL;
                """,
                connection);
            return (bool?)await command.ExecuteScalarAsync() == true;
        }
        catch (NpgsqlException ex)
        {
            _ = ex;
            return false;
        }
    }

    private static async Task<bool> EnsureWorkflowDefinitionMappingsAsync(string connectionString)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM workflow_definitions
                    WHERE definition_key IN ('onboarding', 'offboarding', 'department_change')
                ) AND EXISTS (
                    SELECT 1
                    FROM workflow_definition_versions
                    WHERE status = 'published'
                );
                """,
                connection);
            return (bool?)await command.ExecuteScalarAsync() == true;
        }
        catch (NpgsqlException ex)
        {
            _ = ex;
            return false;
        }
    }

    private static async Task CleanupWorkflowDefinitionAsync(string connectionString, int definitionId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            DELETE FROM workflow_definitions
            WHERE id = @definitionId;
            """,
            connection);
        command.Parameters.AddWithValue("definitionId", definitionId);
        await command.ExecuteNonQueryAsync();
    }

    // LA5: Hilfsfunktion zum nachtraeglichen Anlegen von Task-Specs fuer task/approval-Nodes,
    // falls der Test diese Nodes via ReplaceAdminWorkflowDefinitionVersion erzeugt hat.
    private static async Task SeedTaskNodeSpecsAsync(string connectionString, long versionId, string[] nodeKeys)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        // default_responsibility_id muss gesetzt sein, damit die Runtime einen Assignee aufloesen kann.
        // 12 = 'app_admin' aus dem Seed.
        const string sql = """
            INSERT INTO workflow_node_task_specs (
                workflow_node_id, spec_key, title, description, category, icon_key,
                default_responsibility_id, is_department_phase_task, is_required, due_in_days, sort_order
            )
            SELECT
                n.id, n.node_key, n.node_key, '', 'general', 'berechtigungen',
                12, FALSE, TRUE, 1, 0
            FROM workflow_nodes n
            WHERE n.workflow_definition_version_id = @versionId
              AND n.node_key = ANY(@nodeKeys)
              AND n.node_type IN ('task', 'approval')
            ON CONFLICT DO NOTHING;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("versionId", versionId);
        command.Parameters.AddWithValue("nodeKeys", nodeKeys);
        var inserted = await command.ExecuteNonQueryAsync();
        if (inserted != nodeKeys.Length)
        {
            throw new InvalidOperationException(
                $"SeedTaskNodeSpecsAsync inserted {inserted} rows for version {versionId}, expected {nodeKeys.Length}.");
        }
    }

    private static async Task CleanupWorkflowAsync(string connectionString, long workflowId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            DELETE FROM workflows
            WHERE id = @workflowId;
            """,
            connection);
        command.Parameters.AddWithValue("workflowId", workflowId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<SeededRuntimeTargetPerson> CreateSeededRuntimeTargetPersonAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        int departmentId;
        int roleId;
        int onboardingProcessTypeId;
        await using (var command = new NpgsqlCommand(
                         """
                         SELECT d.id, r.id, wd.id
                         FROM app_roles r
                         JOIN departments d ON d.id = r.department_id
                         JOIN workflow_definitions wd ON wd.definition_key = 'onboarding'
                         WHERE r.role_kind = 'position'
                           AND r.is_active = TRUE
                         ORDER BY r.id
                         LIMIT 1;
                         """,
                         connection,
                         transaction))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("No active seeded position role was found for runtime smoke tests.");
            }

            departmentId = reader.GetInt32(0);
            roleId = reader.GetInt32(1);
            onboardingProcessTypeId = reader.GetInt32(2);
        }

        var suffix = Guid.NewGuid().ToString("N")[..12];
        long actorUserId;
        long targetUserId;
        long personId;
        var employeeNumber = 800000 + Random.Shared.Next(1000, 9999);
        var badgeNumber = 900000 + Random.Shared.Next(1000, 9999);

        await using (var command = new NpgsqlCommand(
                         """
                         INSERT INTO app_users (department_id, display_name, email, is_active)
                         VALUES (@departmentId, @displayName, @email, TRUE)
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("departmentId", departmentId);
            command.Parameters.AddWithValue("displayName", $"Runtime Actor {suffix}");
            command.Parameters.AddWithValue("email", $"runtime.actor.{suffix}@example.test");
            actorUserId = (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Actor user could not be created."));
        }

        await using (var command = new NpgsqlCommand(
                         """
                         INSERT INTO app_users (department_id, display_name, email, is_active)
                         VALUES (@departmentId, @displayName, @email, TRUE)
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("departmentId", departmentId);
            command.Parameters.AddWithValue("displayName", $"Runtime Target {suffix}");
            command.Parameters.AddWithValue("email", $"runtime.target.{suffix}@example.test");
            targetUserId = (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Target user could not be created."));
        }

        await using (var command = new NpgsqlCommand(
                         """
                         INSERT INTO people (
                             app_user_id,
                             department_id,
                             current_position_role_id,
                             first_name,
                             last_name,
                             employee_number,
                             badge_number,
                             employment_status,
                             entry_date,
                             updated_at
                         )
                         VALUES (
                             @appUserId,
                             @departmentId,
                             @roleId,
                             'Target',
                             'Person',
                             @employeeNumber,
                             @badgeNumber,
                             'active',
                             CURRENT_DATE,
                             NOW()
                         )
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("appUserId", targetUserId);
            command.Parameters.AddWithValue("departmentId", departmentId);
            command.Parameters.AddWithValue("roleId", roleId);
            command.Parameters.AddWithValue("employeeNumber", employeeNumber);
            command.Parameters.AddWithValue("badgeNumber", badgeNumber);
            personId = (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Target person could not be created."));
        }

        await using (var command = new NpgsqlCommand(
                         """
                         INSERT INTO workflows (
                             workflow_definition_id,
                             department_id,
                             position_role_id,
                             created_by_user_id,
                             target_person_id,
                             first_name,
                             last_name,
                             employee_number,
                             badge_number,
                             status,
                             started_at,
                             completed_at
                         )
                         VALUES (
                             @processTypeId,
                             @departmentId,
                             @roleId,
                             @createdByUserId,
                             @targetPersonId,
                             'Target',
                             'Person',
                             @employeeNumber,
                             @badgeNumber,
                             'completed',
                             NOW() - INTERVAL '10 day',
                             NOW() - INTERVAL '1 day'
                         )
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("processTypeId", onboardingProcessTypeId);
            command.Parameters.AddWithValue("departmentId", departmentId);
            command.Parameters.AddWithValue("roleId", roleId);
            command.Parameters.AddWithValue("createdByUserId", actorUserId);
            command.Parameters.AddWithValue("targetPersonId", personId);
            command.Parameters.AddWithValue("employeeNumber", employeeNumber);
            command.Parameters.AddWithValue("badgeNumber", badgeNumber);
            _ = await command.ExecuteScalarAsync();
        }

        await transaction.CommitAsync();

        return new SeededRuntimeTargetPerson
        {
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,
            PersonId = personId,
            DepartmentId = departmentId,
            RoleId = roleId,
            EmployeeNumber = employeeNumber,
            BadgeNumber = badgeNumber
        };
    }

    private static async Task CleanupSeededRuntimeTargetPersonAsync(string connectionString, SeededRuntimeTargetPerson targetPerson)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var command = new NpgsqlCommand(
                         """
                         DELETE FROM workflows
                         WHERE target_person_id = @personId
                            OR created_by_user_id IN (@actorUserId, @targetUserId);
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("personId", targetPerson.PersonId);
            command.Parameters.AddWithValue("actorUserId", targetPerson.ActorUserId);
            command.Parameters.AddWithValue("targetUserId", targetPerson.TargetUserId);
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand(
                         """
                         DELETE FROM people
                         WHERE id = @personId;
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("personId", targetPerson.PersonId);
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand(
                         """
                         DELETE FROM app_users
                         WHERE id IN (@actorUserId, @targetUserId);
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("actorUserId", targetPerson.ActorUserId);
            command.Parameters.AddWithValue("targetUserId", targetPerson.TargetUserId);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task<List<RequirementSelectionInputDto>> BuildPositiveSupervisorSelectionsAsync(string connectionString)
    {
        var adUserRequestedId = await LoadAnswerDefinitionIdAsync(connectionString, "ad_user_requested");
        var mailboxRequestedId = await LoadAnswerDefinitionIdAsync(connectionString, "mailbox_requested");
        var hardwareRequestedId = await LoadAnswerDefinitionIdAsync(connectionString, "hardware_requested");
        var hardwareAvailableId = await LoadAnswerDefinitionIdAsync(connectionString, "hardware_available");
        var hardwareTypeId = await LoadAnswerDefinitionIdAsync(connectionString, "hardware_type");
        var laptopVpnTypeId = await LoadAnswerDefinitionIdAsync(connectionString, "laptop_vpn_type");
        var laptopOptionId = await LoadAnswerOptionIdAsync(connectionString, "hardware_type", "laptop");
        var withVpnOptionId = await LoadAnswerOptionIdAsync(connectionString, "laptop_vpn_type", "with_vpn");

        return
        [
            new RequirementSelectionInputDto { RequirementId = adUserRequestedId, ValueBoolean = true },
            new RequirementSelectionInputDto { RequirementId = mailboxRequestedId, ValueBoolean = true },
            new RequirementSelectionInputDto { RequirementId = hardwareRequestedId, ValueBoolean = true },
            new RequirementSelectionInputDto { RequirementId = hardwareAvailableId, ValueBoolean = false },
            new RequirementSelectionInputDto { RequirementId = hardwareTypeId, SelectedOptionId = laptopOptionId },
            new RequirementSelectionInputDto { RequirementId = laptopVpnTypeId, SelectedOptionId = withVpnOptionId }
        ];
    }

    private static async Task<int> LoadAnswerDefinitionIdAsync(string connectionString, string answerKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT id FROM workflow_answer_definitions WHERE answer_key = @answerKey LIMIT 1;",
            connection);
        command.Parameters.AddWithValue("answerKey", answerKey);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is int answerDefinitionId
            ? answerDefinitionId
            : throw new InvalidOperationException($"Answer definition '{answerKey}' could not be loaded.");
    }

    private static async Task<int> LoadAnswerOptionIdAsync(string connectionString, string answerKey, string optionKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = """
SELECT o.id
FROM workflow_answer_options o
JOIN workflow_answer_definitions d ON d.id = o.answer_definition_id
WHERE d.answer_key = @answerKey
  AND o.option_key = @optionKey
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("answerKey", answerKey);
        command.Parameters.AddWithValue("optionKey", optionKey);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is int optionId
            ? optionId
            : throw new InvalidOperationException($"Answer option '{answerKey}:{optionKey}' could not be loaded.");
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var candidate = Path.Combine([currentDirectory.FullName, .. segments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException($"Repository file '{Path.Combine(segments)}' could not be found.");
    }

    private sealed class SeededRuntimeTargetPerson
    {
        public required long ActorUserId { get; init; }
        public required long TargetUserId { get; init; }
        public required long PersonId { get; init; }
        public required int DepartmentId { get; init; }
        public required int RoleId { get; init; }
        public required int EmployeeNumber { get; init; }
        public required int BadgeNumber { get; init; }
    }

    private static class WorkflowDefinitionTestData
    {
        public static WorkflowDefinitionNodeDto FormNode(
            string nodeKey,
            string nodeType,
            string? configJson = null,
            int sortOrder = 0,
            int? positionX = null,
            int? positionY = null)
        {
            return new WorkflowDefinitionNodeDto
            {
                NodeKey = nodeKey,
                NodeType = nodeType,
                SortOrder = sortOrder,
                PositionX = positionX,
                PositionY = positionY,
                Config = configJson is null ? null : System.Text.Json.JsonDocument.Parse(configJson).RootElement.Clone()
            };
        }

        public static WorkflowDefinitionEdgeDto Edge(string sourceNodeKey, string targetNodeKey, int priority)
        {
            return new WorkflowDefinitionEdgeDto
            {
                SourceNodeKey = sourceNodeKey,
                TargetNodeKey = targetNodeKey,
                Priority = priority
            };
        }
    }
}
