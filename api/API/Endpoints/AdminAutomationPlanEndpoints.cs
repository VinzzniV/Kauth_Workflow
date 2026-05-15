using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminAutomationPlanEndpoints
{
    public static IEndpointRouteBuilder MapAdminAutomationPlanEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/automation/plan", async (
            [FromServices] WorkflowAutomationPlanService planService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy,
            [FromBody] AutomationPlanRequest request,
            CancellationToken ct) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            if (string.IsNullOrWhiteSpace(request.WorkflowInstanceUid)
                || string.IsNullOrWhiteSpace(request.NodeKey))
            {
                return Results.BadRequest("workflowInstanceUid und nodeKey sind Pflichtfelder.");
            }

            var result = await planService.ComputeNodePlanAsync(
                request.WorkflowInstanceUid, request.NodeKey, ct);

            return result is null
                ? Results.NotFound($"Kein Workflow oder Node '{request.NodeKey}' für '{request.WorkflowInstanceUid}' gefunden.")
                : Results.Ok(result);
        })
        .Produces<NodePlanResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}

internal sealed record AutomationPlanRequest(string WorkflowInstanceUid, string NodeKey);
