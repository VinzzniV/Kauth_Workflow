using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/config/workflow", async (
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

            var workflowConfig = await repository.GetWorkflowConfig(null);
            return Results.Ok(workflowConfig);
        }).Produces<WorkflowConfigDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/config/notification-email", async (
            INotificationEmailConfigurationService notificationEmailConfigurationService,
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

            return Results.Ok(await notificationEmailConfigurationService.GetAdminConfiguration());
        }).Produces<AdminNotificationEmailConfigurationDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/config/notification-email", async (
            [FromBody] AdminNotificationEmailConfigurationUpdateRequest request,
            INotificationEmailConfigurationService notificationEmailConfigurationService,
            IWorkflowRepository repository,
            IWorkflowEmailNotificationSender emailNotificationSender,
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
            INotificationEmailConfigurationService notificationEmailConfigurationService,
            INotificationEmailTestSender notificationEmailTestSender,
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

        app.MapGet("/admin/auth/users", async (
            IUserAuthorizationRepository userAuthorizationRepository,
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

            return Results.Ok(await userAuthorizationRepository.GetAdminUsers());
        }).Produces<List<AdminUserDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/auth/roles", async (
            IUserAuthorizationRepository userAuthorizationRepository,
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

            return Results.Ok(await userAuthorizationRepository.GetAdminRoles());
        }).Produces<List<AdminRoleDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/auth/groups", async (
            IUserAuthorizationRepository userAuthorizationRepository,
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

            return Results.Ok(await userAuthorizationRepository.GetAdminGroups());
        }).Produces<List<AdminGroupDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/master-data/departments", async (
            IUserAuthorizationRepository userAuthorizationRepository,
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

            return Results.Ok(await userAuthorizationRepository.GetAdminDepartmentAssignments());
        }).Produces<List<AdminDepartmentAssignmentDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/master-data/departments", async (
            [FromBody] AdminDepartmentCreateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
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
                var department = await userAuthorizationRepository.CreateDepartment(request.DepartmentName);
                return Results.Ok(department);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminDepartmentAssignmentDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/admin/master-data/departments/{departmentId:int}", async (
            int departmentId,
            IUserAuthorizationRepository userAuthorizationRepository,
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
                var deleted = await userAuthorizationRepository.DeleteDepartment(departmentId);
                return deleted ? Results.NoContent() : Results.NotFound(new { message = "Department not found." });
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

        app.MapGet("/admin/master-data/responsibilities", async (
            IUserAuthorizationRepository userAuthorizationRepository,
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

            return Results.Ok(await userAuthorizationRepository.GetAdminResponsibilityOwners());
        }).Produces<List<AdminResponsibilityOwnerDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/master-data/users", async (
            [FromBody] AdminUserCreateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
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
                var user = await userAuthorizationRepository.CreateUser(
                    request.ExternalKey,
                    request.DisplayName,
                    request.Email,
                    request.NotificationEmail,
                    request.DepartmentId,
                    request.IsActive);
                return Results.Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminUserDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapDelete("/admin/master-data/users/{userId:long}", async (
            long userId,
            IUserAuthorizationRepository userAuthorizationRepository,
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
                var deleted = await userAuthorizationRepository.DeleteUser(userId);
                return deleted ? Results.NoContent() : Results.NotFound(new { message = "User not found." });
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

        app.MapPatch("/admin/master-data/users/{userId:long}", async (
            long userId,
            [FromBody] AdminUserMasterDataUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
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
                var user = await userAuthorizationRepository.UpdateUserMasterData(
                    userId,
                    request.ExternalKey,
                    request.DisplayName,
                    request.Email,
                    request.NotificationEmail,
                    request.DepartmentId,
                    request.IsActive);
                if (user is null)
                {
                    return Results.NotFound(new { message = "User not found." });
                }

                return Results.Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminUserDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/master-data/departments/{departmentId:int}", async (
            int departmentId,
            [FromBody] AdminDepartmentAssignmentUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
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
                var assignment = await userAuthorizationRepository.UpdateDepartmentAssignment(
                    departmentId,
                    request.DepartmentLeadUserId,
                    request.RequirementOwnerUserId);
                if (assignment is null)
                {
                    return Results.NotFound(new { message = "Department not found." });
                }

                return Results.Ok(assignment);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminDepartmentAssignmentDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/master-data/responsibilities/{responsibilityId:int}", async (
            int responsibilityId,
            [FromBody] AdminResponsibilityOwnerUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
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
                var assignment = await userAuthorizationRepository.UpdateResponsibilityOwner(
                    responsibilityId,
                    request.AppUserId,
                    request.DepartmentId);
                if (assignment is null)
                {
                    return Results.NotFound(new { message = "Responsibility not found." });
                }

                return Results.Ok(assignment);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminResponsibilityOwnerDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/auth/users/{userId:long}/roles", async (
            long userId,
            [FromBody] AdminUserRoleUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
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
                var user = await userAuthorizationRepository.UpdateUserRoles(userId, request.RoleIds);
                if (user is null)
                {
                    return Results.NotFound(new { message = "User not found." });
                }

                return Results.Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminUserDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/auth/users/{userId:long}/groups", async (
            long userId,
            [FromBody] AdminUserGroupUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
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
                var user = await userAuthorizationRepository.UpdateUserGroups(userId, request.GroupIds);
                if (user is null)
                {
                    return Results.NotFound(new { message = "User not found." });
                }

                return Results.Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminUserDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPatch("/admin/auth/groups/{groupId:int}/roles", async (
            int groupId,
            [FromBody] AdminGroupRoleUpdateRequest request,
            IUserAuthorizationRepository userAuthorizationRepository,
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
                var group = await userAuthorizationRepository.UpdateGroupRoles(groupId, request.RoleIds);
                if (group is null)
                {
                    return Results.NotFound(new { message = "Group not found." });
                }

                return Results.Ok(group);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<AdminGroupDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
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
