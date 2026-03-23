using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/tasks", async (
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                currentUser => authorizationPolicy.CanAccessTechnicalTasks(currentUser)
                    || authorizationPolicy.CanManageAdminConfiguration(currentUser),
                "Fachbereich oder Admin-Override ist erforderlich.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var currentUser = access.User!;
            var tasks = await repository.GetTasks();
            if (!authorizationPolicy.CanManageAdminConfiguration(currentUser))
            {
                tasks = tasks
                    .Where(task => authorizationPolicy.CanUpdateTaskStatus(currentUser, task))
                    .ToList();
            }

            EndpointSupport.ApplyTaskPermissions(tasks, currentUser, authorizationPolicy);
            return Results.Ok(tasks);
        }).Produces<List<TaskWithWorkflowDto>>(StatusCodes.Status200OK);

        app.MapGet("/tasks/{id:long}", async (
            long id,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                currentUser => authorizationPolicy.CanAccessTechnicalTasks(currentUser)
                    || authorizationPolicy.CanManageAdminConfiguration(currentUser),
                "Fachbereich oder Admin-Override ist erforderlich.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var currentUser = access.User!;
            var task = await repository.GetTaskById(id);
            if (task is null)
            {
                return Results.NotFound(new { message = "Task not found." });
            }

            if (!authorizationPolicy.CanManageAdminConfiguration(currentUser)
                && !authorizationPolicy.CanUpdateTaskStatus(currentUser, task))
            {
                return EndpointSupport.Forbidden("Task visibility requires matching phase responsibility and assignment.");
            }

            EndpointSupport.ApplyTaskPermissions(task, currentUser, authorizationPolicy);
            return Results.Ok(task);
        }).Produces<TaskWithWorkflowDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/tasks/{id:long}/status", async (
            long id,
            [FromBody] TaskStatusUpdateRequest request,
            IWorkflowRepository repository,
            IWorkflowEmailNotificationSender emailNotificationSender,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessTaskStatusUpdates,
                "Fachbereich oder Admin-Override ist erforderlich.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var currentUser = access.User!;
            var currentTask = await repository.GetTaskById(id);
            if (currentTask is null)
            {
                return Results.NotFound(new { message = "Task not found." });
            }

            if (!authorizationPolicy.CanUpdateTaskStatus(currentUser, currentTask))
            {
                return EndpointSupport.Forbidden("Task updates require the current workflow phase, matching assignment or Admin override.");
            }

            try
            {
                var task = await repository.UpdateTaskStatus(id, request.Status, currentUser.UserId);
                if (task is null)
                {
                    return Results.NotFound(new { message = "Task not found." });
                }

                var notificationTargets = await repository.CreateReadyTaskNotifications(task.Workflow.WorkflowUid);
                if (notificationTargets.Count > 0)
                {
                    var dispatchResults = await emailNotificationSender.SendNotificationsAsync(
                        task.Workflow.WorkflowUid,
                        notificationTargets);
                    if (dispatchResults.Count > 0)
                    {
                        await repository.ApplyNotificationDispatchResults(dispatchResults);
                    }
                }

                var completionTargets = await repository.CreateWorkflowCompletionNotifications(task.Workflow.WorkflowUid);
                if (completionTargets.Count > 0)
                {
                    var completionDispatchResults = await emailNotificationSender.SendNotificationsAsync(
                        task.Workflow.WorkflowUid,
                        completionTargets);
                    if (completionDispatchResults.Count > 0)
                    {
                        await repository.ApplyNotificationDispatchResults(completionDispatchResults);
                    }
                }

                EndpointSupport.ApplyTaskPermissions(task, currentUser, authorizationPolicy);
                return Results.Ok(task);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<TaskWithWorkflowDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/tasks/{id:long}/assign", async (
            long id,
            [FromBody] TaskAssignRequest request,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin-Override ist erforderlich.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var currentUser = access.User!;
            var currentTask = await repository.GetTaskById(id);
            if (currentTask is null)
            {
                return Results.NotFound(new { message = "Task not found." });
            }

            if (!authorizationPolicy.CanUpdateTaskAssignment(currentUser, currentTask))
            {
                return EndpointSupport.Forbidden("Task assignment updates are limited to Admin override.");
            }

            try
            {
                var task = await repository.UpdateTaskAssignment(id, request, currentUser.UserId);
                if (task is null)
                {
                    return Results.NotFound(new { message = "Task not found." });
                }

                return Results.Ok(task);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<TaskWithWorkflowDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/tasks/{id:long}/comments", async (
            long id,
            [FromBody] TaskCommentCreateRequest request,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                currentUser => authorizationPolicy.CanCreateOrStartWorkflow(currentUser)
                    || authorizationPolicy.CanAccessSupervisorStep(currentUser)
                    || authorizationPolicy.CanAccessTechnicalTasks(currentUser)
                    || authorizationPolicy.CanManageAdminConfiguration(currentUser),
                "Kommentar erfordert HR, Abteilungsleitung, Fachbereich oder Admin.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var currentUser = access.User!;
            var currentTask = await repository.GetTaskById(id);
            if (currentTask is null)
            {
                return Results.NotFound(new { message = "Task not found." });
            }

            var commentAuthorizationUser = currentUser;
            if (authorizationPolicy.CanAccessSupervisorStep(currentUser)
                && !authorizationPolicy.CanCreateOrStartWorkflow(currentUser)
                && !authorizationPolicy.CanManageAdminConfiguration(currentUser))
            {
                var observableDepartmentIds = await EndpointSupport.GetObservableWorkflowDepartmentIds(
                    currentUser,
                    repository,
                    authorizationPolicy);

                var canObserveWorkflow = EndpointSupport.CanObserveWorkflow(
                    currentUser,
                    currentTask.Workflow.DepartmentId,
                    currentTask.Workflow.WorkflowStatus,
                    observableDepartmentIds,
                    authorizationPolicy);
                if (!canObserveWorkflow)
                {
                    if (!authorizationPolicy.CanAccessTechnicalTasks(currentUser))
                    {
                        return EndpointSupport.Forbidden("Workflow visibility depends on the current workflow phase and role.");
                    }

                    commentAuthorizationUser = CreateUserWithoutRole(currentUser, AuthorizationRoles.Manager);
                }
            }

            if (!authorizationPolicy.CanAddTaskComment(commentAuthorizationUser, currentTask))
            {
                return EndpointSupport.Forbidden("Kommentare erfordern Sicht auf den Vorgang und passende Bearbeitungsrechte.");
            }

            try
            {
                var task = await repository.AddTaskComment(id, request.CommentText, currentUser.UserId);
                if (task is null)
                {
                    return Results.NotFound(new { message = "Task not found." });
                }

                EndpointSupport.ApplyTaskPermissions(task, currentUser, authorizationPolicy);
                return Results.Ok(task);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<TaskWithWorkflowDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static CurrentUser CreateUserWithoutRole(CurrentUser user, string roleKey)
    {
        return new CurrentUser
        {
            UserId = user.UserId,
            ExternalKey = user.ExternalKey,
            DisplayName = user.DisplayName,
            Email = user.Email,
            IsActive = user.IsActive,
            DepartmentId = user.DepartmentId,
            DepartmentName = user.DepartmentName,
            IdentityProvider = user.IdentityProvider,
            Groups = user.Groups,
            DirectRoles = user.DirectRoles
                .Where(role => !string.Equals(role.RoleKey, roleKey, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            GroupRoles = user.GroupRoles
                .Where(role => !string.Equals(role.RoleKey, roleKey, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            EffectiveRoles = user.EffectiveRoles
                .Where(role => !string.Equals(role.RoleKey, roleKey, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            DirectResponsibilities = user.DirectResponsibilities,
            GroupResponsibilities = user.GroupResponsibilities,
            EffectiveResponsibilities = user.EffectiveResponsibilities
        };
    }
}
