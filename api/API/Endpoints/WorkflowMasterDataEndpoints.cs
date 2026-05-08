using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class WorkflowMasterDataEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/departments", async (
            HttpRequest request,
            IWorkflowCatalogService workflowCatalogService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateWorkflow,
                "HR, Abteilungsleitung oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var query = AdminListQuery.From(request);
            return Results.Ok(await workflowCatalogService.GetDepartmentsAsync(query));
        }).Produces<AdminListPageDto<DepartmentDto>>(StatusCodes.Status200OK);

        app.MapGet("/roles", async (
            HttpRequest request,
            IWorkflowCatalogService workflowCatalogService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateWorkflow,
                "HR, Abteilungsleitung oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var query = AdminListQuery.From(request);
            return Results.Ok(await workflowCatalogService.GetRolesAsync(query, access.User!));
        }).Produces<AdminListPageDto<RoleDto>>(StatusCodes.Status200OK);

        app.MapGet("/workflow-definitions/startable", async (
            IWorkflowCatalogService workflowCatalogService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateWorkflow,
                "HR, Abteilungsleitung oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await workflowCatalogService.GetStartableWorkflowDefinitionsAsync(access.User!));
        }).Produces<List<WorkflowStartableDefinitionDto>>(StatusCodes.Status200OK);

        app.MapGet("/workflow-target-person-sources", async (
            [FromQuery] string? query,
            [FromQuery] int? limit,
            IWorkflowCatalogService workflowCatalogService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateWorkflow,
                "HR, Abteilungsleitung oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var targetPersonSources = await workflowCatalogService.SearchWorkflowTargetPersonSourcesAsync(
                query,
                access.User!,
                limit ?? 20);
            return Results.Ok(targetPersonSources);
        }).Produces<List<WorkflowTargetPersonSourceDto>>(StatusCodes.Status200OK);

        app.MapGet("/workflow-target-people", async (
            [FromQuery] string? query,
            [FromQuery] int? limit,
            IWorkflowCatalogService workflowCatalogService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateWorkflow,
                "HR, Abteilungsleitung oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var people = await workflowCatalogService.SearchWorkflowTargetPeopleAsync(
                query,
                access.User!,
                limit ?? 20);
            return Results.Ok(people);
        }).Produces<List<WorkflowTargetPersonDto>>(StatusCodes.Status200OK);

        app.MapGet("/people/search", async (
            [FromQuery] string? query,
            [FromQuery] int? limit,
            IWorkflowCatalogService workflowCatalogService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessWorkflowOverview,
                "HR, Abteilungsleitung, Admin oder Reader role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var people = await workflowCatalogService.SearchWorkflowTargetPeopleAsync(
                query,
                access.User!,
                limit ?? 20);
            return Results.Ok(people);
        }).Produces<List<WorkflowTargetPersonDto>>(StatusCodes.Status200OK);

        app.MapGet("/people/rotation-eligible", async (
            [FromQuery] string? query,
            [FromQuery] int? limit,
            IWorkflowCatalogService workflowCatalogService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateWorkflow,
                "HR, Abteilungsleitung oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var people = await workflowCatalogService.SearchRotationEligiblePeopleAsync(
                query,
                access.User!,
                limit ?? 20);
            return Results.Ok(people);
        }).Produces<List<WorkflowTargetPersonDto>>(StatusCodes.Status200OK);

        app.MapGet("/requirements", async (
            [FromQuery] string? workflowDefinitionKey,
            IWorkflowCatalogService workflowCatalogService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanReadAllowedViews,
                "Leser role or higher is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            if (string.IsNullOrWhiteSpace(workflowDefinitionKey))
            {
                return Results.BadRequest(new { message = "workflowDefinitionKey is required." });
            }

            return Results.Ok(await workflowCatalogService.GetRequirementsAsync(workflowDefinitionKey));
        }).Produces<List<RequirementDto>>(StatusCodes.Status200OK);

        app.MapGet("/workflow-config", async (
            [FromQuery] int? roleId,
            [FromQuery] string? workflowDefinitionKey,
            IWorkflowCatalogService workflowCatalogService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateWorkflow,
                "HR, Abteilungsleitung oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            if (string.IsNullOrWhiteSpace(workflowDefinitionKey))
            {
                return Results.BadRequest(new { message = "workflowDefinitionKey is required." });
            }

            var workflowConfig = await workflowCatalogService.GetWorkflowConfigAsync(roleId, workflowDefinitionKey);
            if (workflowConfig is null)
            {
                return Results.NotFound(new { message = "Role not found." });
            }

            return Results.Ok(workflowConfig);
        }).Produces<WorkflowConfigDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status404NotFound);

        return app;
    }

}
