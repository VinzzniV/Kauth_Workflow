using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminAutomationPlanEndpoints
{
    public static IEndpointRouteBuilder MapAdminAutomationPlanEndpoints(this IEndpointRouteBuilder app)
    {
        // Slice 1: WhatIf-Plan-Vorschau. Slice 3 erweitert:
        //   1. Response enthaelt planHash (SHA-256 ueber kanonisches NodePlanResult).
        //   2. Auth-Schranke ist nach Node-Typ differenziert:
        //      - automation-Node: CanManageAdminConfiguration (Slice-1-Verhalten, keine Regression)
        //      - task-Node: per-Node-Role-Match gegen workflow_nodes.automation_admin_role
        //   Vorfilter IsPotentialAutomationApprover deckt beide Pfade (Whitelist = Obermenge).
        app.MapPost("/admin/automation/plan", async (
            [FromServices] WorkflowAutomationPlanService planService,
            [FromServices] LifecycleRuntimeSettings runtimeSettings,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy,
            [FromBody] AutomationPlanRequest request,
            CancellationToken ct) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.IsPotentialAutomationApprover,
                "Not a potential automation approver.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            if (string.IsNullOrWhiteSpace(request.WorkflowInstanceUid)
                || string.IsNullOrWhiteSpace(request.NodeKey))
            {
                return Results.BadRequest("workflowInstanceUid und nodeKey sind Pflichtfelder.");
            }

            // Conditional Auth nach Node-Typ.
            var nodeInfo = await AutomationNodeAuthLookup.LoadAsync(
                runtimeSettings.ConnectionString, request.WorkflowInstanceUid, request.NodeKey, ct);
            if (nodeInfo is null)
            {
                return Results.NotFound($"Kein Workflow oder Node '{request.NodeKey}' für '{request.WorkflowInstanceUid}' gefunden.");
            }

            if (string.Equals(nodeInfo.NodeType, "automation", StringComparison.OrdinalIgnoreCase))
            {
                if (!authorizationPolicy.CanManageAdminConfiguration(access.User!))
                {
                    return EndpointSupport.Forbidden("Admin role is required for automation-node plans.");
                }
            }
            else if (string.Equals(nodeInfo.NodeType, "task", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(nodeInfo.AutomationAdminRole))
                {
                    return EndpointSupport.Forbidden("node_not_admin_gated");
                }
                if (!authorizationPolicy.HasAnyRole(access.User!, nodeInfo.AutomationAdminRole))
                {
                    return EndpointSupport.Forbidden("insufficient_role");
                }
            }
            // Andere Node-Typen koennen laut Slice-2-Validierung keine Actions tragen
            // — der Plan-Service liefert sowieso null → 404 unten.

            var result = await planService.ComputeNodePlanAsync(
                request.WorkflowInstanceUid, request.NodeKey, ct);

            if (result is null)
            {
                return Results.NotFound($"Kein Workflow oder Node '{request.NodeKey}' für '{request.WorkflowInstanceUid}' gefunden.");
            }

            var planHash = PlanHash.ComputeHash(result);
            return Results.Ok(new AutomationPlanResponse(result.NodeKey, result.Steps, planHash));
        })
        .Produces<AutomationPlanResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}

internal sealed record AutomationPlanRequest(string WorkflowInstanceUid, string NodeKey);

// Slice 3: planHash ergaenzt das bisherige NodePlanResult-Tuple. Frontend muss
// diesen Hash beim /admin/automation/approve mitschicken — Drift-Check echobacks
// dieselbe Funktion (PlanHash.ComputeHash) am Approval-Endpoint.
internal sealed record AutomationPlanResponse(
    string NodeKey,
    IReadOnlyList<ActionPlanStep> Steps,
    string PlanHash);
