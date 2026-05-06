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
    public async Task CreateDefinitionEndpoint_ReturnsCreatedDefinitionWithInitialDraft()
    {
        var repository = new StubWorkflowDefinitionRepository
        {
            CreatedDefinition = new WorkflowDefinitionSummaryDto
            {
                Id = 7,
                Key = "offboarding",
                Name = "Offboarding",
                Description = "Definition",
                Versions =
                [
                    new WorkflowDefinitionVersionSummaryDto
                    {
                        Id = 42,
                        WorkflowDefinitionId = 7,
                        VersionNumber = 1,
                        Status = "draft",
                        Name = null,
                        Description = null,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CanPublish = false,
                        ValidationIssues = new List<ValidationIssueDto>()
                    }
                ]
            }
        };

        var app = CreateApp(repository);
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definitions", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/admin/config/workflow-definitions",
            new CreateWorkflowDefinitionRequest
            {
                Name = "Offboarding",
                Description = "Definition"
            });

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.CreateAdminWorkflowDefinitionCallCount);
        Assert.NotNull(repository.LastCreateAdminWorkflowDefinitionRequest);
        Assert.Equal("Offboarding", repository.LastCreateAdminWorkflowDefinitionRequest!.Name);
        Assert.Null(repository.LastCreateAdminWorkflowDefinitionRequest.Key);
    }

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
    public async Task UpdateDefinitionEndpoint_ReturnsUpdatedDefinition()
    {
        var repository = new StubWorkflowDefinitionRepository
        {
            UpdatedDefinition = new WorkflowDefinitionSummaryDto
            {
                Id = 7,
                Key = "hr-onboarding",
                Name = "HR Onboarding",
                Description = "Updated description",
                Versions = new List<WorkflowDefinitionVersionSummaryDto>()
            }
        };

        var app = CreateApp(repository);
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definitions/{definitionId:int}", HttpMethods.Patch);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Patch,
            "/admin/config/workflow-definitions/7",
            new UpdateWorkflowDefinitionRequest
            {
                Name = "HR Onboarding",
                Description = "Updated description"
            },
            ("definitionId", 7));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.UpdateAdminWorkflowDefinitionCallCount);
        Assert.Equal(7, repository.LastUpdateAdminWorkflowDefinitionId);
    }

    [Fact]
    public async Task DeleteDefinitionEndpoint_ReturnsConflictWhenDefinitionIsInUse()
    {
        var repository = new StubWorkflowDefinitionRepository
        {
            DeleteAdminWorkflowDefinitionException = new InvalidOperationException("Workflow definition is still referenced.")
        };

        var app = CreateApp(repository);
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definitions/{definitionId:int}", HttpMethods.Delete);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Delete,
            "/admin/config/workflow-definitions/7",
            new { },
            ("definitionId", 7));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal(1, repository.DeleteAdminWorkflowDefinitionCallCount);
        Assert.Equal(7, repository.LastDeleteAdminWorkflowDefinitionId);
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
    public async Task ReplaceDefinitionVersionEndpoint_ReturnsConflictWhenStale()
    {
        var currentUpdatedAt = new DateTime(2026, 5, 4, 9, 30, 0, DateTimeKind.Utc);
        var repository = new StubWorkflowDefinitionRepository
        {
            ReplaceAdminWorkflowDefinitionVersionException =
                new WorkflowDefinitionVersionStaleException(currentUpdatedAt)
        };

        var app = CreateApp(repository);
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definition-versions/{versionId:long}", HttpMethods.Put);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Put,
            "/admin/config/workflow-definition-versions/12",
            new ReplaceWorkflowDefinitionVersionRequest
            {
                ExpectedUpdatedAt = currentUpdatedAt.AddMinutes(-5)
            },
            ("versionId", 12L));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal(1, repository.ReplaceAdminWorkflowDefinitionVersionCallCount);
        Assert.Equal(currentUpdatedAt, repository.LastReplaceAdminWorkflowDefinitionVersionRequest!.ExpectedUpdatedAt!.Value.AddMinutes(5));

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(currentUpdatedAt, document.RootElement.GetProperty("currentUpdatedAt").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task ReplaceDefinitionVersionEndpoint_RoundtripsNodePositions()
    {
        var repository = new StubWorkflowDefinitionRepository
        {
            ReplacedVersion = new WorkflowDefinitionVersionDetailDto
            {
                Id = 12,
                WorkflowDefinitionId = 7,
                DefinitionKey = "hr-onboarding",
                DefinitionName = "HR Onboarding",
                DefinitionDescription = "Updated description",
                VersionNumber = 3,
                Status = "draft",
                Name = "Canvas Draft",
                Description = "Roundtrip positions",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PublishedAt = null,
                CanPublish = false,
                ValidationIssues = new List<ValidationIssueDto>(),
                Nodes =
                [
                    new WorkflowDefinitionNodeDto
                    {
                        NodeKey = "start",
                        NodeType = "start",
                        Title = "Start",
                        SortOrder = 1,
                        PositionX = 120,
                        PositionY = 80
                    },
                    new WorkflowDefinitionNodeDto
                    {
                        NodeKey = "end",
                        NodeType = "end",
                        Title = "End",
                        SortOrder = 2,
                        PositionX = 420,
                        PositionY = 80
                    }
                ],
                Edges =
                [
                    new WorkflowDefinitionEdgeDto
                    {
                        SourceNodeKey = "start",
                        TargetNodeKey = "end",
                        Priority = 1
                    }
                ]
            }
        };

        var app = CreateApp(repository);
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definition-versions/{versionId:long}", HttpMethods.Put);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Put,
            "/admin/config/workflow-definition-versions/12",
            new ReplaceWorkflowDefinitionVersionRequest
            {
                Name = "Canvas Draft",
                Description = "Roundtrip positions",
                Nodes =
                [
                    new WorkflowDefinitionNodeDto
                    {
                        NodeKey = "Start",
                        NodeType = "start",
                        Title = "Start",
                        SortOrder = 1,
                        PositionX = 120,
                        PositionY = 80
                    },
                    new WorkflowDefinitionNodeDto
                    {
                        NodeKey = "End",
                        NodeType = "end",
                        Title = "End",
                        SortOrder = 2,
                        PositionX = 420,
                        PositionY = 80
                    }
                ],
                Edges =
                [
                    new WorkflowDefinitionEdgeDto
                    {
                        SourceNodeKey = "Start",
                        TargetNodeKey = "End",
                        Priority = 1
                    }
                ]
            },
            ("versionId", 12L));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.ReplaceAdminWorkflowDefinitionVersionCallCount);
        Assert.NotNull(repository.LastReplaceAdminWorkflowDefinitionVersionRequest);
        Assert.Equal(120, repository.LastReplaceAdminWorkflowDefinitionVersionRequest!.Nodes[0].PositionX);
        Assert.Equal(80, repository.LastReplaceAdminWorkflowDefinitionVersionRequest.Nodes[0].PositionY);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var nodes = document.RootElement.GetProperty("nodes");
        Assert.Equal(120, nodes[0].GetProperty("positionX").GetInt32());
        Assert.Equal(80, nodes[0].GetProperty("positionY").GetInt32());
    }

    [Fact]
    public async Task PublishDefinitionVersionEndpoint_ReturnsPublishedVersion()
    {
        var publishedVersion = new WorkflowDefinitionVersionDetailDto
        {
            Id = 12,
            WorkflowDefinitionId = 7,
            DefinitionKey = "hr-onboarding",
            DefinitionName = "HR Onboarding",
            VersionNumber = 2,
            Status = "published",
            Name = "Published",
            Description = "Go live",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow,
            CanPublish = true,
            ValidationIssues = new List<ValidationIssueDto>(),
            Nodes = new List<WorkflowDefinitionNodeDto>(),
            Edges = new List<WorkflowDefinitionEdgeDto>()
        };

        var repository = new StubWorkflowDefinitionRepository
        {
            VersionDetailForGet = publishedVersion,
            PublishedVersion = publishedVersion
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
        Assert.Equal(1, repository.GetAdminWorkflowDefinitionVersionCallCount);
        Assert.Equal(1, repository.PublishWorkflowDefinitionVersionCallCount);
        Assert.Equal(12L, repository.LastPublishWorkflowDefinitionVersionId);
    }

    [Fact]
    public async Task PublishDefinitionVersionEndpoint_ReturnsBadRequestWhenNotPublishable()
    {
        var repository = new StubWorkflowDefinitionRepository
        {
            VersionDetailForGet = new WorkflowDefinitionVersionDetailDto
            {
                Id = 12,
                WorkflowDefinitionId = 7,
                DefinitionKey = "hr-onboarding",
                DefinitionName = "HR Onboarding",
                VersionNumber = 1,
                Status = "draft",
                Name = "Broken Draft",
                Description = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PublishedAt = null,
                CanPublish = false,
                ValidationIssues =
                [
                    new ValidationIssueDto
                    {
                        Code = "node_not_reachable",
                        Severity = "error",
                        Scope = "workflow_node",
                        Message = "Node 'orphan' is not reachable from the start node."
                    }
                ],
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

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(1, repository.GetAdminWorkflowDefinitionVersionCallCount);
        Assert.Equal(0, repository.PublishWorkflowDefinitionVersionCallCount);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("cannot be published", body);
    }

    [Fact]
    public async Task GetDefinitionsEndpoint_AllowsBuilderUser()
    {
        var repository = new StubWorkflowDefinitionRepository
        {
            WorkflowDefinitions =
            [
                new WorkflowDefinitionSummaryDto
                {
                    Id = 7,
                    Key = "hr-onboarding",
                    Name = "HR Onboarding",
                    Description = "Definition",
                    Versions = new List<WorkflowDefinitionVersionSummaryDto>()
                }
            ]
        };

        var app = CreateApp(repository, CreateBuilderUser());
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definitions", HttpMethods.Get);
        var context = CreateJsonRequestContext(app.Services, endpoint, HttpMethods.Get, "/admin/config/workflow-definitions", new { });

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetAdminWorkflowDefinitionsCallCount);
    }

    [Fact]
    public async Task GetDefinitionVersionEndpoint_AllowsBuilderUser()
    {
        var repository = new StubWorkflowDefinitionRepository
        {
            VersionDetailForGet = CreateDraftVersionDetail()
        };

        var app = CreateApp(repository, CreateBuilderUser());
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definition-versions/{versionId:long}", HttpMethods.Get);
        var context = CreateJsonRequestContext(app.Services, endpoint, HttpMethods.Get, "/admin/config/workflow-definition-versions/12", new { }, ("versionId", 12L));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetAdminWorkflowDefinitionVersionCallCount);
    }

    [Fact]
    public async Task GetWorkingDraftEndpoint_AllowsBuilderUser()
    {
        var repository = new StubWorkflowDefinitionRepository
        {
            WorkingDraftForGet = CreateDraftVersionDetail()
        };

        var app = CreateApp(repository, CreateBuilderUser());
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definitions/{definitionId:int}/working-draft", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/admin/config/workflow-definitions/7/working-draft",
            new { },
            ("definitionId", 7));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.EnsureAdminWorkflowDefinitionWorkingDraftCallCount);
        Assert.Equal(7, repository.LastEnsureAdminWorkflowDefinitionWorkingDraftDefinitionId);
    }

    [Fact]
    public async Task ReplaceDefinitionVersionEndpoint_AllowsBuilderUser_WhenNoAutomationChanges()
    {
        var existingVersion = CreateDraftVersionDetail();
        var repository = new StubWorkflowDefinitionRepository
        {
            VersionDetailForGet = existingVersion,
            ReplacedVersion = existingVersion
        };

        var app = CreateApp(repository, CreateBuilderUser());
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definition-versions/{versionId:long}", HttpMethods.Put);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Put,
            "/admin/config/workflow-definition-versions/12",
            new ReplaceWorkflowDefinitionVersionRequest
            {
                Name = "Draft",
                Description = "Edited",
                Nodes =
                [
                    new WorkflowDefinitionNodeDto
                    {
                        NodeKey = "start",
                        NodeType = "start",
                        Title = "Start",
                        SortOrder = 1
                    },
                    new WorkflowDefinitionNodeDto
                    {
                        NodeKey = "end",
                        NodeType = "end",
                        Title = "End",
                        SortOrder = 2
                    }
                ],
                Edges =
                [
                    new WorkflowDefinitionEdgeDto
                    {
                        SourceNodeKey = "start",
                        TargetNodeKey = "end",
                        Priority = 1
                    }
                ]
            },
            ("versionId", 12L));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.ReplaceAdminWorkflowDefinitionVersionCallCount);
    }

    [Fact]
    public async Task ReplaceDefinitionVersionEndpoint_ForbidsBuilderUser_WhenAutomationActionsChange()
    {
        var existingVersion = CreateAutomationDraftVersionDetail();
        var repository = new StubWorkflowDefinitionRepository
        {
            VersionDetailForGet = existingVersion,
            ReplacedVersion = existingVersion
        };

        var app = CreateApp(repository, CreateBuilderUser());
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definition-versions/{versionId:long}", HttpMethods.Put);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Put,
            "/admin/config/workflow-definition-versions/12",
            new ReplaceWorkflowDefinitionVersionRequest
            {
                Name = "Draft",
                Description = "Edited",
                Nodes =
                [
                    new WorkflowDefinitionNodeDto
                    {
                        NodeKey = "start",
                        NodeType = "start",
                        Title = "Start",
                        SortOrder = 1
                    },
                    new WorkflowDefinitionNodeDto
                    {
                        NodeKey = "automation_step",
                        NodeType = "automation",
                        Title = "Automation",
                        SortOrder = 2,
                        Actions =
                        [
                            new WorkflowNodeActionDto
                            {
                                ActionKey = "CreateMailbox",
                                ExecutionOrder = 1,
                                OnErrorBehavior = "fail_workflow"
                            }
                        ]
                    },
                    new WorkflowDefinitionNodeDto
                    {
                        NodeKey = "end",
                        NodeType = "end",
                        Title = "End",
                        SortOrder = 3
                    }
                ],
                Edges =
                [
                    new WorkflowDefinitionEdgeDto
                    {
                        SourceNodeKey = "start",
                        TargetNodeKey = "automation_step",
                        Priority = 1
                    },
                    new WorkflowDefinitionEdgeDto
                    {
                        SourceNodeKey = "automation_step",
                        TargetNodeKey = "end",
                        Priority = 1
                    }
                ]
            },
            ("versionId", 12L));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal(0, repository.ReplaceAdminWorkflowDefinitionVersionCallCount);
    }

    [Fact]
    public async Task PublishDefinitionVersionEndpoint_ForbidsBuilderUser()
    {
        var repository = new StubWorkflowDefinitionRepository();
        var app = CreateApp(repository, CreateBuilderUser());
        var endpoint = GetEndpoint(app, "/admin/config/workflow-definition-versions/{versionId:long}/publish", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/admin/config/workflow-definition-versions/12/publish",
            new { },
            ("versionId", 12L));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal(0, repository.PublishWorkflowDefinitionVersionCallCount);
    }

    private static WorkflowDefinitionVersionDetailDto CreateDraftVersionDetail()
    {
        return new WorkflowDefinitionVersionDetailDto
        {
            Id = 12,
            WorkflowDefinitionId = 7,
            DefinitionKey = "hr-onboarding",
            DefinitionName = "HR Onboarding",
            VersionNumber = 1,
            Status = "draft",
            Name = "Draft",
            Description = "Draft detail",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CanPublish = false,
            ValidationIssues = new List<ValidationIssueDto>(),
            Nodes =
            [
                new WorkflowDefinitionNodeDto
                {
                    NodeKey = "start",
                    NodeType = "start",
                    Title = "Start",
                    SortOrder = 1
                },
                new WorkflowDefinitionNodeDto
                {
                    NodeKey = "end",
                    NodeType = "end",
                    Title = "End",
                    SortOrder = 2
                }
            ],
            Edges =
            [
                new WorkflowDefinitionEdgeDto
                {
                    SourceNodeKey = "start",
                    TargetNodeKey = "end",
                    Priority = 1
                }
            ]
        };
    }

    private static WorkflowDefinitionVersionDetailDto CreateAutomationDraftVersionDetail()
    {
        return new WorkflowDefinitionVersionDetailDto
        {
            Id = 12,
            WorkflowDefinitionId = 7,
            DefinitionKey = "hr-onboarding",
            DefinitionName = "HR Onboarding",
            VersionNumber = 1,
            Status = "draft",
            Name = "Draft",
            Description = "Draft detail",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CanPublish = false,
            ValidationIssues = new List<ValidationIssueDto>(),
            Nodes =
            [
                new WorkflowDefinitionNodeDto
                {
                    NodeKey = "start",
                    NodeType = "start",
                    Title = "Start",
                    SortOrder = 1
                },
                new WorkflowDefinitionNodeDto
                {
                    NodeKey = "automation_step",
                    NodeType = "automation",
                    Title = "Automation",
                    SortOrder = 2,
                    Actions =
                    [
                        new WorkflowNodeActionDto
                        {
                            ActionKey = "CreateAdUser",
                            ExecutionOrder = 1,
                            OnErrorBehavior = "fail_workflow"
                        }
                    ]
                },
                new WorkflowDefinitionNodeDto
                {
                    NodeKey = "end",
                    NodeType = "end",
                    Title = "End",
                    SortOrder = 3
                }
            ],
            Edges =
            [
                new WorkflowDefinitionEdgeDto
                {
                    SourceNodeKey = "start",
                    TargetNodeKey = "automation_step",
                    Priority = 1
                },
                new WorkflowDefinitionEdgeDto
                {
                    SourceNodeKey = "automation_step",
                    TargetNodeKey = "end",
                    Priority = 1
                }
            ]
        };
    }

    private static WebApplication CreateApp(StubWorkflowDefinitionRepository repository, CurrentUser? user = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IWorkflowRepository>(repository);
        builder.Services.AddSingleton<IWorkflowDefinitionRuntimeRepository>(repository);
        builder.Services.AddSingleton<IUserContext>(new StubUserContext(user ?? CreateAdminUser()));
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

    private static CurrentUser CreateBuilderUser()
    {
        return new CurrentUser
        {
            UserId = 100,
            ExternalKey = "builder.test",
            DisplayName = "Builder Test",
            Email = "builder.test@example.com",
            IsActive = true,
            DepartmentId = null,
            DepartmentName = null,
            IdentityProvider = "test",
            Groups = new List<CurrentUserGroup>(),
            DirectRoles = new List<CurrentUserRole>(),
            GroupRoles = new List<CurrentUserRole>(),
            EffectiveRoles = new List<CurrentUserRole>(),
            EffectivePermissions =
            [
                new CurrentUserPermission
                {
                    PermissionId = 1,
                    PermissionKey = "workflows.create.onboarding",
                    PermissionName = "Workflow create onboarding"
                }
            ],
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
        public WorkflowDefinitionSummaryDto? CreatedDefinition { get; set; }
        public WorkflowDefinitionVersionSummaryDto? CreatedVersion { get; set; }
        public WorkflowDefinitionVersionDetailDto? ReplacedVersion { get; set; }
        public WorkflowDefinitionVersionDetailDto? PublishedVersion { get; set; }
        public WorkflowDefinitionSummaryDto? UpdatedDefinition { get; set; }
        public List<WorkflowDefinitionSummaryDto> WorkflowDefinitions { get; set; } = new();
        public WorkflowDefinitionVersionDetailDto? VersionDetailForGet { get; set; }
        public WorkflowDefinitionVersionDetailDto? WorkingDraftForGet { get; set; }
        public Exception? DeleteAdminWorkflowDefinitionException { get; set; }
        public Exception? ReplaceAdminWorkflowDefinitionVersionException { get; set; }
        public int CreateAdminWorkflowDefinitionCallCount { get; private set; }
        public int CreateAdminWorkflowDefinitionVersionCallCount { get; private set; }
        public int DeleteAdminWorkflowDefinitionCallCount { get; private set; }
        public int EnsureAdminWorkflowDefinitionWorkingDraftCallCount { get; private set; }
        public int UpdateAdminWorkflowDefinitionCallCount { get; private set; }
        public int ReplaceAdminWorkflowDefinitionVersionCallCount { get; private set; }
        public int PublishWorkflowDefinitionVersionCallCount { get; private set; }
        public int GetAdminWorkflowDefinitionsCallCount { get; private set; }
        public int GetAdminWorkflowDefinitionVersionCallCount { get; private set; }
        public CreateWorkflowDefinitionRequest? LastCreateAdminWorkflowDefinitionRequest { get; private set; }
        public int? LastCreateAdminWorkflowDefinitionVersionDefinitionId { get; private set; }
        public int? LastDeleteAdminWorkflowDefinitionId { get; private set; }
        public int? LastEnsureAdminWorkflowDefinitionWorkingDraftDefinitionId { get; private set; }
        public int? LastUpdateAdminWorkflowDefinitionId { get; private set; }
        public long? LastPublishWorkflowDefinitionVersionId { get; private set; }
        public ReplaceWorkflowDefinitionVersionRequest? LastReplaceAdminWorkflowDefinitionVersionRequest { get; private set; }

        public Task<AdminListPageDto<DepartmentDto>> GetDepartments(AdminListQuery query) => throw new NotSupportedException();
        public Task<AdminListPageDto<RoleDto>> GetRoles(AdminListQuery query) => throw new NotSupportedException();
        public Task<List<WorkflowStartableDefinitionDto>> GetStartableWorkflowDefinitions() => throw new NotSupportedException();
        public Task<List<RequirementDto>> GetRequirements(string workflowDefinitionKey) => throw new NotSupportedException();
        public Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId, string workflowDefinitionKey) => throw new NotSupportedException();
        public Task<bool> IsManagerCreatableDefinition(string workflowDefinitionKey) => throw new NotSupportedException();
        public Task<IReadOnlySet<string>> GetManagerCreatableDefinitionKeys() => throw new NotSupportedException();
        public Task<WorkflowCreationResult> CreateWorkflow(CreateWorkflowRequest request, long createdByUserId) => throw new NotSupportedException();
        public Task<WorkflowTargetPersonDto> CreatePerson(CreatePersonRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<WorkflowDetailDto?> CompleteSupervisorStep(Guid workflowUid, IReadOnlyList<RequirementSelectionInputDto> selections, long actorUserId) => throw new NotSupportedException();
        public Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotifications(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowCompletionNotifications(Guid workflowUid) => throw new NotSupportedException();
        public Task ApplyNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results) => throw new NotSupportedException();
        public Task<List<WorkflowListItemDto>> GetWorkflows() => throw new NotSupportedException();
        public Task<WorkflowListResult> GetFilteredWorkflows(WorkflowListQuery query) => throw new NotSupportedException();
        public Task<WorkflowDetailDto?> GetWorkflowByUid(Guid workflowUid) => throw new NotSupportedException();
        public Task<HashSet<int>> GetRequirementSelectionDepartmentIds(long userId) => throw new NotSupportedException();
        public Task<List<TaskWithWorkflowDto>> GetTasks() => throw new NotSupportedException();
        public Task<List<TaskWithWorkflowDto>> GetTasksForUser(long userId, int[] effectiveResponsibilityIds) => throw new NotSupportedException();
        public Task<List<TaskWithWorkflowDto>> GetTasksForUserNarrowed(long userId, int[] effectiveResponsibilityIds) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> GetTaskById(long taskId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> GetTaskByRef(string taskRef) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> UpdateTaskStatusByRef(string taskRef, string status, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> DecideTaskApprovalByRef(string taskRef, TaskApprovalDecisionRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> UpdateTaskAssignment(long taskId, TaskAssignRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> UpdateTaskAssignmentByRef(string taskRef, TaskAssignRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> AddTaskComment(long taskId, string commentText, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> AddTaskCommentByRef(string taskRef, string commentText, long actorUserId) => throw new NotSupportedException();
        public Task<bool> ArchiveWorkflow(Guid workflowUid, long actorUserId) => throw new NotSupportedException();
        public Task<bool> DeleteDraftWorkflow(Guid workflowUid) => throw new NotSupportedException();
        public Task<PersonWorkflowHistoryDto?> GetPersonWorkflowHistory(long personId) => throw new NotSupportedException();
        public Task<List<WorkflowLinkDto>> GetWorkflowLinks(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<RelatedWorkflowSummaryDto>> GetRelatedWorkflows(Guid workflowUid) => throw new NotSupportedException();
        public Task<WorkflowLinkDto?> CreateWorkflowLink(Guid targetWorkflowUid, CreateWorkflowLinkRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<bool> DeleteWorkflowLink(Guid workflowUid, long linkId, long actorUserId) => throw new NotSupportedException();
        public Task<List<WorkflowTargetPersonSourceDto>> SearchWorkflowTargetPersonSources(string? search, int limit = 20, IReadOnlyCollection<int>? observableDepartmentIds = null) => throw new NotSupportedException();
        public Task<List<WorkflowTargetPersonDto>> SearchWorkflowTargetPeople(string? query, int limit = 20, IReadOnlyCollection<int>? observableDepartmentIds = null) => throw new NotSupportedException();
        public Task<List<WorkflowTargetPersonDto>> SearchRotationEligiblePeople(string? query, int limit = 20, IReadOnlyCollection<int>? observableDepartmentIds = null) => throw new NotSupportedException();
        public Task ApplyPersonLifecycleProjection(Guid workflowUid, long? actorUserId = null) => throw new NotSupportedException();
        public Task<List<LinkableWorkflowDto>> FindLinkableWorkflows(int employeeNumber, Guid? excludeWorkflowUid = null) => throw new NotSupportedException();
        public Task<List<DerivedAnswerDto>> GetDerivedAnswers(Guid sourceWorkflowUid, string targetWorkflowDefinitionKey) => throw new NotSupportedException();
        public Task<AdminListPageDto<WorkflowDefinitionSummaryDto>> GetAdminWorkflowDefinitions(AdminListQuery query)
        {
            GetAdminWorkflowDefinitionsCallCount += 1;
            return Task.FromResult(new AdminListPageDto<WorkflowDefinitionSummaryDto>
            {
                Items = WorkflowDefinitions,
                Total = WorkflowDefinitions.Count,
                Limit = query.Limit,
                Offset = query.Offset
            });
        }
        public Task<WorkflowDefinitionSummaryDto> CreateAdminWorkflowDefinition(CreateWorkflowDefinitionRequest request)
        {
            CreateAdminWorkflowDefinitionCallCount += 1;
            LastCreateAdminWorkflowDefinitionRequest = request;
            return Task.FromResult(CreatedDefinition ?? throw new NotSupportedException());
        }
        public Task<WorkflowDefinitionSummaryDto?> UpdateAdminWorkflowDefinition(int definitionId, UpdateWorkflowDefinitionRequest request)
        {
            UpdateAdminWorkflowDefinitionCallCount += 1;
            LastUpdateAdminWorkflowDefinitionId = definitionId;
            return Task.FromResult(UpdatedDefinition);
        }
        public Task<bool> DeleteAdminWorkflowDefinition(int definitionId)
        {
            DeleteAdminWorkflowDefinitionCallCount += 1;
            LastDeleteAdminWorkflowDefinitionId = definitionId;
            if (DeleteAdminWorkflowDefinitionException is not null)
            {
                throw DeleteAdminWorkflowDefinitionException;
            }

            return Task.FromResult(true);
        }

        public Task<WorkflowDefinitionVersionSummaryDto?> CreateAdminWorkflowDefinitionVersion(
            int definitionId,
            CreateWorkflowDefinitionVersionRequest request)
        {
            CreateAdminWorkflowDefinitionVersionCallCount += 1;
            LastCreateAdminWorkflowDefinitionVersionDefinitionId = definitionId;
            return Task.FromResult(CreatedVersion);
        }

        public Task<WorkflowDefinitionVersionDetailDto?> GetAdminWorkflowDefinitionVersion(long versionId)
        {
            GetAdminWorkflowDefinitionVersionCallCount += 1;
            return Task.FromResult(VersionDetailForGet);
        }
        public Task<WorkflowDefinitionVersionDetailDto?> EnsureAdminWorkflowDefinitionWorkingDraft(int definitionId)
        {
            EnsureAdminWorkflowDefinitionWorkingDraftCallCount += 1;
            LastEnsureAdminWorkflowDefinitionWorkingDraftDefinitionId = definitionId;
            return Task.FromResult(WorkingDraftForGet);
        }

        public Task<WorkflowDefinitionVersionDetailDto?> ReplaceAdminWorkflowDefinitionVersion(
            long versionId,
            ReplaceWorkflowDefinitionVersionRequest request)
        {
            ReplaceAdminWorkflowDefinitionVersionCallCount += 1;
            LastReplaceAdminWorkflowDefinitionVersionRequest = request;
            if (ReplaceAdminWorkflowDefinitionVersionException is not null)
            {
                throw ReplaceAdminWorkflowDefinitionVersionException;
            }

            return Task.FromResult(ReplacedVersion);
        }

        public Task<WorkflowDefinitionVersionDetailDto?> PublishWorkflowDefinitionVersion(long versionId)
        {
            PublishWorkflowDefinitionVersionCallCount += 1;
            LastPublishWorkflowDefinitionVersionId = versionId;
            return Task.FromResult(PublishedVersion);
        }

        public Task<WorkflowDefinitionRuntimeDetailDto?> GetWorkflowDefinitionRuntimeDetail(Guid workflowUid) => throw new NotSupportedException();

        public Task<List<WorkflowRuntimeEventDto>> GetWorkflowDefinitionRuntimeEvents(Guid workflowUid) => throw new NotSupportedException();

        public Task<AdminListPageDto<AdminTaskTemplateDto>> GetAdminTaskTemplates(int workflowDefinitionId, AdminListQuery query) => throw new NotSupportedException();
        public Task<AdminTaskTemplateDto> CreateAdminTaskTemplate(AdminTaskTemplateUpsertRequest request) => throw new NotSupportedException();
        public Task<AdminTaskTemplateDto?> UpdateAdminTaskTemplate(int templateId, AdminTaskTemplateUpsertRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminTaskTemplate(int templateId) => throw new NotSupportedException();
        public Task<AdminListPageDto<AdminTaskTemplateConditionDto>> GetAdminTaskTemplateConditions(int templateId, AdminListQuery query) => throw new NotSupportedException();
        public Task<AdminTaskTemplateConditionDto> CreateAdminTaskTemplateCondition(int templateId, AdminTaskTemplateConditionCreateRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminTaskTemplateCondition(int templateId, long conditionId) => throw new NotSupportedException();
        public Task<AdminListPageDto<AdminTaskTemplateDependencyDto>> GetAdminTaskTemplateDependencies(int templateId, AdminListQuery query) => throw new NotSupportedException();
        public Task<AdminTaskTemplateDependencyDto> CreateAdminTaskTemplateDependency(int templateId, AdminTaskTemplateDependencyCreateRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminTaskTemplateDependency(int templateId, long dependencyId) => throw new NotSupportedException();
        public Task<AdminListPageDto<AdminAnswerDefinitionDto>> GetAdminAnswerDefinitions(int workflowDefinitionId, AdminListQuery query) => throw new NotSupportedException();
        public Task<AdminAnswerDefinitionDto> CreateAdminAnswerDefinition(AdminAnswerDefinitionUpsertRequest request) => throw new NotSupportedException();
        public Task<AdminAnswerDefinitionDto?> UpdateAdminAnswerDefinition(int definitionId, AdminAnswerDefinitionUpsertRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminAnswerDefinition(int definitionId) => throw new NotSupportedException();
        public Task<AdminListPageDto<AdminRoleAnswerDefaultDto>> GetAdminRoleAnswerDefaults(int workflowDefinitionId, AdminListQuery query) => throw new NotSupportedException();
        public Task<List<AdminRoleAnswerDefaultDto>> UpsertAdminRoleAnswerDefaults(AdminRoleAnswerDefaultsBulkUpsertRequest request) => throw new NotSupportedException();
        public Task<AdminDependencyGraphDto> GetAdminDependencyGraph(int workflowDefinitionId) => throw new NotSupportedException();
    }
}
