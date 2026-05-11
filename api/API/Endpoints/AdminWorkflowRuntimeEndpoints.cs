using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminWorkflowRuntimeEndpoints
{
    public static IEndpointRouteBuilder MapAdminWorkflowRuntimeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/runtime/workflow-instances", async (
            [FromBody] CreateWorkflowDefinitionInstanceRequest request,
            [FromServices] IWorkflowDefinitionRuntimeService runtimeService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy,
            CancellationToken cancellationToken) =>
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
                var created = await runtimeService.CreateWorkflowInstanceAsync(request, access.User!, cancellationToken);
                return Results.Created($"/admin/runtime/workflow-instances/{created.WorkflowUid}", created);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionRuntimeDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/runtime/workflow-instances/{uid:guid}", async (
            Guid uid,
            [FromServices] IWorkflowDefinitionRuntimeService runtimeService,
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

            var detail = await runtimeService.GetWorkflowInstanceAsync(uid, access.User!);
            return detail is null
                ? Results.NotFound(new { message = "Workflow runtime instance not found." })
                : Results.Ok(detail);
        }).Produces<WorkflowDefinitionRuntimeDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/runtime/workflow-instances/{uid:guid}/events", async (
            HttpRequest request,
            Guid uid,
            [FromServices] IWorkflowDefinitionRuntimeService runtimeService,
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

            var query = CursorPageQuery.From(request);
            return Results.Ok(await runtimeService.GetWorkflowInstanceEventsAsync(uid, query, access.User!));
        }).Produces<CursorPageDto<WorkflowRuntimeEventDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/admin/runtime/workflow-instances/{uid:guid}/automation-jobs", async (
            HttpRequest request,
            Guid uid,
            [FromServices] IWorkflowAutomationService automationService,
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

            var query = CursorPageQuery.From(request);
            return Results.Ok(await automationService.GetWorkflowAutomationJobsAsync(uid, query));
        }).Produces<CursorPageDto<AutomationJobDetailDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/runtime/workflow-instances/{uid:guid}/nodes/{nodeInstanceId:long}/form-completions", async (
            Guid uid,
            long nodeInstanceId,
            [FromBody] CompleteRuntimeFormNodeRequest request,
            [FromServices] IWorkflowDefinitionRuntimeService runtimeService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy,
            CancellationToken cancellationToken) =>
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
                var updated = await runtimeService.CompleteFormNodeAsync(uid, nodeInstanceId, request, access.User!, cancellationToken);
                return updated is null
                    ? Results.NotFound(new { message = "Workflow runtime instance not found." })
                    : Results.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionRuntimeDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/runtime/workflow-instances/{uid:guid}/nodes/{nodeInstanceId:long}/approval-completions", async (
            Guid uid,
            long nodeInstanceId,
            [FromBody] CompleteRuntimeApprovalNodeRequest request,
            [FromServices] IWorkflowDefinitionRuntimeService runtimeService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy,
            CancellationToken cancellationToken) =>
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
                var updated = await runtimeService.CompleteApprovalNodeAsync(uid, nodeInstanceId, request, access.User!, cancellationToken);
                return updated is null
                    ? Results.NotFound(new { message = "Workflow runtime instance not found." })
                    : Results.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionRuntimeDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/admin/runtime/workflow-instances/{uid:guid}/nodes/{nodeInstanceId:long}/task-completions", async (
            Guid uid,
            long nodeInstanceId,
            [FromBody] CompleteRuntimeTaskNodeRequest request,
            [FromServices] IWorkflowDefinitionRuntimeService runtimeService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy,
            CancellationToken cancellationToken) =>
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
                var updated = await runtimeService.CompleteTaskNodeAsync(uid, nodeInstanceId, request, access.User!, cancellationToken);
                return updated is null
                    ? Results.NotFound(new { message = "Workflow runtime instance not found." })
                    : Results.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowDefinitionRuntimeDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
