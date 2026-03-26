using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace API.Tests;

public sealed class WorkflowEndpointsTests
{
    [Fact]
    public async Task AuditLogEndpoint_RejectsNonPositiveLimit()
    {
        var repository = new StubWorkflowRepository();
        var app = CreateApp(repository);
        var endpoint = GetAuditLogEndpoint(app);
        var workflowUid = Guid.NewGuid();
        var context = CreateRequestContext(app.Services, endpoint, workflowUid, "?limit=0");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(0, repository.GetWorkflowByUidCallCount);
        Assert.Equal(0, repository.GetWorkflowAuditLogCallCount);
    }

    [Fact]
    public async Task AuditLogEndpoint_RejectsNegativeOffset()
    {
        var repository = new StubWorkflowRepository();
        var app = CreateApp(repository);
        var endpoint = GetAuditLogEndpoint(app);
        var workflowUid = Guid.NewGuid();
        var context = CreateRequestContext(app.Services, endpoint, workflowUid, "?offset=-1");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(0, repository.GetWorkflowByUidCallCount);
        Assert.Equal(0, repository.GetWorkflowAuditLogCallCount);
    }

    [Fact]
    public async Task AuditLogEndpoint_CapsLimitAndPassesOffsetToRepository()
    {
        var workflowUid = Guid.NewGuid();
        var repository = new StubWorkflowRepository
        {
            Workflow = CreateWorkflowDetail(workflowUid),
            AuditEntries = new List<WorkflowAuditEntryDto>()
        };

        var app = CreateApp(repository);
        var endpoint = GetAuditLogEndpoint(app);
        var context = CreateRequestContext(app.Services, endpoint, workflowUid, "?limit=999&offset=7");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetWorkflowByUidCallCount);
        Assert.Equal(1, repository.GetWorkflowAuditLogCallCount);
        Assert.Equal(500, repository.LastAuditLogLimit);
        Assert.Equal(7, repository.LastAuditLogOffset);
    }

    [Fact]
    public async Task CreateWorkflowLinkEndpoint_RejectsInvalidLinkType()
    {
        var repository = new StubWorkflowRepository();
        var app = CreateApp(repository);
        var endpoint = GetWorkflowEndpoint(app, "/workflows/{uid:guid}/links", HttpMethods.Post);
        var workflowUid = Guid.NewGuid();
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            $"/workflows/{workflowUid}/links",
            new CreateWorkflowLinkRequest
            {
                SourceWorkflowUid = Guid.NewGuid(),
                LinkType = "invalid"
            },
            ("uid", workflowUid));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(0, repository.CreateWorkflowLinkCallCount);
    }

    [Fact]
    public async Task RolesEndpoint_AllowsAdminOnlyUser()
    {
        var repository = new StubWorkflowRepository
        {
            Roles =
            [
                new RoleDto
                {
                    Id = 5,
                    DepartmentId = 2,
                    DepartmentName = "HR",
                    Name = "Sachbearbeitung",
                    IsActive = true
                }
            ]
        };

        var app = CreateApp(repository, CreateUser(AuthorizationRoles.Admin));
        var endpoint = GetWorkflowEndpoint(app, "/roles", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/roles",
            "");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetRolesCallCount);
    }

    [Fact]
    public async Task CreateWorkflowLinkEndpoint_ReturnsCreatedAndPassesActorToRepository()
    {
        var targetWorkflowUid = Guid.NewGuid();
        var sourceWorkflowUid = Guid.NewGuid();
        var repository = new StubWorkflowRepository
        {
            CreatedWorkflowLink = new WorkflowLinkDto
            {
                Id = 42,
                SourceWorkflowUid = sourceWorkflowUid,
                TargetWorkflowUid = targetWorkflowUid,
                LinkType = "derived_from",
                LinkedWorkflowFirstName = "Ada",
                LinkedWorkflowLastName = "Lovelace",
                LinkedWorkflowProcessType = new WorkflowProcessTypeDto
                {
                    Key = "onboarding",
                    Name = "Onboarding",
                    RequiresTargetPerson = false
                },
                LinkedWorkflowStatus = "draft",
                LinkedWorkflowCreatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            }
        };

        var app = CreateApp(repository);
        var endpoint = GetWorkflowEndpoint(app, "/workflows/{uid:guid}/links", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            $"/workflows/{targetWorkflowUid}/links",
            new CreateWorkflowLinkRequest
            {
                SourceWorkflowUid = sourceWorkflowUid,
                LinkType = "derived_from",
                Notes = "Auto link"
            },
            ("uid", targetWorkflowUid));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
        Assert.Equal(1, repository.CreateWorkflowLinkCallCount);
        Assert.Equal(targetWorkflowUid, repository.LastCreateWorkflowLinkTargetWorkflowUid);
        Assert.Equal(sourceWorkflowUid, repository.LastCreateWorkflowLinkRequest!.SourceWorkflowUid);
        Assert.Equal("derived_from", repository.LastCreateWorkflowLinkRequest.LinkType);
        Assert.Equal(99, repository.LastCreateWorkflowLinkActorUserId);
    }

    [Fact]
    public async Task CreateWorkflowLinkEndpoint_AllowsAdminOnlyUser()
    {
        var targetWorkflowUid = Guid.NewGuid();
        var sourceWorkflowUid = Guid.NewGuid();
        var repository = new StubWorkflowRepository
        {
            CreatedWorkflowLink = new WorkflowLinkDto
            {
                Id = 42,
                SourceWorkflowUid = sourceWorkflowUid,
                TargetWorkflowUid = targetWorkflowUid,
                LinkType = "derived_from",
                LinkedWorkflowFirstName = "Ada",
                LinkedWorkflowLastName = "Lovelace",
                LinkedWorkflowProcessType = new WorkflowProcessTypeDto
                {
                    Key = "onboarding",
                    Name = "Onboarding",
                    RequiresTargetPerson = false
                },
                LinkedWorkflowStatus = "draft",
                LinkedWorkflowCreatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            }
        };

        var app = CreateApp(repository, CreateUser(AuthorizationRoles.Admin));
        var endpoint = GetWorkflowEndpoint(app, "/workflows/{uid:guid}/links", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            $"/workflows/{targetWorkflowUid}/links",
            new CreateWorkflowLinkRequest
            {
                SourceWorkflowUid = sourceWorkflowUid,
                LinkType = "derived_from"
            },
            ("uid", targetWorkflowUid));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
        Assert.Equal(1, repository.CreateWorkflowLinkCallCount);
    }

    [Fact]
    public async Task FindLinkableWorkflowsEndpoint_ParsesExcludeUidAndPassesItToRepository()
    {
        var excludeUid = Guid.NewGuid();
        var repository = new StubWorkflowRepository
        {
            LinkableWorkflows = new List<LinkableWorkflowDto>()
        };

        var app = CreateApp(repository);
        var endpoint = GetWorkflowEndpoint(app, "/workflows/linkable", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/workflows/linkable",
            $"?employeeNumber=12345&excludeUid={excludeUid}");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.FindLinkableWorkflowsCallCount);
        Assert.Equal(12345, repository.LastFindLinkableWorkflowsEmployeeNumber);
        Assert.Equal(excludeUid, repository.LastFindLinkableWorkflowsExcludeUid);
    }

    [Fact]
    public async Task DeriveAnswersEndpoint_RejectsInvalidSourceUid()
    {
        var repository = new StubWorkflowRepository();
        var app = CreateApp(repository);
        var endpoint = GetWorkflowEndpoint(app, "/workflows/derive-answers", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/workflows/derive-answers",
            "?sourceUid=not-a-guid&targetProcessTypeKey=offboarding");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(0, repository.GetDerivedAnswersCallCount);
    }

    [Fact]
    public async Task DeriveAnswersEndpoint_PassesTrimmedTargetProcessTypeKeyToRepository()
    {
        var sourceUid = Guid.NewGuid();
        var repository = new StubWorkflowRepository
        {
            DerivedAnswers = new List<DerivedAnswerDto>()
        };

        var app = CreateApp(repository);
        var endpoint = GetWorkflowEndpoint(app, "/workflows/derive-answers", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/workflows/derive-answers",
            $"?sourceUid={sourceUid}&targetProcessTypeKey=%20offboarding%20");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetDerivedAnswersCallCount);
        Assert.Equal(sourceUid, repository.LastGetDerivedAnswersSourceWorkflowUid);
        Assert.Equal("offboarding", repository.LastGetDerivedAnswersTargetProcessTypeKey);
    }

    [Fact]
    public async Task DeleteWorkflowLinkEndpoint_PassesPerspectiveWorkflowUidToRepository()
    {
        var workflowUid = Guid.NewGuid();
        var repository = new StubWorkflowRepository
        {
            DeleteWorkflowLinkResult = true
        };

        var app = CreateApp(repository);
        var endpoint = GetWorkflowEndpoint(app, "/workflows/{uid:guid}/links/{linkId:long}", HttpMethods.Delete);
        var context = CreateDeleteRequestContext(
            app.Services,
            endpoint,
            $"/workflows/{workflowUid}/links/77",
            ("uid", workflowUid),
            ("linkId", 77));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal(1, repository.DeleteWorkflowLinkCallCount);
        Assert.Equal(workflowUid, repository.LastDeleteWorkflowLinkWorkflowUid);
        Assert.Equal(77, repository.LastDeleteWorkflowLinkId);
        Assert.Equal(99, repository.LastDeleteWorkflowLinkActorUserId);
    }

    [Fact]
    public async Task DeleteWorkflowLinkEndpoint_AllowsAdminOnlyUser()
    {
        var workflowUid = Guid.NewGuid();
        var repository = new StubWorkflowRepository
        {
            DeleteWorkflowLinkResult = true
        };

        var app = CreateApp(repository, CreateUser(AuthorizationRoles.Admin));
        var endpoint = GetWorkflowEndpoint(app, "/workflows/{uid:guid}/links/{linkId:long}", HttpMethods.Delete);
        var context = CreateDeleteRequestContext(
            app.Services,
            endpoint,
            $"/workflows/{workflowUid}/links/77",
            ("uid", workflowUid),
            ("linkId", 77));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal(1, repository.DeleteWorkflowLinkCallCount);
    }

    [Fact]
    public async Task GetWorkflowLinksEndpoint_RejectsInvisibleWorkflowForReader()
    {
        var workflowUid = Guid.NewGuid();
        var repository = new StubWorkflowRepository
        {
            Workflow = CreateWorkflowDetail(workflowUid),
            WorkflowLinks = new List<WorkflowLinkDto>()
        };

        var app = CreateApp(repository, CreateUser(AuthorizationRoles.Reader));
        var endpoint = GetWorkflowEndpoint(app, "/workflows/{uid:guid}/links", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            $"/workflows/{workflowUid}/links",
            "",
            ("uid", workflowUid));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal(1, repository.GetWorkflowByUidCallCount);
        Assert.Equal(0, repository.GetWorkflowLinksCallCount);
    }

    [Theory]
    [InlineData(AuthorizationRoles.Admin)]
    [InlineData(AuthorizationRoles.Manager)]
    [InlineData(AuthorizationRoles.Reader)]
    public async Task ProcessTypesEndpoint_AllowsWorkflowOverviewRoles(string roleKey)
    {
        var repository = new StubWorkflowRepository
        {
            ActiveProcessTypes =
            [
                new WorkflowProcessTypeDto
                {
                    Key = "onboarding",
                    Name = "Onboarding",
                    RequiresTargetPerson = false
                }
            ]
        };

        var app = CreateApp(repository, CreateUser(roleKey));
        var endpoint = GetWorkflowEndpoint(app, "/process-types", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/process-types",
            "");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetActiveProcessTypesCallCount);
    }

    [Fact]
    public async Task BulkDepartmentChangeEndpoint_RejectsIdenticalDepartments()
    {
        var repository = new StubWorkflowRepository();
        var app = CreateApp(repository);
        var endpoint = GetWorkflowEndpoint(app, "/admin/bulk/department-change", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/admin/bulk/department-change",
            new BulkDepartmentChangeRequest
            {
                SourceDepartmentId = 7,
                TargetDepartmentId = 7,
                TargetRoleId = 2,
                DryRun = true
            });

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(0, repository.BulkCreateDepartmentChangeWorkflowsCallCount);
    }

    [Fact]
    public async Task BulkDepartmentChangeEndpoint_PassesActorToRepository()
    {
        var repository = new StubWorkflowRepository
        {
            BulkOperationResult = new BulkOperationResultDto
            {
                TotalEmployees = 3,
                CreatedWorkflows = 2,
                SkippedEmployees = 1,
                FailedEmployees = 0,
                IsDryRun = false,
                Items = new List<BulkOperationItemDto>()
            }
        };

        var app = CreateApp(repository);
        var endpoint = GetWorkflowEndpoint(app, "/admin/bulk/department-change", HttpMethods.Post);
        var request = new BulkDepartmentChangeRequest
        {
            SourceDepartmentId = 1,
            TargetDepartmentId = 2,
            TargetRoleId = 9,
            DryRun = false
        };
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/admin/bulk/department-change",
            request);

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.BulkCreateDepartmentChangeWorkflowsCallCount);
        Assert.Equal(99, repository.LastBulkActorUserId);
        Assert.Equal(request.SourceDepartmentId, repository.LastBulkRequest!.SourceDepartmentId);
        Assert.Equal(request.TargetDepartmentId, repository.LastBulkRequest.TargetDepartmentId);
        Assert.Equal(request.TargetRoleId, repository.LastBulkRequest.TargetRoleId);
    }

    [Fact]
    public async Task BulkDepartmentChangeEndpoint_DispatchesWorkflowCreatedNotifications_ForCreatedWorkflows()
    {
        var workflowUid = Guid.NewGuid();
        var repository = new StubWorkflowRepository
        {
            BulkOperationResult = new BulkOperationResultDto
            {
                TotalEmployees = 1,
                CreatedWorkflows = 1,
                SkippedEmployees = 0,
                FailedEmployees = 0,
                IsDryRun = false,
                Items =
                [
                    new BulkOperationItemDto
                    {
                        PersonId = 1,
                        DisplayName = "Ada Lovelace",
                        Status = "created",
                        WorkflowUid = workflowUid
                    }
                ]
            }
        };
        repository.WorkflowCreatedNotificationTargetsByUid[workflowUid] =
        [
            new WorkflowNotificationDispatchTarget
            {
                NotificationId = 17,
                NotificationType = "workflow_created",
                ProcessTypeKey = "department_change",
                ProcessTypeName = "Abteilungswechsel",
                WorkflowTaskId = null,
                RecipientUserId = 99,
                RecipientIdentityKey = "admin.test",
                TargetName = "Admin Test",
                TargetEmail = "admin.test@example.com",
                TaskTitle = null,
                PreferredPath = "/workflows"
            }
        ];

        var app = CreateApp(repository);
        var endpoint = GetWorkflowEndpoint(app, "/admin/bulk/department-change", HttpMethods.Post);
        var request = new BulkDepartmentChangeRequest
        {
            SourceDepartmentId = 1,
            TargetDepartmentId = 2,
            TargetRoleId = 9,
            DryRun = false
        };
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/admin/bulk/department-change",
            request);

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetWorkflowCreatedNotificationDispatchTargetsCallCount);
        Assert.Equal(1, repository.ApplyNotificationDispatchResultsCallCount);
    }

    [Fact]
    public async Task GetAdminProcessTypesEndpoint_ReturnsRepositoryData()
    {
        var repository = new StubWorkflowRepository
        {
            AdminProcessTypes = new List<AdminProcessTypeDto>
            {
                CreateAdminProcessTypeDto(1, "offboarding", false, false, "Keine aktiven Anforderungen konfiguriert.")
            }
        };

        var app = CreateApp(repository);
        var endpoint = GetWorkflowEndpoint(app, "/admin/config/process-types", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/admin/config/process-types",
            "");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetAdminProcessTypesCallCount);
    }

    [Fact]
    public async Task UpdateProcessTypeEndpoint_ReturnsBadRequestForDomainErrors()
    {
        var repository = new StubWorkflowRepository
        {
            UpdateProcessTypeException = new InvalidOperationException("Aktivierung blockiert.")
        };

        var app = CreateApp(repository);
        var endpoint = GetWorkflowEndpoint(app, "/admin/config/process-types/{processTypeId:int}", HttpMethods.Patch);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Patch,
            "/admin/config/process-types/5",
            new AdminProcessTypeUpdateRequest
            {
                IsActive = true
            },
            ("processTypeId", 5));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(1, repository.UpdateProcessTypeCallCount);
        Assert.Equal(5, repository.LastUpdateProcessTypeId);
        Assert.True(repository.LastUpdateProcessTypeRequest!.IsActive);
    }

    private static WebApplication CreateApp(StubWorkflowRepository repository, CurrentUser? user = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IWorkflowRepository>(repository);
        builder.Services.AddSingleton<IUserAuthorizationRepository, StubUserAuthorizationRepository>();
        builder.Services.AddSingleton<IUserContext>(new StubUserContext(user ?? CreateAdminHrUser()));
        builder.Services.AddSingleton<IAuthorizationPolicyService, AuthorizationPolicyService>();
        builder.Services.AddSingleton<IWorkflowEmailNotificationSender, StubWorkflowEmailNotificationSender>();
        builder.Services.AddSingleton<INotificationEmailTestSender, StubNotificationEmailTestSender>();
        builder.Services.AddSingleton<INotificationEmailConfigurationService, StubNotificationEmailConfigurationService>();
        builder.Services.AddSingleton<ISupervisorStepService, StubSupervisorStepService>();

        var app = builder.Build();
        app.MapWorkflowEndpoints();
        app.MapAdminEndpoints();
        return app;
    }

    private static RouteEndpoint GetAuditLogEndpoint(WebApplication app)
    {
        return GetWorkflowEndpoint(app, "/workflows/{uid:guid}/audit-log", HttpMethods.Get);
    }

    private static RouteEndpoint GetWorkflowEndpoint(WebApplication app, string routePattern, string httpMethod)
    {
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint =>
                endpoint.RoutePattern.RawText == routePattern
                && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(httpMethod) == true);
    }

    private static DefaultHttpContext CreateRequestContext(
        IServiceProvider services,
        RouteEndpoint endpoint,
        Guid workflowUid,
        string queryString)
    {
        return CreateGetRequestContext(
            services,
            endpoint,
            $"/workflows/{workflowUid}/audit-log",
            queryString,
            ("uid", workflowUid));
    }

    private static DefaultHttpContext CreateGetRequestContext(
        IServiceProvider services,
        RouteEndpoint endpoint,
        string path,
        string queryString,
        params (string Key, object Value)[] routeValues)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(queryString);
        foreach (var (key, value) in routeValues)
        {
            context.Request.RouteValues[key] = value.ToString();
        }
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        return context;
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

    private static DefaultHttpContext CreateDeleteRequestContext(
        IServiceProvider services,
        RouteEndpoint endpoint,
        string path,
        params (string Key, object Value)[] routeValues)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Method = HttpMethods.Delete;
        context.Request.Path = path;
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

    private static WorkflowDetailDto CreateWorkflowDetail(Guid workflowUid)
    {
        return new WorkflowDetailDto
        {
            Uid = workflowUid,
            FirstName = "Audit",
            LastName = "Workflow",
            EmployeeNumber = 1001,
            BadgeNumber = 2001,
            DepartmentId = 1,
            DepartmentName = "IT",
            ProcessType = new WorkflowProcessTypeDto
            {
                Key = "onboarding",
                Name = "Onboarding",
                RequiresTargetPerson = false
            },
            RoleId = 1,
            RoleName = "Developer",
            Status = "open",
            WorkflowStatus = "draft",
            CreatedAt = DateTime.UtcNow,
            DeadlineDate = null,
            Requirements = new List<WorkflowRequirementSnapshotDto>(),
            RequirementSummary = new WorkflowRequirementSummaryDto
            {
                TotalCount = 0,
                VisibleCount = 0,
                AnsweredVisibleCount = 0,
                PendingVisibleCount = 0
            },
            Tasks = new List<WorkflowTaskDto>(),
            TaskMetrics = new WorkflowTaskMetricsDto
            {
                Overall = new WorkflowTaskCountSummaryDto
                {
                    TotalCount = 0,
                    OpenCount = 0,
                    InProgressCount = 0,
                    DoneCount = 0,
                    CompletedCount = 0,
                    ActiveCount = 0
                },
                DepartmentPhase = new WorkflowTaskCountSummaryDto
                {
                    TotalCount = 0,
                    OpenCount = 0,
                    InProgressCount = 0,
                    DoneCount = 0,
                    CompletedCount = 0,
                    ActiveCount = 0
                }
            },
            TaskAreas = new List<WorkflowTaskAreaSummaryDto>(),
            Notifications = new List<WorkflowNotificationDto>()
        };
    }

    private static AdminProcessTypeDto CreateAdminProcessTypeDto(
        int id,
        string key,
        bool isActive,
        bool canActivate,
        string? activationBlockedReason)
    {
        return new AdminProcessTypeDto
        {
            Id = id,
            Key = key,
            Name = key,
            Description = null,
            RequiresSupervisorStep = false,
            ApprovalTaskTemplateKey = null,
            RequiresTargetPerson = true,
            IconKey = null,
            IsActive = isActive,
            SortOrder = id,
            WorkflowCount = 0,
            AnswerDefinitionCount = 0,
            TaskTemplateCount = 0,
            CanActivate = canActivate,
            ActivationBlockedReason = activationBlockedReason
        };
    }

    private static CurrentUser CreateAdminHrUser()
    {
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
            DirectRoles = new List<CurrentUserRole>(),
            GroupRoles = new List<CurrentUserRole>(),
            EffectiveRoles = new List<CurrentUserRole>
            {
                new()
                {
                    RoleId = 1,
                    RoleKey = AuthorizationRoles.Admin,
                    RoleName = "Admin",
                    RoleKind = "system",
                    AssignmentSource = "test",
                    GroupId = null,
                    GroupKey = null
                },
                new()
                {
                    RoleId = 2,
                    RoleKey = AuthorizationRoles.Hr,
                    RoleName = "HR",
                    RoleKind = "system",
                    AssignmentSource = "test",
                    GroupId = null,
                    GroupKey = null
                }
            },
            DirectResponsibilities = new List<CurrentUserResponsibility>(),
            GroupResponsibilities = new List<CurrentUserResponsibility>(),
            EffectiveResponsibilities = new List<CurrentUserResponsibility>()
        };
    }

    private static CurrentUser CreateUser(params string[] roleKeys)
    {
        var roles = roleKeys
            .Select((roleKey, index) => new CurrentUserRole
            {
                RoleId = index + 1,
                RoleKey = roleKey,
                RoleName = roleKey,
                RoleKind = AuthorizationRoles.SystemRoleKind,
                AssignmentSource = "test",
                GroupId = null,
                GroupKey = null
            })
            .ToList();

        return new CurrentUser
        {
            UserId = 99,
            ExternalKey = "test.user",
            DisplayName = "Test User",
            Email = "test.user@example.com",
            IsActive = true,
            DepartmentId = null,
            DepartmentName = null,
            IdentityProvider = "test",
            Groups = new List<CurrentUserGroup>(),
            DirectRoles = roles,
            GroupRoles = new List<CurrentUserRole>(),
            EffectiveRoles = roles,
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

    private sealed class StubWorkflowRepository : IWorkflowRepository
    {
        public WorkflowDetailDto? Workflow { get; set; }
        public List<WorkflowAuditEntryDto> AuditEntries { get; set; } = new();
        public List<RoleDto> Roles { get; set; } = new();
        public WorkflowLinkDto? CreatedWorkflowLink { get; set; }
        public List<WorkflowLinkDto> WorkflowLinks { get; set; } = new();
        public List<LinkableWorkflowDto> LinkableWorkflows { get; set; } = new();
        public List<DerivedAnswerDto> DerivedAnswers { get; set; } = new();
        public BulkOperationResultDto? BulkOperationResult { get; set; }
        public List<AdminProcessTypeDto> AdminProcessTypes { get; set; } = new();
        public AdminProcessTypeDto? UpdatedProcessType { get; set; }
        public Exception? UpdateProcessTypeException { get; set; }
        public bool DeleteWorkflowLinkResult { get; set; }
        public List<WorkflowProcessTypeDto> ActiveProcessTypes { get; set; } = new();
        public int GetWorkflowByUidCallCount { get; private set; }
        public int GetWorkflowAuditLogCallCount { get; private set; }
        public int GetRolesCallCount { get; private set; }
        public int CreateWorkflowLinkCallCount { get; private set; }
        public int GetWorkflowLinksCallCount { get; private set; }
        public int FindLinkableWorkflowsCallCount { get; private set; }
        public int GetDerivedAnswersCallCount { get; private set; }
        public int DeleteWorkflowLinkCallCount { get; private set; }
        public int BulkCreateDepartmentChangeWorkflowsCallCount { get; private set; }
        public int GetAdminProcessTypesCallCount { get; private set; }
        public int UpdateProcessTypeCallCount { get; private set; }
        public int GetActiveProcessTypesCallCount { get; private set; }
        public int GetWorkflowCreatedNotificationDispatchTargetsCallCount { get; private set; }
        public int ApplyNotificationDispatchResultsCallCount { get; private set; }
        public int? LastAuditLogLimit { get; private set; }
        public int? LastAuditLogOffset { get; private set; }
        public Guid? LastCreateWorkflowLinkTargetWorkflowUid { get; private set; }
        public CreateWorkflowLinkRequest? LastCreateWorkflowLinkRequest { get; private set; }
        public long? LastCreateWorkflowLinkActorUserId { get; private set; }
        public int? LastFindLinkableWorkflowsEmployeeNumber { get; private set; }
        public Guid? LastFindLinkableWorkflowsExcludeUid { get; private set; }
        public Guid? LastGetDerivedAnswersSourceWorkflowUid { get; private set; }
        public string? LastGetDerivedAnswersTargetProcessTypeKey { get; private set; }
        public Guid? LastDeleteWorkflowLinkWorkflowUid { get; private set; }
        public long? LastDeleteWorkflowLinkId { get; private set; }
        public long? LastDeleteWorkflowLinkActorUserId { get; private set; }
        public BulkDepartmentChangeRequest? LastBulkRequest { get; private set; }
        public long? LastBulkActorUserId { get; private set; }
        public int? LastUpdateProcessTypeId { get; private set; }
        public AdminProcessTypeUpdateRequest? LastUpdateProcessTypeRequest { get; private set; }
        public Dictionary<Guid, List<WorkflowNotificationDispatchTarget>> WorkflowCreatedNotificationTargetsByUid { get; } = new();

        public Task<List<DepartmentDto>> GetDepartments() => throw new NotSupportedException();
        public Task<List<RoleDto>> GetRoles()
        {
            GetRolesCallCount += 1;
            return Task.FromResult(Roles);
        }
        public Task<List<WorkflowProcessTypeDto>> GetActiveProcessTypes()
        {
            GetActiveProcessTypesCallCount += 1;
            return Task.FromResult(ActiveProcessTypes);
        }
        public Task<List<RequirementDto>> GetRequirements(string? processTypeKey = null) => throw new NotSupportedException();
        public Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId, string? processTypeKey = null) => throw new NotSupportedException();
        public Task<WorkflowCreationResult> CreateWorkflow(CreateWorkflowRequest request, long createdByUserId) => throw new NotSupportedException();
        public Task<WorkflowDetailDto?> CompleteSupervisorStep(Guid workflowUid, IReadOnlyList<RequirementSelectionInputDto> selections, long actorUserId) => throw new NotSupportedException();
        public Task<List<WorkflowNotificationDispatchTarget>> GetWorkflowCreatedNotificationDispatchTargets(Guid workflowUid)
        {
            GetWorkflowCreatedNotificationDispatchTargetsCallCount += 1;
            return Task.FromResult(
                WorkflowCreatedNotificationTargetsByUid.TryGetValue(workflowUid, out var targets)
                    ? targets
                    : new List<WorkflowNotificationDispatchTarget>());
        }
        public Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotifications(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowCompletionNotifications(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<Guid>> GetWorkflowUidsWithDisabledNotifications(string notificationType) => throw new NotSupportedException();
        public Task ApplyNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results)
        {
            ApplyNotificationDispatchResultsCallCount += 1;
            return Task.CompletedTask;
        }
        public Task<List<WorkflowListItemDto>> GetWorkflows() => throw new NotSupportedException();

        public Task<WorkflowDetailDto?> GetWorkflowByUid(Guid workflowUid)
        {
            GetWorkflowByUidCallCount += 1;
            return Task.FromResult(Workflow?.Uid == workflowUid ? Workflow : null);
        }

        public Task<List<WorkflowAuditEntryDto>> GetWorkflowAuditLog(Guid workflowUid, int limit = 200, int offset = 0)
        {
            GetWorkflowAuditLogCallCount += 1;
            LastAuditLogLimit = limit;
            LastAuditLogOffset = offset;
            return Task.FromResult(AuditEntries);
        }

        public Task<HashSet<int>> GetRequirementSelectionDepartmentIds(long userId) => throw new NotSupportedException();
        public Task<List<TaskWithWorkflowDto>> GetTasks() => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> GetTaskById(long taskId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> UpdateTaskStatus(long taskId, string status, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> UpdateTaskAssignment(long taskId, TaskAssignRequest request, long actorUserId) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> AddTaskComment(long taskId, string commentText, long actorUserId) => throw new NotSupportedException();
        public Task<List<WorkflowLinkDto>> GetWorkflowLinks(Guid workflowUid)
        {
            GetWorkflowLinksCallCount += 1;
            return Task.FromResult(WorkflowLinks);
        }

        public Task<WorkflowLinkDto?> CreateWorkflowLink(Guid targetWorkflowUid, CreateWorkflowLinkRequest request, long actorUserId)
        {
            CreateWorkflowLinkCallCount += 1;
            LastCreateWorkflowLinkTargetWorkflowUid = targetWorkflowUid;
            LastCreateWorkflowLinkRequest = request;
            LastCreateWorkflowLinkActorUserId = actorUserId;
            return Task.FromResult(CreatedWorkflowLink);
        }

        public Task<bool> DeleteWorkflowLink(Guid workflowUid, long linkId, long actorUserId)
        {
            DeleteWorkflowLinkCallCount += 1;
            LastDeleteWorkflowLinkWorkflowUid = workflowUid;
            LastDeleteWorkflowLinkId = linkId;
            LastDeleteWorkflowLinkActorUserId = actorUserId;
            return Task.FromResult(DeleteWorkflowLinkResult);
        }
        public Task<List<WorkflowTargetPersonDto>> SearchWorkflowTargetPeople(string? query, int limit = 20) => throw new NotSupportedException();

        public Task<List<LinkableWorkflowDto>> FindLinkableWorkflows(int employeeNumber, Guid? excludeWorkflowUid = null)
        {
            FindLinkableWorkflowsCallCount += 1;
            LastFindLinkableWorkflowsEmployeeNumber = employeeNumber;
            LastFindLinkableWorkflowsExcludeUid = excludeWorkflowUid;
            return Task.FromResult(LinkableWorkflows);
        }

        public Task<List<DerivedAnswerDto>> GetDerivedAnswers(Guid sourceWorkflowUid, string targetProcessTypeKey)
        {
            GetDerivedAnswersCallCount += 1;
            LastGetDerivedAnswersSourceWorkflowUid = sourceWorkflowUid;
            LastGetDerivedAnswersTargetProcessTypeKey = targetProcessTypeKey;
            return Task.FromResult(DerivedAnswers);
        }

        public Task<BulkOperationResultDto> BulkCreateDepartmentChangeWorkflows(BulkDepartmentChangeRequest request, long actorUserId)
        {
            BulkCreateDepartmentChangeWorkflowsCallCount += 1;
            LastBulkRequest = request;
            LastBulkActorUserId = actorUserId;
            return Task.FromResult(BulkOperationResult ?? new BulkOperationResultDto
            {
                TotalEmployees = 0,
                CreatedWorkflows = 0,
                SkippedEmployees = 0,
                FailedEmployees = 0,
                IsDryRun = request.DryRun,
                Items = new List<BulkOperationItemDto>()
            });
        }

        public Task<List<AdminProcessTypeDto>> GetAdminProcessTypes()
        {
            GetAdminProcessTypesCallCount += 1;
            return Task.FromResult(AdminProcessTypes);
        }

        public Task<AdminProcessTypeDto?> UpdateProcessType(int processTypeId, AdminProcessTypeUpdateRequest request)
        {
            UpdateProcessTypeCallCount += 1;
            LastUpdateProcessTypeId = processTypeId;
            LastUpdateProcessTypeRequest = request;

            if (UpdateProcessTypeException is not null)
            {
                throw UpdateProcessTypeException;
            }

            return Task.FromResult<AdminProcessTypeDto?>(UpdatedProcessType);
        }

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

    private sealed class StubWorkflowEmailNotificationSender : IWorkflowEmailNotificationSender
    {
        public int SendNotificationsCallCount { get; private set; }

        public Task<IReadOnlyList<NotificationDispatchResult>> SendNotificationsAsync(
            Guid workflowUid,
            IReadOnlyList<WorkflowNotificationDispatchTarget> targets,
            CancellationToken cancellationToken = default)
        {
            SendNotificationsCallCount += 1;
            return Task.FromResult<IReadOnlyList<NotificationDispatchResult>>(
            [
                .. targets.Select(target => new NotificationDispatchResult
                {
                    NotificationId = target.NotificationId,
                    Status = "sent",
                    Success = true,
                    Attempted = true,
                    ErrorMessage = null
                })
            ]);
        }
    }

    private sealed class StubNotificationEmailConfigurationService : INotificationEmailConfigurationService
    {
        public Task<AdminNotificationEmailConfigurationDto> GetAdminConfiguration(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AdminNotificationEmailConfigurationDto> SaveAdminConfiguration(
            AdminNotificationEmailConfigurationUpdateRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AdminNotificationEmailConfigurationDto> UpdateTestStatus(
            string lastTestStatus,
            string? lastError,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<NotificationEmailRuntimeConfiguration> GetRuntimeConfiguration(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubNotificationEmailTestSender : INotificationEmailTestSender
    {
        public Task<NotificationEmailTestSendResult> SendTestEmailAsync(
            string recipientEmail,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubUserAuthorizationRepository : IUserAuthorizationRepository
    {
        public Task<CurrentUser?> ResolveCurrentUser(ResolvedIdentity identity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<DemoLoginUserOptionDto>> GetDemoLoginUsers(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AdminUserDto>> GetAdminUsers(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AdminRoleDto>> GetAdminRoles(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AdminGroupDto>> GetAdminGroups(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AdminDepartmentAssignmentDto>> GetAdminDepartmentAssignments(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AdminResponsibilityOwnerDto>> GetAdminResponsibilityOwners(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminDepartmentAssignmentDto> CreateDepartment(string departmentName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteDepartment(int departmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminUserDto> CreateUser(string? externalKey, string displayName, string email, string? notificationEmail, int? departmentId, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteUser(long userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminUserDto?> UpdateUserMasterData(long userId, string? externalKey, string displayName, string email, string? notificationEmail, int? departmentId, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminUserDto?> UpdateUserRoles(long userId, IReadOnlyList<int> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminUserDto?> UpdateUserGroups(long userId, IReadOnlyList<int> groupIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminGroupDto?> UpdateGroupRoles(int groupId, IReadOnlyList<int> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminDepartmentAssignmentDto?> UpdateDepartmentAssignment(int departmentId, long? departmentLeadUserId, long? requirementOwnerUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminResponsibilityOwnerDto?> UpdateResponsibilityOwner(int responsibilityId, long? appUserId, int? departmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubSupervisorStepService : ISupervisorStepService
    {
        public Task<List<WorkflowListItemDto>> GetAssignedWorkflows(CurrentUser currentUser)
        {
            throw new NotSupportedException();
        }

        public Task<List<WorkflowRequirementSnapshotDto>?> GetRequirements(Guid workflowUid, CurrentUser currentUser)
        {
            throw new NotSupportedException();
        }

        public Task<WorkflowDetailDto?> UpdateRequirements(
            Guid workflowUid,
            IReadOnlyList<RequirementSelectionInputDto> selections,
            CurrentUser currentUser)
        {
            throw new NotSupportedException();
        }
    }
}
