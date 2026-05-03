using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class WorkflowLinkEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowLinkEndpoints(this IEndpointRouteBuilder app)
    {
        // Workflow-Verknüpfung: Links lesen, erstellen, löschen; ableitbare Workflows und abgeleitete Antworten.
        app.MapGet("/workflows/{uid:guid}/links", async (
            Guid uid,
            IWorkflowRepository repository,
            IWorkflowVisibilityService workflowVisibilityService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessWorkflowOverview,
                "Workflow overview access is required.");
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
            var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);

            if (!workflowVisibilityService.CanObserveWorkflow(
                currentUser,
                workflow.DepartmentId,
                workflow.WorkflowStatus,
                observableDepartmentIds))
            {
                return EndpointSupport.Forbidden("Workflow visibility depends on the current workflow phase and role.");
            }

            var links = await repository.GetWorkflowLinks(uid);
            return Results.Ok(links);
        }).Produces<List<WorkflowLinkDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/workflows/{uid:guid}/related", async (
            Guid uid,
            IWorkflowRepository repository,
            IWorkflowVisibilityService workflowVisibilityService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessWorkflowOverview,
                "Workflow overview access is required.");
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
            var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);

            if (!workflowVisibilityService.CanObserveWorkflow(
                currentUser,
                workflow.DepartmentId,
                workflow.WorkflowStatus,
                observableDepartmentIds))
            {
                return EndpointSupport.Forbidden("Workflow visibility depends on the current workflow phase and role.");
            }

            var relatedWorkflows = await repository.GetRelatedWorkflows(uid);
            var visibleRelatedWorkflows = relatedWorkflows
                .Where(related => workflowVisibilityService.CanObserveWorkflow(
                    currentUser,
                    related.DepartmentId,
                    related.WorkflowStatus,
                    observableDepartmentIds))
                .ToList();

            return Results.Ok(visibleRelatedWorkflows);
        }).Produces<List<RelatedWorkflowSummaryDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/workflows/{uid:guid}/links", async (
            Guid uid,
            [FromBody] CreateWorkflowLinkRequest request,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                currentUser => authorizationPolicy.CanCreateOrStartWorkflow(currentUser)
                    || authorizationPolicy.CanManageAdminConfiguration(currentUser),
                "HR or Admin role is required to create workflow links.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var validLinkTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "derived_from", "supersedes", "related" };
            if (!validLinkTypes.Contains(request.LinkType))
            {
                return Results.BadRequest(new { message = $"Invalid link type '{request.LinkType}'. Allowed: derived_from, supersedes, related." });
            }

            var link = await repository.CreateWorkflowLink(uid, request, access.User!.UserId);
            if (link is null)
            {
                return Results.BadRequest(new { message = "Could not create link. Workflows not found or link already exists." });
            }

            return Results.Created($"/workflows/{uid}/links/{link.Id}", link);
        }).Produces<WorkflowLinkDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest);

        app.MapDelete("/workflows/{uid:guid}/links/{linkId:long}", async (
            Guid uid,
            long linkId,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                currentUser => authorizationPolicy.CanCreateOrStartWorkflow(currentUser)
                    || authorizationPolicy.CanManageAdminConfiguration(currentUser),
                "HR or Admin role is required to delete workflow links.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var deleted = await repository.DeleteWorkflowLink(uid, linkId, access.User!.UserId);
            if (!deleted)
            {
                return Results.NotFound(new { message = "Link not found." });
            }

            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/workflows/linkable", async (
            [FromQuery] int employeeNumber,
            [FromQuery] string? excludeUid,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateWorkflow,
                "HR, Abteilungsleitung oder Admin role is required to find linkable workflows.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            Guid? excludeGuid = null;
            if (!string.IsNullOrWhiteSpace(excludeUid) && Guid.TryParse(excludeUid, out var parsed))
            {
                excludeGuid = parsed;
            }

            var workflows = await repository.FindLinkableWorkflows(employeeNumber, excludeGuid);
            return Results.Ok(workflows);
        }).Produces<List<LinkableWorkflowDto>>(StatusCodes.Status200OK);

        app.MapGet("/workflows/derive-answers", async (
            [FromQuery] string sourceUid,
            [FromQuery] string targetWorkflowDefinitionKey,
            IWorkflowRepository repository,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateOrStartWorkflow,
                "HR role is required to derive answers.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            if (!Guid.TryParse(sourceUid, out var sourceGuid))
            {
                return Results.BadRequest(new { message = "Invalid sourceUid." });
            }

            if (string.IsNullOrWhiteSpace(targetWorkflowDefinitionKey))
            {
                return Results.BadRequest(new { message = "targetWorkflowDefinitionKey is required." });
            }

            var derived = await repository.GetDerivedAnswers(sourceGuid, targetWorkflowDefinitionKey.Trim());
            return Results.Ok(derived);
        }).Produces<List<DerivedAnswerDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest);

        return app;
    }
}
