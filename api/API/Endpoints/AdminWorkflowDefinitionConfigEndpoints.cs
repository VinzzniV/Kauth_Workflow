using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminWorkflowDefinitionConfigEndpoints
{
    public static IEndpointRouteBuilder MapAdminWorkflowDefinitionConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/config/workflow-definitions", async (
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await repository.GetAdminWorkflowDefinitions());
        }).Produces<List<WorkflowDefinitionSummaryDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/config/workflow-definitions", async (
            [FromBody] CreateWorkflowDefinitionRequest request,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                return Results.Ok(await repository.CreateAdminWorkflowDefinition(request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionSummaryDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/config/workflow-definitions/{definitionId:int}/versions", async (
            int definitionId,
            [FromBody] CreateWorkflowDefinitionVersionRequest request,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var created = await repository.CreateAdminWorkflowDefinitionVersion(definitionId, request);
                return created is null
                    ? Results.NotFound(new { message = "Workflow definition not found." })
                    : Results.Created($"/admin/config/workflow-definition-versions/{created.Id}", created);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionVersionSummaryDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/config/workflow-definition-versions/{versionId:long}", async (
            long versionId,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var version = await repository.GetAdminWorkflowDefinitionVersion(versionId);
                return version is null
                    ? Results.NotFound(new { message = "Workflow definition version not found." })
                    : Results.Ok(version);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionVersionDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPut("/admin/config/workflow-definition-versions/{versionId:long}", async (
            long versionId,
            [FromBody] ReplaceWorkflowDefinitionVersionRequest request,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var updated = await repository.ReplaceAdminWorkflowDefinitionVersion(versionId, request);
                return updated is null
                    ? Results.NotFound(new { message = "Workflow definition version not found." })
                    : Results.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionVersionDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/config/workflow-definition-versions/{versionId:long}/publish", async (
            long versionId,
            [FromServices] IWorkflowDefinitionRuntimeRepository runtimeRepository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var published = await runtimeRepository.PublishWorkflowDefinitionVersion(versionId);
                return published is null
                    ? Results.NotFound(new { message = "Workflow definition version not found." })
                    : Results.Ok(published);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionVersionDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
