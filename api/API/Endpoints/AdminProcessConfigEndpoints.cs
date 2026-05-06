using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminProcessConfigEndpoints
{
    public static IEndpointRouteBuilder MapAdminProcessConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/config/task-templates", async (
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
                return Results.Ok(await repository.GetAdminTaskTemplates(workflowDefinitionId, query));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminListPageDto<AdminTaskTemplateDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/config/task-templates", async (
            [FromBody] AdminTaskTemplateUpsertRequest request,
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
                return Results.Ok(await repository.CreateAdminTaskTemplate(request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminTaskTemplateDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/config/task-templates/{templateId:int}", async (
            int templateId,
            [FromBody] AdminTaskTemplateUpsertRequest request,
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
                var result = await repository.UpdateAdminTaskTemplate(templateId, request);
                return result is null
                    ? Results.NotFound(new { message = "Task template not found." })
                    : Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminTaskTemplateDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/admin/config/task-templates/{templateId:int}", async (
            int templateId,
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
                var deleted = await repository.DeleteAdminTaskTemplate(templateId);
                return deleted ? Results.NoContent() : Results.NotFound(new { message = "Task template not found." });
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

        app.MapGet("/admin/config/task-templates/{templateId:int}/conditions", async (
            int templateId,
            HttpRequest httpRequest,
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
                return Results.Ok(await repository.GetAdminTaskTemplateConditions(templateId, query));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminListPageDto<AdminTaskTemplateConditionDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/config/task-templates/{templateId:int}/conditions", async (
            int templateId,
            [FromBody] AdminTaskTemplateConditionCreateRequest request,
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
                return Results.Ok(await repository.CreateAdminTaskTemplateCondition(templateId, request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminTaskTemplateConditionDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/admin/config/task-templates/{templateId:int}/conditions/{conditionId:long}", async (
            int templateId,
            long conditionId,
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
                var deleted = await repository.DeleteAdminTaskTemplateCondition(templateId, conditionId);
                return deleted ? Results.NoContent() : Results.NotFound(new { message = "Task template condition not found." });
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

        app.MapGet("/admin/config/task-templates/{templateId:int}/dependencies", async (
            int templateId,
            HttpRequest httpRequest,
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
                return Results.Ok(await repository.GetAdminTaskTemplateDependencies(templateId, query));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminListPageDto<AdminTaskTemplateDependencyDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/config/task-templates/{templateId:int}/dependencies", async (
            int templateId,
            [FromBody] AdminTaskTemplateDependencyCreateRequest request,
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
                return Results.Ok(await repository.CreateAdminTaskTemplateDependency(templateId, request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminTaskTemplateDependencyDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/admin/config/task-templates/{templateId:int}/dependencies/{dependencyId:long}", async (
            int templateId,
            long dependencyId,
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
                var deleted = await repository.DeleteAdminTaskTemplateDependency(templateId, dependencyId);
                return deleted ? Results.NoContent() : Results.NotFound(new { message = "Task template dependency not found." });
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

        return app;
    }
}
