using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class AdminAutomationApprovalEndpoints
{
    public static IEndpointRouteBuilder MapAdminAutomationApprovalEndpoints(this IEndpointRouteBuilder app)
    {
        // Slice 3: Issue one-shot Re-Auth-Token (60s TTL). Vorfilter ueber
        // IsPotentialAutomationApprover schuetzt vor Probing; per-Node-Role-Match
        // erfolgt erst am /approve-Endpoint.
        app.MapPost("/admin/automation/reauth", async (
            [FromServices] AutomationReauthTokenService reauthTokenService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy,
            [FromBody] ReauthTokenIssueRequest? request,
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

            var purpose = string.IsNullOrWhiteSpace(request?.Purpose)
                ? AutomationReauthTokenService.PurposeAutomationApproval
                : request!.Purpose!;

            var issued = await reauthTokenService.IssueAsync(access.User!.UserId, purpose, ct);
            return Results.Ok(new ReauthTokenIssueResponse(issued.Token, issued.ExpiresAt));
        })
        .Produces<ReauthTokenIssueResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Slice 3: Approve + execute. Drei-Schichten-Auth (Auth + Vorfilter + per-Node).
        app.MapPost("/admin/automation/approve", async (
            [FromServices] AutomationApprovalService approvalService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy,
            [FromBody] AutomationApprovalRequest request,
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

            var outcome = await approvalService.ApproveAsync(request, access.User!, ct);
            return outcome switch
            {
                AutomationApprovalOutcome.Success s => Results.Accepted(
                    uri: null,
                    value: new AutomationApprovalSuccessResponse(s.ApprovalId, s.FirstJobId)),
                AutomationApprovalOutcome.BadRequest br => Results.BadRequest(new { message = br.Message }),
                AutomationApprovalOutcome.NotFound nf => Results.NotFound(new { message = nf.Message }),
                AutomationApprovalOutcome.NodeTypeNotApprovable =>
                    Results.BadRequest(new { error = "node_type_not_approvable" }),
                AutomationApprovalOutcome.NodeNotAdminGated =>
                    EndpointSupport.Forbidden("node_not_admin_gated"),
                AutomationApprovalOutcome.InsufficientRole =>
                    EndpointSupport.Forbidden("insufficient_role"),
                AutomationApprovalOutcome.ReauthRequired =>
                    Results.Json(new { error = "reauth_required" }, statusCode: StatusCodes.Status401Unauthorized),
                AutomationApprovalOutcome.PlanUnavailable p =>
                    Results.Json(
                        new { error = "plan_unavailable", failedActions = p.FailedActionKeys },
                        statusCode: StatusCodes.Status422UnprocessableEntity),
                AutomationApprovalOutcome.PlanDrift d =>
                    Results.Json(
                        new { error = "plan_drift", currentPlanHash = d.CurrentPlanHash, plan = d.Plan },
                        statusCode: StatusCodes.Status409Conflict),
                AutomationApprovalOutcome.AlreadyApproved a =>
                    Results.Json(
                        new
                        {
                            error = "already_approved",
                            existingApprovalId = a.ExistingApproval?.ApprovalId,
                            approverUserId = a.ExistingApproval?.ApproverUserId,
                            approvedAt = a.ExistingApproval?.ApprovedAt
                        },
                        statusCode: StatusCodes.Status409Conflict),
                _ => Results.Problem("Unhandled approval outcome.")
            };
        })
        .Produces<AutomationApprovalSuccessResponse>(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status422UnprocessableEntity);

        // Slice 7: Live-Status pro Approval — Approval-Dialog polled das im running-State.
        app.MapGet("/admin/automation/approvals/{approvalId:long}/status", async (
            [FromRoute] long approvalId,
            [FromServices] AutomationApprovalStatusService statusService,
            [FromServices] IUserContext userContext,
            [FromServices] IAuthorizationPolicyService authorizationPolicy,
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

            var outcome = await statusService.GetStatusAsync(approvalId, access.User!, ct);
            return outcome switch
            {
                AutomationApprovalStatusOutcome.Success s => Results.Ok(s.Response),
                AutomationApprovalStatusOutcome.NotFound => Results.NotFound(new { message = "approval_not_found" }),
                AutomationApprovalStatusOutcome.InsufficientRole => EndpointSupport.Forbidden("insufficient_role"),
                _ => Results.Problem("Unhandled status outcome.")
            };
        })
        .Produces<AutomationApprovalStatusResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

internal sealed record ReauthTokenIssueRequest(string? Purpose);
internal sealed record ReauthTokenIssueResponse(string Token, DateTimeOffset ExpiresAt);
internal sealed record AutomationApprovalSuccessResponse(long ApprovalId, long FirstJobId);
