using Npgsql;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class PostgresWorkflowRepositoryAdminConfigIntegrationTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=25432;Database=appdb;Username=app;Password=app_pw";

    // ── Department assignment supervisor eligibility ───────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DepartmentAssignment_UsesSupervisorEligibilityConsistently_ForDirectoryMappedUsers()
    {
        var connectionString = GetTestConnectionString();
        var suffix = Guid.NewGuid().ToString("N");
        var departmentId = await CreateTemporaryDepartmentAsync(connectionString, suffix);
        var supervisorUserId = await CreateTemporaryUserAsync(connectionString, $"supervisor_{suffix}", isActive: true);
        var plainUserId = await CreateTemporaryUserAsync(connectionString, $"plain_{suffix}", isActive: true);
        var directoryIdentityId = await CreateTemporaryDirectoryIdentityAsync(connectionString, supervisorUserId, suffix);
        var directoryGroupId = await CreateTemporaryDirectoryGroupAsync(connectionString, suffix);
        var managerRoleId = await GetRoleIdByKeyAsync(connectionString, AuthorizationRoles.Manager);

        try
        {
            await AddDirectoryGroupMemberAsync(connectionString, directoryGroupId, directoryIdentityId);
            await AddDirectoryGroupRoleMappingAsync(connectionString, directoryGroupId, managerRoleId);

            var adminUsers = await WithUserAuthorizationRepositoryAsync(connectionString, repo => repo.GetAdminUsers());
            var supervisorUser = Assert.Single(adminUsers, user => user.UserId == supervisorUserId);
            var plainUser = Assert.Single(adminUsers, user => user.UserId == plainUserId);

            Assert.True(supervisorUser.HasManagerAccess);
            Assert.True(supervisorUser.CanAccessSupervisorStep);
            Assert.False(plainUser.CanAccessSupervisorStep);

            var assignment = await WithUserAuthorizationRepositoryAsync(connectionString, repo =>
                repo.UpdateDepartmentAssignment(departmentId, supervisorUserId, null));

            Assert.NotNull(assignment);
            Assert.Equal(supervisorUserId, assignment!.DepartmentLeadUserId);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithUserAuthorizationRepositoryAsync(connectionString, repo =>
                    repo.UpdateDepartmentAssignment(departmentId, plainUserId, null)));

            Assert.Contains("supervisor access", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await CleanupTemporaryDepartmentAssignmentScenarioAsync(
                connectionString,
                departmentId,
                [supervisorUserId, plainUserId],
                directoryIdentityId,
                directoryGroupId);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Responsibility_CreateDelete_Works_AndKeepsPlainName()
    {
        var connectionString = GetTestConnectionString();
        var suffix = Guid.NewGuid().ToString("N");
        var departmentId = await CreateTemporaryDepartmentAsync(connectionString, suffix);

        try
        {
            var created = await WithUserAuthorizationRepositoryAsync(connectionString, repo =>
                repo.CreateResponsibility("AD", departmentId));

            Assert.Equal("AD", created.ResponsibilityName);
            Assert.Equal(departmentId, created.DepartmentId);
            Assert.DoesNotContain("Admin Assignment Test", created.ResponsibilityName, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(created.ResponsibilityKey));
            Assert.False(string.IsNullOrWhiteSpace(created.SystemKey));

            var deleted = await WithUserAuthorizationRepositoryAsync(connectionString, repo =>
                repo.DeleteResponsibility(created.ResponsibilityId));

            Assert.True(deleted);

            var responsibilities = await WithUserAuthorizationRepositoryAsync(connectionString, repo =>
                repo.GetAdminResponsibilityOwners());

            Assert.DoesNotContain(responsibilities, item => item.ResponsibilityId == created.ResponsibilityId);
        }
        finally
        {
            await CleanupTemporaryResponsibilitiesAsync(connectionString, departmentId);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Responsibility_Delete_RejectsWorkflowDefinitionReferences()
    {
        var connectionString = GetTestConnectionString();
        var suffix = Guid.NewGuid().ToString("N");
        var departmentId = await CreateTemporaryDepartmentAsync(connectionString, suffix);
        int responsibilityId = 0;

        try
        {
            var created = await WithUserAuthorizationRepositoryAsync(connectionString, repo =>
                repo.CreateResponsibility("Hardware", departmentId));
            responsibilityId = created.ResponsibilityId;

            await CreateTemporaryWorkflowDefinitionReferenceAsync(
                connectionString,
                suffix,
                created.ResponsibilityKey);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithUserAuthorizationRepositoryAsync(connectionString, repo =>
                    repo.DeleteResponsibility(created.ResponsibilityId)));

            Assert.Contains("workflow definitions", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await CleanupTemporaryWorkflowDefinitionsAsync(connectionString, suffix);
            if (responsibilityId > 0)
            {
                await CleanupTemporaryResponsibilitiesAsync(connectionString, departmentId);
            }
            else
            {
                await CleanupTemporaryDepartmentOnlyAsync(connectionString, departmentId);
            }
        }
    }

    // ── Task Templates ──────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TaskTemplate_CRUD_Works()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);

        try
        {
            var created = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"test_tmpl_{processType.Suffix}",
                    Title = "Test Task",
                    Category = "general",
                    Description = "Test description",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    DueInDays = 5,
                    SortOrder = 10,
                    IsActive = true
                }));

            Assert.Equal(processType.Id, created.ProcessTypeId);
            Assert.Equal($"test_tmpl_{processType.Suffix}", created.TemplateKey);
            Assert.Equal("Test Task", created.Title);
            Assert.Equal(5, created.DueInDays);
            Assert.Equal(0, created.ConditionCount);
            Assert.Equal(0, created.DependencyCount);

            var updated = await WithRepositoryAsync(connectionString, repo =>
                repo.UpdateAdminTaskTemplate(created.Id, new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"test_tmpl_{processType.Suffix}",
                    Title = "Updated Task",
                    Category = "general",
                    Description = "Updated description",
                    IsDepartmentPhaseTask = false,
                    IsRequired = false,
                    DueInDays = 10,
                    SortOrder = 20,
                    IsActive = false
                }));

            Assert.NotNull(updated);
            Assert.Equal("Updated Task", updated!.Title);
            Assert.Equal(10, updated.DueInDays);
            Assert.False(updated.IsActive);

            var list = await WithRepositoryAsync(connectionString, repo =>
                repo.GetAdminTaskTemplates(processType.Id));

            Assert.Single(list);
            Assert.Equal("Updated Task", list[0].Title);

            var deleted = await WithRepositoryAsync(connectionString, repo =>
                repo.DeleteAdminTaskTemplate(created.Id));

            Assert.True(deleted);

            var listAfterDelete = await WithRepositoryAsync(connectionString, repo =>
                repo.GetAdminTaskTemplates(processType.Id));

            Assert.Empty(listAfterDelete);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TaskTemplate_RejectsDuplicateTemplateKey()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);

        try
        {
            await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"dup_key_{processType.Suffix}",
                    Title = "First",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryAsync(connectionString, repo =>
                    repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                    {
                        ProcessTypeId = processType.Id,
                        TemplateKey = $"dup_key_{processType.Suffix}",
                        Title = "Second",
                        Category = "general",
                        Description = "",
                        IsDepartmentPhaseTask = true,
                        IsRequired = true,
                        SortOrder = 2,
                        IsActive = true
                    })));

            Assert.Contains("Key existiert bereits", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TaskTemplate_RejectsMissingTitle()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);

        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryAsync(connectionString, repo =>
                    repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                    {
                        ProcessTypeId = processType.Id,
                        TemplateKey = $"no_title_{processType.Suffix}",
                        Title = "",
                        Category = "general",
                        Description = "",
                        IsDepartmentPhaseTask = true,
                        IsRequired = true,
                        SortOrder = 1,
                        IsActive = true
                    })));

            Assert.Contains("Titel", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    // ── Task Template Conditions ────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TaskTemplateCondition_CRUD_Works()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);
        var answerKey = $"cond_answer_{processType.Suffix}";

        try
        {
            await InsertAnswerDefinitionAsync(connectionString, processType.Id, answerKey);

            var template = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"cond_tmpl_{processType.Suffix}",
                    Title = "Condition Test",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            var condition = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplateCondition(template.Id, new AdminTaskTemplateConditionCreateRequest
                {
                    ConditionGroup = 1,
                    AnswerKey = answerKey,
                    Operator = "eq",
                    ExpectedValueBoolean = true
                }));

            Assert.Equal(template.Id, condition.TaskTemplateId);
            Assert.Equal(1, condition.ConditionGroup);
            Assert.Equal(answerKey, condition.AnswerKey);
            Assert.Equal("eq", condition.Operator);
            Assert.True(condition.ExpectedValueBoolean);

            var conditions = await WithRepositoryAsync(connectionString, repo =>
                repo.GetAdminTaskTemplateConditions(template.Id));

            Assert.Single(conditions);

            var deleted = await WithRepositoryAsync(connectionString, repo =>
                repo.DeleteAdminTaskTemplateCondition(template.Id, condition.Id));

            Assert.True(deleted);

            var conditionsAfterDelete = await WithRepositoryAsync(connectionString, repo =>
                repo.GetAdminTaskTemplateConditions(template.Id));

            Assert.Empty(conditionsAfterDelete);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TaskTemplateCondition_RejectsInvalidOperator()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);
        var answerKey = $"op_answer_{processType.Suffix}";

        try
        {
            await InsertAnswerDefinitionAsync(connectionString, processType.Id, answerKey);

            var template = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"op_tmpl_{processType.Suffix}",
                    Title = "Op Test",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryAsync(connectionString, repo =>
                    repo.CreateAdminTaskTemplateCondition(template.Id, new AdminTaskTemplateConditionCreateRequest
                    {
                        ConditionGroup = 1,
                        AnswerKey = answerKey,
                        Operator = "invalid_op"
                    })));

            Assert.Contains("Operator", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TaskTemplateCondition_RejectsAnswerKeyFromDifferentProcessType()
    {
        var connectionString = GetTestConnectionString();
        var processType1 = await CreateTemporaryProcessTypeAsync(connectionString);
        var processType2 = await CreateTemporaryProcessTypeAsync(connectionString);
        var answerKey = $"cross_answer_{processType2.Suffix}";

        try
        {
            await InsertAnswerDefinitionAsync(connectionString, processType2.Id, answerKey);

            var template = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType1.Id,
                    TemplateKey = $"cross_tmpl_{processType1.Suffix}",
                    Title = "Cross Process Test",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryAsync(connectionString, repo =>
                    repo.CreateAdminTaskTemplateCondition(template.Id, new AdminTaskTemplateConditionCreateRequest
                    {
                        ConditionGroup = 1,
                        AnswerKey = answerKey,
                        Operator = "eq",
                        ExpectedValueBoolean = true
                    })));

            Assert.Contains("Prozesstyp", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType1.Id, processType1.Suffix);
            await CleanupTemporaryProcessTypeAsync(connectionString, processType2.Id, processType2.Suffix);
        }
    }

    // ── Task Template Dependencies ──────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TaskTemplateDependency_CRUD_Works()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);

        try
        {
            var templateA = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"dep_a_{processType.Suffix}",
                    Title = "Task A",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            var templateB = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"dep_b_{processType.Suffix}",
                    Title = "Task B",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 2,
                    IsActive = true
                }));

            var dependency = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplateDependency(templateB.Id, new AdminTaskTemplateDependencyCreateRequest
                {
                    DependsOnTaskTemplateId = templateA.Id,
                    RequiredStatus = "done"
                }));

            Assert.Equal(templateB.Id, dependency.TaskTemplateId);
            Assert.Equal(templateA.Id, dependency.DependsOnTaskTemplateId);
            Assert.Equal("Task A", dependency.DependsOnTemplateTitle);
            Assert.Equal("done", dependency.RequiredStatus);

            var dependencies = await WithRepositoryAsync(connectionString, repo =>
                repo.GetAdminTaskTemplateDependencies(templateB.Id));

            Assert.Single(dependencies);

            var deleted = await WithRepositoryAsync(connectionString, repo =>
                repo.DeleteAdminTaskTemplateDependency(templateB.Id, dependency.Id));

            Assert.True(deleted);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TaskTemplateDependency_RejectsSelfDependency()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);

        try
        {
            var template = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"self_dep_{processType.Suffix}",
                    Title = "Self Dep",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryAsync(connectionString, repo =>
                    repo.CreateAdminTaskTemplateDependency(template.Id, new AdminTaskTemplateDependencyCreateRequest
                    {
                        DependsOnTaskTemplateId = template.Id,
                        RequiredStatus = "done"
                    })));

            Assert.Contains("sich selbst", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TaskTemplateDependency_RejectsDuplicateDependency()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);

        try
        {
            var templateA = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"dup_dep_a_{processType.Suffix}",
                    Title = "A",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            var templateB = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"dup_dep_b_{processType.Suffix}",
                    Title = "B",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 2,
                    IsActive = true
                }));

            await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplateDependency(templateB.Id, new AdminTaskTemplateDependencyCreateRequest
                {
                    DependsOnTaskTemplateId = templateA.Id,
                    RequiredStatus = "done"
                }));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryAsync(connectionString, repo =>
                    repo.CreateAdminTaskTemplateDependency(templateB.Id, new AdminTaskTemplateDependencyCreateRequest
                    {
                        DependsOnTaskTemplateId = templateA.Id,
                        RequiredStatus = "done"
                    })));

            Assert.Contains("bereits vorhanden", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TaskTemplateDependency_RejectsCircularDependency()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);

        try
        {
            var templateA = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"cycle_a_{processType.Suffix}",
                    Title = "A",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            var templateB = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"cycle_b_{processType.Suffix}",
                    Title = "B",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 2,
                    IsActive = true
                }));

            var templateC = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"cycle_c_{processType.Suffix}",
                    Title = "C",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 3,
                    IsActive = true
                }));

            // A → B → C, then try C → A (cycle)
            await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplateDependency(templateB.Id, new AdminTaskTemplateDependencyCreateRequest
                {
                    DependsOnTaskTemplateId = templateA.Id,
                    RequiredStatus = "done"
                }));

            await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplateDependency(templateC.Id, new AdminTaskTemplateDependencyCreateRequest
                {
                    DependsOnTaskTemplateId = templateB.Id,
                    RequiredStatus = "done"
                }));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryAsync(connectionString, repo =>
                    repo.CreateAdminTaskTemplateDependency(templateA.Id, new AdminTaskTemplateDependencyCreateRequest
                    {
                        DependsOnTaskTemplateId = templateC.Id,
                        RequiredStatus = "done"
                    })));

            Assert.Contains("Kreis", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TaskTemplateDependency_RejectsCrossProcessTypeDependency()
    {
        var connectionString = GetTestConnectionString();
        var processType1 = await CreateTemporaryProcessTypeAsync(connectionString);
        var processType2 = await CreateTemporaryProcessTypeAsync(connectionString);

        try
        {
            var templateA = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType1.Id,
                    TemplateKey = $"cross_dep_a_{processType1.Suffix}",
                    Title = "A",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            var templateB = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType2.Id,
                    TemplateKey = $"cross_dep_b_{processType2.Suffix}",
                    Title = "B",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryAsync(connectionString, repo =>
                    repo.CreateAdminTaskTemplateDependency(templateB.Id, new AdminTaskTemplateDependencyCreateRequest
                    {
                        DependsOnTaskTemplateId = templateA.Id,
                        RequiredStatus = "done"
                    })));

            Assert.Contains("Prozesstyps", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType1.Id, processType1.Suffix);
            await CleanupTemporaryProcessTypeAsync(connectionString, processType2.Id, processType2.Suffix);
        }
    }

    // ── Answer Definitions ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnswerDefinition_CRUD_Works()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);

        try
        {
            var created = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminAnswerDefinition(new AdminAnswerDefinitionUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    AnswerKey = $"ans_def_{processType.Suffix}",
                    Title = "Test Answer",
                    Category = "general",
                    Description = "Test",
                    InputType = "boolean",
                    IsRequired = true,
                    SortOrder = 10,
                    IsActive = true
                }));

            Assert.Equal(processType.Id, created.ProcessTypeId);
            Assert.Equal($"ans_def_{processType.Suffix}", created.AnswerKey);
            Assert.Equal("boolean", created.InputType);

            var updated = await WithRepositoryAsync(connectionString, repo =>
                repo.UpdateAdminAnswerDefinition(created.Id, new AdminAnswerDefinitionUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    AnswerKey = $"ans_def_{processType.Suffix}",
                    Title = "Updated Answer",
                    Category = "general",
                    Description = "Updated",
                    InputType = "text",
                    IsRequired = false,
                    SortOrder = 20,
                    IsActive = false
                }));

            Assert.NotNull(updated);
            Assert.Equal("Updated Answer", updated!.Title);
            Assert.Equal("text", updated.InputType);

            var list = await WithRepositoryAsync(connectionString, repo =>
                repo.GetAdminAnswerDefinitions(processType.Id));

            Assert.Single(list);

            var deleted = await WithRepositoryAsync(connectionString, repo =>
                repo.DeleteAdminAnswerDefinition(created.Id));

            Assert.True(deleted);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnswerDefinition_RejectsDuplicateAnswerKey()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);

        try
        {
            await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminAnswerDefinition(new AdminAnswerDefinitionUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    AnswerKey = $"dup_ans_{processType.Suffix}",
                    Title = "First",
                    Category = "general",
                    Description = "",
                    InputType = "boolean",
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryAsync(connectionString, repo =>
                    repo.CreateAdminAnswerDefinition(new AdminAnswerDefinitionUpsertRequest
                    {
                        ProcessTypeId = processType.Id,
                        AnswerKey = $"dup_ans_{processType.Suffix}",
                        Title = "Second",
                        Category = "general",
                        Description = "",
                        InputType = "boolean",
                        IsRequired = true,
                        SortOrder = 2,
                        IsActive = true
                    })));

            Assert.Contains("Key existiert bereits", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnswerDefinition_RejectsInvalidInputType()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);

        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryAsync(connectionString, repo =>
                    repo.CreateAdminAnswerDefinition(new AdminAnswerDefinitionUpsertRequest
                    {
                        ProcessTypeId = processType.Id,
                        AnswerKey = $"bad_type_{processType.Suffix}",
                        Title = "Bad Type",
                        Category = "general",
                        Description = "",
                        InputType = "invalid_type",
                        IsRequired = true,
                        SortOrder = 1,
                        IsActive = true
                    })));

            Assert.Contains("Input-Typ", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AnswerDefinition_DeleteBlockedByConditionReference()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);

        try
        {
            var answer = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminAnswerDefinition(new AdminAnswerDefinitionUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    AnswerKey = $"ref_ans_{processType.Suffix}",
                    Title = "Referenced Answer",
                    Category = "general",
                    Description = "",
                    InputType = "boolean",
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            var template = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"ref_tmpl_{processType.Suffix}",
                    Title = "Referencing Template",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplateCondition(template.Id, new AdminTaskTemplateConditionCreateRequest
                {
                    ConditionGroup = 1,
                    AnswerKey = $"ref_ans_{processType.Suffix}",
                    Operator = "is_true"
                }));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryAsync(connectionString, repo =>
                    repo.DeleteAdminAnswerDefinition(answer.Id)));

            Assert.Contains("Bedingungen", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    // ── Role Answer Defaults ────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RoleAnswerDefaults_UpsertAndRead_Works()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);
        var answerKey = $"rad_answer_{processType.Suffix}";
        var roleId = await GetFirstActiveRoleIdAsync(connectionString);

        try
        {
            await InsertAnswerDefinitionAsync(connectionString, processType.Id, answerKey);

            var result = await WithRepositoryAsync(connectionString, repo =>
                repo.UpsertAdminRoleAnswerDefaults(new AdminRoleAnswerDefaultsBulkUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    Items = new List<AdminRoleAnswerDefaultUpsertItemRequest>
                    {
                        new()
                        {
                            AppRoleId = roleId,
                            AnswerKey = answerKey,
                            DefaultValueBoolean = true
                        }
                    }
                }));

            Assert.Single(result);
            Assert.Equal(answerKey, result[0].AnswerKey);
            Assert.True(result[0].DefaultValueBoolean);

            var readBack = await WithRepositoryAsync(connectionString, repo =>
                repo.GetAdminRoleAnswerDefaults(processType.Id));

            Assert.Single(readBack);
            Assert.Equal(roleId, readBack[0].AppRoleId);

            // Upsert again with changed value
            var updated = await WithRepositoryAsync(connectionString, repo =>
                repo.UpsertAdminRoleAnswerDefaults(new AdminRoleAnswerDefaultsBulkUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    Items = new List<AdminRoleAnswerDefaultUpsertItemRequest>
                    {
                        new()
                        {
                            AppRoleId = roleId,
                            AnswerKey = answerKey,
                            DefaultValueBoolean = false
                        }
                    }
                }));

            Assert.Single(updated);
            Assert.False(updated[0].DefaultValueBoolean);
        }
        finally
        {
            await CleanupRoleAnswerDefaultsAsync(connectionString, processType.Id);
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RoleAnswerDefaults_RejectsInvalidRoleId()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);
        var answerKey = $"bad_role_answer_{processType.Suffix}";

        try
        {
            await InsertAnswerDefinitionAsync(connectionString, processType.Id, answerKey);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryAsync(connectionString, repo =>
                    repo.UpsertAdminRoleAnswerDefaults(new AdminRoleAnswerDefaultsBulkUpsertRequest
                    {
                        ProcessTypeId = processType.Id,
                        Items = new List<AdminRoleAnswerDefaultUpsertItemRequest>
                        {
                            new()
                            {
                                AppRoleId = 99999,
                                AnswerKey = answerKey,
                                DefaultValueBoolean = true
                            }
                        }
                    })));

            Assert.Contains("Rollen", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    // ── Condition count reflects in template DTO ────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TaskTemplate_ConditionCount_ReflectsActualConditions()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);
        var answerKey = $"count_answer_{processType.Suffix}";

        try
        {
            await InsertAnswerDefinitionAsync(connectionString, processType.Id, answerKey);

            var template = await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplate(new AdminTaskTemplateUpsertRequest
                {
                    ProcessTypeId = processType.Id,
                    TemplateKey = $"count_tmpl_{processType.Suffix}",
                    Title = "Count Test",
                    Category = "general",
                    Description = "",
                    IsDepartmentPhaseTask = true,
                    IsRequired = true,
                    SortOrder = 1,
                    IsActive = true
                }));

            Assert.Equal(0, template.ConditionCount);

            await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplateCondition(template.Id, new AdminTaskTemplateConditionCreateRequest
                {
                    ConditionGroup = 1,
                    AnswerKey = answerKey,
                    Operator = "is_true"
                }));

            await WithRepositoryAsync(connectionString, repo =>
                repo.CreateAdminTaskTemplateCondition(template.Id, new AdminTaskTemplateConditionCreateRequest
                {
                    ConditionGroup = 2,
                    AnswerKey = answerKey,
                    Operator = "is_false"
                }));

            var templates = await WithRepositoryAsync(connectionString, repo =>
                repo.GetAdminTaskTemplates(processType.Id));

            Assert.Single(templates);
            Assert.Equal(2, templates[0].ConditionCount);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static string GetTestConnectionString()
    {
        return Environment.GetEnvironmentVariable("ONBOARDING_TEST_CONNECTION_STRING")
               ?? DefaultTestConnectionString;
    }

    private static async Task<T> WithRepositoryAsync<T>(
        string connectionString,
        Func<PostgresWorkflowRepository, Task<T>> action)
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        try
        {
            var repository = new PostgresWorkflowRepository();
            return await action(repository);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);
        }
    }

    private static async Task<T> WithUserAuthorizationRepositoryAsync<T>(
        string connectionString,
        Func<PostgresUserAuthorizationRepository, Task<T>> action)
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        try
        {
            var repository = new PostgresUserAuthorizationRepository();
            return await action(repository);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);
        }
    }

    private static async Task<TemporaryProcessType> CreateTemporaryProcessTypeAsync(string connectionString)
    {
        var suffix = Guid.NewGuid().ToString("N");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO process_types (
                key, name, description,
                requires_supervisor_step, requires_target_person,
                is_active, sort_order
            )
            VALUES (
                @key, @name, 'Integration test',
                FALSE, FALSE,
                FALSE, 9999
            )
            RETURNING id;
            """,
            connection);
        command.Parameters.AddWithValue("key", $"admin_config_test_{suffix}");
        command.Parameters.AddWithValue("name", $"Admin Config Test {suffix}");

        var id = (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Temporary process type could not be created."));

        return new TemporaryProcessType { Id = id, Suffix = suffix };
    }

    private static async Task<int> CreateTemporaryDepartmentAsync(string connectionString, string suffix)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO departments (name)
            VALUES (@name)
            RETURNING id;
            """,
            connection);
        command.Parameters.AddWithValue("name", $"Admin Assignment Test {suffix}");
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Temporary department could not be created."));
    }

    private static async Task<long> CreateTemporaryUserAsync(string connectionString, string suffix, bool isActive)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        long userId;
        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO app_users (
                external_key,
                display_name,
                email,
                notification_email,
                is_active,
                directory_synced,
                department_source,
                department_override_active
            )
            VALUES (
                @externalKey,
                @displayName,
                @email,
                NULL,
                @isActive,
                FALSE,
                'unassigned',
                FALSE
            )
            RETURNING id;
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("externalKey", suffix);
            command.Parameters.AddWithValue("displayName", $"Test User {suffix}");
            command.Parameters.AddWithValue("email", $"{suffix}@integration.local");
            command.Parameters.AddWithValue("isActive", isActive);
            userId = (long)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Temporary user could not be created."));
        }

        await using (var personCommand = new NpgsqlCommand(
            """
            INSERT INTO people (app_user_id, updated_at)
            VALUES (@userId, NOW());
            """,
            connection,
            transaction))
        {
            personCommand.Parameters.AddWithValue("userId", userId);
            await personCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return userId;
    }

    private static async Task<long> CreateTemporaryDirectoryIdentityAsync(string connectionString, long userId, string suffix)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO directory_identities (
                entra_object_id,
                user_principal_name,
                mail,
                display_name,
                account_enabled,
                app_user_id
            )
            VALUES (
                @entraObjectId,
                @upn,
                @mail,
                @displayName,
                TRUE,
                @userId
            )
            RETURNING id;
            """,
            connection);
        command.Parameters.AddWithValue("entraObjectId", Guid.NewGuid());
        command.Parameters.AddWithValue("upn", $"{suffix}@directory.local");
        command.Parameters.AddWithValue("mail", $"{suffix}@directory.local");
        command.Parameters.AddWithValue("displayName", $"Directory {suffix}");
        command.Parameters.AddWithValue("userId", userId);
        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Temporary directory identity could not be created."));
    }

    private static async Task<int> CreateTemporaryDirectoryGroupAsync(string connectionString, string suffix)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO directory_groups (
                external_group_id,
                display_name,
                description
            )
            VALUES (
                @externalGroupId,
                @displayName,
                'Integration test'
            )
            RETURNING id;
            """,
            connection);
        command.Parameters.AddWithValue("externalGroupId", Guid.NewGuid());
        command.Parameters.AddWithValue("displayName", $"Supervisor Group {suffix}");
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Temporary directory group could not be created."));
    }

    private static async Task AddDirectoryGroupMemberAsync(
        string connectionString,
        int directoryGroupId,
        long directoryIdentityId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO directory_group_members (directory_group_id, directory_identity_id)
            VALUES (@directoryGroupId, @directoryIdentityId);
            """,
            connection);
        command.Parameters.AddWithValue("directoryGroupId", directoryGroupId);
        command.Parameters.AddWithValue("directoryIdentityId", directoryIdentityId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AddDirectoryGroupRoleMappingAsync(
        string connectionString,
        int directoryGroupId,
        int roleId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO directory_group_role_mappings (
                directory_group_id,
                app_role_id,
                scope,
                scope_department_id,
                is_active
            )
            VALUES (
                @directoryGroupId,
                @roleId,
                'global',
                NULL,
                TRUE
            );
            """,
            connection);
        command.Parameters.AddWithValue("directoryGroupId", directoryGroupId);
        command.Parameters.AddWithValue("roleId", roleId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> GetRoleIdByKeyAsync(string connectionString, string roleKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT id
            FROM app_roles
            WHERE role_key = @roleKey
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("roleKey", roleKey);

        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException($"Role '{roleKey}' not found."));
    }

    private static async Task InsertAnswerDefinitionAsync(string connectionString, int processTypeId, string answerKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO workflow_answer_definitions (
                process_type_id, answer_key, title, category,
                description, icon_key, input_type,
                is_required, sort_order, is_active
            )
            VALUES (
                @processTypeId, @answerKey, 'Test Answer', 'general',
                'Test', 'berechtigungen', 'boolean',
                FALSE, 10, TRUE
            );
            """,
            connection);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        command.Parameters.AddWithValue("answerKey", answerKey);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> GetFirstActiveRoleIdAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT id FROM app_roles ORDER BY id LIMIT 1;",
            connection);

        var result = await command.ExecuteScalarAsync();
        if (result is not int roleId)
        {
            throw new InvalidOperationException("No app_roles found in test database.");
        }

        return roleId;
    }

    private static async Task CleanupRoleAnswerDefaultsAsync(string connectionString, int processTypeId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "DELETE FROM app_role_answer_defaults WHERE process_type_id = @processTypeId;",
            connection);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupTemporaryDepartmentAssignmentScenarioAsync(
        string connectionString,
        int departmentId,
        IReadOnlyCollection<long> userIds,
        long directoryIdentityId,
        int directoryGroupId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            DELETE FROM department_settings
            WHERE department_id = @departmentId;

            DELETE FROM directory_group_role_mappings
            WHERE directory_group_id = @directoryGroupId;

            DELETE FROM directory_group_members
            WHERE directory_group_id = @directoryGroupId;

            DELETE FROM directory_groups
            WHERE id = @directoryGroupId;

            DELETE FROM directory_identities
            WHERE id = @directoryIdentityId;

            DELETE FROM people
            WHERE app_user_id = ANY(@userIds);

            DELETE FROM app_users
            WHERE id = ANY(@userIds);

            DELETE FROM departments
            WHERE id = @departmentId;
            """,
            connection);
        command.Parameters.AddWithValue("departmentId", departmentId);
        command.Parameters.AddWithValue("directoryGroupId", directoryGroupId);
        command.Parameters.AddWithValue("directoryIdentityId", directoryIdentityId);
        command.Parameters.Add("userIds", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Bigint).Value =
            userIds.ToArray();
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CreateTemporaryWorkflowDefinitionReferenceAsync(
        string connectionString,
        string suffix,
        string responsibilityKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        int definitionId;
        await using (var definitionCommand = new NpgsqlCommand(
            """
            INSERT INTO workflow_definitions (definition_key, name, description)
            VALUES (@definitionKey, @name, 'Integration test')
            RETURNING id;
            """,
            connection,
            transaction))
        {
            definitionCommand.Parameters.AddWithValue("definitionKey", $"resp_ref_{suffix}");
            definitionCommand.Parameters.AddWithValue("name", $"Responsibility Ref {suffix}");
            definitionId = (int)(await definitionCommand.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Temporary workflow definition could not be created."));
        }

        long versionId;
        await using (var versionCommand = new NpgsqlCommand(
            """
            INSERT INTO workflow_definition_versions (
                workflow_definition_id,
                version_number,
                status,
                name,
                description
            )
            VALUES (
                @definitionId,
                1,
                'draft',
                @name,
                'Integration test'
            )
            RETURNING id;
            """,
            connection,
            transaction))
        {
            versionCommand.Parameters.AddWithValue("definitionId", definitionId);
            versionCommand.Parameters.AddWithValue("name", $"Draft {suffix}");
            versionId = (long)(await versionCommand.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Temporary workflow definition version could not be created."));
        }

        long nodeId;
        await using (var nodeCommand = new NpgsqlCommand(
            """
            INSERT INTO workflow_nodes (
                workflow_definition_version_id,
                node_key,
                node_type,
                title,
                sort_order
            )
            VALUES (
                @versionId,
                'task_ref',
                'task',
                'Task Ref',
                10
            )
            RETURNING id;
            """,
            connection,
            transaction))
        {
            nodeCommand.Parameters.AddWithValue("versionId", versionId);
            nodeId = (long)(await nodeCommand.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Temporary workflow node could not be created."));
        }

        await using (var configCommand = new NpgsqlCommand(
            """
            INSERT INTO workflow_node_configs (workflow_node_id, config_json)
            VALUES (@nodeId, jsonb_build_object('responsibilityKey', @responsibilityKey));
            """,
            connection,
            transaction))
        {
            configCommand.Parameters.AddWithValue("nodeId", nodeId);
            configCommand.Parameters.AddWithValue("responsibilityKey", responsibilityKey);
            await configCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task CleanupTemporaryWorkflowDefinitionsAsync(string connectionString, string suffix)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            DELETE FROM workflow_definitions
            WHERE definition_key = @definitionKey;
            """,
            connection);
        command.Parameters.AddWithValue("definitionKey", $"resp_ref_{suffix}");
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupTemporaryResponsibilitiesAsync(
        string connectionString,
        int departmentId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            DELETE FROM app_responsibilities
            WHERE department_id = @departmentId
              AND responsibility_type = 'application';

            DELETE FROM departments
            WHERE id = @departmentId;
            """,
            connection);
        command.Parameters.AddWithValue("departmentId", departmentId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupTemporaryDepartmentOnlyAsync(string connectionString, int departmentId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            DELETE FROM departments
            WHERE id = @departmentId;
            """,
            connection);
        command.Parameters.AddWithValue("departmentId", departmentId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupTemporaryProcessTypeAsync(string connectionString, int processTypeId, string suffix)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            DELETE FROM task_template_dependencies
            WHERE task_template_id IN (
                SELECT id FROM task_templates WHERE process_type_id = @processTypeId
            );

            DELETE FROM task_template_conditions
            WHERE task_template_id IN (
                SELECT id FROM task_templates WHERE process_type_id = @processTypeId
            );

            DELETE FROM task_templates
            WHERE process_type_id = @processTypeId;

            DELETE FROM app_role_answer_defaults
            WHERE process_type_id = @processTypeId;

            DELETE FROM workflow_answer_definitions
            WHERE process_type_id = @processTypeId;

            DELETE FROM process_types
            WHERE id = @processTypeId
              AND key = @processTypeKey;
            """,
            connection);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        command.Parameters.AddWithValue("processTypeKey", $"admin_config_test_{suffix}");
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TemporaryProcessType
    {
        public required int Id { get; init; }
        public required string Suffix { get; init; }
    }
}
