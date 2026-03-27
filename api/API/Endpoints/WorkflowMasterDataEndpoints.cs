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
            IWorkflowRepository repository,
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

            return Results.Ok(await repository.GetDepartments());
        }).Produces<List<DepartmentDto>>(StatusCodes.Status200OK);

        app.MapGet("/roles", async (
            IWorkflowRepository repository,
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

            return Results.Ok(await repository.GetRoles());
        }).Produces<List<RoleDto>>(StatusCodes.Status200OK);

        app.MapGet("/process-types", async (
            IWorkflowRepository repository,
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

            var currentUser = access.User!;
            var managerOnly = authorizationPolicy.HasAnyRole(currentUser, AuthorizationRoles.Manager)
                && !authorizationPolicy.HasAnyRole(currentUser, AuthorizationRoles.Hr, AuthorizationRoles.Admin);

            return Results.Ok(await repository.GetActiveProcessTypes(managerOnly));
        }).Produces<List<WorkflowProcessTypeDto>>(StatusCodes.Status200OK);

        app.MapGet("/workflows/completed-onboardings", async (
            [FromQuery] string? search,
            [FromQuery] int? limit,
            IWorkflowRepository repository,
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

            var completedOnboardings = await repository.SearchCompletedOnboardings(search, limit ?? 20);
            return Results.Ok(completedOnboardings);
        }).Produces<List<CompletedOnboardingSearchResultDto>>(StatusCodes.Status200OK);

        app.MapGet("/workflow-target-people", async (
            [FromQuery] string? query,
            [FromQuery] int? limit,
            IWorkflowRepository repository,
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

            var people = await repository.SearchWorkflowTargetPeople(query, limit ?? 20);
            return Results.Ok(people);
        }).Produces<List<WorkflowTargetPersonDto>>(StatusCodes.Status200OK);

        app.MapGet("/requirements", async (
            [FromQuery] string? processTypeKey,
            IWorkflowRepository repository,
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

            return Results.Ok(await repository.GetRequirements(processTypeKey));
        }).Produces<List<RequirementDto>>(StatusCodes.Status200OK);

        app.MapGet("/workflow-config", async (
            [FromQuery] int? roleId,
            [FromQuery] string? processTypeKey,
            IWorkflowRepository repository,
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

            var workflowConfig = await repository.GetWorkflowConfig(roleId, processTypeKey);
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
