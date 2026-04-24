using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API;

internal static class RotationPlanningEndpoints
{
    public static IEndpointRouteBuilder MapRotationPlanningEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/rotation/completed-onboardings", async (
            [FromQuery] string? search,
            [FromQuery] int? limit,
            IWorkflowCatalogService workflowCatalogService,
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

            var targetPersonSources = await workflowCatalogService.SearchWorkflowTargetPersonSourcesAsync(
                search,
                access.User!,
                limit ?? 20);
            return Results.Ok(targetPersonSources.Select(ToCompletedOnboardingSearchResult).ToList());
        }).Produces<List<CompletedOnboardingSearchResultDto>>(StatusCodes.Status200OK);

        app.MapGet("/rotation/plans", async (
            [FromQuery] long? personId,
            IRotationPlanningService rotationPlanningService,
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
                var plans = await rotationPlanningService.GetRotationPlansAsync(personId, access.User!);
                return Results.Ok(plans);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<List<RotationPlanListItemDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest);

        app.MapGet("/rotation/plans/{planId:long}", async (
            long planId,
            IRotationPlanningService rotationPlanningService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                currentUser => authorizationPolicy.CanCreateWorkflow(currentUser)
                    || authorizationPolicy.CanAccessTechnicalTasks(currentUser)
                    || authorizationPolicy.CanManageAdminConfiguration(currentUser),
                "HR, Fachbereich oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var plan = await rotationPlanningService.GetRotationPlanAsync(planId, access.User!);
                return plan is null
                    ? Results.NotFound(new { message = "Durchlaufplan nicht gefunden." })
                    : Results.Ok(plan);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<RotationPlanDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/rotation/plans/{planId:long}/audit", async (
            long planId,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            IRotationPlanningService rotationPlanningService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            if (limit is <= 0)
            {
                return Results.BadRequest(new { message = "limit must be greater than zero." });
            }

            if (offset is < 0)
            {
                return Results.BadRequest(new { message = "offset must be greater than or equal to zero." });
            }

            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                currentUser => authorizationPolicy.CanCreateWorkflow(currentUser)
                    || authorizationPolicy.CanAccessTechnicalTasks(currentUser)
                    || authorizationPolicy.CanManageAdminConfiguration(currentUser),
                "HR, Fachbereich oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var auditLog = await rotationPlanningService.GetRotationAuditLogAsync(
                    planId,
                    Math.Clamp(limit ?? 50, 1, 500),
                    offset ?? 0,
                    access.User!);
                return auditLog is null
                    ? Results.NotFound(new { message = "Durchlaufplan nicht gefunden." })
                    : Results.Ok(auditLog);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<List<RotationAuditEntryDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/rotation/plans/{planId:long}/notifications", async (
            long planId,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            IRotationPlanningService rotationPlanningService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            if (limit is <= 0)
            {
                return Results.BadRequest(new { message = "limit must be greater than zero." });
            }

            if (offset is < 0)
            {
                return Results.BadRequest(new { message = "offset must be greater than or equal to zero." });
            }

            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                currentUser => authorizationPolicy.CanCreateWorkflow(currentUser)
                    || authorizationPolicy.CanAccessTechnicalTasks(currentUser)
                    || authorizationPolicy.CanManageAdminConfiguration(currentUser),
                "HR, Fachbereich oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var notifications = await rotationPlanningService.GetRotationNotificationsAsync(
                    planId,
                    Math.Clamp(limit ?? 50, 1, 500),
                    offset ?? 0,
                    access.User!);
                return notifications is null
                    ? Results.NotFound(new { message = "Durchlaufplan nicht gefunden." })
                    : Results.Ok(notifications);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<List<RotationNotificationDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/rotation/plans", async (
            [FromBody] CreateRotationPlanRequest request,
            IRotationPlanningService rotationPlanningService,
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
                var plan = await rotationPlanningService.CreateRotationPlanAsync(request, access.User!);
                return Results.Created($"/rotation/plans/{plan.Id}", plan);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<RotationPlanDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden);

        app.MapGet("/rotation/plans/{planId:long}/stations", async (
            long planId,
            IRotationPlanningService rotationPlanningService,
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
                var stations = await rotationPlanningService.GetRotationStationsAsync(planId, access.User!);
                return stations is null
                    ? Results.NotFound(new { message = "Durchlaufplan nicht gefunden." })
                    : Results.Ok(stations);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<List<RotationStationDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/rotation/plans/{planId:long}/generated-tasks", async (
            long planId,
            IRotationTaskGenerationService rotationTaskGenerationService,
            IUserContext userContext,
            IAuthorizationPolicyService authorizationPolicy) =>
        {
            var access = await EndpointSupport.RequireAuthorization(
                userContext,
                currentUser => authorizationPolicy.CanCreateWorkflow(currentUser)
                    || authorizationPolicy.CanAccessTechnicalTasks(currentUser)
                    || authorizationPolicy.CanManageAdminConfiguration(currentUser),
                "HR, Fachbereich oder Admin role is required.");
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var tasks = await rotationTaskGenerationService.GetGeneratedTasksAsync(planId, access.User!);
                return tasks is null
                    ? Results.NotFound(new { message = "Durchlaufplan nicht gefunden." })
                    : Results.Ok(tasks);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
        }).Produces<List<RotationGeneratedTaskDto>>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/rotation/plans/{planId:long}/generated-tasks/regenerate", async (
            long planId,
            IRotationTaskGenerationService rotationTaskGenerationService,
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
                var result = await rotationTaskGenerationService.RegeneratePlanTasksAsync(planId, access.User!);
                return result is null
                    ? Results.NotFound(new { message = "Durchlaufplan nicht gefunden." })
                    : Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<RotationTaskRegenerationResultDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/rotation/plans/{planId:long}/stations", async (
            long planId,
            [FromBody] RotationStationUpsertRequest request,
            IRotationPlanningService rotationPlanningService,
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
                var station = await rotationPlanningService.CreateRotationStationAsync(planId, request, access.User!);
                return station is null
                    ? Results.NotFound(new { message = "Durchlaufplan nicht gefunden." })
                    : Results.Created($"/rotation/stations/{station.Id}", station);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex) when (IsRotationStationConflict(ex))
            {
                return Results.Conflict(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<RotationStationDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status409Conflict)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/rotation/stations/{stationId:long}", async (
            long stationId,
            [FromBody] RotationStationUpsertRequest request,
            IRotationPlanningService rotationPlanningService,
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
                var station = await rotationPlanningService.UpdateRotationStationAsync(stationId, request, access.User!);
                return station is null
                    ? Results.NotFound(new { message = "RotationStation nicht gefunden." })
                    : Results.Ok(station);
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex) when (IsRotationStationConflict(ex))
            {
                return Results.Conflict(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<RotationStationDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status409Conflict)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/rotation/stations/{stationId:long}", async (
            long stationId,
            IRotationPlanningService rotationPlanningService,
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
                var deleted = await rotationPlanningService.DeleteRotationStationAsync(stationId, access.User!);
                return deleted
                    ? Results.NoContent()
                    : Results.NotFound(new { message = "RotationStation nicht gefunden." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return EndpointSupport.Forbidden(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status400BadRequest)
          .Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static CompletedOnboardingSearchResultDto ToCompletedOnboardingSearchResult(
        WorkflowTargetPersonSourceDto source)
    {
        return new CompletedOnboardingSearchResultDto
        {
            WorkflowUid = source.WorkflowUid,
            PersonId = source.PersonId,
            DisplayName = source.DisplayName,
            FirstName = source.FirstName,
            LastName = source.LastName,
            EmployeeNumber = source.EmployeeNumber,
            BadgeNumber = source.BadgeNumber,
            DepartmentId = source.DepartmentId,
            DepartmentName = source.DepartmentName,
            RoleId = source.RoleId,
            RoleName = source.RoleName,
            CompletedAt = source.CompletedAt,
            ArchivedAt = source.ArchivedAt
        };
    }

    private static bool IsRotationStationConflict(InvalidOperationException exception)
    {
        return exception.Message.Contains("überschneidet", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("orderIndex", StringComparison.OrdinalIgnoreCase);
    }
}
