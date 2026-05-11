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
    public async Task GetRelatedWorkflowsEndpoint_PassesWorkflowUidToRepository()
    {
        var workflowUid = Guid.NewGuid();
        var repository = new StubWorkflowRepository
        {
            Workflow = CreateWorkflowDetail(workflowUid),
            RelatedWorkflows = new List<RelatedWorkflowSummaryDto>()
        };

        var app = CreateApp(repository);
        var endpoint = GetWorkflowEndpoint(app, "/workflows/{uid:guid}/related", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            $"/workflows/{workflowUid}/related",
            "",
            ("uid", workflowUid));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetRelatedWorkflowsCallCount);
        Assert.Equal(workflowUid, repository.LastGetRelatedWorkflowsWorkflowUid);
    }

    [Fact]
    public async Task GetRelatedWorkflowsEndpoint_FiltersInvisibleRelatedWorkflowsForManager()
    {
        var workflowUid = Guid.NewGuid();
        var repository = new StubWorkflowRepository
        {
            Workflow = CreateWorkflowDetail(workflowUid),
            RelatedWorkflows = new List<RelatedWorkflowSummaryDto>
            {
                new()
                {
                    Uid = Guid.NewGuid(),
                    WorkflowStatus = "draft",
                    CreatedAt = DateTime.UtcNow,
                    DepartmentId = 1,
                    ProcessType = new WorkflowProcessTypeDto
                    {
                        Key = "onboarding",
                        Name = "Onboarding",
                        RequiresTargetPerson = true
                    }
                },
                new()
                {
                    Uid = Guid.NewGuid(),
                    WorkflowStatus = "draft",
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    DepartmentId = 2,
                    ProcessType = new WorkflowProcessTypeDto
                    {
                        Key = "offboarding",
                        Name = "Offboarding",
                        RequiresTargetPerson = true
                    }
                }
            }
        };

        var app = CreateApp(repository, CreateManagerUserForDepartment(1));
        var endpoint = GetWorkflowEndpoint(app, "/workflows/{uid:guid}/related", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            $"/workflows/{workflowUid}/related",
            "",
            ("uid", workflowUid));

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("Onboarding", body);
        Assert.DoesNotContain("Offboarding", body);
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
            "?sourceUid=not-a-guid&targetWorkflowDefinitionKey=offboarding");

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
            $"?sourceUid={sourceUid}&targetWorkflowDefinitionKey=%20offboarding%20");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetDerivedAnswersCallCount);
        Assert.Equal(sourceUid, repository.LastGetDerivedAnswersSourceWorkflowUid);
        Assert.Equal("offboarding", repository.LastGetDerivedAnswersTargetDefinitionKey);
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

    [Fact]
    public async Task StartableWorkflowDefinitionsEndpoint_ReturnsDefinitionFirstCatalog()
    {
        var repository = new StubWorkflowRepository
        {
            StartableWorkflowDefinitions =
            [
                new WorkflowStartableDefinitionDto
                {
                    DefinitionKey = "onboarding",
                    Name = "Onboarding",
                    Description = "Neue Person anlegen",
                    RequiresTargetPerson = false,
                    LatestPublishedVersionNumber = 3
                }
            ]
        };

        var app = CreateApp(repository, CreateUser(AuthorizationRoles.Hr));
        var endpoint = GetWorkflowEndpoint(app, "/workflow-definitions/startable", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/workflow-definitions/startable",
            "");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetStartableWorkflowDefinitionsCallCount);
    }

    [Fact]
    public async Task StartableWorkflowDefinitionsEndpoint_ManagerUsesBulkLookupWithoutPerDefinitionCalls()
    {
        var repository = new StubWorkflowRepository
        {
            StartableWorkflowDefinitions =
            [
                new WorkflowStartableDefinitionDto
                {
                    DefinitionKey = "onboarding",
                    Name = "Onboarding",
                    RequiresTargetPerson = false,
                    LatestPublishedVersionNumber = 1
                },
                new WorkflowStartableDefinitionDto
                {
                    DefinitionKey = "department_change",
                    Name = "Abteilungswechsel",
                    RequiresTargetPerson = true,
                    LatestPublishedVersionNumber = 1
                },
                new WorkflowStartableDefinitionDto
                {
                    DefinitionKey = "name_change",
                    Name = "Namensaenderung",
                    RequiresTargetPerson = true,
                    LatestPublishedVersionNumber = 1
                }
            ],
            ManagerCreatableDefinitionKeys = new(StringComparer.OrdinalIgnoreCase) { "department_change", "name_change" }
        };

        var app = CreateApp(repository, CreateUser(AuthorizationRoles.Manager));
        var endpoint = GetWorkflowEndpoint(app, "/workflow-definitions/startable", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/workflow-definitions/startable", "");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetStartableWorkflowDefinitionsCallCount);
        Assert.Equal(1, repository.GetManagerCreatableDefinitionKeysCallCount);
        Assert.Equal(0, repository.IsManagerCreatableDefinitionCallCount);
    }

    [Fact]
    public async Task CreateWorkflowEndpoint_UsesWorkflowDefinitionKey_WhenPublishedDefinitionIsStartable()
    {
        var workflowUid = Guid.NewGuid();
        var repository = new StubWorkflowRepository
        {
            StartableWorkflowDefinitions =
            [
                new WorkflowStartableDefinitionDto
                {
                    DefinitionKey = "onboarding",
                    Name = "Onboarding",
                    Description = "Neue Person anlegen",
                    RequiresTargetPerson = false,
                    LatestPublishedVersionNumber = 1
                }
            ],
            RuntimeWorkflowCreationResult = new WorkflowDefinitionRuntimeDetailDto
            {
                WorkflowId = 12,
                WorkflowUid = workflowUid,
                WorkflowDefinitionKey = "onboarding",
                WorkflowDefinitionName = "Onboarding",
                WorkflowDefinitionVersionId = 7,
                WorkflowDefinitionVersionNumber = 1,
                CurrentRuntimeStatus = "waiting",
                DepartmentId = 1,
                RoleId = 2,
                CreatedAt = DateTime.UtcNow,
                NodeInstances = new List<WorkflowNodeInstanceDto>()
            },
            Workflow = CreateWorkflowDetail(workflowUid)
        };

        var app = CreateApp(repository, CreateUser(AuthorizationRoles.Hr));
        var endpoint = GetWorkflowEndpoint(app, "/workflows", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/workflows",
            new CreateWorkflowRequest
            {
                WorkflowDefinitionKey = "onboarding",
                DepartmentId = 1,
                RoleId = 2,
                TargetPersonId = 77,
                FirstName = "Ada",
                LastName = "Lovelace",
                EmployeeNumber = 1001,
                BadgeNumber = 2002
            });

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
        Assert.Equal(1, repository.GetStartableWorkflowDefinitionsCallCount);
        Assert.Equal(0, repository.CreateWorkflowCallCount);
    }

    [Fact]
    public async Task WorkflowListEndpoint_RestrictsManagerToObservableDepartments()
    {
        var repository = new StubWorkflowRepository
        {
            RequirementSelectionDepartmentIdsResult = new HashSet<int> { 7 }
        };

        var app = CreateApp(repository, CreateManagerUserForDepartment(7));
        var endpoint = GetWorkflowEndpoint(app, "/workflows", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/workflows", "");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.GetFilteredWorkflowsCallCount);
        Assert.NotNull(repository.LastWorkflowListQuery);
        Assert.Equal(new[] { 7 }, repository.LastWorkflowListQuery!.ObservableDepartmentIds);
    }

    [Fact]
    public async Task WorkflowTargetPersonSourcesEndpoint_RestrictsManagerToObservableDepartments()
    {
        var repository = new StubWorkflowRepository
        {
            RequirementSelectionDepartmentIdsResult = new HashSet<int> { 7 }
        };

        var app = CreateApp(repository, CreateManagerUserForDepartment(7));
        var endpoint = GetWorkflowEndpoint(app, "/workflow-target-person-sources", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/workflow-target-person-sources",
            "?query=ada");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.SearchWorkflowTargetPersonSourcesCallCount);
        Assert.Equal(new[] { 7 }, repository.LastSearchWorkflowTargetPersonSourcesDepartmentIds);
    }

    [Fact]
    public async Task WorkflowTargetPeopleEndpoint_RestrictsManagerToObservableDepartments()
    {
        var repository = new StubWorkflowRepository
        {
            RequirementSelectionDepartmentIdsResult = new HashSet<int> { 7 }
        };

        var app = CreateApp(repository, CreateManagerUserForDepartment(7));
        var endpoint = GetWorkflowEndpoint(app, "/workflow-target-people", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/workflow-target-people",
            "?query=ada");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, repository.SearchWorkflowTargetPeopleCallCount);
        Assert.Equal(new[] { 7 }, repository.LastSearchWorkflowTargetPeopleDepartmentIds);
    }

    [Fact]
    public async Task CreateWorkflowEndpoint_ManagerOnlyRejectsDisallowedProcessTypes()
    {
        var repository = new StubWorkflowRepository
        {
            IsManagerCreatableDefinitionResult = false,
            StartableWorkflowDefinitions =
            [
                new WorkflowStartableDefinitionDto
                {
                    DefinitionKey = "onboarding",
                    Name = "Onboarding",
                    RequiresTargetPerson = false,
                    LatestPublishedVersionNumber = 1
                }
            ]
        };

        var app = CreateApp(repository, CreateUser(AuthorizationRoles.Manager));
        var endpoint = GetWorkflowEndpoint(app, "/workflows", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/workflows",
            new CreateWorkflowRequest
            {
                WorkflowDefinitionKey = "onboarding",
                DepartmentId = 1,
                RoleId = 2,
                FirstName = "Ada",
                LastName = "Lovelace",
                EmployeeNumber = 1001,
                BadgeNumber = 2002
            });

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal(1, repository.IsManagerCreatableDefinitionCallCount);
        Assert.Equal("onboarding", repository.LastIsManagerCreatableDefinitionKey);
    }

    [Fact]
    public async Task CreateWorkflowEndpoint_ManagerRejectsTargetPersonOutsideObservableDepartments()
    {
        var repository = new StubWorkflowRepository
        {
            IsManagerCreatableDefinitionResult = true,
            RequirementSelectionDepartmentIdsResult = new HashSet<int> { 7 },
            PersonWorkflowHistory = new PersonWorkflowHistoryDto
            {
                PersonId = 55,
                DisplayName = "Ada Lovelace",
                DepartmentId = 9,
                DepartmentName = "Fremd",
                EmployeeNumber = 1001,
                BadgeNumber = 2002,
                FirstName = "Ada",
                LastName = "Lovelace",
                Workflows = new List<PersonWorkflowSummaryDto>()
            }
        };

        var app = CreateApp(repository, CreateManagerUserForDepartment(7));
        var endpoint = GetWorkflowEndpoint(app, "/workflows", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/workflows",
            new CreateWorkflowRequest
            {
                WorkflowDefinitionKey = "department_change",
                TargetPersonId = 55,
                SourceWorkflowUid = Guid.NewGuid(),
                FirstName = "Ada",
                LastName = "Lovelace",
                EmployeeNumber = 1001,
                BadgeNumber = 2002
            });

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal(0, repository.CreateWorkflowCallCount);
        Assert.Equal(1, repository.GetPersonWorkflowHistoryCallCount);
    }

    [Fact]
    public async Task CreateWorkflowEndpoint_ManagerAllowsTargetPersonInsideObservableDepartments()
    {
        var repository = new StubWorkflowRepository
        {
            IsManagerCreatableDefinitionResult = true,
            StartableWorkflowDefinitions =
            [
                new WorkflowStartableDefinitionDto
                {
                    DefinitionKey = "department_change",
                    Name = "Abteilungswechsel",
                    RequiresTargetPerson = true,
                    LatestPublishedVersionNumber = 1
                }
            ],
            RequirementSelectionDepartmentIdsResult = new HashSet<int> { 7 },
            PersonWorkflowHistory = new PersonWorkflowHistoryDto
            {
                PersonId = 55,
                DisplayName = "Ada Lovelace",
                DepartmentId = 7,
                DepartmentName = "Eigene Abteilung",
                EmployeeNumber = 1001,
                BadgeNumber = 2002,
                FirstName = "Ada",
                LastName = "Lovelace",
                Workflows = new List<PersonWorkflowSummaryDto>()
            },
            Workflow = CreateWorkflowDetail(Guid.Parse("11111111-1111-1111-1111-111111111111"))
        };

        var app = CreateApp(repository, CreateManagerUserForDepartment(7));
        var endpoint = GetWorkflowEndpoint(app, "/workflows", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/workflows",
            new CreateWorkflowRequest
            {
                WorkflowDefinitionKey = "department_change",
                TargetPersonId = 55,
                SourceWorkflowUid = Guid.NewGuid(),
                FirstName = "Ada",
                LastName = "Lovelace",
                EmployeeNumber = 1001,
                BadgeNumber = 2002
            });

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
        Assert.Equal(1, repository.GetPersonWorkflowHistoryCallCount);
    }

    [Fact]
    public async Task CreateWorkflowEndpoint_RejectsTargetProcessWithoutTargetPerson()
    {
        var repository = new StubWorkflowRepository
        {
            StartableWorkflowDefinitions =
            [
                new WorkflowStartableDefinitionDto
                {
                    DefinitionKey = "department_change",
                    Name = "Abteilungswechsel",
                    RequiresTargetPerson = true,
                    LatestPublishedVersionNumber = 1
                }
            ]
        };

        var app = CreateApp(repository, CreateUser(AuthorizationRoles.Hr));
        var endpoint = GetWorkflowEndpoint(app, "/workflows", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/workflows",
            new CreateWorkflowRequest
            {
                WorkflowDefinitionKey = "department_change",
                DepartmentId = 1,
                RoleId = 2,
                FirstName = "Ada",
                LastName = "Lovelace",
                EmployeeNumber = 1001,
                BadgeNumber = 2002
            });

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(1, repository.IsManagerCreatableDefinitionCallCount);
    }

    [Fact]
    public async Task CreateWorkflowEndpoint_AllowsNewPersonProcessWithExistingTargetPerson()
    {
        var repository = new StubWorkflowRepository
        {
            StartableWorkflowDefinitions =
            [
                new WorkflowStartableDefinitionDto
                {
                    DefinitionKey = "onboarding",
                    Name = "Onboarding",
                    RequiresTargetPerson = false,
                    LatestPublishedVersionNumber = 1
                }
            ],
            Workflow = CreateWorkflowDetail(Guid.Parse("11111111-1111-1111-1111-111111111111"))
        };

        var app = CreateApp(repository, CreateUser(AuthorizationRoles.Hr));
        var endpoint = GetWorkflowEndpoint(app, "/workflows", HttpMethods.Post);
        var context = CreateJsonRequestContext(
            app.Services,
            endpoint,
            HttpMethods.Post,
            "/workflows",
            new CreateWorkflowRequest
            {
                WorkflowDefinitionKey = "onboarding",
                DepartmentId = 1,
                RoleId = 2,
                TargetPersonId = 77,
                FirstName = "Ada",
                LastName = "Lovelace",
                EmployeeNumber = 1001,
                BadgeNumber = 2002
            });

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
        Assert.Equal(1, repository.IsManagerCreatableDefinitionCallCount);
    }

    [Fact]
    public async Task RequirementsEndpoint_RejectsMissingProcessTypeKey()
    {
        var repository = new StubWorkflowRepository();
        var app = CreateApp(repository, CreateUser(AuthorizationRoles.Reader));
        var endpoint = GetWorkflowEndpoint(app, "/requirements", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/requirements", "");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task WorkflowConfigEndpoint_RejectsMissingProcessTypeKey()
    {
        var repository = new StubWorkflowRepository();
        var app = CreateApp(repository, CreateUser(AuthorizationRoles.Hr));
        var endpoint = GetWorkflowEndpoint(app, "/workflow-config", HttpMethods.Get);
        var context = CreateGetRequestContext(app.Services, endpoint, "/workflow-config", "?roleId=7");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task GraphApplicationEndpoint_ReturnsReadOnlyRuntimeConfiguration()
    {
        var repository = new StubWorkflowRepository();
        var app = CreateApp(repository);
        var endpoint = GetWorkflowEndpoint(app, "/admin/config/graph-application", HttpMethods.Get);
        var context = CreateGetRequestContext(
            app.Services,
            endpoint,
            "/admin/config/graph-application",
            "");

        await endpoint.RequestDelegate!(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("\"configurationSource\":\"runtime\"", body);
        Assert.DoesNotContain("\"clientSecret\":", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GraphApplicationPatchEndpoint_IsNotRegistered()
    {
        var app = CreateApp(new StubWorkflowRepository());

        var exists = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Any(endpoint =>
                endpoint.RoutePattern.RawText == "/admin/config/graph-application"
                && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(HttpMethods.Patch) == true);

        Assert.False(exists);
    }

    private static WebApplication CreateApp(StubWorkflowRepository repository, CurrentUser? user = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IWorkflowRepository>(repository);
        builder.Services.AddSingleton<IWorkflowDefinitionRuntimeRepository>(repository);
        builder.Services.AddSingleton<IWorkflowAuditReadRepository>(repository);
        builder.Services.AddSingleton<IWorkflowNotificationReadRepository>(repository);
        builder.Services.AddSingleton<IUserAuthorizationRepository, StubUserAuthorizationRepository>();
        builder.Services.AddSingleton<IUserContext>(new StubUserContext(user ?? CreateAdminHrUser()));
        builder.Services.AddSingleton<IAuthorizationPolicyService, AuthorizationPolicyService>();
        builder.Services.AddSingleton<IWorkflowVisibilityService, WorkflowVisibilityService>();
        builder.Services.AddSingleton<IWorkflowNotificationDispatchService, WorkflowNotificationDispatchService>();
        builder.Services.AddSingleton<IWorkflowCatalogService, WorkflowCatalogService>();
        builder.Services.AddSingleton<IPersonLifecycleProjectionService, PersonLifecycleProjectionService>();
        builder.Services.AddSingleton<IWorkflowLifecycleService>(new StubWorkflowLifecycleService(repository));
        builder.Services.AddSingleton<IWorkflowRuntimeService, WorkflowRuntimeService>();
        builder.Services.AddSingleton<ITaskApplicationService, TaskApplicationService>();
        builder.Services.AddSingleton<IGraphApplicationConfigurationService, StubGraphApplicationConfigurationService>();
        builder.Services.AddSingleton<IWorkflowEmailNotificationSender, StubWorkflowEmailNotificationSender>();
        builder.Services.AddSingleton<INotificationEmailTestSender, StubNotificationEmailTestSender>();
        builder.Services.AddSingleton<INotificationEmailConfigurationService, StubNotificationEmailConfigurationService>();
        builder.Services.AddSingleton<INotificationTemplateService, StubNotificationTemplateService>();
        builder.Services.AddSingleton<ISupervisorStepService, StubSupervisorStepService>();
        builder.Services.AddSingleton<ISystemEventLogService, StubSystemEventLogService>();
        builder.Services.AddSingleton<IRotationTemplateAdminService>(_ => throw new NotSupportedException());

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
                    BlockedCount = 0,
                    DoneCount = 0,
                    CompletedCount = 0,
                    ActiveCount = 0
                },
                Required = new WorkflowTaskCountSummaryDto
                {
                    TotalCount = 0,
                    OpenCount = 0,
                    InProgressCount = 0,
                    BlockedCount = 0,
                    DoneCount = 0,
                    CompletedCount = 0,
                    ActiveCount = 0
                },
                DepartmentPhase = new WorkflowTaskCountSummaryDto
                {
                    TotalCount = 0,
                    OpenCount = 0,
                    InProgressCount = 0,
                    BlockedCount = 0,
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
            ApprovalSpecKey = null,
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

    private static CurrentUser CreateManagerUserForDepartment(int departmentId)
    {
        return new CurrentUser
        {
            UserId = 99,
            ExternalKey = "manager.user",
            DisplayName = "Manager User",
            Email = "manager.user@example.com",
            IsActive = true,
            DepartmentId = departmentId,
            DepartmentName = "Test Department",
            IdentityProvider = "test",
            Groups = new List<CurrentUserGroup>(),
            DirectRoles =
            [
                new CurrentUserRole
                {
                    RoleId = 1,
                    RoleKey = AuthorizationRoles.Manager,
                    RoleName = AuthorizationRoles.Manager,
                    RoleKind = AuthorizationRoles.SystemRoleKind,
                    AssignmentSource = "test",
                    GroupId = null,
                    GroupKey = null
                }
            ],
            GroupRoles = new List<CurrentUserRole>(),
            EffectiveRoles =
            [
                new CurrentUserRole
                {
                    RoleId = 1,
                    RoleKey = AuthorizationRoles.Manager,
                    RoleName = AuthorizationRoles.Manager,
                    RoleKind = AuthorizationRoles.SystemRoleKind,
                    AssignmentSource = "test",
                    GroupId = null,
                    GroupKey = null
                }
            ],
            DirectResponsibilities =
            [
                new CurrentUserResponsibility
                {
                    ResponsibilityId = 501,
                    ResponsibilityKey = $"leadership_{departmentId}",
                    ResponsibilityName = "Department Lead",
                    ResponsibilityType = "department_lead",
                    DepartmentId = departmentId,
                    DepartmentName = "Test Department",
                    AssignmentSource = "test",
                    GroupId = null,
                    GroupKey = null
                }
            ],
            GroupResponsibilities = new List<CurrentUserResponsibility>(),
            EffectiveResponsibilities =
            [
                new CurrentUserResponsibility
                {
                    ResponsibilityId = 501,
                    ResponsibilityKey = $"leadership_{departmentId}",
                    ResponsibilityName = "Department Lead",
                    ResponsibilityType = "department_lead",
                    DepartmentId = departmentId,
                    DepartmentName = "Test Department",
                    AssignmentSource = "test",
                    GroupId = null,
                    GroupKey = null
                }
            ]
        };
    }

    private sealed class StubUserContext(CurrentUser user) : IUserContext
    {
        public Task<CurrentUser?> GetCurrentUser(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CurrentUser?>(user);
        }
    }

    // Delegiert Definition-Runtime-Mutationen an die bestehende Stub-Repo-Methode, damit Endpoint-Tests
    // den Z7-1.2-Lifecycle-Pfad nicht real instanziieren muessen.
    private sealed class StubWorkflowLifecycleService(StubWorkflowRepository repository) : IWorkflowLifecycleService
    {
        public Task<TaskWithWorkflowDto?> UpdateTaskStatusAsync(long taskId, string status, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> UpdateTaskStatusByRefAsync(string taskRef, string status, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> DecideTaskApprovalAsync(long taskId, TaskApprovalDecisionRequest request, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TaskWithWorkflowDto?> DecideTaskApprovalByRefAsync(string taskRef, TaskApprovalDecisionRequest request, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task OnAutomationJobCompletedAsync(ClaimedAutomationJobRecord job, WorkflowAutomationHandlerResult result, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowInstanceAsync(CreateWorkflowDefinitionInstanceRequest request, long actorUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(repository.RuntimeWorkflowCreationResult);
        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteFormNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeFormNodeRequest request, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteApprovalNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeApprovalNodeRequest request, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkflowDefinitionRuntimeDetailDto?> CompleteTaskNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeTaskNodeRequest request, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubWorkflowRepository : IWorkflowRepository, IWorkflowDefinitionRuntimeRepository, IWorkflowAuditReadRepository, IWorkflowNotificationReadRepository
    {
        public WorkflowDetailDto? Workflow { get; set; }
        public List<WorkflowAuditEntryDto> AuditEntries { get; set; } = new();
        public List<RoleDto> Roles { get; set; } = new();
        public WorkflowLinkDto? CreatedWorkflowLink { get; set; }
        public List<WorkflowLinkDto> WorkflowLinks { get; set; } = new();
        public List<RelatedWorkflowSummaryDto> RelatedWorkflows { get; set; } = new();
        public List<LinkableWorkflowDto> LinkableWorkflows { get; set; } = new();
        public List<DerivedAnswerDto> DerivedAnswers { get; set; } = new();
        public List<WorkflowStartableDefinitionDto> StartableWorkflowDefinitions { get; set; } = new();
        public WorkflowTargetPersonDto? CreatedPerson { get; set; }
        public List<WorkflowTargetPersonDto> WorkflowTargetPeople { get; set; } = new();
        public List<WorkflowTargetPersonDto> RotationEligiblePeople { get; set; } = new();
        public WorkflowDefinitionRuntimeDetailDto RuntimeWorkflowCreationResult { get; set; } = new()
        {
            WorkflowId = 1,
            WorkflowUid = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkflowDefinitionKey = "onboarding",
            WorkflowDefinitionName = "Onboarding",
            WorkflowDefinitionVersionId = 1,
            WorkflowDefinitionVersionNumber = 1,
            CurrentRuntimeStatus = "waiting",
            DepartmentId = 1,
            RoleId = 2,
            CreatedAt = DateTime.UtcNow,
            NodeInstances = new List<WorkflowNodeInstanceDto>()
        };
        public WorkflowListResult FilteredWorkflowsResult { get; set; } = new()
        {
            Items = new List<WorkflowListItemDto>(),
            TotalCount = 0
        };
        public HashSet<int> RequirementSelectionDepartmentIdsResult { get; set; } = new();
        public PersonWorkflowHistoryDto? PersonWorkflowHistory { get; set; }
        public bool DeleteWorkflowLinkResult { get; set; }
        public int GetWorkflowByUidCallCount { get; private set; }
        public int GetWorkflowAuditLogCallCount { get; private set; }
        public int GetRolesCallCount { get; private set; }
        public int CreateWorkflowLinkCallCount { get; private set; }
        public int GetWorkflowLinksCallCount { get; private set; }
        public int GetRelatedWorkflowsCallCount { get; private set; }
        public int FindLinkableWorkflowsCallCount { get; private set; }
        public int GetDerivedAnswersCallCount { get; private set; }
        public int DeleteWorkflowLinkCallCount { get; private set; }
        public int GetStartableWorkflowDefinitionsCallCount { get; private set; }
        public int SearchWorkflowTargetPersonSourcesCallCount { get; private set; }
        public int SearchWorkflowTargetPeopleCallCount { get; private set; }
        public int SearchRotationEligiblePeopleCallCount { get; private set; }
        public int CreateWorkflowCallCount { get; private set; }
        public int CreatePersonCallCount { get; private set; }
        public int IsManagerCreatableDefinitionCallCount { get; private set; }
        public int GetWorkflowCreatedNotificationDispatchTargetsCallCount { get; private set; }
        public int ApplyNotificationDispatchResultsCallCount { get; private set; }
        public int ApplyPersonLifecycleProjectionCallCount { get; private set; }
        public int GetPersonWorkflowHistoryCallCount { get; private set; }
        public int GetRequirementSelectionDepartmentIdsCallCount { get; private set; }
        public int GetFilteredWorkflowsCallCount { get; private set; }
        public string? LastSearchWorkflowTargetPersonSourcesSearch { get; private set; }
        public int? LastSearchWorkflowTargetPersonSourcesLimit { get; private set; }
        public IReadOnlyCollection<int>? LastSearchWorkflowTargetPersonSourcesDepartmentIds { get; private set; }
        public string? LastSearchWorkflowTargetPeopleQuery { get; private set; }
        public int? LastSearchWorkflowTargetPeopleLimit { get; private set; }
        public IReadOnlyCollection<int>? LastSearchWorkflowTargetPeopleDepartmentIds { get; private set; }
        public string? LastSearchRotationEligiblePeopleQuery { get; private set; }
        public int? LastSearchRotationEligiblePeopleLimit { get; private set; }
        public IReadOnlyCollection<int>? LastSearchRotationEligiblePeopleDepartmentIds { get; private set; }
        public int? LastAuditLogLimit { get; private set; }
        public int? LastAuditLogOffset { get; private set; }
        public string? LastIsManagerCreatableDefinitionKey { get; private set; }
        public Guid? LastCreateWorkflowLinkTargetWorkflowUid { get; private set; }
        public CreateWorkflowLinkRequest? LastCreateWorkflowLinkRequest { get; private set; }
        public long? LastCreateWorkflowLinkActorUserId { get; private set; }
        public Guid? LastGetRelatedWorkflowsWorkflowUid { get; private set; }
        public int? LastFindLinkableWorkflowsEmployeeNumber { get; private set; }
        public Guid? LastFindLinkableWorkflowsExcludeUid { get; private set; }
        public Guid? LastGetDerivedAnswersSourceWorkflowUid { get; private set; }
        public string? LastGetDerivedAnswersTargetDefinitionKey { get; private set; }
        public Guid? LastDeleteWorkflowLinkWorkflowUid { get; private set; }
        public long? LastDeleteWorkflowLinkId { get; private set; }
        public long? LastDeleteWorkflowLinkActorUserId { get; private set; }
        public CreateWorkflowRequest? LastCreateWorkflowRequest { get; private set; }
        public long? LastCreateWorkflowUserId { get; private set; }
        public CreatePersonRequest? LastCreatePersonRequest { get; private set; }
        public long? LastCreatePersonActorUserId { get; private set; }
        public Guid? LastApplyPersonLifecycleProjectionWorkflowUid { get; private set; }
        public long? LastApplyPersonLifecycleProjectionActorUserId { get; private set; }
        public WorkflowListQuery? LastWorkflowListQuery { get; private set; }
        public bool IsManagerCreatableDefinitionResult { get; set; } = true;
        public List<WorkflowTargetPersonSourceDto> WorkflowTargetPersonSources { get; set; } = new();
        public WorkflowCreationResult WorkflowCreationResult { get; set; } = new()
        {
            WorkflowId = 1,
            Uid = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            NotificationTargets = new List<WorkflowNotificationDispatchTarget>()
        };
        public Dictionary<Guid, List<WorkflowNotificationDispatchTarget>> WorkflowCreatedNotificationTargetsByUid { get; } = new();

        public Task<AdminListPageDto<DepartmentDto>> GetDepartments(AdminListQuery query) => throw new NotSupportedException();
        public Task<AdminListPageDto<PersonDirectoryItemDto>> GetPeopleDirectory(AdminListQuery query) => throw new NotSupportedException();
        public Task<AdminListPageDto<RoleDto>> GetRoles(AdminListQuery query)
        {
            GetRolesCallCount += 1;
            return Task.FromResult(new AdminListPageDto<RoleDto>
            {
                Items = Roles,
                Total = Roles.Count,
                Limit = query.Limit,
                Offset = query.Offset
            });
        }
        public Task<List<WorkflowStartableDefinitionDto>> GetStartableWorkflowDefinitions()
        {
            GetStartableWorkflowDefinitionsCallCount += 1;
            return Task.FromResult(StartableWorkflowDefinitions);
        }
        public Task<List<RequirementDto>> GetRequirements(string workflowDefinitionKey) => throw new NotSupportedException();
        public Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId, string workflowDefinitionKey) => throw new NotSupportedException();
        public Task<bool> IsManagerCreatableDefinition(string workflowDefinitionKey)
        {
            IsManagerCreatableDefinitionCallCount += 1;
            LastIsManagerCreatableDefinitionKey = workflowDefinitionKey;
            return Task.FromResult(IsManagerCreatableDefinitionResult);
        }
        public int GetManagerCreatableDefinitionKeysCallCount { get; private set; }
        public HashSet<string> ManagerCreatableDefinitionKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Task<IReadOnlySet<string>> GetManagerCreatableDefinitionKeys()
        {
            GetManagerCreatableDefinitionKeysCallCount += 1;
            return Task.FromResult<IReadOnlySet<string>>(ManagerCreatableDefinitionKeys);
        }
        public Task<WorkflowCreationResult> CreateWorkflow(CreateWorkflowRequest request, long createdByUserId)
        {
            CreateWorkflowCallCount += 1;
            LastCreateWorkflowRequest = request;
            LastCreateWorkflowUserId = createdByUserId;
            return Task.FromResult(WorkflowCreationResult);
        }
        public Task<WorkflowTargetPersonDto> CreatePerson(CreatePersonRequest request, long actorUserId)
        {
            CreatePersonCallCount += 1;
            LastCreatePersonRequest = request;
            LastCreatePersonActorUserId = actorUserId;
            return Task.FromResult(CreatedPerson ?? new WorkflowTargetPersonDto
            {
                PersonId = 1,
                DisplayName = "Test Person"
            });
        }
        public Task<WorkflowDetailDto?> CompleteSupervisorStep(Guid workflowUid, IReadOnlyList<RequirementSelectionInputDto> selections, long actorUserId) => throw new NotSupportedException();
        public Task<List<WorkflowNotificationDispatchTarget>> GetWorkflowCreatedNotificationDispatchTargets(Guid workflowUid)
        {
            GetWorkflowCreatedNotificationDispatchTargetsCallCount += 1;
            return Task.FromResult(
                WorkflowCreatedNotificationTargetsByUid.TryGetValue(workflowUid, out var targets)
                    ? targets
                    : new List<WorkflowNotificationDispatchTarget>());
        }
        public Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotifications(Guid workflowUid) => Task.FromResult(new List<WorkflowNotificationDispatchTarget>());
        public Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowCompletionNotifications(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<Guid>> GetWorkflowUidsWithDisabledNotifications(string notificationType) => throw new NotSupportedException();
        public Task ApplyNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results)
        {
            ApplyNotificationDispatchResultsCallCount += 1;
            return Task.CompletedTask;
        }
        public Task<List<WorkflowListItemDto>> GetWorkflows() => throw new NotSupportedException();
        public Task<WorkflowListResult> GetFilteredWorkflows(WorkflowListQuery query, CancellationToken cancellationToken = default)
        {
            GetFilteredWorkflowsCallCount += 1;
            LastWorkflowListQuery = query;
            return Task.FromResult(FilteredWorkflowsResult);
        }

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

        public Task<HashSet<int>> GetRequirementSelectionDepartmentIds(long userId)
        {
            GetRequirementSelectionDepartmentIdsCallCount += 1;
            return Task.FromResult(RequirementSelectionDepartmentIdsResult);
        }
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
        public Task<List<WorkflowLinkDto>> GetWorkflowLinks(Guid workflowUid)
        {
            GetWorkflowLinksCallCount += 1;
            return Task.FromResult(WorkflowLinks);
        }

        public Task<List<RelatedWorkflowSummaryDto>> GetRelatedWorkflows(Guid workflowUid)
        {
            GetRelatedWorkflowsCallCount += 1;
            LastGetRelatedWorkflowsWorkflowUid = workflowUid;
            return Task.FromResult(RelatedWorkflows);
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
        public Task<bool> ArchiveWorkflow(Guid workflowUid, long actorUserId) => throw new NotSupportedException();
        public Task<bool> DeleteDraftWorkflow(Guid workflowUid) => throw new NotSupportedException();
        public Task<PersonWorkflowHistoryDto?> GetPersonWorkflowHistory(long personId)
        {
            GetPersonWorkflowHistoryCallCount += 1;
            return Task.FromResult(PersonWorkflowHistory);
        }
        public Task<List<WorkflowTargetPersonSourceDto>> SearchWorkflowTargetPersonSources(
            string? search,
            int limit = 20,
            IReadOnlyCollection<int>? observableDepartmentIds = null)
        {
            SearchWorkflowTargetPersonSourcesCallCount += 1;
            LastSearchWorkflowTargetPersonSourcesSearch = search;
            LastSearchWorkflowTargetPersonSourcesLimit = limit;
            LastSearchWorkflowTargetPersonSourcesDepartmentIds = observableDepartmentIds;
            return Task.FromResult(WorkflowTargetPersonSources);
        }
        public Task<List<WorkflowTargetPersonDto>> SearchWorkflowTargetPeople(
            string? query,
            int limit = 20,
            IReadOnlyCollection<int>? observableDepartmentIds = null)
        {
            SearchWorkflowTargetPeopleCallCount += 1;
            LastSearchWorkflowTargetPeopleQuery = query;
            LastSearchWorkflowTargetPeopleLimit = limit;
            LastSearchWorkflowTargetPeopleDepartmentIds = observableDepartmentIds;
            return Task.FromResult(WorkflowTargetPeople);
        }
        public Task<List<WorkflowTargetPersonDto>> SearchRotationEligiblePeople(
            string? query,
            int limit = 20,
            IReadOnlyCollection<int>? observableDepartmentIds = null)
        {
            SearchRotationEligiblePeopleCallCount += 1;
            LastSearchRotationEligiblePeopleQuery = query;
            LastSearchRotationEligiblePeopleLimit = limit;
            LastSearchRotationEligiblePeopleDepartmentIds = observableDepartmentIds;
            return Task.FromResult(RotationEligiblePeople);
        }

        public Task ApplyPersonLifecycleProjection(Guid workflowUid, long? actorUserId = null)
        {
            ApplyPersonLifecycleProjectionCallCount += 1;
            LastApplyPersonLifecycleProjectionWorkflowUid = workflowUid;
            LastApplyPersonLifecycleProjectionActorUserId = actorUserId;
            return Task.CompletedTask;
        }

        public Task<List<LinkableWorkflowDto>> FindLinkableWorkflows(int employeeNumber, Guid? excludeWorkflowUid = null)
        {
            FindLinkableWorkflowsCallCount += 1;
            LastFindLinkableWorkflowsEmployeeNumber = employeeNumber;
            LastFindLinkableWorkflowsExcludeUid = excludeWorkflowUid;
            return Task.FromResult(LinkableWorkflows);
        }

        public Task<List<DerivedAnswerDto>> GetDerivedAnswers(Guid sourceWorkflowUid, string targetWorkflowDefinitionKey)
        {
            GetDerivedAnswersCallCount += 1;
            LastGetDerivedAnswersSourceWorkflowUid = sourceWorkflowUid;
            LastGetDerivedAnswersTargetDefinitionKey = targetWorkflowDefinitionKey;
            return Task.FromResult(DerivedAnswers);
        }

        public Task<AdminListPageDto<WorkflowDefinitionSummaryDto>> GetAdminWorkflowDefinitions(AdminListQuery query) => throw new NotSupportedException();
        public Task<WorkflowDefinitionSummaryDto> CreateAdminWorkflowDefinition(CreateWorkflowDefinitionRequest request) => throw new NotSupportedException();
        public Task<WorkflowDefinitionSummaryDto?> UpdateAdminWorkflowDefinition(int definitionId, UpdateWorkflowDefinitionRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteAdminWorkflowDefinition(int definitionId) => throw new NotSupportedException();
        public Task<WorkflowDefinitionVersionSummaryDto?> CreateAdminWorkflowDefinitionVersion(int definitionId, CreateWorkflowDefinitionVersionRequest request) => throw new NotSupportedException();
        public Task<WorkflowDefinitionVersionDetailDto?> EnsureAdminWorkflowDefinitionWorkingDraft(int definitionId) => throw new NotSupportedException();
        public Task<WorkflowDefinitionVersionDetailDto?> GetAdminWorkflowDefinitionVersion(long versionId) => throw new NotSupportedException();
        public Task<WorkflowDefinitionVersionDetailDto?> ReplaceAdminWorkflowDefinitionVersion(long versionId, ReplaceWorkflowDefinitionVersionRequest request) => throw new NotSupportedException();

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
        public Task<WorkflowDefinitionVersionDetailDto?> PublishWorkflowDefinitionVersion(long versionId) => throw new NotSupportedException();

        public Task<WorkflowDefinitionRuntimeDetailDto?> GetWorkflowDefinitionRuntimeDetail(Guid workflowUid) => throw new NotSupportedException();
        public Task<List<WorkflowRuntimeEventDto>> GetWorkflowDefinitionRuntimeEvents(Guid workflowUid) => throw new NotSupportedException();
        public Task<bool> UpdatePersonCoreFields(long personId, DateOnly? entryDate, int? badgeNumber) => throw new NotSupportedException();
        public Task<AdminListPageDto<UnlinkedDirectoryIdentityDto>> GetUnlinkedDirectoryIdentities(string? departmentFilter, bool? onlyEnabled, int limit, int offset) => throw new NotSupportedException();
        public Task<ImportPeopleFromDirectoryResultDto> ImportPeopleFromDirectory(List<long> directoryIdentityIds, long? actorUserId) => throw new NotSupportedException();
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

        public Task<IReadOnlyList<NotificationDispatchResult>> SendRotationNotificationsAsync(
            IReadOnlyList<RotationNotificationDispatchTarget> targets,
            CancellationToken cancellationToken = default)
        {
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

    private sealed class StubNotificationTemplateService : INotificationTemplateService
    {
        public Task<IReadOnlyList<AdminNotificationTemplateDto>> GetAdminTemplates(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AdminNotificationTemplateDto>>([]);

        public Task<AdminNotificationTemplateDto> UpdateAdminTemplate(
            string templateKey,
            AdminNotificationTemplateUpdateRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<AdminNotificationTemplateWorkflowPreviewTargetDto>> SearchWorkflowPreviewTargets(
            string? query,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AdminNotificationTemplateWorkflowPreviewTargetDto>>([]);

        public Task<IReadOnlyList<AdminNotificationTemplateRotationPlanPreviewTargetDto>> SearchRotationPlanPreviewTargets(
            string? query,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AdminNotificationTemplateRotationPlanPreviewTargetDto>>([]);

        public Task<AdminNotificationTemplatePreviewResponseDto> BuildPreview(
            string templateKey,
            AdminNotificationTemplatePreviewRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<NotificationTemplateRenderResult> RenderWorkflowNotification(
            WorkflowNotificationRenderContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<NotificationTemplateRenderResult> RenderRotationNotification(
            RotationNotificationRenderContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubNotificationEmailTestSender : INotificationEmailTestSender
    {
        public Task<NotificationEmailTestSendResult> SendTestEmailAsync(
            string recipientEmail,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubGraphApplicationConfigurationService : IGraphApplicationConfigurationService
    {
        public Task<AdminGraphApplicationConfigurationDto> GetAdminConfiguration(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AdminGraphApplicationConfigurationDto
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                HasClientSecret = true,
                UpdatedAt = null,
                ConfigurationSource = "runtime",
                ConfigurationStatus = "ready",
                ConfigurationMessage = null
            });
        }

        public Task<GraphApplicationRuntimeConfiguration> GetRuntimeConfiguration(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GraphApplicationRuntimeConfiguration
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                ClientSecret = "runtime-secret",
                HasClientSecret = true,
                UpdatedAt = null
            });
        }
    }

    private sealed class StubSystemEventLogService : ISystemEventLogService
    {
        public Task<IReadOnlyList<AdminSystemLogEntryDto>> GetAdminLogsAsync(
            SystemEventLogQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AdminSystemLogEntryDto>>([]);

        public Task<AdminSystemLogSummaryDto> GetAdminLogSummaryAsync(
            SystemEventLogQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminSystemLogSummaryDto
            {
                TotalCount = 0,
                InfoCount = 0,
                WarningCount = 0,
                ErrorCount = 0,
                Sources = []
            });

        public Task WriteAsync(SystemEventLogWriteModel model, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubUserAuthorizationRepository : IUserAuthorizationRepository
    {
        public Task<CurrentUser?> ResolveCurrentUser(ResolvedIdentity identity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CurrentUser?> FindOrCreateFromExternalIdentity(ResolvedIdentity identity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<SimulationLoginUserOptionDto>> GetSimulationLoginUsers(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AdminUserDto>> GetAdminUsers(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AdminRoleDto>> GetAdminRoles(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AdminGroupDto>> GetAdminGroups(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AdminPermissionDto>> GetAdminPermissions(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CursorPageDto<AdminPermissionAuditEntryDto>> GetAdminPermissionAudit(CursorPageQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminListPageDto<AdminDepartmentAssignmentDto>> GetAdminDepartmentAssignments(AdminListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminListPageDto<AdminRoleDto>> GetAdminDepartmentPositions(AdminListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminListPageDto<AdminResponsibilityOwnerDto>> GetAdminResponsibilityOwners(AdminListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminDepartmentAssignmentDto> CreateDepartment(string departmentName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteDepartment(int departmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminRoleDto> CreateDepartmentPosition(int departmentId, string positionName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminRoleDto?> UpdateDepartmentPosition(int positionId, string positionName, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteDepartmentPosition(int positionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminResponsibilityOwnerDto> CreateResponsibility(string responsibilityName, int? departmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteResponsibility(int responsibilityId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminUserDto> CreateUser(string? externalKey, string displayName, string email, string? notificationEmail, int? departmentId, bool isActive, long? actorUserId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteUser(long userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminUserDto?> UpdateUserMasterData(long userId, string? externalKey, string displayName, string email, string? notificationEmail, int? departmentId, bool isActive, long? actorUserId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminUserDto?> UpdateUserRoles(long userId, IReadOnlyList<int> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminUserDto?> UpdateUserGroups(long userId, IReadOnlyList<int> groupIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminGroupDto?> UpdateGroupRoles(int groupId, IReadOnlyList<int> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminRoleDto?> UpdateRolePermissions(int roleId, IReadOnlyList<int> permissionIds, long? actorUserId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminUserDto?> UpdateUserPermissionOverrides(long userId, IReadOnlyList<AdminUserPermissionOverrideUpsertRequest> overrides, long? actorUserId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminDepartmentAssignmentDto?> UpdateDepartmentAssignment(int departmentId, long? departmentLeadUserId, long? requirementOwnerUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdminResponsibilityOwnerDto?> UpdateResponsibilityOwner(int responsibilityId, long? appUserId, int? departmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<EntraJobTitleDto>> GetDepartmentEntraJobTitles(int departmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ImportPositionsFromEntraResult> ImportDepartmentPositionsFromEntra(int departmentId, IReadOnlyList<string> jobTitles, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
