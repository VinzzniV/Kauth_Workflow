using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminNotificationTemplateEndpoints
{
    public static IEndpointRouteBuilder MapAdminNotificationTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/notification-templates", async (
            HttpRequest request,
            [FromServices] INotificationTemplateService notificationTemplateService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var query = AdminListQuery.From(request);
            return Results.Ok(await notificationTemplateService.GetAdminTemplates(query));
        }).Produces<AdminListPageDto<AdminNotificationTemplateDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPut("/admin/notification-templates/{templateKey}", async (
            string templateKey,
            [FromBody] AdminNotificationTemplateUpdateRequest request,
            [FromServices] INotificationTemplateService notificationTemplateService,
            [FromServices] ISystemEventLogService systemEventLogService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
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
                var updatedTemplate = await notificationTemplateService.UpdateAdminTemplate(templateKey, request);
                await systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                {
                    Severity = "info",
                    Source = "admin",
                    Category = "configuration",
                    EventKey = "notification_template_updated",
                    Message = $"Notification template '{updatedTemplate.TemplateKey}' updated.",
                    ActorUserId = access.User?.UserId,
                    EntityType = "notification_template",
                    EntityId = updatedTemplate.TemplateKey,
                    Details = new
                    {
                        updatedTemplate.TemplateKey,
                        updatedTemplate.DisplayName
                    }
                });

                return Results.Ok(updatedTemplate);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminNotificationTemplateDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/notification-templates/preview-targets/workflows", async (
            [FromQuery] string? search,
            [FromQuery] int? limit,
            [FromServices] INotificationTemplateService notificationTemplateService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await notificationTemplateService.SearchWorkflowPreviewTargets(search, limit ?? 20));
        }).Produces<List<AdminNotificationTemplateWorkflowPreviewTargetDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/notification-templates/preview-targets/rotation-plans", async (
            [FromQuery] string? search,
            [FromQuery] int? limit,
            [FromServices] INotificationTemplateService notificationTemplateService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await notificationTemplateService.SearchRotationPlanPreviewTargets(search, limit ?? 20));
        }).Produces<List<AdminNotificationTemplateRotationPlanPreviewTargetDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/notification-templates/{templateKey}/preview", async (
            string templateKey,
            [FromBody] AdminNotificationTemplatePreviewRequest request,
            [FromServices] INotificationTemplateService notificationTemplateService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy) =>
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
                return Results.Ok(await notificationTemplateService.BuildPreview(templateKey, request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminNotificationTemplatePreviewResponseDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
