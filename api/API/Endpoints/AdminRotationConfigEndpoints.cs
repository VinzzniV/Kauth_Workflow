using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminRotationConfigEndpoints
{
    public static IEndpointRouteBuilder MapAdminRotationConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/rotation/action-templates", async (
            [FromQuery] int? departmentId,
            [FromQuery] bool? isActive,
            IRotationTemplateAdminService rotationTemplateAdminService,
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
                return Results.Ok(await rotationTemplateAdminService.GetDepartmentActionTemplatesAsync(departmentId, isActive));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<List<DepartmentActionTemplateDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/rotation/action-templates/{templateId:int}", async (
            int templateId,
            IRotationTemplateAdminService rotationTemplateAdminService,
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
                var template = await rotationTemplateAdminService.GetDepartmentActionTemplateAsync(templateId);
                return template is null
                    ? Results.NotFound(new { message = "Maßnahmenvorlage nicht gefunden." })
                    : Results.Ok(template);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<DepartmentActionTemplateDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/rotation/action-templates", async (
            [FromBody] DepartmentActionTemplateUpsertRequest request,
            IRotationTemplateAdminService rotationTemplateAdminService,
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
                var template = await rotationTemplateAdminService.CreateDepartmentActionTemplateAsync(request, access.User!);
                return Results.Created($"/admin/rotation/action-templates/{template.Id}", template);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<DepartmentActionTemplateDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPut("/admin/rotation/action-templates/{templateId:int}", async (
            int templateId,
            [FromBody] DepartmentActionTemplateUpsertRequest request,
            IRotationTemplateAdminService rotationTemplateAdminService,
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
                var template = await rotationTemplateAdminService.UpdateDepartmentActionTemplateAsync(templateId, request, access.User!);
                return template is null
                    ? Results.NotFound(new { message = "Maßnahmenvorlage nicht gefunden." })
                    : Results.Ok(template);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<DepartmentActionTemplateDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/admin/rotation/action-templates/{templateId:int}", async (
            int templateId,
            IRotationTemplateAdminService rotationTemplateAdminService,
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
                var deleted = await rotationTemplateAdminService.DeleteDepartmentActionTemplateAsync(templateId, access.User!);
                return deleted
                    ? Results.NoContent()
                    : Results.NotFound(new { message = "Maßnahmenvorlage nicht gefunden." });
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
