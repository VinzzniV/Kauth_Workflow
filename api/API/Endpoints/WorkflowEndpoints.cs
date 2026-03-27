using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class WorkflowEndpoints
{
    private static readonly HashSet<string> SupportedWorkflowRuntimeStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "draft",
        "waiting_for_supervisor",
        "waiting_for_department",
        "in_progress",
        "completed"
    };

    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapWorkflowMasterDataEndpoints();
        app.MapWorkflowSupervisorEndpoints();
        app.MapWorkflowLinkEndpoints();

        app.MapPost("/workflows", async (
            [FromBody] CreateWorkflowRequest request,
            IWorkflowRepository repository,
            IWorkflowEmailNotificationSender emailNotificationSender,
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
                var currentUser = access.User!;
                if (string.IsNullOrWhiteSpace(request.ProcessTypeKey))
                {
                    return Results.BadRequest(new { message = "Der Prozesstyp ist erforderlich." });
                }

                var normalizedProcessTypeKey = request.ProcessTypeKey.Trim().ToLowerInvariant();
                var managerCreatableProcessType = await repository.IsManagerCreatableProcessType(normalizedProcessTypeKey);
                if (!authorizationPolicy.CanCreateWorkflowForProcessType(currentUser, managerCreatableProcessType))
                {
                    return EndpointSupport.Forbidden("Der gewählte Prozesstyp ist für Ihre Rolle nicht freigegeben.");
                }

                var selectedProcessType = (await repository.GetActiveProcessTypes())
                    .FirstOrDefault(processType => string.Equals(processType.Key, normalizedProcessTypeKey, StringComparison.OrdinalIgnoreCase));
                if (selectedProcessType is null)
                {
                    return Results.BadRequest(new { message = $"Unbekannter oder inaktiver Prozesstyp '{normalizedProcessTypeKey}'." });
                }

                var requestValidationError = ValidateCreateWorkflowRequest(request, selectedProcessType);
                if (requestValidationError is not null)
                {
                    return Results.BadRequest(new { message = requestValidationError });
                }

                if (request.DeadlineDate.HasValue
                    && request.DeadlineDate.Value < DateOnly.FromDateTime(DateTime.Today))
                {
                    return Results.BadRequest(new { message = "Die Deadline darf nicht in der Vergangenheit liegen." });
                }

                var creation = await repository.CreateWorkflow(request, currentUser.UserId);
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
            [FromQuery] string? status,
            [FromQuery] int? department,
            [FromQuery] string? processTypeKey,
            [FromQuery] string? search,
            [FromQuery] string? responsibility,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
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

            if (limit.HasValue && limit.Value <= 0)
            {
                return Results.BadRequest(new { message = "limit must be greater than 0." });
            }

            if (offset.HasValue && offset.Value < 0)
            {
                return Results.BadRequest(new { message = "offset must be greater than or equal to 0." });
            }

            var normalizedStatus = string.IsNullOrWhiteSpace(status)
                ? null
                : status.Trim().ToLowerInvariant();
            if (normalizedStatus is not null && !SupportedWorkflowRuntimeStatuses.Contains(normalizedStatus))
            {
                return Results.BadRequest(new { message = $"status '{status}' is not supported." });
            }

            var normalizedSearch = string.IsNullOrWhiteSpace(search)
                ? null
                : search.Trim().ToLowerInvariant();
            var normalizedProcessTypeKey = string.IsNullOrWhiteSpace(processTypeKey)
                ? null
                : processTypeKey.Trim().ToLowerInvariant();
            var normalizedResponsibility = string.IsNullOrWhiteSpace(responsibility)
                ? null
                : responsibility.Trim();

            var currentUser = access.User!;
            var isPaged = limit.HasValue || offset.HasValue;
            var effectiveOffset = offset ?? 0;

            var query = new WorkflowListQuery
            {
                ReaderOnly = !authorizationPolicy.CanCreateWorkflow(currentUser),
                Status = normalizedStatus,
                ProcessTypeKey = normalizedProcessTypeKey,
                Search = normalizedSearch,
                DepartmentId = department,
                Responsibility = normalizedResponsibility,
                Limit = limit,
                Offset = effectiveOffset,
                IncludeFilterOptions = isPaged
            };

            var result = await repository.GetFilteredWorkflows(query);

            return Results.Ok(new WorkflowListPageDto
            {
                Items = result.Items,
                Count = result.TotalCount,
                Offset = effectiveOffset,
                Limit = limit,
                DepartmentOptions = result.DepartmentOptions,
                ResponsibilityOptions = result.ResponsibilityOptions
            });
        }).Produces<WorkflowListPageDto>(StatusCodes.Status200OK);

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
            [FromQuery] int? limit,
            [FromQuery] int? offset,
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

            if (limit.HasValue && limit.Value <= 0)
            {
                return Results.BadRequest(new { message = "limit must be greater than 0." });
            }

            if (offset.HasValue && offset.Value < 0)
            {
                return Results.BadRequest(new { message = "offset must be greater than or equal to 0." });
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

            var effectiveLimit = Math.Min(limit ?? 200, 500);
            var effectiveOffset = offset ?? 0;
            return Results.Ok(await repository.GetWorkflowAuditLog(uid, effectiveLimit, effectiveOffset));
        }).Produces<List<WorkflowAuditEntryDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
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

        // Workflow-Lifecycle: Archivierung (nur abgeschlossene) und Draft-Löschung (nur vor dem Start).
        app.MapPost("/workflows/{uid:guid}/archive", async (
            Guid uid,
            IWorkflowRepository repository,
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

            var archived = await repository.ArchiveWorkflow(uid, access.User!.UserId);
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
            IWorkflowRepository repository,
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

            var deleted = await repository.DeleteDraftWorkflow(uid);
            if (!deleted)
            {
                return Results.BadRequest(new { message = "Workflow kann nicht gelöscht werden. Nur Entwürfe (Status: draft) können gelöscht werden." });
            }

            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden);

        app.MapGet("/people/{personId:long}/workflows", async (
            long personId,
            IWorkflowRepository repository,
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

            var history = await repository.GetPersonWorkflowHistory(personId);
            if (history is null)
            {
                return Results.NotFound(new { message = "Person nicht gefunden." });
            }

            return Results.Ok(history);
        }).Produces<PersonWorkflowHistoryDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    private static string? ValidateCreateWorkflowRequest(
        CreateWorkflowRequest request,
        WorkflowProcessTypeDto selectedProcessType)
    {
        if (selectedProcessType.RequiresTargetPerson)
        {
            return request.TargetPersonId.HasValue
                ? null
                : $"Der Prozesstyp '{selectedProcessType.Name}' erfordert eine bestehende Zielperson.";
        }

        if (request.TargetPersonId.HasValue)
        {
            return $"Der Prozesstyp '{selectedProcessType.Name}' darf nicht mit einer bestehenden Zielperson angelegt werden.";
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return $"Der Prozesstyp '{selectedProcessType.Name}' erfordert Vorname und Nachname der neuen Person.";
        }

        if (!request.EmployeeNumber.HasValue || request.EmployeeNumber.Value <= 0)
        {
            return $"Der Prozesstyp '{selectedProcessType.Name}' erfordert eine gültige Personalnummer.";
        }

        if (!request.BadgeNumber.HasValue || request.BadgeNumber.Value <= 0)
        {
            return $"Der Prozesstyp '{selectedProcessType.Name}' erfordert eine gültige Kartennummer.";
        }

        if (!request.DepartmentId.HasValue || !request.RoleId.HasValue)
        {
            return $"Der Prozesstyp '{selectedProcessType.Name}' erfordert Abteilung und Stelle.";
        }

        return null;
    }
}
