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
            [FromQuery] int processTypeId,
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
                return Results.Ok(await repository.GetAdminAnswerDefinitions(processTypeId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<List<AdminAnswerDefinitionDto>>(StatusCodes.Status200OK)
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
            [FromQuery] int processTypeId,
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
                return Results.Ok(await repository.GetAdminRoleAnswerDefaults(processTypeId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<List<AdminRoleAnswerDefaultDto>>(StatusCodes.Status200OK)
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

        // Bulk-Operationen: Massenhafte Workflow-Erstellung
        app.MapPost("/admin/bulk/department-change", async (
            [FromBody] BulkDepartmentChangeRequest request,
            IWorkflowRepository repository,
            IWorkflowEmailNotificationSender emailNotificationSender,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required for bulk operations.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            if (request.SourceDepartmentId == request.TargetDepartmentId)
            {
                return Results.BadRequest(new { message = "Quell- und Ziel-Abteilung dürfen nicht identisch sein." });
            }

            try
            {
                var result = await repository.BulkCreateDepartmentChangeWorkflows(request, access.User!.UserId);

                if (!request.DryRun)
                {
                    foreach (var workflowUid in result.Items
                                 .Where(item => item.WorkflowUid.HasValue)
                                 .Select(item => item.WorkflowUid!.Value))
                    {
                        var notificationTargets = await repository.GetWorkflowCreatedNotificationDispatchTargets(workflowUid);
                        if (notificationTargets.Count == 0)
                        {
                            continue;
                        }

                        var dispatchResults = await emailNotificationSender.SendNotificationsAsync(workflowUid, notificationTargets);
                        if (dispatchResults.Count > 0)
                        {
                            await repository.ApplyNotificationDispatchResults(dispatchResults);
                        }
                    }
                }

                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<BulkOperationResultDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
