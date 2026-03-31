using Npgsql;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class PostgresWorkflowRepositoryAdminConfigIntegrationTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=25432;Database=appdb;Username=app;Password=app_pw";

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
