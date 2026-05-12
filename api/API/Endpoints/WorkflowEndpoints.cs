using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapWorkflowMasterDataEndpoints();
        app.MapWorkflowSupervisorEndpoints();
        app.MapWorkflowLinkEndpoints();

        app.MapPost("/workflows", async (
            [FromBody] CreateWorkflowRequest request,
            IWorkflowRuntimeService workflowRuntimeService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateWorkflow,
                "HR, Abteilungsleitung oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var response = await workflowRuntimeService.CreateWorkflowAsync(request, access.User!);
                return Results.Created($"/workflows/{response.Uid}", response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (WorkflowRuntimeConsistencyException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }).Produces<WorkflowCreateResponse>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest);

        app.MapPost("/people", async (
            [FromBody] CreatePersonRequest request,
            IWorkflowRuntimeService workflowRuntimeService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateWorkflow,
                "HR, Abteilungsleitung oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var created = await workflowRuntimeService.CreatePersonAsync(request, access.User!);
                return Results.Created($"/people/{created.PersonId}", created);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowTargetPersonDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden);

        app.MapGet("/workflows", async (
            [FromQuery] string? status,
            [FromQuery] int? department,
            [FromQuery] string? workflowDefinitionKey,
            [FromQuery] string? search,
            [FromQuery] string? responsibility,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            IWorkflowRuntimeService workflowRuntimeService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy,
            CancellationToken cancellationToken) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessWorkflowOverview,
                "Workflow overview requires HR, Abteilungsleitung, Leser or Admin role.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var result = await workflowRuntimeService.GetWorkflowsAsync(
                    status,
                    department,
                    workflowDefinitionKey,
                    search,
                    responsibility,
                    limit,
                    offset,
                    access.User!,
                    cancellationToken);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<WorkflowListPageDto>(StatusCodes.Status200OK);

        app.MapGet("/workflows/{uid:guid}", async (
            Guid uid,
            IWorkflowRuntimeService workflowRuntimeService,
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

            try
            {
                var workflow = await workflowRuntimeService.GetWorkflowByUidAsync(uid, access.User!);
                if (workflow is null)
                {
                    return Results.NotFound(new { message = "Workflow not found." });
                }

                return Results.Ok(workflow);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
        }).Produces<WorkflowDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/workflows/{uid:guid}/audit-log", async (
            Guid uid,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            IWorkflowRuntimeService workflowRuntimeService,
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

            try
            {
                var auditLog = await workflowRuntimeService.GetWorkflowAuditLogAsync(
                    uid,
                    limit ?? 200,
                    offset ?? 0,
                    access.User!);
                if (auditLog is null)
                {
                    return Results.NotFound(new { message = "Workflow not found." });
                }

                return Results.Ok(auditLog);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<List<WorkflowAuditEntryDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/workflows/{uid:guid}/tasks", async (
            Guid uid,
            IWorkflowRuntimeService workflowRuntimeService,
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

            try
            {
                var tasks = await workflowRuntimeService.GetWorkflowTasksAsync(uid, access.User!);
                if (tasks is null)
                {
                    return Results.NotFound(new { message = "Workflow not found." });
                }

                return Results.Ok(tasks.Select(task => task.Task).ToList());
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
        }).Produces<List<WorkflowTaskDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/workflows/{uid:guid}/archive", async (
            Guid uid,
            IWorkflowRuntimeService workflowRuntimeService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanManageAdminConfiguration,
                "Admin role is required to archive workflows.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var archived = await workflowRuntimeService.ArchiveWorkflowAsync(uid, access.User!);
            if (!archived)
            {
                return Results.BadRequest(new { message = "Workflow kann nicht archiviert werden. Nur abgeschlossene, noch nicht archivierte Vorgänge können archiviert werden." });
            }

            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden);

        app.MapDelete("/workflows/{uid:guid}", async (
            Guid uid,
            IWorkflowRuntimeService workflowRuntimeService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateWorkflow,
                "HR, Abteilungsleitung oder Admin role ist erforderlich um Entwürfe zu löschen.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            var deleted = await workflowRuntimeService.DeleteWorkflowAsync(uid, access.User!);
            if (!deleted)
            {
                return Results.BadRequest(new { message = "Workflow kann nicht gelöscht werden. Nur Entwürfe (Status: draft) können gelöscht werden." });
            }

            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden);

        app.MapPost("/workflows/{uid:guid}/cancel", async (
            Guid uid,
            WorkflowCancellationRequest? request,
            IWorkflowRuntimeService workflowRuntimeService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            // Eintrittspolicy: irgendeine Workflow-Bearbeitungsrolle. Feinere Pruefung
            // (HR/Admin global, Manager nur eigene Abteilung) erfolgt im Service mit Department.
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanCreateWorkflow,
                "HR, Abteilungsleitung oder Admin role ist erforderlich um Vorgaenge zu stornieren.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            if (request is null)
            {
                return Results.BadRequest(new { message = "Storno-Grund ist erforderlich.", code = "missing_reason" });
            }

            var outcome = await workflowRuntimeService.CancelWorkflowAsync(uid, request, access.User!);
            return outcome.Status switch
            {
                WorkflowCancellationOutcomeStatus.Success => Results.Ok(outcome.Result),
                WorkflowCancellationOutcomeStatus.NotFound => Results.NotFound(new { message = outcome.ErrorMessage }),
                WorkflowCancellationOutcomeStatus.Forbidden => EndpointSupport.Forbidden(outcome.ErrorMessage ?? "Storno-Berechtigung fehlt."),
                WorkflowCancellationOutcomeStatus.InvalidStatus => Results.BadRequest(new { message = outcome.ErrorMessage, code = "invalid_status" }),
                WorkflowCancellationOutcomeStatus.InvalidReason => Results.BadRequest(new { message = outcome.ErrorMessage, code = "invalid_reason" }),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        }).Produces<WorkflowCancellationResultDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/people/{personId:long}/workflows", async (
            long personId,
            IWorkflowRuntimeService workflowRuntimeService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                authorizationPolicy.CanAccessWorkflowOverview,
                "Workflow-Übersicht Zugriff ist erforderlich.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var history = await workflowRuntimeService.GetPersonWorkflowHistoryAsync(personId, access.User!);
                if (history is null)
                {
                    return Results.NotFound(new { message = "Person nicht gefunden." });
                }

                return Results.Ok(history);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
        }).Produces<PersonWorkflowHistoryDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
