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

            return Results.Ok(await workflowCatalogService.GetDepartmentsAsync());
        }).Produces<List<DepartmentDto>>(StatusCodes.Status200OK);

        app.MapGet("/roles", async (
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

            return Results.Ok(await workflowCatalogService.GetRolesAsync(access.User!));
        }).Produces<List<RoleDto>>(StatusCodes.Status200OK);

        app.MapGet("/process-types", async (
            IWorkflowCatalogService workflowCatalogService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessWorkflowOverview,
                "Workflow overview access is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await workflowCatalogService.GetProcessTypesAsync(access.User!));
        }).Produces<List<WorkflowProcessTypeDto>>(StatusCodes.Status200OK);

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

        app.MapGet("/workflows/completed-onboardings", async (
            [FromQuery] string? search,
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
                search,
                access.User!,
                limit ?? 20);
            return Results.Ok(targetPersonSources.Select(ToCompletedOnboardingSearchResult).ToList());
        }).Produces<List<CompletedOnboardingSearchResultDto>>(StatusCodes.Status200OK);

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

        app.MapGet("/requirements", async (
            [FromQuery] string? processTypeKey,
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

            if (string.IsNullOrWhiteSpace(processTypeKey))
            {
                return Results.BadRequest(new { message = "processTypeKey is required." });
            }

            return Results.Ok(await workflowCatalogService.GetRequirementsAsync(processTypeKey));
        }).Produces<List<RequirementDto>>(StatusCodes.Status200OK);

        app.MapGet("/workflow-config", async (
            [FromQuery] int? roleId,
            [FromQuery] string? processTypeKey,
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

            if (string.IsNullOrWhiteSpace(processTypeKey))
            {
                return Results.BadRequest(new { message = "processTypeKey is required." });
            }

            var workflowConfig = await workflowCatalogService.GetWorkflowConfigAsync(roleId, processTypeKey);
            if (workflowConfig is null)
            {
                return Results.NotFound(new { message = "Role not found." });
            }

            return Results.Ok(workflowConfig);
        }).Produces<WorkflowConfigDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static CompletedOnboardingSearchResultDto ToCompletedOnboardingSearchResult(
        WorkflowTargetPersonSourceDto source)
    {
        return new CompletedOnboardingSearchResultDto
        {
            WorkflowUid = source.WorkflowUid,
            PersonId = source.PersonId,
            DisplayName = source.DisplayName,
            FirstName = source.FirstName,
            LastName = source.LastName,
            EmployeeNumber = source.EmployeeNumber,
            BadgeNumber = source.BadgeNumber,
            DepartmentId = source.DepartmentId,
            DepartmentName = source.DepartmentName,
            RoleId = source.RoleId,
            RoleName = source.RoleName,
            CompletedAt = source.CompletedAt,
            ArchivedAt = source.ArchivedAt
        };
    }
}
