using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/departments", async (
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateOrStartWorkflow,
                "HR role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await repository.GetDepartments());
        }).Produces<List<DepartmentDto>>(StatusCodes.Status200OK);

        app.MapGet("/roles", async (
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateOrStartWorkflow,
                "HR role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await repository.GetRoles());
        }).Produces<List<RoleDto>>(StatusCodes.Status200OK);

        app.MapGet("/requirements", async (
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanReadAllowedViews,
                "Leser role or higher is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            return Results.Ok(await repository.GetRequirements());
        }).Produces<List<RequirementDto>>(StatusCodes.Status200OK);

        app.MapGet("/workflow-config", async (
            [FromQuery] int? roleId,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateOrStartWorkflow,
                "HR role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var workflowConfig = await repository.GetWorkflowConfig(roleId);
            if (workflowConfig is null)
            {
                return Results.NotFound(new { message = "Role not found." });
            }

            return Results.Ok(workflowConfig);
        }).Produces<WorkflowConfigDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/workflows", async (
            [FromBody] CreateWorkflowRequest request,
            IWorkflowRepository repository,
            IWorkflowEmailNotificationSender emailNotificationSender,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateOrStartWorkflow,
                "HR role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                if (request.DeadlineDate.HasValue
                    && request.DeadlineDate.Value < DateOnly.FromDateTime(DateTime.Today))
                {
                    return Results.BadRequest(new { message = "Die Deadline darf nicht in der Vergangenheit liegen." });
                }

                var creation = await repository.CreateWorkflow(request, access.User!.UserId);
                var dispatchResults = await emailNotificationSender.SendNotificationsAsync(
                    creation.Uid,
                    creation.NotificationTargets);

                if (dispatchResults.Count > 0)
                {
                    await repository.ApplyNotificationDispatchResults(dispatchResults);
                }

                var workflow = await repository.GetWorkflowByUid(creation.Uid);
                if (workflow is null)
                {
                    return Results.Problem(
                        detail: "Workflow was created but could not be loaded afterwards.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                var taskCount = workflow.Tasks.Count;
                var assignmentCount = workflow.Tasks.Sum(task => task.Assignments.Count);
                var pendingNotifications = workflow.Notifications.Count(notification => notification.Status == "pending");
                var failedNotifications = workflow.Notifications.Count(notification => notification.Status == "failed");

                return Results.Created(
                    $"/workflows/{creation.Uid}",
                    new WorkflowCreateResponse
                    {
                        Uid = creation.Uid,
                        NotificationTargets = workflow.Notifications.Count,
                        FailedNotifications = failedNotifications,
                        Summary = new WorkflowCreateSummaryDto
                        {
                            WorkflowStatus = workflow.WorkflowStatus,
                            TaskCount = taskCount,
                            ReadyTaskCount = workflow.Tasks.Count(task => task.Status == "ready"),
                            BlockedTaskCount = workflow.Tasks.Count(task => task.Status == "blocked"),
                            DoneTaskCount = workflow.Tasks.Count(task => task.Status == "done"),
                            AssignmentCount = assignmentCount,
                            PendingNotifications = pendingNotifications
                        }
                    });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowCreateResponse>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest);

        app.MapGet("/workflows", async (
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessWorkflowOverview,
                "Workflow overview requires HR, Abteilungsleitung, Leser or Admin role.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var currentUser = access.User!;
            var observableDepartmentIds = await EndpointSupport.GetObservableWorkflowDepartmentIds(
                currentUser,
                repository,
                authorizationPolicy);

            var workflows = (await repository.GetWorkflows())
                .Where(workflow => EndpointSupport.CanObserveWorkflow(
                    currentUser,
                    workflow.DepartmentId,
                    workflow.WorkflowStatus,
                    observableDepartmentIds,
                    authorizationPolicy))
                .ToList();
            return Results.Ok(workflows);
        }).Produces<List<WorkflowListItemDto>>(StatusCodes.Status200OK);

        app.MapGet("/workflows/{uid:guid}", async (
            Guid uid,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessWorkflowOverview,
                "Workflow overview requires HR, Abteilungsleitung, Leser or Admin role.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var workflow = await repository.GetWorkflowByUid(uid);
            if (workflow is null)
            {
                return Results.NotFound(new { message = "Workflow not found." });
            }

            var currentUser = access.User!;
            var observableDepartmentIds = await EndpointSupport.GetObservableWorkflowDepartmentIds(
                currentUser,
                repository,
                authorizationPolicy);

            if (!EndpointSupport.CanObserveWorkflow(
                currentUser,
                workflow.DepartmentId,
                workflow.WorkflowStatus,
                observableDepartmentIds,
                authorizationPolicy))
            {
                return EndpointSupport.Forbidden("Workflow visibility depends on the current workflow phase and role.");
            }

            EndpointSupport.ApplyWorkflowTaskPermissions(workflow, currentUser, authorizationPolicy);
            return Results.Ok(workflow);
        }).Produces<WorkflowDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/workflows/{uid:guid}/audit-log", async (
            Guid uid,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                currentUser => authorizationPolicy.CanCreateOrStartWorkflow(currentUser)
                    || authorizationPolicy.CanAccessSupervisorStep(currentUser)
                    || authorizationPolicy.CanManageAdminConfiguration(currentUser),
                "Audit-Log erfordert HR, Abteilungsleitung oder Admin.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var workflow = await repository.GetWorkflowByUid(uid);
            if (workflow is null)
            {
                return Results.NotFound(new { message = "Workflow not found." });
            }

            var currentUser = access.User!;
            var observableDepartmentIds = await EndpointSupport.GetObservableWorkflowDepartmentIds(
                currentUser,
                repository,
                authorizationPolicy);

            if (!EndpointSupport.CanObserveWorkflow(
                currentUser,
                workflow.DepartmentId,
                workflow.WorkflowStatus,
                observableDepartmentIds,
                authorizationPolicy))
            {
                return EndpointSupport.Forbidden("Workflow visibility depends on the current workflow phase and role.");
            }

            return Results.Ok(await repository.GetWorkflowAuditLog(uid));
        }).Produces<List<WorkflowAuditEntryDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/workflows/{uid:guid}/tasks", async (
            Guid uid,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessWorkflowOverview,
                "Workflow overview requires HR, Abteilungsleitung, Leser or Admin role.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var workflow = await repository.GetWorkflowByUid(uid);
            if (workflow is null)
            {
                return Results.NotFound(new { message = "Workflow not found." });
            }

            var currentUser = access.User!;
            var observableDepartmentIds = await EndpointSupport.GetObservableWorkflowDepartmentIds(
                currentUser,
                repository,
                authorizationPolicy);

            if (!EndpointSupport.CanObserveWorkflow(
                currentUser,
                workflow.DepartmentId,
                workflow.WorkflowStatus,
                observableDepartmentIds,
                authorizationPolicy))
            {
                return EndpointSupport.Forbidden("Workflow visibility depends on the current workflow phase and role.");
            }

            EndpointSupport.ApplyWorkflowTaskPermissions(workflow, currentUser, authorizationPolicy);
            return Results.Ok(workflow.Tasks);
        }).Produces<List<WorkflowTaskDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

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
