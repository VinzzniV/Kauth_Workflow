using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class AdminWorkflowDefinitionConfigEndpointsTests
{
    [Fact]
    public async Task CreateDefinitionVersionEndpoint_AllowsAdminAndReturnsCreated()
    {
        var repository = new StubWorkflowDefinitionRepository
        {
            CreatedVersion = new WorkflowDefinitionVersionSummaryDto
            {
                Id = 42,
                WorkflowDefinitionId = 7,
                VersionNumber = 1,
                Status = "draft",
                Name = "Draft 1",
                Description = "Initial draft",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CanPublish = true,
                ValidationIssues = new List<ValidationIssueDto>()
            }
        };

        var app = CreateApp(repository);
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definitions/{definitionId:int}/versions", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/admin/config/workflow-definitions/7/versions",
            new CreateWorkflowDefinitionVersionRequest
            {
                Name = "Draft 1",
                Description = "Initial draft"
            },
            ("definitionId", 7));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
        Assert.Equal(1, repository.CreateAdminWorkflowDefinitionVersionCallCount);
        Assert.Equal(7, repository.LastCreateAdminWorkflowDefinitionVersionDefinitionId);
    }

    [Fact]
    public async Task ReplaceDefinitionVersionEndpoint_ReturnsBadRequestForInvalidDraft()
    {
        var repository = new StubWorkflowDefinitionRepository
        {
            ReplaceAdminWorkflowDefinitionVersionException =
                new InvalidOperationException("A workflow definition draft must contain exactly one start node.")
        };

        var app = CreateApp(repository);
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definition-versions/{versionId:long}", HttpMethods.Put);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Put,
            "/admin/config/workflow-definition-versions/12",
            new ReplaceWorkflowDefinitionVersionRequest(),
            ("versionId", 12L));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(1, repository.ReplaceAdminWorkflowDefinitionVersionCallCount);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("exactly one start node", body);
    }

    [Fact]
    public async Task PublishDefinitionVersionEndpoint_ReturnsPublishedVersion()
    {
        var repository = new StubWorkflowDefinitionRepository
        {
            PublishedVersion = new WorkflowDefinitionVersionDetailDto
            {
                Id = 12,
                WorkflowDefinitionId = 7,
                DefinitionKey = "hr-onboarding",
                DefinitionName = "HR Onboarding",
                VersionNumber = 2,
                Status = "published",
                Name = "Published",
                Description = "Go live",
                PrimaryLegacyProcessTypeKey = "onboarding",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PublishedAt = DateTime.UtcNow,
                CanPublish = true,
                ValidationIssues = new List<ValidationIssueDto>(),
                Nodes = new List<WorkflowDefinitionNodeDto>(),
                Edges = new List<WorkflowDefinitionEdgeDto>()
            }
        };

        var app = CreateApp(repository);
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definition-versions/{versionId:long}/publish", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/admin/config/workflow-definition-versions/12/publish",
            new { },
            ("versionId", 12L));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.PublishWorkflowDefinitionVersionCallCount);
        Assert.Equal(12L, repository.LastPublishWorkflowDefinitionVersionId);
    }

    private static WebApplication CreateApp(StubWorkflowDefinitionRepository repository)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IWorkflowRepository>(repository);
        builder.Services.AddSingleton<IWorkflowDefinitionRuntimeRepository>(repository);
        builder.Services.AddSingleton<IUserContext>(new StubUserContext(CreateAdminUser()));
        builder.Services.AddSingleton<IAuthorizationPolicyService, AuthorizationPolicyService>();

        var app = builder.Build();
        app.MapAdminWorkflowDefinitionConfigEndpoints();
        return app;
    }

    private static RouteEndpoint GetEndpoint(WebApplication app, string routePattern, string httpMethod)
    {
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint =>
                endpoint.RoutePattern.RawText == routePattern
                && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(httpMethod) == true);
    }

    private static DefaultHttpContext CreateJsonRequestContext<TBody>(
        IServiceProvider services,
        RouteEndpoint endpoint,
        string httpMethod,
        string path,
        TBody body,
        params (string Key, object Value)[] routeValues)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Method = httpMethod;
        context.Request.Path = path;
        context.Request.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(body);
        var bytes = Encoding.UTF8.GetBytes(payload);
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new StubHttpRequestBodyDetectionFeature());
        foreach (var (key, value) in routeValues)
        {
            context.Request.RouteValues[key] = value.ToString();
        }

        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        return context;
    }

    private sealed class StubHttpRequestBodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private static CurrentUser CreateAdminUser()
    {
        var adminRole = new CurrentUserRole
        {
            RoleId = 1,
            RoleKey = AuthorizationRoles.Admin,
            RoleName = AuthorizationRoles.Admin,
            RoleKind = AuthorizationRoles.SystemRoleKind,
            AssignmentSource = "test",
            GroupId = null,
            GroupKey = null
        };

        return new CurrentUser
        {
            UserId = 99,
            ExternalKey = "admin.test",
            DisplayName = "Admin Test",
            Email = "admin.test@example.com",
            IsActive = true,
            DepartmentId = null,
            DepartmentName = null,
            IdentityProvider = "test",
            Groups = new List<CurrentUserGroup>(),
            DirectRoles = [adminRole],
            GroupRoles = new List<CurrentUserRole>(),
            EffectiveRoles = [adminRole],
            DirectResponsibilities = new List<CurrentUserResponsibility>(),
            GroupResponsibilities = new List<CurrentUserResponsibility>(),
            EffectiveResponsibilities = new List<CurrentUserResponsibility>()
        };
    }

    private sealed class StubUserContext(CurrentUser user) : IUserContext
    {
        public Task<CurrentUser?> GetCurrentUser(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CurrentUser?>(user);
        }
    }

    private sealed class StubWorkflowDefinitionRepository : IWorkflowRepository, IWorkflowDefinitionRuntimeRepository
    {
        public WorkflowDefinitionVersionSummaryDto? CreatedVersion { get; set; }
        public WorkflowDefinitionVersionDetailDto? PublishedVersion { get; set; }
        public Exception? ReplaceAdminWorkflowDefinitionVersionException { get; set; }
        public int CreateAdminWorkflowDefinitionVersionCallCount { get; private set; }
        public int ReplaceAdminWorkflowDefinitionVersionCallCount { get; private set; }
        public int PublishWorkflowDefinitionVersionCallCount { get; private set; }
        public int? LastCreateAdminWorkflowDefinitionVersionDefinitionId { get; private set; }
        public long? LastPublishWorkflowDefinitionVersionId { get; private set; }

        public Task<List<DepartmentDto>> GetDepartments() => throw new NotSupportedException();
        public Task<List<RoleDto>> GetRoles() => throw new NotSupportedException();
        public Task<List<WorkflowProcessTypeDto>> GetActiveProcessTypes(bool managerOnly = false) => throw new NotSupportedException();
        public Task<List<RequirementDto>> GetRequirements(string processTypeKey) => throw new NotSupportedException();
        public Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId, string processTypeKey) => throw new NotSupportedException();
        public Task<bool> IsManagerCreatableProcessType(string processTypeKey) => throw new NotSupportedException();
        public Task<WorkflowCreationResult> CreateWorkflow(CreateWorkflowRequest request, long createdByUserId) => throw new NotSupportedException();
        public Task<WorkflowDetailDto?> CompleteSupervisorStep(Guid workflowUid, IReadOnlyList<RequirementSelectionInputDto> selections, long actorUserId) => throw new NotSupportedException();
        public Task<List<WorkflowNotificationDispatchTarget>> GetWorkflowCreatedNotificationDispatchTargets(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotifications(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowCompletionNotifications(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<Guid>> GetWorkflowUidsWithDisabledNotifications(string notificationType) => throw new NotSupportedException();
        public Task ApplyNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results) => throw new NotSupportedException();
        public Task<List<WorkflowListItemDto>> GetWorkflows() => throw new NotSupportedException();
        public Task<WorkflowListResult> GetFilteredWorkflows(WorkflowListQuery query) => throw new NotSupportedException();
        public Task<WorkflowDetailDto?> GetWorkflowByUid(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<WorkflowAuditEntryDto>> GetWorkflowAuditLog(Guid workflowUid, int limit = 200, int offset = 0) => throw new NotSupportedException();
        public Task<HashSet<int>> GetRequirementSelectionDepartmentIds(long userId) => throw new NotSupportedException();
        public Task<List<TaskWithWorkflowDto>> GetTasks() => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> GetTaskById(long taskId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> UpdateTaskStatus(long taskId, string status, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> DecideTaskApproval(long taskId, TaskApprovalDecisionRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> UpdateTaskAssignment(long taskId, TaskAssignRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> AddTaskComment(long taskId, string commentText, long actorUserId) => throw new NotSupportedException();
        public Task<bool> ArchiveWorkflow(Guid workflowUid, long actorUserId) => throw new NotSupportedException();
        public Task<bool> DeleteDraftWorkflow(Guid workflowUid) => throw new NotSupportedException();
        public Task<PersonWorkflowHistoryDto?> GetPersonWorkflowHistory(long personId) => throw new NotSupportedException();
        public Task<List<WorkflowLinkDto>> GetWorkflowLinks(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<RelatedWorkflowSummaryDto>> GetRelatedWorkflows(Guid workflowUid) => throw new NotSupportedException();
        public Task<WorkflowLinkDto?> CreateWorkflowLink(Guid targetWorkflowUid, CreateWorkflowLinkRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<bool> DeleteWorkflowLink(Guid workflowUid, long linkId, long actorUserId) => throw new NotSupportedException();
        public Task<List<WorkflowTargetPersonSourceDto>> SearchWorkflowTargetPersonSources(string? search, int limit = 20, IReadOnlyCollection<int>? observableDepartmentIds = null) => throw new NotSupportedException();
        public Task<List<WorkflowTargetPersonDto>> SearchWorkflowTargetPeople(string? query, int limit = 20, IReadOnlyCollection<int>? observableDepartmentIds = null) => throw new NotSupportedException();
        public Task<List<LinkableWorkflowDto>> FindLinkableWorkflows(int employeeNumber, Guid? excludeWorkflowUid = null) => throw new NotSupportedException();
        public Task<List<DerivedAnswerDto>> GetDerivedAnswers(Guid sourceWorkflowUid, string targetProcessTypeKey) => throw new NotSupportedException();
        public Task<BulkOperationResultDto> BulkCreateDepartmentChangeWorkflows(BulkDepartmentChangeRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<List<WorkflowDefinitionSummaryDto>> GetAdminWorkflowDefinitions() => throw new NotSupportedException();
        public Task<WorkflowDefinitionSummaryDto> CreateAdminWorkflowDefinition(CreateWorkflowDefinitionRequest request) => throw new NotSupportedException();

        public Task<WorkflowDefinitionVersionSummaryDto?> CreateAdminWorkflowDefinitionVersion(
            int definitionId,
            CreateWorkflowDefinitionVersionRequest request)
        {
            CreateAdminWorkflowDefinitionVersionCallCount += 1;
            LastCreateAdminWorkflowDefinitionVersionDefinitionId = definitionId;
            return Task.FromResult(CreatedVersion);
        }

        public Task<WorkflowDefinitionVersionDetailDto?> GetAdminWorkflowDefinitionVersion(long versionId) => throw new NotSupportedException();

        public Task<WorkflowDefinitionVersionDetailDto?> ReplaceAdminWorkflowDefinitionVersion(
            long versionId,
            ReplaceWorkflowDefinitionVersionRequest request)
        {
            ReplaceAdminWorkflowDefinitionVersionCallCount += 1;
            if (ReplaceAdminWorkflowDefinitionVersionException is not null)
            {
                throw ReplaceAdminWorkflowDefinitionVersionException;
            }

            throw new NotSupportedException();
        }

        public Task<WorkflowDefinitionVersionDetailDto?> PublishWorkflowDefinitionVersion(long versionId)
        {
            PublishWorkflowDefinitionVersionCallCount += 1;
            LastPublishWorkflowDefinitionVersionId = versionId;
            return Task.FromResult(PublishedVersion);
        }

        public Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowDefinitionInstance(
            CreateWorkflowDefinitionInstanceRequest request,
            long createdByUserId) => throw new NotSupportedException();

        public Task<WorkflowDefinitionRuntimeDetailDto?> GetWorkflowDefinitionRuntimeDetail(Guid workflowUid) => throw new NotSupportedException();

        public Task<List<WorkflowRuntimeEventDto>> GetWorkflowDefinitionRuntimeEvents(Guid workflowUid) => throw new NotSupportedException();

        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteRuntimeFormNode(
            Guid workflowUid,
            long nodeInstanceId,
            CompleteRuntimeFormNodeRequest request,
            long actorUserId) => throw new NotSupportedException();

        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteRuntimeApprovalNode(
            Guid workflowUid,
            long nodeInstanceId,
            CompleteRuntimeApprovalNodeRequest request,
            long actorUserId) => throw new NotSupportedException();

        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteRuntimeTaskNode(
            Guid workflowUid,
            long nodeInstanceId,
            CompleteRuntimeTaskNodeRequest request,
            long actorUserId) => throw new NotSupportedException();

        public Task<List<AdminProcessTypeDto>> GetAdminProcessTypes() => throw new NotSupportedException();
        public Task<AdminProcessTypeDto?> UpdateProcessType(int processTypeId, AdminProcessTypeUpdateRequest request) => throw new NotSupportedException();
        public Task<List<AdminTaskTemplateDto>> GetAdminTaskTemplates(int processTypeId) => throw new NotSupportedException();
        public Task<AdminTaskTemplateDto> CreateAdminTaskTemplate(AdminTaskTemplateUpsertRequest request) => throw new NotSupportedException();
        public Task<AdminTaskTemplateDto?> UpdateAdminTaskTemplate(int templateId, AdminTaskTemplateUpsertRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminTaskTemplate(int templateId) => throw new NotSupportedException();
        public Task<List<AdminTaskTemplateConditionDto>> GetAdminTaskTemplateConditions(int templateId) => throw new NotSupportedException();
        public Task<AdminTaskTemplateConditionDto> CreateAdminTaskTemplateCondition(int templateId, AdminTaskTemplateConditionCreateRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminTaskTemplateCondition(int templateId, long conditionId) => throw new NotSupportedException();
        public Task<List<AdminTaskTemplateDependencyDto>> GetAdminTaskTemplateDependencies(int templateId) => throw new NotSupportedException();
        public Task<AdminTaskTemplateDependencyDto> CreateAdminTaskTemplateDependency(int templateId, AdminTaskTemplateDependencyCreateRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminTaskTemplateDependency(int templateId, long dependencyId) => throw new NotSupportedException();
        public Task<List<AdminAnswerDefinitionDto>> GetAdminAnswerDefinitions(int processTypeId) => throw new NotSupportedException();
        public Task<AdminAnswerDefinitionDto> CreateAdminAnswerDefinition(AdminAnswerDefinitionUpsertRequest request) => throw new NotSupportedException();
        public Task<AdminAnswerDefinitionDto?> UpdateAdminAnswerDefinition(int definitionId, AdminAnswerDefinitionUpsertRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminAnswerDefinition(int definitionId) => throw new NotSupportedException();
        public Task<List<AdminRoleAnswerDefaultDto>> GetAdminRoleAnswerDefaults(int processTypeId) => throw new NotSupportedException();
        public Task<List<AdminRoleAnswerDefaultDto>> UpsertAdminRoleAnswerDefaults(AdminRoleAnswerDefaultsBulkUpsertRequest request) => throw new NotSupportedException();
        public Task<AdminDependencyGraphDto> GetAdminDependencyGraph(int processTypeId) => throw new NotSupportedException();
    }
}
