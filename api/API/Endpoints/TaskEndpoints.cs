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
            ITaskApplicationService taskApplicationService,
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

            return Results.Ok(await taskApplicationService.GetTasksAsync(access.User!));
        }).Produces<List<TaskWithWorkflowDto>>(StatusCodes.Status200OK);

        app.MapGet("/tasks/{id:long}", async (
            long id,
            ITaskApplicationService taskApplicationService,
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

            try
            {
                var task = await taskApplicationService.GetTaskByIdAsync(id, access.User!);
                if (task is null)
                {
                    return Results.NotFound(new { message = "Task not found." });
                }

                return Results.Ok(task);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
        }).Produces<TaskWithWorkflowDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/tasks/{id:long}/status", async (
            long id,
            [FromBody] TaskStatusUpdateRequest request,
            ITaskApplicationService taskApplicationService,
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

            try
            {
                var task = await taskApplicationService.UpdateTaskStatusAsync(id, request, access.User!);
                if (task is null)
                {
                    return Results.NotFound(new { message = "Task not found." });
                }

                return Results.Ok(task);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
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
            ITaskApplicationService taskApplicationService,
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

            try
            {
                var task = await taskApplicationService.UpdateTaskAssignmentAsync(id, request, access.User!);
                if (task is null)
                {
                    return Results.NotFound(new { message = "Task not found." });
                }

                return Results.Ok(task);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
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
            ITaskApplicationService taskApplicationService,
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

            try
            {
                var task = await taskApplicationService.AddTaskCommentAsync(id, request, access.User!);
                if (task is null)
                {
                    return Results.NotFound(new { message = "Task not found." });
                }

                return Results.Ok(task);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
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
}
