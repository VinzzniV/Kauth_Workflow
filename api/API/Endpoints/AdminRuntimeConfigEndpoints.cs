using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminRuntimeConfigEndpoints
{
    public static IEndpointRouteBuilder MapAdminRuntimeConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/config/workflow", async (
            [FromServices] IWorkflowRepository repository,
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

            var workflowConfig = await repository.GetWorkflowConfig(null);
            return Results.Ok(workflowConfig);
        }).Produces<WorkflowConfigDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/config/notification-email", async (
            [FromServices] INotificationEmailConfigurationService notificationEmailConfigurationService,
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

            return Results.Ok(await notificationEmailConfigurationService.GetAdminConfiguration());
        }).Produces<AdminNotificationEmailConfigurationDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/config/graph-application", async (
            [FromServices] IGraphApplicationConfigurationService graphApplicationConfigurationService,
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

            return Results.Ok(await graphApplicationConfigurationService.GetAdminConfiguration());
        }).Produces<AdminGraphApplicationConfigurationDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/config/notification-email", async (
            [FromBody] AdminNotificationEmailConfigurationUpdateRequest request,
            [FromServices] INotificationEmailConfigurationService notificationEmailConfigurationService,
            [FromServices] IWorkflowRepository repository,
            [FromServices] IWorkflowEmailNotificationSender emailNotificationSender,
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
                var previousConfiguration = await notificationEmailConfigurationService.GetAdminConfiguration();
                var updatedConfiguration = await notificationEmailConfigurationService.SaveAdminConfiguration(request);

                if (updatedConfiguration.Enabled)
                {
                    var shouldReplayTaskReady = updatedConfiguration.NotifyOnTaskReady
                        && (!previousConfiguration.Enabled || !previousConfiguration.NotifyOnTaskReady);
                    if (shouldReplayTaskReady)
                    {
                        await ReplayDisabledNotifications(
                            "task_ready",
                            repository,
                            emailNotificationSender,
                            repository.CreateReadyTaskNotifications);
                    }

                    var shouldReplayWorkflowCompleted = updatedConfiguration.NotifyOnWorkflowCompleted
                        && (!previousConfiguration.Enabled || !previousConfiguration.NotifyOnWorkflowCompleted);
                    if (shouldReplayWorkflowCompleted)
                    {
                        await ReplayDisabledNotifications(
                            "workflow_completed",
                            repository,
                            emailNotificationSender,
                            repository.CreateWorkflowCompletionNotifications);
                    }
                }

                return Results.Ok(updatedConfiguration);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminNotificationEmailConfigurationDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/config/notification-email/test", async (
            [FromBody] AdminNotificationEmailTestRequest request,
            [FromServices] INotificationEmailConfigurationService notificationEmailConfigurationService,
            [FromServices] INotificationEmailTestSender notificationEmailTestSender,
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

            var runtimeConfiguration = await notificationEmailConfigurationService.GetRuntimeConfiguration();
            var recipientEmail = string.IsNullOrWhiteSpace(request.RecipientEmail)
                ? runtimeConfiguration.TestRecipientEmail
                : request.RecipientEmail.Trim();

            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return Results.BadRequest(new { message = "Eine Testempfänger-Mailadresse ist erforderlich." });
            }

            var testResult = await notificationEmailTestSender.SendTestEmailAsync(recipientEmail);
            var updatedConfiguration = await notificationEmailConfigurationService.UpdateTestStatus(
                testResult.Status,
                testResult.ErrorMessage);

            return Results.Ok(new AdminNotificationEmailTestResponse
            {
                Configuration = updatedConfiguration,
                Result = new AdminNotificationEmailTestResultDto
                {
                    Success = testResult.Success,
                    Status = testResult.Status,
                    Message = testResult.Message,
                    RecipientEmail = testResult.RecipientEmail
                }
            });
        }).Produces<AdminNotificationEmailTestResponse>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task ReplayDisabledNotifications(
        string notificationType,
        IWorkflowRepository repository,
        IWorkflowEmailNotificationSender emailNotificationSender,
        Func<Guid, Task<List<WorkflowNotificationDispatchTarget>>> createNotifications)
    {
        var workflowUids = await repository.GetWorkflowUidsWithDisabledNotifications(notificationType);
        foreach (var workflowUid in workflowUids)
        {
            var notificationTargets = await createNotifications(workflowUid);
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
}
