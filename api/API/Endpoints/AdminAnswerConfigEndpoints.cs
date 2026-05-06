using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminAnswerConfigEndpoints
{
    public static IEndpointRouteBuilder MapAdminAnswerConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/config/answer-definitions", async (
            HttpRequest httpRequest,
            [FromQuery] int workflowDefinitionId,
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
                var query = AdminListQuery.From(httpRequest);
                return Results.Ok(await repository.GetAdminAnswerDefinitions(workflowDefinitionId, query));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminListPageDto<AdminAnswerDefinitionDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/config/answer-definitions", async (
            [FromBody] AdminAnswerDefinitionUpsertRequest request,
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
                return Results.Ok(await repository.CreateAdminAnswerDefinition(request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminAnswerDefinitionDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/config/answer-definitions/{definitionId:int}", async (
            int definitionId,
            [FromBody] AdminAnswerDefinitionUpsertRequest request,
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
                var result = await repository.UpdateAdminAnswerDefinition(definitionId, request);
                return result is null
                    ? Results.NotFound(new { message = "Answer definition not found." })
                    : Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminAnswerDefinitionDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/admin/config/answer-definitions/{definitionId:int}", async (
            int definitionId,
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
                var deleted = await repository.DeleteAdminAnswerDefinition(definitionId);
                return deleted ? Results.NoContent() : Results.NotFound(new { message = "Answer definition not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/config/role-answer-defaults", async (
            HttpRequest httpRequest,
            [FromQuery] int workflowDefinitionId,
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
                var query = AdminListQuery.From(httpRequest);
                return Results.Ok(await repository.GetAdminRoleAnswerDefaults(workflowDefinitionId, query));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminListPageDto<AdminRoleAnswerDefaultDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/config/role-answer-defaults", async (
            [FromBody] AdminRoleAnswerDefaultsBulkUpsertRequest request,
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
                return Results.Ok(await repository.UpsertAdminRoleAnswerDefaults(request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<List<AdminRoleAnswerDefaultDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
