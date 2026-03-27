using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class WorkflowSupervisorEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowSupervisorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/workflows/supervisor-step", async (
            ISupervisorStepService supervisorStepService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanEditSupervisorRequirements,
                "Abteilungsleitung oder Admin-Override ist erforderlich.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var workflows = await supervisorStepService.GetAssignedWorkflows(access.User!);
            return Results.Ok(workflows);
        }).Produces<List<WorkflowListItemDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/workflows/{uid:guid}/supervisor-step", async (
            Guid uid,
            ISupervisorStepService supervisorStepService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanEditSupervisorRequirements,
                "Abteilungsleitung oder Admin-Override ist erforderlich.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var requirements = await supervisorStepService.GetRequirements(uid, access.User!);
                if (requirements is null)
                {
                    return Results.NotFound(new { message = "Workflow not found." });
                }

                return Results.Ok(requirements);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<List<WorkflowRequirementSnapshotDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/workflows/{uid:guid}/supervisor-step", async (
            Guid uid,
            [FromBody] SupervisorStepUpdateRequest request,
            ISupervisorStepService supervisorStepService,
            IWorkflowRepository repository,
            IWorkflowEmailNotificationSender emailNotificationSender,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanEditSupervisorRequirements,
                "Abteilungsleitung oder Admin-Override ist erforderlich.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var workflow = await supervisorStepService.UpdateRequirements(
                    uid,
                    request.RequirementSelections,
                    access.User!);
                if (workflow is null)
                {
                    return Results.NotFound(new { message = "Workflow not found." });
                }

                var notificationTargets = await repository.CreateReadyTaskNotifications(uid);
                if (notificationTargets.Count > 0)
                {
                    var dispatchResults = await emailNotificationSender.SendNotificationsAsync(uid, notificationTargets);
                    if (dispatchResults.Count > 0)
                    {
                        await repository.ApplyNotificationDispatchResults(dispatchResults);
                    }
                }

                return Results.Ok(workflow);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
